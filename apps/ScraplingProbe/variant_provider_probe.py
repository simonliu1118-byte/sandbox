"""Read-only test of public Apify example data, without paid jobs or Shopee requests."""
import json
from datetime import datetime, timezone
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen
from pathlib import Path

ACTORS = {
    "khalid_alsaba": "khalid_alsaba~shopee-product-scraper",
    "sian_agency": "sian.agency~shopee-product-scraper",
}


def assess_variant_data(item):
    """Require model ID + name + individual price. Never derive models from min/max."""
    if not isinstance(item, dict):
        return {"status": "invalid_row"}
    region = str(item.get("region") or item.get("site") or item.get("market") or "").lower()
    currency = str(item.get("currency") or "").upper()
    hostname = urlparse(str(item.get("url") or item.get("productUrl") or "")).hostname
    if (region not in {"tw", "taiwan"} and hostname != "shopee.tw") or currency not in {"", "TWD"}:
        return {"status": "not_taiwan_product"}
    data = ((item.get("raw") or {}).get("get_pc") or {}).get("data") or {}
    raw = (data.get("item") or {})
    models = raw.get("models") if raw else (item.get("skuModels") or item.get("variants"))
    models = models or []
    scale = 100000 if raw else 1
    priced = {}
    named = 0
    for m in models:
        if not isinstance(m, dict):
            continue
        mid = m.get("model_id") if "model_id" in m else m.get("modelId")
        price = m.get("price")
        if mid is None or not isinstance(price, (int, float)) or isinstance(price, bool):
            continue
        value = price / scale
        if not 0 < value < 10000000:
            continue
        priced[str(mid)] = value
        if str(m.get("name") or m.get("label") or "").strip():
            named += 1
    count = len(priced)
    distinct = len(set(priced.values()))
    if count >= 2 and named == count:
        status = "full_sku_pricing" if distinct >= 2 else "uniform_sku_pricing"
    elif count:
        status = "partial_sku_pricing"
    else:
        status = "no_sku_prices"
    return {"status": status, "priced_models": count,
            "labelled_models": named, "distinct_prices": distinct}


def get_once(url):
    try:
        with urlopen(Request(url, headers={"Accept": "application/json",
                                          "User-Agent": "PriceResearchProbe/0.2"}), timeout=15) as r:
            status, data = r.status, r.read(900000)
        try:
            return status, json.loads(data), "json"
        except ValueError:
            return status, None, "not_json"
    except HTTPError as e:
        return e.code, None, "http_error"
    except (URLError, TimeoutError, OSError) as e:
        return None, None, type(e).__name__


def probe(name, actor):
    base = "https://api.apify.com/v2/acts/" + actor
    result = {"provider": name, "actor": actor,
              "time_utc": datetime.now(timezone.utc).isoformat(),
              "paid_runs_started": 0, "shopee_requests": 0}
    status, body, kind = get_once(base)
    result.update(metadata_http_status=status, metadata_kind=kind,
                  public_actor_metadata=isinstance(body, dict) and isinstance(body.get("data"), dict))
    status, body, kind = get_once(base + "/runs/last/dataset/items?clean=true&limit=3")
    result.update(public_example_http_status=status, public_example_kind=kind)
    if isinstance(body, list):
        checks = [assess_variant_data(x) for x in body[:3]]
        result["public_rows_examined"] = len(checks)
        result["variant_results"] = checks
        result["has_usable_multi_price_models"] = any(x.get("status") == "full_sku_pricing" for x in checks)
    else:
        result["public_rows_examined"] = 0
        result["has_usable_multi_price_models"] = False
    return result


if __name__ == "__main__":
    summary = {"scope": "public provider examples only",
               "providers": [probe(name, actor) for name, actor in ACTORS.items()]}
    path = Path("apps/ScraplingProbe/provider-public-check.json")
    path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
