"""Offline validator for vendor-delivered Taiwan Shopee variant price samples.

Never requests Shopee or vendors. Keeps only aggregate findings in report JSON.
Inputs are normalized CSV/JSON sample records -- do not commit real vendor data.
"""
import argparse
import csv
import json
import re
from collections import defaultdict
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from urllib.parse import urlparse


def pick(record, *names):
    for name in names:
        value = record.get(name)
        if value is not None and str(value).strip():
            return value
    return None


def numeric_price(value):
    if value is None or isinstance(value, bool):
        return None
    text = str(value).replace(",", "").strip()
    text = re.sub(r"^(?:NT\$|TWD\s*)", "", text, flags=re.IGNORECASE).strip()
    if not re.fullmatch(r"\d+(?:\.\d{1,2})?", text):
        return None
    try:
        amount = Decimal(text)
    except InvalidOperation:
        return None
    return amount if Decimal("0") < amount <= Decimal("10000000") else None


def records_from_path(path):
    path = Path(path)
    if path.suffix.lower() == ".csv":
        with path.open(encoding="utf-8-sig", newline="") as f:
            return list(csv.DictReader(f))
    if path.suffix.lower() == ".json":
        payload = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(payload, list):
            return payload
        if isinstance(payload, dict):
            if isinstance(payload.get("skus"), list):
                parents = {}
                for product in payload.get("products", []):
                    if isinstance(product, dict):
                        pid = str(pick(product, "spu_id", "product_id", "item_id", "itemId") or "")
                        if pid:
                            parents[pid] = product
                merged = []
                for sku in payload["skus"]:
                    if not isinstance(sku, dict):
                        continue
                    pid = str(pick(sku, "spu_id", "product_id", "item_id", "itemId") or "")
                    merged.append({**parents.get(pid, {}), **sku})
                return merged
            if isinstance(payload.get("rows"), list):
                return payload["rows"]
    raise ValueError("Input must be CSV, JSON array, or JSON {products, skus}")


def normalize(row):
    if not isinstance(row, dict):
        return None, "invalid_row"
    region = str(pick(row, "market", "region", "site", "country") or "").strip().lower()
    currency = str(pick(row, "currency", "currency_code") or "").strip().upper()
    if region not in {"tw", "taiwan", "taiwan (tw)"} or currency != "TWD":
        return None, "not_taiwan_twd"
    shop = pick(row, "shop_id", "shopId")
    item = pick(row, "item_id", "itemId")
    sku = pick(row, "sku_id", "skuId", "model_id", "modelId")
    label = pick(row, "sku_name", "model_name", "modelName", "variant_name", "variantName", "options_label")
    if not all((shop, item, sku, label)):
        return None, "missing_identity_or_variant_name"
    price = numeric_price(pick(row, "sku_real_price", "sku_price", "variant_price", "price_twd",
                               "current_price", "price"))
    if price is None:
        return None, "missing_individual_price"
    url = pick(row, "product_url", "productUrl", "source_url", "url")
    parsed = urlparse(str(url or ""))
    if parsed.scheme != "https" or parsed.hostname not in {"shopee.tw", "www.shopee.tw"}:
        return None, "missing_shopee_product_link"
    captured = pick(row, "captured_at", "collected_at", "scraped_at", "timestamp", "checked_at")
    try:
        dt = datetime.fromisoformat(str(captured).replace("Z", "+00:00"))
        if dt.tzinfo is None:
            raise ValueError("timezone required")
    except (ValueError, TypeError, OverflowError):
        return None, "missing_timestamp_timezone"
    return {
        "key": (str(shop), str(item)),
        "sku": str(sku),
        "label": str(label).strip(),
        "price": price,
        "captured": dt,
    }, None


def normalize_fallback(row):
    """Validate product-level price bounds separately from SKU-level pricing."""
    if not isinstance(row, dict):
        return None, "invalid_row"
    region = str(pick(row, "market", "region", "site", "country") or "").strip().lower()
    currency = str(pick(row, "currency", "currency_code") or "").strip().upper()
    if region not in {"tw", "taiwan", "taiwan (tw)"} or currency != "TWD":
        return None, "not_taiwan_twd"
    shop = pick(row, "shop_id", "shopId")
    item = pick(row, "item_id", "itemId")
    if not shop or not item:
        return None, "missing_product_identity"
    low = numeric_price(pick(row, "price_min", "min_price", "priceMin",
                             "minPrice", "lowest_price"))
    high = numeric_price(pick(row, "price_max", "max_price", "priceMax",
                              "maxPrice", "highest_price"))
    if low is None and high is None:
        return None, "no_product_price_bounds"
    if low is not None and high is not None and low > high:
        return None, "inverted_price_range"
    url = pick(row, "product_url", "productUrl", "source_url", "url")
    parsed = urlparse(str(url or ""))
    if parsed.scheme != "https" or parsed.hostname not in {"shopee.tw", "www.shopee.tw"}:
        return None, "missing_shopee_product_link"
    captured = pick(row, "captured_at", "collected_at", "scraped_at", "timestamp", "checked_at")
    try:
        dt = datetime.fromisoformat(str(captured).replace("Z", "+00:00"))
        if dt.tzinfo is None:
            raise ValueError("timezone required")
    except (ValueError, TypeError, OverflowError):
        return None, "missing_timestamp_timezone"
    return {"key": (str(shop), str(item)), "min": low, "max": high, "captured": dt}, None


