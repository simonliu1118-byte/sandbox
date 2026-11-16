"""One-off, unauthenticated, single-page Shopee TW variant and price check.

Research basis: omkarcloud/shopee-scraper documents server-rendered
mfe-initial-data including model IDs/names but null model prices when
signed out. This experiment tests a verified indexed TW four-option listing, without
its retry/proxy fallback code, browser login or third-party price sources.

One HTTPS GET; no cookies/login/proxy, no retries, no challenge handling,
no raw HTML/headers stored or logged. If a challenge appears, stop.
"""
import json
import re
from datetime import datetime, timezone
from urllib.parse import urlparse
import never_primp as primp

SHOP_ID = 1133901773
ITEM_ID = 28163998437
URL = f"https://shopee.tw/product/{SHOP_ID}/{ITEM_ID}"
BLOB_RE = re.compile(r'<script\s+type="text/mfe-initial-data"[^>]*>(.*?)</script>', re.S)
DENOMINATOR = 100000


def valid_price(raw):
    if isinstance(raw, bool) or not isinstance(raw, (int, float)) or raw <= 0:
        return None
    result = raw / DENOMINATOR
    return round(result, 2) if 0 < result < 10000000 else None


def analyse_json(blob, expected_shop=SHOP_ID, expected_item=ITEM_ID):
    """Require a matching product and independently priced, named models."""
    try:
        cached = blob["initialState"]["DOMAIN_PDP"]["data"]["PDP_BFF_DATA"]["cachedMap"]
    except (TypeError, KeyError):
        return {"status": "unexpected_json_layout"}
    if not isinstance(cached, dict) or not cached:
        return {"status": "missing_product"}
    record = next((v for v in cached.values() if isinstance(v, dict) and
                   isinstance(v.get("item"), dict)), None)
    if record is None:
        return {"status": "missing_product"}
    item = record["item"]
    shop_id = item.get("shop_id") or item.get("shopid")
    item_id = item.get("item_id") or item.get("itemid")
    if str(shop_id) != str(expected_shop) or str(item_id) != str(expected_item):
        return {"status": "unmatched_item"}
    models = item.get("models")
    models = models if isinstance(models, list) else []
    output = []
    for model in models[:60]:
        if not isinstance(model, dict):
            continue
        mid = model.get("model_id") or model.get("modelid")
        name = str(model.get("name") or "").strip()
        ext = model.get("extinfo") if isinstance(model.get("extinfo"), dict) else {}
        output.append({"model_id": str(mid) if mid else "",
                       "name": name[:80],
                       "option_indices": ext.get("tier_index"),
                       "display_price_twd": valid_price(model.get("price"))})
    keys = [x["model_id"] for x in output if x["model_id"]]
    complete = [x for x in output if x["model_id"] and x["name"] and
                x["display_price_twd"] is not None]
    qualifies = (len(output) >= 2 and len(keys) == len(output) and
                 len(set(keys)) == len(keys) and len(complete) == len(output))
    state = ("complete_multi_sku_price_evidence" if qualifies
             else "variant_structure_without_prices" if len(output) >= 2 and not complete
             else "partial_or_incomplete_sku_evidence")
    return {"status": state,
            "currency": "TWD",
            "models_total": len(output),
            "models_with_id": len(keys),
            "models_with_independent_price": sum(x["display_price_twd"] is not None for x in output),
            "models_with_full_evidence": len(complete),
            "parent_price_min_present": valid_price(item.get("price_min")) is not None,
            "parent_price_max_present": valid_price(item.get("price_max")) is not None,
            "samples": output[:3],
            "note": "Parent min/max or product-level price never counts as per-model evidence."}


def evaluate_html(html, expected_shop=SHOP_ID, expected_item=ITEM_ID):
    if any(marker in html[:3500].lower() for marker in
           ("captcha", "access denied", "verify/traffic", "流量驗證")):
        return {"status": "traffic_challenge"}
    match = BLOB_RE.search(html)
    if match is None:
        return {"status": "html_without_product_blob"}
    try:
        payload = json.loads(match.group(1))
    except ValueError:
        return {"status": "invalid_product_json"}
    return analyse_json(payload, expected_shop, expected_item)


def run():
    started = datetime.now(timezone.utc).isoformat()
    report = {"source": "shopee_tw_anonymous_public_product",
              "checked_at": started, "status": "network_error",
              "request_count": 1, "paid_requests": 0,
              "product_ids": f"{SHOP_ID}.{ITEM_ID}"}
    # Run exactly one ordinary request using the existing project's
    # public-page HTTP client; never use a logged-in session or proxies.
    headers = {"accept": "text/html,application/xhtml+xml",
               "accept-language": "zh-TW,zh;q=0.9",
               "x-api-source": "pc",
               "x-requested-with": "XMLHttpRequest",
               "x-shopee-language": "en"}
    try:
        with primp.Client(impersonate="chrome_142", impersonate_os="windows",
                          timeout=20.0, http2_only=True, headers=headers) as client:
            response = client.get(URL)
            report["http_status"] = response.status_code
            final = urlparse(str(response.url))
            report["final_path_kind"] = (
                "verify" if "/verify/" in final.path else
                "login" if "/buyer/login" in final.path else
                "product" if "/product/" in final.path else "other")
            if final.hostname != "shopee.tw" or final.scheme != "https":
                report["status"] = "unexpected_redirect"
                return report
            if response.status_code in (403, 418, 429):
                report["status"] = "blocked"
                return report
            if "/verify/" in final.path or "/buyer/login" in final.path:
                report["status"] = "traffic_challenge"
                return report
            if response.status_code != 200:
                report["status"] = "http_error"
                return report
            report.update(evaluate_html(response.text))
    except Exception as exc:
        report["error_type"] = type(exc).__name__
    return report


if __name__ == "__main__":
    print(json.dumps(run(), ensure_ascii=False))
