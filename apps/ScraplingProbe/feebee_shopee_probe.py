"""Historic Shopee Feebee research adapter, now using the shared site parser.

No independent crawler/parser is kept here; both markets use site/feebee_search.py.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "site"))
from feebee_search import fetch_once, make_search_url, parse_feebee_results

def make_url(keyword):
    return make_search_url(keyword)


def _research_result(status, rows):
    counts = {"mall": 0, "marketplace": 0}
    samples = []
    for row in rows:
        counts[row["shop_type"]] += 1
        samples.append({
            "market_type": row["shop_type"],
            "merchant": row["merchant"], "seller": row["seller"],
            "title": row["title"], "reference_price_twd": row["price"],
            "feebee_link": row["url"]
        })
    return {"status": "paired" if status == "ok"
            else "unpaired" if status == "no_price" else "no_shopee",
            "matched_count": len(rows), "market_counts": counts,
            "samples": samples[:8]}


def parse_search_html(html, source_url):
    status, rows = parse_feebee_results(html, source_url, market="shopee")
    return _research_result(status, rows)


def run(keyword):
    report = fetch_once(keyword)
    if report.get("status") != "ok":
        return {"status": report["status"], "checked_at": report.get("checked_at")}
    status, rows = report["markets"]["shopee"]
    return {"checked_at": report.get("checked_at"), "http_status": 200,
            **_research_result(status, rows)}


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--keyword", default="3m 1530-1")
    args = ap.parse_args()
    print(json.dumps(run(args.keyword), ensure_ascii=False))
