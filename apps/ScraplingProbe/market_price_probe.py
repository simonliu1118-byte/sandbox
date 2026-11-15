"""One-request-per-source public price probe for momo, Coupang, and ECPRO.

No authentication, retries, proxy switching, CAPTCHA handling, or checkout activity.
Returned prices are labelled public-page/third-party observations, not verified prices.
"""
import argparse
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlparse
from urllib.request import Request, urlopen

SAMPLES = {
    "momo": "https://www.momoshop.com.tw/product/9956761",
    "coupang": "https://www.tw.coupang.com/products/463932540010509",
    "ecpro_momo": "https://ecpro.tw/api/v1/products?q=" + quote("氣炸鍋") + "&max=30",
}
CHALLENGE = ("captcha", "verify/traffic", "access denied", "robot check", "流量驗證", "安全驗證")
PRICE_TEXT = re.compile(r"^\s*(?:NT\$|\$)?\s*(\d[\d,]{0,7})(?:\s*元)?\s*$")
META_KEYS = ("product:price:amount", "og:price:amount", "price")
TITLE_KEYS = ("og:title", "twitter:title")
PRICE_KEYS = ("price", "sale_price", "current_price", "offer_price", "min_price")
CHANNEL_KEYS = ("channel", "platform", "merchant", "seller", "site", "store", "source")


class PageTokens(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.text = []
        self.title_text = []
        self.meta = {}
        self._in_title = False
        self._skip = 0

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag in ("script", "style", "noscript"):
            self._skip += 1
        if tag == "title":
            self._in_title = True
        if tag == "meta":
            key = (a.get("property") or a.get("name") or a.get("itemprop") or "").lower()
            if key and a.get("content"):
                self.meta[key] = a["content"].strip()

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript"):
            self._skip = max(0, self._skip - 1)
        if tag == "title":
            self._in_title = False

    def handle_data(self, data):
        value = " ".join(data.split())
        if not value:
            return
        if self._in_title:
            self.title_text.append(value)
        if not self._skip:
            self.text.append(value)


def labelled_price(tokens, labels):
    """Only accept a price adjacent to an explicit listing-price label."""
    for label in labels:
        for index, token in enumerate(tokens):
            if token == label or (token.startswith(label) and len(token) <= len(label) + 4):
                for candidate in tokens[index + 1:index + 6]:
                    m = PRICE_TEXT.fullmatch(candidate)
                    if m:
                        value = int(m.group(1).replace(",", ""))
                        if 0 < value <= 1000000:
                            return {"price_twd": value, "price_kind": label}
                    if any(word in candidate for word in ("市售價", "原價", "折價券", "商品特色")):
                        break
    return None


def parse_product_html(html, platform, expected_id, final_path):
    page = PageTokens()
    page.feed(html)
    name = page.meta.get("og:title") or " ".join(page.title_text)
    name = name[:180].strip()
    marker = "M331"
    if marker.lower() not in name.lower() and not any(marker.lower() in s.lower() for s in page.text[:120]):
        return {"status": "identity_not_confirmed", "title": name, "price_twd": None}
    if platform == "momo":
        candidate = labelled_price(page.text, ("促銷價", "活動價"))
        if not candidate:
            for key in META_KEYS:
                price = page.meta.get(key)
                if price and re.fullmatch(r"\d{1,7}(?:\.\d{1,2})?", price):
                    candidate = {"price_twd": int(float(price)), "price_kind": "structured_meta"}
                    break
    else:
        candidate = labelled_price(page.text, ("酷澎售價", "折扣後價格", "首購折扣價"))
    if not candidate:
        return {"status": "price_not_found", "title": name, "price_twd": None}
    return {"status": "price_found", "title": name, "product_id": expected_id,
            "price_twd": candidate["price_twd"], "price_kind": candidate["price_kind"],
            "price_verified": False, "final_path": final_path}


def extract_momo_offer(payload):
    """Find up to three momo offers with title and numeric price in public JSON."""
    found = []

    def visit(node, inherited_title="", depth=0):
        if depth > 6 or len(found) >= 3:
            return
        if isinstance(node, list):
            for item in node[:40]:
                visit(item, inherited_title, depth + 1)
        elif isinstance(node, dict):
            title = str(node.get("title") or node.get("name") or node.get("product_name")
                        or node.get("productName") or inherited_title)[:180]
            channel = " ".join(str(node.get(k) or "") for k in CHANNEL_KEYS).lower()
            raw_price = next((node[k] for k in PRICE_KEYS if k in node
                              and isinstance(node[k], (int, float, str)) and not isinstance(node[k], bool)), None)
            try:
                price = int(str(raw_price).replace(",", "").replace("NT$", "").strip())
            except (ValueError, TypeError):
                price = 0
            if "momo" in channel and title and 0 < price <= 1000000:
                url = str(node.get("url") or node.get("link") or node.get("product_url") or "")
                found.append({"title": title, "price_twd": price, "channel": "momo",
                              "source_url": url[:320], "price_verified": False})
            for key, value in node.items():
                if isinstance(value, (dict, list)):
                    visit(value, title, depth + 1)

    visit(payload)
    return found


def retrieve(name, url):
    result = {"source": name, "target": url, "time_utc": datetime.now(timezone.utc).isoformat(),
              "status": "not_started", "http_status": None, "price_verified": False}
    try:
        request = Request(url, headers={"User-Agent": "Mozilla/5.0 (compatible; PriceResearchProbe/0.2)",
                                        "Accept": "application/json" if name == "ecpro_momo" else "text/html",
                                        "Accept-Language": "zh-TW,zh;q=0.9"})
        with urlopen(request, timeout=20) as response:
            result["http_status"] = response.status
            final = response.geturl()
            body = response.read(1500000).decode("utf-8", errors="replace")
        result["final_path"] = urlparse(final).path
        if name == "ecpro_momo":
            try:
                payload = json.loads(body)
            except ValueError:
                result["status"] = "not_json"
                return result
            offers = extract_momo_offer(payload)
            result["status"] = "price_found" if offers else "no_matching_offer"
            result["offers"] = offers
            result["data_source"] = "ECPRO third-party API, source attribution required"
        else:
            if any(s in (final + " " + body[:2500]).lower() for s in CHALLENGE):
                result["status"] = "blocked_or_challenged"
                return result
            result.update(parse_product_html(body, name, "9956761" if name == "momo" else
                                              "463932540010509", result["final_path"]))
    except HTTPError as exc:
        result["http_status"] = exc.code
        result["status"] = "http_denied" if exc.code in (403, 429) else "http_error"
    except (URLError, TimeoutError, OSError) as exc:
        result["status"] = "network_error"
        result["error_type"] = type(exc).__name__
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="market-result.json")
    args = parser.parse_args()
    # A single GET for each independent source; never retry a rejected request.
    results = [retrieve(name, url) for name, url in SAMPLES.items()]
    path = Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(results, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
