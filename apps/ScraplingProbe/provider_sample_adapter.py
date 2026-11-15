"""Normalize *provided* Apify Shopee results into the offline dual-track validator.

No network calls, credentials, actor runs or paid actions. Provider JSON samples
are user-owned local inputs; never commit them to a public repository.
"""
import argparse
import json
from decimal import Decimal, InvalidOperation
from pathlib import Path

from sku_vendor_validator import evaluate

SUPPORTED = ("khalid", "sian")


def money(value, scale=1):
    """Parse provider numeric prices; never convert missing/hidden prices to zero."""
    if value is None or isinstance(value, bool):
        return None
    try:
        number = Decimal(str(value).replace(",", "").strip()) / Decimal(scale)
    except (InvalidOperation, TypeError, ValueError, ZeroDivisionError):
        return None
    if not number.is_finite() or number <= 0 or number > 10000000:
        return None
    return format(number.normalize(), "f")


def first(*values):
    for value in values:
        if value is not None and str(value).strip():
            return value
    return None


def base_record(row, provider):
    raw = row.get("raw") or {}
    get_pc = raw.get("get_pc") or {}
    item = ((get_pc.get("data") or {}).get("item") or {})
    result = {
        "site": first(row.get("region"), row.get("market"), row.get("site")),
        "currency": first(row.get("currency"), item.get("currency")),
        "shop_id": first(row.get("shop_id"), row.get("shopId"), item.get("shop_id")),
        "item_id": first(row.get("item_id"), row.get("itemId"), item.get("item_id")),
        "product_url": first(row.get("url"), row.get("productUrl")),
        "captured_at": first(row.get("scrapedAt"), row.get("_fetchedAt"),
                             row.get("updatedAt")),
    }
    if provider == "khalid":
        result["price_min"] = first(
            money(item.get("price_min"), 100000), money(row.get("priceMin")))
        result["price_max"] = first(
            money(item.get("price_max"), 100000), money(row.get("priceMax")))
    elif provider == "sian":
        result["price_min"] = money(first(row.get("priceMin"), row.get("minPrice")))
        result["price_max"] = money(first(row.get("priceMax"), row.get("maxPrice")))
    return result, item


def to_records(provider, payload):
    """Return flat SKU and bound rows. No guessing of model names or price scale."""
    if provider not in SUPPORTED:
        raise ValueError("Unsupported provider; only khalid and sian are supported")
    if isinstance(payload, dict):
        rows = payload.get("items") if isinstance(payload.get("items"), list) else [payload]
    elif isinstance(payload, list):
        rows = payload
    else:
        raise ValueError("Expected a JSON item, list or object with items")
    normalized = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        status = str(row.get("status") or "success").lower()
        if status in {"failed", "expired", "no_data", "invalid_input",
                      "error", "not_found", "product_not_available"}:
            continue
        if provider == "sian" and row.get("_rowType") not in (None, "product"):
            continue
        base, item = base_record(row, provider)
        # Fallback bounds are a product-level record, not a SKU price.
        normalized.append(base)
        models = (item.get("models") or []) if provider == "khalid" else (
            row.get("skuModels") or []
        )
        for model in models[:200] if isinstance(models, list) else []:
            if not isinstance(model, dict):
                continue
            model_id = first(model.get("model_id"), model.get("modelId"))
            label = first(model.get("name"), model.get("modelName"),
                          model.get("variantName"), model.get("label"))
            price = money(
                model.get("price"),
                100000 if provider == "khalid" else 1
            )
            if model_id is None or price is None:
                continue
            normalized.append({
                **base, "sku_id": str(model_id), "sku_name": label,
                "sku_price": price,
            })
    return normalized


def assess(provider, payload):
    data = to_records(provider, payload)
    result = evaluate(data)
    result["provider"] = provider
    result["normalized_rows"] = len(data)
    result["source_proof"] = "unverified_vendor_output"
    result["disclaimer"] = (
        "The offline test checks fields, not actual Shopee access, provider "
        "permissions, live freshness, commercial licensing or checkout prices."
    )
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--provider", required=True, choices=SUPPORTED)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", default="provider-adapter-report.json")
    args = parser.parse_args()
    payload = json.loads(Path(args.input).read_text(encoding="utf-8"))
    report = assess(args.provider, payload)
    dest = Path(args.output)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
