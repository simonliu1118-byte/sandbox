"""One-time Coupang Taiwan product detail accessibility check (public, anonymous).

Use discovery links only to identify an exact product; this request directly
asks the merchant for its current public displayed price. No login, cookies,
proxy, retries, CAPTCHA, bypass, stored HTML, or order actions.
"""
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

PRODUCT_URL = "https://www.tw.coupang.com/products/460322787786761"


class VisibleText(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.texts = []
        self.hidden = 0

    def handle_starttag(self, tag, attrs):
        if tag in ("style", "script", "noscript"):
            self.hidden += 1

    def handle_endtag(self, tag):
        if tag in ("style", "script", "noscript"):
            self.hidden = max(0, self.hidden-1)

    def handle_data(self, data):
        if not self.hidden and data.strip():
            self.texts.append(" ".join(data.split())[:240])


def assess(html):
    parser = VisibleText()
    parser.feed(html)
    text = " ".join(parser.texts)
    names = [n for n in ("酷澎售價", "首購折扣價", "商品主要規格", "暫時缺貨")
             if n in text]
    # Detection-only, NOT authoritative price extraction.
    return {
        "html_chars": len(html),
        "visible_text_chars": len(text),
        "has_product_title": bool(re.search(r"3M.{0,80}1530[-－]1", text, re.I)),
        "price_markers": names,
        "currency_symbol_mentions": text.count("$"),
        "note": "Detection only; price markers and digit counts do not prove a current or checkout price.",
    }


def run():
    now = datetime.now(timezone.utc).isoformat()
    req = Request(PRODUCT_URL, headers={
        "User-Agent": "Mozilla/5.0 (compatible; PriceResearchPrototype/0.1)",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(req, timeout=20) as response:
            status, final = response.status, response.geturl()
            body = response.read(2200000).decode("utf-8", "replace")
        if urlparse(final).hostname != "www.tw.coupang.com":
            return {"status": "unexpected_redirect", "http_status": status, "checked_at": now}
        if any(v in body[:3000].lower() for v in ("captcha", "access denied", "verify/traffic")):
            return {"status": "traffic_challenge", "http_status": status, "checked_at": now}
        return {"status": "direct_page_loaded", "http_status": status,
                "checked_at": now, **assess(body)}
    except HTTPError as e:
        return {"status": "http_denied" if e.code in (403,429) else "http_error",
                "http_status": e.code, "checked_at": now}
    except (URLError, OSError, TimeoutError) as e:
        return {"status": "network_error", "error_type": type(e).__name__, "checked_at": now}


if __name__ == "__main__":
    print(json.dumps(run(),ensure_ascii=False))
