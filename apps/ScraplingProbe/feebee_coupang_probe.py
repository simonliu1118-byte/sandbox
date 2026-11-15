"""One anonymous, zero-cost Feebee keyword lookup reachability test.

Tests Feebee price aggregator as a possible Taiwan Coupang product-level
source. No login, credentials, proxy, CAPTCHA, retry, or saved raw HTML.
"""
import json
import re
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

URL = "https://feebee.com.tw/s/3m%201530-1/"


class TextOnly(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.texts = []
        self.skip = 0

    def handle_starttag(self, tag, attrs):
        if tag in {"script", "style", "noscript"}:
            self.skip += 1

    def handle_endtag(self, tag):
        if tag in {"script", "style", "noscript"}:
            self.skip = max(0, self.skip - 1)

    def handle_data(self, text):
        if not self.skip and text.strip():
            self.texts.append(" ".join(text.split())[:300])


def describe(html):
    parsed = TextOnly()
    parsed.feed(html)
    texts = " ".join(parsed.texts)
    merchants = list(re.finditer(r"Coupang\s*酷澎", texts, re.I))
    return {
        "html_length": len(html),
        "visible_text_length": len(texts),
        "coupang_merchant_mentions": len(merchants),
        "mentions_1530": texts.upper().count("1530"),
        "price_number_candidates": len(re.findall(r"(?<!\d)\d{2,5}(?!\d)", texts)),
        "merchant_context_examples": [
            re.sub(r"\s+", " ", texts[max(0,m.start()-130):m.end()+50])
            for m in merchants[:4]
        ],
        "note": "Merchant mentions and numbers are not independently verified current product prices.",
    }


def run():
    req = Request(URL, headers={
        "User-Agent": "Mozilla/5.0 (compatible; PriceResearchPrototype/0.1)",
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-TW,zh;q=0.9",
    })
    try:
        with urlopen(req, timeout=20) as res:
            status = res.status
            target = res.geturl()
            body = res.read(2000000).decode("utf-8", "replace")
        if urlparse(target).hostname not in {"feebee.com.tw", "www.feebee.com.tw"}:
            return {"status": "unexpected_redirect", "http_status": status}
        if any(c in body[:3000].lower() for c in ("captcha", "access denied", "流量驗證")):
            return {"status": "traffic_challenge", "http_status": status}
        return {"status": "html_received", "http_status": status, **describe(body)}
    except HTTPError as e:
        return {"status": "http_denied" if e.code in (403, 429) else "http_error",
                "http_status": e.code}
    except (URLError, TimeoutError, OSError) as e:
        return {"status": "network_error", "error_type": type(e).__name__}


if __name__ == "__main__":
    print(json.dumps(run(), ensure_ascii=False))
