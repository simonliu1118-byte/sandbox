"""One-request Shopee public-listing observation, without login or retries.

Only tests whether listing HTML has named product cards with a visible price
and a link. Does not interact with verification or use browser/proxies.
"""
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError

URL = "https://shopee.tw/list/Logitech%E7%BE%85%E6%8A%80/M331"
PRICE = re.compile(r"(?:NT\$|\$)\s*([0-9][0-9,]*)")
LINK_ID = re.compile(r"(?:-i\.|/product/)(\d+)[./](\d+)")


class Cards(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.cards = []
        self.visible_tokens = []
        self.skip = 0

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag in ("style", "script", "noscript"):
            self.skip += 1
        if tag == "a" and self.skip == 0:
            self.stack.append({"href": data.get("href", ""), "text": []})

    def handle_data(self, data):
        if self.skip:
            return
        value = " ".join(data.split())
        if not value:
            return
        self.visible_tokens.append(value)
        for node in self.stack:
            node["text"].append(value)

    def handle_endtag(self, tag):
        if tag in ("style", "script", "noscript"):
            self.skip = max(0, self.skip - 1)
        if tag == "a" and self.stack:
            entry = self.stack.pop()
            value = " ".join(entry["text"])
            href = urljoin(URL, entry["href"])
            match = LINK_ID.search(urlparse(href).path)
            price = PRICE.search(value)
            if match and price and "m331" in value.lower():
                self.cards.append({
                    "title_and_labels": value[:140],
                    "shop_id": match.group(1),
                    "item_id": match.group(2),
                    "price_candidate_twd": int(price.group(1).replace(",", "")),
                    "source_url": href.split("?")[0][:300],
                })


def parse_cards(html):
    parser = Cards()
    parser.feed(html)
    distinct = []
    seen = set()
    for card in parser.cards:
        key = (card["shop_id"], card["item_id"])
        if key not in seen:
            seen.add(key)
            distinct.append(card)
    return distinct[:5], len(parser.visible_tokens), "M331" in " ".join(parser.visible_tokens).upper()


def probe():
    report = {
        "source": "shopee_public_listing",
        "url": URL,
        "time_utc": datetime.now(timezone.utc).isoformat(),
        "status": "not_started",
        "http_status": None,
        "price_verified": False,
    }
    try:
        with urlopen(Request(URL, headers={"User-Agent": "Mozilla/5.0",
                                            "Accept": "text/html",
                                            "Accept-Language": "zh-TW,zh;q=0.9"}), timeout=18) as response:
            report["http_status"] = response.status
            final = urlparse(response.geturl())
            html = response.read(1400000).decode("utf-8", errors="replace")
        report["final_path"] = final.path
        report["body_bytes_max"] = len(html.encode("utf-8"))
        if final.hostname != "shopee.tw" or "/verify/traffic" in final.path:
            report["status"] = "blocked_or_redirected"
        else:
            cards, token_count, has_keyword = parse_cards(html)
            report["visible_token_count"] = token_count
            report["has_keyword"] = has_keyword
            report["cards"] = cards
            report["status"] = "linked_price_cards_found" if cards else "no_linked_price_cards"
    except HTTPError as error:
        report["http_status"] = error.code
        report["status"] = "http_denied" if error.code in (403, 429) else "http_error"
    except (URLError, TimeoutError, OSError) as error:
        report["status"] = "network_error"
        report["error_type"] = type(error).__name__
    return report


if __name__ == "__main__":
    output = probe()
    Path("apps/ScraplingProbe/shopee-listing-result.json").write_text(
        json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(output, ensure_ascii=False, indent=2))
