"""One anonymous Feebee search for Taiwan Shopee offers, research-only.

Returned prices are third-party references, not live Shopee/SKU checkout
prices. No login, proxies, retries, CAPTCHA handling or persistent records.
"""
import argparse
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urljoin, urlparse
from urllib.request import Request, urlopen

BASE = "https://feebee.com.tw"
SHOP_RE = re.compile(r"^(蝦皮商城|蝦皮購物)(?:\s*[-－—]\s*(.+))?$")
PRICE_RE = re.compile(r"^(?:NT)?\$?\s*([1-9]\d{0,2}(?:,\d{3})*|[1-9]\d{0,6})\s*(?:元)?$")
IGNORE = {"推薦商品", "找同款商品", "前往購買", "促銷",
          "Feebee 飛比價格", "熱門搜尋", "購物精選", "拍賣商品", "所有商品"}


def make_url(keyword):
    keyword = " ".join(str(keyword).split())
    if not 2 <= len(keyword) <= 80 or any(ord(c) < 32 for c in keyword):
        raise ValueError("invalid keyword")
    return BASE + "/s/" + quote(keyword, safe="") + "/"


class VisibleNodes(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.nodes, self.links, self.skipped = [], [], 0

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style", "noscript", "svg"):
            self.skipped += 1
        if tag == "a":
            self.links.append(dict(attrs).get("href", ""))

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript", "svg"):
            self.skipped = max(0, self.skipped - 1)
        if tag == "a" and self.links:
            self.links.pop()

    def handle_data(self, data):
        if self.skipped:
            return
        value = " ".join(data.split()).strip()
        if value:
            self.nodes.append((value[:300], self.links[-1] if self.links else ""))


def safe_link(href, fallback):
    if not href:
        return fallback
    uri = urljoin(BASE + "/", href)
    url = urlparse(uri)
    if url.scheme == "https" and url.hostname in ("feebee.com.tw", "www.feebee.com.tw"):
        return uri
    return fallback


def parse_card(parts, fallback):
    shops = [(i, m) for i, (v, _) in enumerate(parts)
             if (m := SHOP_RE.fullmatch(v))]
    if len(shops) != 1:
        return None
    index, merchant = shops[0]
    if index < 1:
        return None
    match = PRICE_RE.fullmatch(parts[index - 1][0])
    if not match:
        return None
    price = int(match.group(1).replace(",", ""))
    if price <= 0 or price > 10000000:
        return None
    # Title is linked to the same card; exclude site-wide header links.
    candidates = [(text, href) for text, href in parts[max(0, index - 11):index - 1]
                  if href and len(text) >= 8 and text not in IGNORE
                  and "飛比價格" not in text and "精選商品優惠價格" not in text
                  and not PRICE_RE.fullmatch(text)]
    if not candidates:
        return None
    title, href = candidates[-1]
    kind = "mall" if merchant.group(1) == "蝦皮商城" else "marketplace"
    return {"market_type": kind, "merchant": merchant.group(1),
            "seller": (merchant.group(2) or "")[:100],
            "title": title[:220], "reference_price_twd": price,
            "feebee_link": safe_link(href, fallback)}


def parse_search_html(html, source_url):
    parser = VisibleNodes()
    parser.feed(html)
    counts = {"mall": 0, "marketplace": 0}
    cards, seen, parts, mentions = [], set(), [], 0
    for text, href in parser.nodes:
        if SHOP_RE.fullmatch(text):
            mentions += 1
        if text == "前往購買":
            row = parse_card(parts, source_url)
            if row:
                key = (row["market_type"], row["seller"], row["title"], row["reference_price_twd"])
                if key not in seen:
                    seen.add(key)
                    counts[row["market_type"]] += 1
                    cards.append(row)
            parts = []
        else:
            parts.append((text, href))
    return {
        "status": "paired" if cards else "unpaired" if mentions else "no_shopee",
        "merchant_mentions": mentions,
        "matched_count": len(cards),
        "market_counts": counts,
        "samples": cards[:8],
        "note": "Third-party Feebee indexed reference prices; not live Shopee or per-SKU prices."
    }


def run(keyword):
    url = make_url(keyword)
    report = {"status": "network_error", "checked_at": datetime.now(timezone.utc).isoformat()}
    req = Request(url, headers={
        "User-Agent": "Mozilla/5.0 (compatible; PriceResearchPrototype/0.1)",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(req, timeout=18) as resp:
            location = urlparse(resp.geturl())
            if resp.status != 200:
                return {**report, "status": "http_error", "http_status": resp.status}
            if location.scheme != "https" or location.hostname not in (
                    "feebee.com.tw", "www.feebee.com.tw"):
                return {**report, "status": "unexpected_redirect"}
            html = resp.read(1600000).decode("utf-8", "replace")
        if any(x in html[:3000].lower() for x in ("captcha", "access denied", "流量驗證")):
            return {**report, "status": "traffic_challenge"}
        return {**report, "http_status": 200, "keyword": keyword, **parse_search_html(html, url)}
    except HTTPError as exc:
        return {**report, "status": "blocked" if exc.code in (403, 429) else "http_error",
                "http_status": exc.code}
    except (URLError, TimeoutError, OSError, ValueError) as exc:
        return {**report, "status": "network_error", "error_type": type(exc).__name__}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--keyword", default="3m 1530-1")
    args = parser.parse_args()
    print(json.dumps(run(args.keyword), ensure_ascii=False))
