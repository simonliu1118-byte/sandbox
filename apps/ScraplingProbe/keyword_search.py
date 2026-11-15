"""Keyword-first market discovery; no manual product URLs required.

One public momo keyword GET only when explicitly enabled. Shopee observations
are only imported from a separately authorized keyword-search JSON dataset.
No Shopee direct request, login, proxy, captcha, paid Actor, or scheduled job.
"""
import argparse
import json
import re
from datetime import datetime, timezone
from html import escape
from html.parser import HTMLParser
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urljoin, urlparse, parse_qs
from urllib.request import Request, urlopen

MOMO_HOSTS = {"momoshop.com.tw", "www.momoshop.com.tw"}
SHOPEE_HOSTS = {"shopee.tw", "www.shopee.tw"}
MOMO_SEARCH = "https://www.momoshop.com.tw/search/searchShop.jsp?keyword="
PRICE = re.compile(r"(?:NT\$|\$)\s*([1-9]\d{0,2}(?:,\d{3})*|[1-9]\d{0,6})")


def keyword_value(raw):
    if not isinstance(raw, str):
        raise ValueError("Keyword must be text")
    value = " ".join(raw.split())
    if not 2 <= len(value) <= 80 or any(ord(c) < 32 for c in value):
        raise ValueError("Keyword must be 2-80 readable characters")
    return value


def fits(keyword, title):
    return all(token.casefold() in title.casefold() for token in keyword.split())


def momo_id(url):
    target = urlparse(url)
    if target.scheme != "https" or target.hostname not in MOMO_HOSTS:
        return None
    direct = re.fullmatch(r"/product/(\d+)", target.path)
    if direct:
        return direct.group(1)
    if target.path.endswith("/goods/GoodsDetail.jsp"):
        code = parse_qs(target.query).get("i_code", [""])[0]
        if re.fullmatch(r"\d+", code):
            return code
    return None


class ListingLinks(HTMLParser):
    def __init__(self, base):
        super().__init__(convert_charrefs=True)
        self.base = base
        self.anchors = []
        self.active = None
        self.hidden = 0
        self.visible_nodes = 0

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag in ("script", "style", "noscript"):
            self.hidden += 1
        elif tag == "a" and not self.hidden:
            if self.active:
                self.anchors.append(self.active)
            self.active = {"href": urljoin(self.base, attrs.get("href", "")),
                           "title": str(attrs.get("title") or attrs.get("aria-label") or ""),
                           "text": []}

    def handle_data(self, data):
        text = " ".join(data.split())
        if text and not self.hidden:
            self.visible_nodes += 1
            if self.active:
                self.active["text"].append(text)

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript"):
            self.hidden = max(0, self.hidden - 1)
        elif tag == "a" and self.active:
            self.anchors.append(self.active)
            self.active = None


def parse_momo_listing(html, keyword, base, limit=20):
    parser = ListingLinks(base)
    parser.feed(html)
    rows, seen, links = [], set(), 0
    for anchor in parser.anchors:
        pid = momo_id(anchor["href"])
        if not pid:
            continue
        links += 1
        if pid in seen:
            continue
        body = " ".join(anchor["text"])
        title = anchor["title"].strip() or body[:180]
        if not fits(keyword, title):
            continue
        seen.add(pid)
        found = PRICE.search(body)
        amount = int(found.group(1).replace(",", "")) if found else None
        rows.append({"platform": "momo", "product_id": pid, "title": title,
                     "product_url": anchor["href"], "price_twd": amount,
                     "price_type": "unverified_search_card" if amount is not None else None,
                     "price_status": "candidate" if amount is not None else "no_price"})
        if len(rows) >= limit:
            break
    return {"products": rows, "product_links_seen": links,
            "visible_text_nodes": parser.visible_nodes}


def search_momo(keyword, limit=20):
    url = MOMO_SEARCH + quote(keyword, safe="")
    result = {"source": "momo_public_keyword", "keyword": keyword, "source_url": url,
              "checked_at_utc": datetime.now(timezone.utc).isoformat(),
              "status": "not_started", "http_status": None, "products": []}
    try:
        with urlopen(Request(url, headers={
                "User-Agent": "Mozilla/5.0 (compatible; PriceResearchPrototype/0.3)",
                "Accept": "text/html", "Accept-Language": "zh-TW,zh;q=0.9"
            }), timeout=20) as response:
            result["http_status"] = response.status
            final = response.geturl()
            body = response.read(2000000).decode("utf-8", errors="replace")
        target = urlparse(final)
        result["final_path"] = target.path
        if target.hostname not in MOMO_HOSTS:
            result["status"] = "unexpected_redirect"
            return result
        if any(word in (target.path + " " + body[:1500]).lower() for word in
               ("captcha", "access denied", "verify/traffic", "流量驗證")):
            result["status"] = "blocked_or_challenged"
            return result
        parsed = parse_momo_listing(body, keyword, final, limit)
        result.update(parsed)
        result["status"] = (
            "products_with_price_candidates" if any(
                row["price_twd"] is not None for row in parsed["products"]
            ) else "products_without_prices" if parsed["products"] else
            "no_linked_products_found"
        )
    except HTTPError as exc:
        result["http_status"] = exc.code
        result["status"] = "http_denied" if exc.code in (403, 429) else "http_error"
    except (URLError, TimeoutError, OSError) as exc:
        result["status"] = "network_error"
        result["error_type"] = type(exc).__name__
    return result


