"""One-off anonymous Coupang Taiwan public search reachability probe.

No login, cookies, proxy, captcha, retry, personal IP, or raw HTML output.
Outputs only basic aggregate HTML parsing diagnostics.
"""
import json
import re
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urljoin, urlparse
from urllib.request import Request, urlopen

BASE = "https://www.tw.coupang.com"
KEYWORD = "3m透氣膠帶"


class Cards(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.active = None
        self.cards = []

    def handle_starttag(self, tag, attrs):
        props = dict(attrs)
        if tag == "a":
            href = urljoin(BASE, props.get("href", ""))
            u = urlparse(href)
            if u.hostname == "www.tw.coupang.com" and u.path.startswith("/products/"):
                self.active = {"path": u.path, "text": []}

    def handle_data(self, data):
        if self.active and data.strip():
            self.active["text"].append(data.strip())

    def handle_endtag(self, tag):
        if tag == "a" and self.active:
            row = self.active
            row["text"] = " ".join(row["text"])[:650]
            self.cards.append(row)
            self.active = None


def describe(status, html, final_url):
    parser = Cards()
    parser.feed(html)
    unique = {p["path"]: p["text"] for p in parser.cards}
    snippets = list(unique.values())
    return {
        "status": "html_with_product_links" if unique else "html_without_product_links",
        "http_status": status,
        "final_host": urlparse(final_url).hostname,
        "html_chars": len(html),
        "product_count": len(unique),
        "product_cards_with_currency": sum("$" in x for x in snippets),
        "product_cards_with_promo_marker": sum("首購" in x for x in snippets),
        "visible_1530_mentions": html.lower().count("1530"),
        "sample_card_text": snippets[:3],
    }


def run():
    url = BASE + "/srp/" + quote(KEYWORD)
    request = Request(url, headers={
        "User-Agent": "Mozilla/5.0 (compatible; PricePrototypeResearch/0.1)",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(request, timeout=18) as response:
            status = response.status
            final = response.geturl()
            html = response.read(2200000).decode("utf-8", "replace")
        if urlparse(final).hostname != "www.tw.coupang.com":
            return {"status": "unexpected_redirect", "http_status": status}
        if any(x in html[:3000].lower() for x in ["captcha", "access denied", "verify/traffic"]):
            return {"status": "traffic_challenge", "http_status": status}
        return describe(status, html, final)
    except HTTPError as e:
        return {"status": "http_denied" if e.code in (403, 429) else "http_error",
                "http_status": e.code}
    except (URLError, OSError, TimeoutError) as e:
        return {"status": "network_error", "error_type": type(e).__name__}


if __name__ == "__main__":
    print(json.dumps(run(), ensure_ascii=False))
