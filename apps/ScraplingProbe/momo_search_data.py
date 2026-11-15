"""Single-request momo keyword search via public website's search data endpoint.

Endpoint is undocumented and may reject access or change without notice.
No login, proxy, retries, CAPTCHA handling, or background jobs.
"""
import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from keyword_search import keyword_value, fits

API = "https://apisearch.momoshop.com.tw/momoSearchCloud/moec/textSearch"
MONEY = re.compile(r"^\s*(?:NT\$|\$)?\s*([1-9]\d{0,2}(?:,\d{3})*|[1-9]\d{0,6})(?:\s*(?:元|\(.*\)))?\s*$")


def price_value(value):
    if value is None or isinstance(value, bool):
        return None
    text = str(value).split("(")[0].strip()
    m = MONEY.fullmatch(text)
    if not m:
        return None
    number = int(m.group(1).replace(",", ""))
    return number if number <= 10000000 else None


def read_goods(payload, keyword, limit=20):
    if not isinstance(payload, dict):
        return {"status": "unexpected_json_shape", "products": [], "top_level_keys": []}
    section = payload.get("rtnSearchData")
    goods = section.get("goodsInfoList") if isinstance(section, dict) else None
    if not isinstance(goods, list):
        return {"status": "unexpected_json_shape", "products": [],
                "top_level_keys": sorted(str(k) for k in payload)[:12]}
    products, seen = [], set()
    for row in goods:
        if not isinstance(row, dict):
            continue
        name = str(row.get("goodsName") or "").strip()
        if not name or not fits(keyword, name):
            continue
        code = str(row.get("goodsCode") or row.get("goodsCodeId") or row.get("i_code") or "").strip()
        if not re.fullmatch(r"\d{5,14}", code) or code in seen:
            continue
        seen.add(code)
        amount = price_value(row.get("goodsPrice"))
        products.append({
            "platform": "momo", "product_id": code,
            "title": name[:180],
            "price_twd": amount,
            "price_type": "public_search_display_price" if amount is not None else None,
            "price_status": "candidate" if amount is not None else "no_price",
            "product_url": "https://www.momoshop.com.tw/product/" + code,
        })
        if len(products) >= limit:
            break
    return {"status": "products_with_prices" if any(
                p["price_twd"] is not None for p in products
            ) else "products_without_prices" if products else "no_matching_products",
            "products": products, "raw_goods_count": len(goods)}


def fetch_keyword(keyword, limit=20):
    keyword = keyword_value(keyword)
    payload = {"host": "momoshop", "flag": "searchEngine",
               "data": {"searchValue": keyword, "curPage": "1", "priceS": "0",
                        "priceE": "9999999", "searchType": "1"}}
    result = {"source": "momo_search_data", "keyword": keyword, "http_status": None,
              "checked_at_utc": datetime.now(timezone.utc).isoformat(),
              "status": "not_started", "products": []}
    request = Request(API, data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
                      headers={"User-Agent": "Mozilla/5.0",
                               "Accept": "application/json",
                               "Content-Type": "application/json",
                               "Accept-Language": "zh-TW,zh;q=0.9"}, method="POST")
    try:
        with urlopen(request, timeout=22) as response:
            result["http_status"] = response.status
            raw = response.read(1600000).decode("utf-8", errors="replace")
            final_url = response.geturl()
        if final_url != API:
            result["status"] = "unexpected_redirect"
        elif any(marker in raw[:1500].lower() for marker in ("captcha", "access denied", "流量驗證")):
            result["status"] = "blocked_or_challenged"
        else:
            try:
                parsed = json.loads(raw)
            except ValueError:
                result["status"] = "not_json"
            else:
                result.update(read_goods(parsed, keyword, limit))
    except HTTPError as exc:
        result["http_status"] = exc.code
        result["status"] = "http_denied" if exc.code in (403, 429) else "http_error"
    except (TimeoutError, URLError, OSError) as exc:
        result["status"] = "network_error"
        result["error_type"] = type(exc).__name__
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--keyword", required=True)
    p.add_argument("--limit", type=int, default=20)
    p.add_argument("--output", default="momo-keyword-api-result.json")
    args = p.parse_args()
    if not 1 <= args.limit <= 50:
        p.error("limit must be 1..50")
    result = fetch_keyword(args.keyword, args.limit)
    path = Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