def import_shopee_keyword_results(keyword, rows, limit=20):
    """Offline only: import result of an authorized Shopee keyword provider."""
    if not isinstance(rows, list):
        raise ValueError("Expected a JSON array of search results")
    results, seen = [], set()
    for row in rows:
        if not isinstance(row, dict):
            continue
        title = str(row.get("title") or row.get("name") or "").strip()
        link = str(row.get("productUrl") or row.get("product_url") or row.get("url") or "")
        dest = urlparse(link)
        if (dest.scheme != "https" or dest.hostname not in SHOPEE_HOSTS
                or str(row.get("currency") or "TWD").upper() != "TWD"
                or not fits(keyword, title)):
            continue
        item = row.get("itemId") or row.get("item_id")
        shop = row.get("shopId") or row.get("shop_id")
        key = (str(shop), str(item)) if item and shop else (link,)
        if key in seen:
            continue
        seen.add(key)
        try:
            amount = int(str(row.get("price")).replace(",", ""))
            if not 0 < amount <= 10000000:
                amount = None
        except (TypeError, ValueError):
            amount = None
        results.append({"platform": "shopee", "product_id": item,
                        "shop_id": shop, "title": title[:180],
                        "product_url": link, "price_twd": amount,
                        "price_type": "third_party_card_display" if amount is not None else None,
                        "price_status": "external_unverified" if amount is not None else "no_price"})
        if len(results) >= limit:
            break
    return {"source": "shopee_authorized_keyword_dataset", "keyword": keyword,
            "status": "external_data_not_independently_verified",
            "products": results}


def html_report(reports):
    cells = []
    for report in reports:
        for item in report["products"]:
            price = "NT$" + str(item["price_twd"]) if item["price_twd"] is not None else "待查"
            cells.append("<tr><td>" + escape(item["platform"]) + "</td><td>" +
                         escape(item["title"]) + "</td><td>" + escape(price) +
                         "</td><td><a href='" + escape(item["product_url"], quote=True) +
                         "' rel='noopener noreferrer' target='_blank'>來源</a></td><td>" +
                         escape(item["price_status"]) + "</td></tr>")
    summaries = "；".join(escape(report["source"] + "：" + report["status"]) for report in reports)
    return ("<!doctype html><html lang='zh-Hant'><meta charset='utf-8'>"
            "<meta name='viewport' content='width=device-width,initial-scale=1'>"
            "<title>競品關鍵字搜尋原型</title>"
            "<style>body{font:16px system-ui;max-width:1100px;margin:3rem auto;padding:1rem}"
            "table{border-collapse:collapse;width:100%}th,td{padding:12px;text-align:left;"
            "border-bottom:1px solid #ddd}a{color:#92572a}</style>"
            "<h1>競品關鍵字搜尋原型</h1><p>" + summaries +
            "</p><table><thead><tr><th>平台</th><th>商品</th><th>展示價</th><th>連結</th>"
            "<th>資料狀態</th></tr></thead><tbody>" + "".join(cells) +
            "</tbody></table><p>價格僅供研究；不代表指定規格或結帳實付價。"
            "尚未驗證的蝦皮免費搜尋來源不得顯示為已成功。</p></html>")


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--keyword", required=True)
    cli.add_argument("--limit", type=int, default=20)
    cli.add_argument("--live-momo", action="store_true",
                     help="Explicit single momo keyword search HTTP GET")
    cli.add_argument("--shopee-dataset", help="Authorized local JSON dataset, no API run")
    cli.add_argument("--output", default="keyword-result.json")
    cli.add_argument("--html", help="Optional offline HTML report")
    options = cli.parse_args()
    keyword = keyword_value(options.keyword)
    if not 1 <= options.limit <= 50:
        cli.error("limit must be 1-50")
    if options.live_momo:
        reports = [search_momo(keyword, options.limit)]
    else:
        reports = [{"source": "momo_public_keyword", "keyword": keyword,
                    "status": "no_live_request_by_default", "products": []}]
    if options.shopee_dataset:
        rows = json.loads(Path(options.shopee_dataset).read_text(encoding="utf-8"))
        reports.append(import_shopee_keyword_results(keyword, rows, options.limit))
    else:
        reports.append({"source": "shopee_keyword", "keyword": keyword,
                        "status": "free_source_not_yet_verified", "products": []})
    record = {"keyword": keyword, "checked_at_utc": datetime.now(timezone.utc).isoformat(),
              "results": reports}
    output = Path(options.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n",
                      encoding="utf-8")
    if options.html:
        html_file = Path(options.html)
        html_file.parent.mkdir(parents=True, exist_ok=True)
        html_file.write_text(html_report(reports), encoding="utf-8")
    print(json.dumps({"keyword": keyword, "sources": [
        {"source": r["source"], "status": r["status"], "products": len(r["products"])}
        for r in reports
    ]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
