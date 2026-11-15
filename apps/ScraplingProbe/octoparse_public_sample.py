"""Check a publicly published Octoparse Shopee SKU dataset sample.

Reads only Hugging Face public dataset metadata and example rows.
Does not read Shopee, use accounts, buy an API job or download full datasets.
"""
from __future__ import annotations
import json
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode

REPO = "Octoparse/shopee-product-sku-data-sample"
BASE = "https://datasets-server.huggingface.co"


def get_json(url: str):
    try:
        with urlopen(Request(url, headers={"User-Agent": "PriceResearchProbe/0.2",
                                            "Accept": "application/json"}), timeout=15) as response:
            return response.status, json.loads(response.read(800000))
    except HTTPError as exc:
        return exc.code, None
    except (URLError, TimeoutError, OSError, ValueError) as exc:
        return None, type(exc).__name__


def inspect_rows(rows: list[dict]) -> dict:
    records = [r.get("row", r) for r in rows if isinstance(r, dict)]
    named = []
    for r in records:
        if not isinstance(r, dict):
            continue
        sku = r.get("sku_id") or r.get("skuId") or r.get("model_id")
        spu = r.get("spu_id") or r.get("spuId") or r.get("item_id")
        price = r.get("sku_real_price") or r.get("skuRealPrice") or r.get("price")
        region = str(r.get("market") or r.get("site") or "").lower()
        if sku is None or spu is None or not isinstance(price, (float, int, str)):
            continue
        try:
            val = float(str(price).replace(",", ""))
        except ValueError:
            continue
        if val <= 0:
            continue
        named.append((str(spu), str(sku), val, region))
    pair_counts = {}
    for spu, sku, price, region in named:
        pair_counts.setdefault((spu, region), {})[sku] = price
    multi = [(a, p) for a, p in pair_counts.items() if len(p) >= 2]
    return {
        "sample_rows": len(records),
        "sku_rows_with_numeric_price_and_parent": len(named),
        "multi_sku_product_groups_in_sample": len(multi),
        "groups_with_distinct_prices": sum(len(set(p.values())) >= 2 for _, p in multi),
        "market_codes_observed": sorted({r[3] for r in named if r[3]})[:10],
        "taiwan_multi_price_groups": sum(
            r in {"tw", "taiwan"} and len(set(p.values())) >= 2
            for (s, r), p in multi
        ),
    }


def main():
    report = {
        "dataset": REPO, "at_utc": datetime.now(timezone.utc).isoformat(),
        "platform": "Hugging Face public sample",
        "shopee_requests": 0, "paid_runs_started": 0,
        "status": "unknown", "configs": [],
    }
    status, info = get_json(BASE + "/splits?" + urlencode({"dataset": REPO}))
    report["splits_http_status"] = status
    if isinstance(info, dict) and isinstance(info.get("splits"), list):
        splits = info["splits"][:8]
        report["configs"] = [
            {"config": x.get("config"), "split": x.get("split"), "num_rows": x.get("num_rows")}
            for x in splits
        ]
        for x in splits[:4]:
            args = {"dataset": REPO, "config": x.get("config"), "split": x.get("split"),
                    "offset": 0, "length": 100}
            code, data = get_json(BASE + "/rows?" + urlencode(args))
            report.setdefault("samples", []).append({
                "config": x.get("config"), "split": x.get("split"),
                "http_status": code,
                "inspection": inspect_rows(data.get("rows", [])) if isinstance(data, dict) else None,
            })
        report["status"] = "sample_inspected"
    else:
        report["status"] = "public_rows_unavailable"
        report["error_class"] = info if isinstance(info, str) else None
    path = Path("apps/ScraplingProbe/octoparse-public-sample-report.json")
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
