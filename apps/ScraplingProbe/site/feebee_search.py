"""Conservative Feebee/Coupang *reference price* parser; not a live Coupang quote."""
import json
import re
import threading
import time
from datetime import datetime, timezone
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urljoin, urlparse
from urllib.request import Request, urlopen

BASE = "https://feebee.com.tw"
MERCHANT_RE = re.compile(r"Coupang\s*酷澎|酷澎\s*Coupang", re.I)
PRICE_RE = re.compile(r"^(?:NT)?\$?\s*([1-9]\d{0,2}(?:,\d{3})*|[1-9]\d{0,6})\s*(?:元)?$")
_CACHE = {}
_LOCK = threading.Lock()
_LAST_REQUEST = 0.0
_BLOCKED_UNTIL = 0.0
IGNORE = {"推薦商品", "找同款商品", "前往購買", "促銷", "Coupang 酷澎"}


def make_search_url(keyword):
    return BASE + "/s/" + quote(keyword, safe="") + "/"


def _safe_link(href, fallback):
    if not href:
        return fallback
    url = urljoin(BASE + "/", href)
    parsed = urlparse(url)
    return url if parsed.scheme == "https" and parsed.hostname in (
        "feebee.com.tw", "www.feebee.com.tw") else fallback


class VisibleNodes(HTMLParser):
    """Visible text nodes with their enclosing anchor's URL."""
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts, self.links, self.skip = [], [], 0

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style", "noscript", "svg"):
            self.skip += 1
        if tag == "a":
            self.links.append(dict(attrs).get("href", ""))

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript", "svg"):
            self.skip = max(0, self.skip - 1)
        if tag == "a" and self.links:
            self.links.pop()

    def handle_data(self, data):
        if self.skip:
            return
        text = " ".join(data.split()).strip()
        if text:
            self.parts.append((text[:300], self.links[-1] if self.links else ""))


def _card(parts, search_url):
    merchants = [i for i, (text, _) in enumerate(parts) if MERCHANT_RE.search(text)]
    if len(merchants) != 1:
        return None
    price_idx = merchants[0] - 1
    if price_idx < 0:
        return None
    match = PRICE_RE.fullmatch(parts[price_idx][0])
    if match is None:
        return None
    price = int(match.group(1).replace(",", ""))
    if price <= 0 or price > 10000000:
        return None
    # A global header link can appear before the first listing. Product titles
    # must be anchored nearby, directly preceding this card's price/merchant.
    # When the site changes its markup, omit the card rather than mislabel it.
    candidates = [(text, href) for text, href in parts[max(0, price_idx - 12):price_idx]
                  if href and len(text) >= 8 and text not in IGNORE
                  and "Feebee" not in text and "飛比價格" not in text
                  and not MERCHANT_RE.search(text) and not PRICE_RE.fullmatch(text)
                  and "精選商品優惠價格" not in text and "相關搜尋" not in text]
    if not candidates:
        return None
    title, href = candidates[-1]
    return {"title": title[:200], "price": price,
            "url": _safe_link(href, search_url), "source": "飛比／酷澎"}


def parse_feebee_results(html, search_url):
    """Extract ONLY one title-price-Coupang association per purchase listing."""
    parser = VisibleNodes()
    parser.feed(html)
    results, seen, parts, merchant_count = [], set(), [], 0
    for text, href in parser.parts:
        if MERCHANT_RE.search(text):
            merchant_count += 1
        if text == "前往購買":
            row = _card(parts, search_url)
            if row:
                identity = (row["title"], row["price"])
                if identity not in seen:
                    seen.add(identity)
                    results.append(row)
                    if len(results) >= 20:
                        break
            parts = []
        else:
            parts.append((text, href))
    if results:
        return "ok", results
    return ("no_price" if merchant_count else "no_match"), []


def fetch_once(keyword):
    search_url = make_search_url(keyword)
    report = {"status": "network_error", "results": [], "source_url": search_url,
              "checked_at": datetime.now(timezone.utc).isoformat()}
    req = Request(search_url, headers={
        "User-Agent": "Mozilla/5.0 (compatible; PriceResearchPrototype/0.1)",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(req, timeout=18) as response:
            target = urlparse(response.geturl())
            if response.status != 200:
                report["status"] = "http_error"
                return report
            if target.scheme != "https" or target.hostname not in (
                    "feebee.com.tw", "www.feebee.com.tw"):
                report["status"] = "unexpected_redirect"
                return report
            html = response.read(1500000).decode("utf-8", "replace")
        if any(x in html[:3000].lower() for x in ("captcha", "access denied", "流量驗證")):
            report["status"] = "blocked"
            return report
        report["status"], report["results"] = parse_feebee_results(html, search_url)
    except HTTPError as error:
        report["status"] = "blocked" if error.code in (403, 429) else "http_error"
    except (URLError, TimeoutError, OSError, ValueError) as error:
        report["error_type"] = type(error).__name__
    return report


def safe_search(keyword):
    """In-memory 10-minute cache, not a persistent quotation history."""
    global _LAST_REQUEST, _BLOCKED_UNTIL
    now = time.monotonic()
    with _LOCK:
        entry = _CACHE.get(keyword)
        if entry and now - entry[0] < 600:
            return {**entry[1], "from_cache": True}
        if now < _BLOCKED_UNTIL:
            return {"status": "blocked_cooldown", "results": [],
                    "source_url": make_search_url(keyword)}
        if now - _LAST_REQUEST < 15:
            return {"status": "rate_limited", "results": [],
                    "source_url": make_search_url(keyword)}
        _LAST_REQUEST = now
        result = fetch_once(keyword)
        if result["status"] == "blocked":
            _BLOCKED_UNTIL = time.monotonic() + 1800
        elif result["status"] in ("ok", "no_price", "no_match"):
            _CACHE[keyword] = (time.monotonic(), result)
        return result


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--keyword", required=True)
    args = ap.parse_args()
    outcome = fetch_once(args.keyword)
    print(json.dumps({"status": outcome["status"], "count": len(outcome["results"]),
                      "samples": [{"title": x["title"], "price": x["price"]}
                                  for x in outcome["results"][:4]]},
                     ensure_ascii=False))
