"""Retrieve one momo product's publicly advertised price with one HTTPS request."""
import argparse
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

LABELS = ("促銷價", "優惠價", "活動價")
MONEY = re.compile(r"^\s*(?:NT\$|\$)?\s*(\d[\d,]{0,7})(?:\s*元)?\s*$")


class TextAndMeta(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.tokens, self.meta, self.titles = [], {}, []
        self.skip, self.title_mode = 0, False

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag in ("script", "style", "noscript"):
            self.skip += 1
        elif tag == "title":
            self.title_mode = True
        elif tag == "meta":
            key = (attrs.get("name") or attrs.get("property") or "").lower()
            if key and attrs.get("content"):
                self.meta[key] = attrs["content"]

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript"):
            self.skip = max(self.skip - 1, 0)
        elif tag == "title":
            self.title_mode = False

    def handle_data(self, data):
        value = " ".join(data.split())
        if value and self.title_mode:
            self.titles.append(value)
        if value and not self.skip:
            self.tokens.append(value)


def product_id_from_url(url):
    parts = urlparse(url)
    if parts.scheme != "https" or parts.hostname not in ("momoshop.com.tw", "www.momoshop.com.tw"):
        raise ValueError("Only HTTPS momo product pages are supported")
    if parts.username or parts.password:
        raise ValueError("Do not embed credentials")
    matched = re.fullmatch(r"/product/(\d+)", parts.path)
    if not matched:
        raise ValueError("Product URL must have /product/<numeric-id>")
    return matched.group(1)


def parse_product_html(html, expected_id):
    parsed = TextAndMeta()
    parsed.feed(html)
    name = (parsed.meta.get("og:title") or " ".join(parsed.titles)).strip()[:180]
    visible = " ".join(parsed.tokens[:2500])
    confirmed = re.search(r"品號\s*[:：]?\s*" + re.escape(expected_id) + r"(?!\d)", visible)
    if not name or not confirmed:
        return {"status": "identity_not_confirmed", "title": name, "price_twd": None}
    for index, token in enumerate(parsed.tokens):
        if token not in LABELS:
            continue
        for after in parsed.tokens[index + 1:index + 6]:
            match = MONEY.fullmatch(after)
            if match:
                value = int(match.group(1).replace(",", ""))
                if 0 < value <= 1000000:
                    return {"status": "price_found", "title": name, "product_id": expected_id,
                            "price_twd": value, "price_kind": token, "price_verified": False}
            if any(stop in after for stop in ("市售價", "原價", "折價券", "商品特色")):
                break
    return {"status": "price_not_found", "title": name, "product_id": expected_id,
            "price_twd": None, "price_verified": False}


def fetch_once(url):
    item_id = product_id_from_url(url)
    result = {"source": "momo", "url": url, "time_utc": datetime.now(timezone.utc).isoformat(),
              "status": "not_started", "http_status": None, "price_verified": False}
    try:
        request = Request(url, headers={"User-Agent": "Mozilla/5.0 (compatible; PriceResearchProbe/0.2)",
                                        "Accept": "text/html", "Accept-Language": "zh-TW,zh;q=0.9"})
        with urlopen(request, timeout=20) as response:
            result["http_status"] = response.status
            final = urlparse(response.geturl())
            html = response.read(1500000).decode("utf-8", errors="replace")
        result["final_path"] = final.path
        if final.hostname not in ("momoshop.com.tw", "www.momoshop.com.tw"):
            result["status"] = "unexpected_redirect"
        elif any(word in (final.path + " " + html[:2000]).lower() for word in
                 ("captcha", "verify/traffic", "access denied", "robot check", "流量驗證")):
            result["status"] = "blocked_or_challenged"
        elif final.path != "/product/" + item_id:
            result["status"] = "unexpected_redirect"
        else:
            result.update(parse_product_html(html, item_id))
    except HTTPError as err:
        result["http_status"] = err.code
        result["status"] = "http_denied" if err.code in (403, 429) else "http_error"
    except (URLError, TimeoutError, OSError) as err:
        result["status"] = "network_error"
        result["error_type"] = type(err).__name__
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", required=True)
    parser.add_argument("--output", default="momo-result.json")
    args = parser.parse_args()
    output = fetch_once(args.url)
    dest = Path(args.output)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(output, ensure_ascii=False, indent=2))
