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
    if payload.get("success") is not True:
        return {"status": "provider_response_not_successful", "products": [], "raw_goods_count": len(goods)}
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
        amount = price_value(row.get("SALE_PRICE"))
        if amount is None:
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
    payload = {
        "host": "ecmobile", "flag": "searchEngine",
        "data": {
            "maxPage": 30, "cateLevel": -1, "serviceCode": "MT01",
            "platform": 16, "has3P": "Y",
            "NAM": "N", "china": "N", "cp": "N", "first": "N",
            "freeze": "N", "prefere": "N", "stockYN": "N",
            "superstore": "N", "threeHours": "N", "tomorrow": "N",
            "tvshop": "N", "video": "N", "cycle": "N",
            "cod": "N", "superstorePay": "N", "moCoinFeedback": "N",
            "superstoreFree": "N", "discount": "N",
            "isBrandSeriesPage": False, "isShowAdShop": False,
            "curRecommendedWordsCnt": 0,
            "searchValue": keyword, "curPage": 1,
        },
    }
    result = {"source": "momo_search_data", "keyword": keyword, "http_status": None,
              "checked_at_utc": datetime.now(timezone.utc).isoformat(),
              "status": "not_started", "products": []}
    try:
        import never_primp as primp
        with primp.Client(
                impersonate="chrome_142", impersonate_os="windows",
                timeout=22.0, http2_only=True,
                headers={"content-type": "application/json",
                         "origin": "https://m.momoshop.com.tw",
                         "referer": "https://m.momoshop.com.tw/"}
        ) as client:
            response = client.post(API, json=payload)
            result["http_status"] = response.status_code
            if response.status_code in (403, 429):
                result["status"] = "http_denied"
                return result
            if response.status_code != 200:
                result["status"] = "http_error"
                return result
            if any(marker in response.text[:1500].lower() for marker in
                   ("captcha", "access denied", "流量驗證")):
                result["status"] = "blocked_or_challenged"
                return result
            try:
                parsed = response.json()
            except ValueError:
                result["status"] = "not_json"
                return result
            result["provider_success"] = parsed.get("success") if isinstance(parsed, dict) else False
            result.update(read_goods(parsed, keyword, limit))
    except (RuntimeError, OSError, TimeoutError, ValueError, Exception) as exc:
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
