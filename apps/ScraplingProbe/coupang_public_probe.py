"""One-off anonymous query of Coupang Taiwan's *own* public search page.

The web-search index may show an older page; only a direct HTTP 200 response
from Coupang in this runtime can validate live access. Prices are diagnostic
candidates, not a checkout price, and promotional labels remain conditional.

No account, cookies, proxy rotation, CAPTCHA handling, repeated requests,
stored response HTML, price history, or third-party price services.
"""
import argparse
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import parse_qs, quote, urljoin, urlparse
from urllib.request import Request, urlopen

BASE = "https://www.tw.coupang.com"
DEFAULT_KEYWORD = "3m膠帶"
MONEY_RE = re.compile(r"(?:NT)?\$\s*((?:\d{1,3}(?:,\d{3})+|\d{1,7}))(?![\d.])")
PRICE_MARKERS = ("首購折扣價", "折扣後價格", "酷澎售價")


def search_url(keyword):
    """The only host contacted is Coupang's Taiwan search page."""
    value = " ".join(str(keyword).split())
    if not 2 <= len(value) <= 80 or any(ord(c) < 32 for c in value):
        raise ValueError("invalid keyword length or characters")
    return BASE + "/srp/" + quote(value, safe="")


class Cards(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.active = None
        self.cards = []

    def handle_starttag(self, tag, attrs):
        if tag == "a" and self.active is None:
            props = dict(attrs)
            href = urljoin(BASE + "/", props.get("href", ""))
            parsed = urlparse(href)
            if (parsed.scheme == "https" and parsed.hostname == "www.tw.coupang.com"
                    and parsed.path.startswith("/products/")):
                opts = parse_qs(parsed.query)
                self.active = {
                    "path": parsed.path,
                    "item_id": (opts.get("itemId") or [""])[0][:25],
                    "vendor_item_id": (opts.get("vendorItemId") or [""])[0][:25],
                    "text": [],
                }

    def handle_data(self, data):
        if self.active is not None and data.strip():
            self.active["text"].append(" ".join(data.split()))

    def handle_endtag(self, tag):
        if tag == "a" and self.active is not None:
            card = self.active
            card["text"] = " ".join(card["text"])[:900]
            self.cards.append(card)
            self.active = None


def _price_summary(text):
    amounts = []
    for match in MONEY_RE.finditer(text):
        amount = int(match.group(1).replace(",", ""))
        if 0 < amount <= 10000000 and amount not in amounts:
            amounts.append(amount)
        if len(amounts) == 4:
            break
    if "首購" in text:
        kind = "first_purchase_promotion"
    elif "折扣後價格" in text:
        kind = "discounted_display"
    else:
        kind = "unclassified_display"
    title = re.split(r"首購折扣價|折扣後價格|酷澎售價|(?:NT)?\$", text, maxsplit=1)[0].strip()
    return {
        "title_candidate": title[:160],
        "amount_candidates_twd": amounts,
        "price_kind": kind,
    }


def describe(status, html, final_url):
    parsed = Cards()
    parsed.feed(html)
    seen, rows = set(), []
    for card in parsed.cards:
        key = (card["path"], card["item_id"], card["vendor_item_id"])
        if key in seen:
            continue
        seen.add(key)
        item = _price_summary(card["text"])
        item["has_numeric_price"] = bool(item["amount_candidates_twd"])
        item["has_conditional_promotion"] = "首購" in card["text"]
        rows.append(item)
    return {
        "status": "html_with_product_links" if rows else "html_without_product_links",
        "http_status": status,
        "final_host": urlparse(final_url).hostname,
        "html_chars": len(html),
        "product_count": len(rows),
        "product_cards_with_currency": sum("$" in c["text"] for c in parsed.cards),
        "product_cards_with_promo_marker": sum(
            ("首購" in c["text"] or "折扣後價格" in c["text"]) for c in parsed.cards),
        "product_cards_with_price_candidates": sum(r["has_numeric_price"] for r in rows),
        "visible_1530_mentions": html.lower().count("1530"),
        "sample_price_candidates": rows[:3],
        "note": "Direct same-runtime public page candidates only. Promo/discount amounts are not regular or checkout prices.",
    }


def run(keyword=DEFAULT_KEYWORD):
    now = datetime.now(timezone.utc).isoformat()
    url = search_url(keyword)
    req = Request(url, headers={
        "User-Agent": "PricePrototypeResearch/0.2",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(req, timeout=18) as response:
            status, final = response.status, response.geturl()
            body = response.read(2200000).decode("utf-8", "replace")
        final_url = urlparse(final)
        if final_url.scheme != "https" or final_url.hostname != "www.tw.coupang.com":
            return {"status": "unexpected_redirect", "http_status": status, "checked_at": now}
        if any(marker in body[:3500].lower() for marker in (
                "captcha", "access denied", "verify/traffic", "流量驗證")):
            return {"status": "traffic_challenge", "http_status": status, "checked_at": now}
        return {"checked_at": now, "keyword": keyword, **describe(status, body, final)}
    except HTTPError as exc:
        return {"status": "http_denied" if exc.code in (403, 429) else "http_error",
                "http_status": exc.code, "checked_at": now}
    except (URLError, OSError, TimeoutError) as exc:
        return {"status": "network_error", "error_type": type(exc).__name__,
                "checked_at": now}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="One ordinary Coupang Taiwan public search request")
    parser.add_argument("--keyword", default=DEFAULT_KEYWORD)
    args = parser.parse_args()
    print(json.dumps(run(args.keyword), ensure_ascii=False))