def fallback_cost_gate(fallback_count, source_cost_model="unknown",
                       free_terms_verified=False,
                       projected_volume_within_free_limit=False,
                       automatic_paid_overage_disabled=False):
    """An available price bound is not an approved backup unless ongoing cost is zero.

    Inputs are *operator* attestations, never read from provider-supplied JSON.
    A one-time registration gift or trial does not qualify as ongoing free access.
    """
    if not fallback_count:
        return {"decision": "not_applicable", "approved_product_count": 0}
    if source_cost_model == "free_unlimited" and free_terms_verified:
        return {"decision": "eligible_zero_cost", "approved_product_count": fallback_count}
    if (source_cost_model == "free_recurring_limited"
            and free_terms_verified
            and projected_volume_within_free_limit
            and automatic_paid_overage_disabled):
        return {"decision": "eligible_zero_cost_with_hard_quota",
                "approved_product_count": fallback_count}
    return {"decision": "excluded_not_proven_permanently_free",
            "approved_product_count": 0}


def evaluate(rows, as_of=None, *, source_cost_model="unknown",
             free_terms_verified=False,
             projected_volume_within_free_limit=False,
             automatic_paid_overage_disabled=False):
    now = as_of or datetime.now(timezone.utc)
    groups = defaultdict(dict)
    fallback_groups = {}
    errors = defaultdict(int)
    for record in rows:
        normalized, sku_error = normalize(record)
        if normalized:
            groups[normalized["key"]][normalized["sku"]] = normalized
        fallback, fallback_error = normalize_fallback(record)
        if fallback:
            fallback_groups[fallback["key"]] = fallback
        if normalized is None and fallback is None:
            errors[sku_error or fallback_error or "invalid_row"] += 1
    qualified = []
    stale = 0
    distinct_prices = 0
    for group in groups.values():
        if len(group) < 2:
            continue
        qualified.append(group)
        if len({row["price"] for row in group.values()}) > 1:
            distinct_prices += 1
        if any(abs((now - row["captured"]).total_seconds()) > 86400 * 7 for row in group.values()):
            stale += 1
    qualified_keys = {next(iter(group.values()))["key"] for group in qualified}
    backup_only = {
        key: product for key, product in fallback_groups.items()
        if key not in qualified_keys
    }
    cost_gate = fallback_cost_gate(
        len(backup_only),
        source_cost_model=source_cost_model,
        free_terms_verified=free_terms_verified,
        projected_volume_within_free_limit=projected_volume_within_free_limit,
        automatic_paid_overage_disabled=automatic_paid_overage_disabled,
    )
    if qualified:
        status = "complete_multi_sku_price_evidence"
    elif backup_only:
        status = "fallback_product_price_bounds"
    else:
        status = "insufficient_evidence"
    return {
        "status": status,
        "rows_examined": len(rows),
        "valid_sku_rows": sum(len(g) for g in groups.values()),
        "multi_sku_products": len(qualified),
        "multi_sku_products_with_distinct_prices": distinct_prices,
        "multi_sku_products_older_than_seven_days": stale,
        "fallback_product_count": len(backup_only),
        "fallback_budget_eligibility": cost_gate["decision"],
        "eligible_zero_cost_fallback_count": cost_gate["approved_product_count"],
        "fallback_range_count": sum(p["min"] is not None and p["max"] is not None for p in backup_only.values()),
        "fallback_min_only_count": sum(p["min"] is not None and p["max"] is None for p in backup_only.values()),
        "fallback_max_only_count": sum(p["min"] is None and p["max"] is not None for p in backup_only.values()),
        "fallback_older_than_seven_days": sum(
            abs((now - p["captured"]).total_seconds()) > 86400 * 7 for p in backup_only.values()
        ),
        "reject_reasons": dict(sorted(errors.items())),
        "notice": "Backup bounds are product-level estimates, not prices of a chosen variant; checkout not verified.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, help="Local vendor CSV or JSON file; do not commit")
    parser.add_argument("--source-cost-model", choices=[
        "unknown", "paid", "trial", "signup_credits",
        "free_unlimited", "free_recurring_limited"
    ], default="unknown")
    parser.add_argument("--free-terms-verified", action="store_true",
                        help="Operator verified recurring free terms")
    parser.add_argument("--projected-volume-within-free-limit", action="store_true",
                        help="Operator verified expected volume stays within recurring free allowance")
    parser.add_argument("--automatic-paid-overage-disabled", action="store_true",
                        help="Hard-stop before any billable request")
    parser.add_argument("--output", default="vendor-sku-validation.json")
    opts = parser.parse_args()
    report = evaluate(
        records_from_path(opts.input),
        source_cost_model=opts.source_cost_model,
        free_terms_verified=opts.free_terms_verified,
        projected_volume_within_free_limit=opts.projected_volume_within_free_limit,
        automatic_paid_overage_disabled=opts.automatic_paid_overage_disabled,
    )
    output = Path(opts.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
