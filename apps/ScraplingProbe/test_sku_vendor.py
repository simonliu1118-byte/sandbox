import json
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from sku_vendor_validator import evaluate, records_from_path


def sku(sid, price, **overrides):
    row = {
        "site": "TW",
        "currency": "TWD",
        "shop_id": "123",
        "item_id": "456",
        "sku_id": sid,
        "sku_name": "測試規格" + str(sid),
        "sku_price": price,
        "product_url": "https://shopee.tw/product/123/456",
        "captured_at": "2026-11-15T00:00:00+08:00",
    }
    row.update(overrides)
    return row


class SkuVendorValidatorTests(unittest.TestCase):
    def test_two_variants_with_prices_pass(self):
        r = evaluate([sku("a", 299), sku("b", "NT$399")],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["multi_sku_products"], 1)
        self.assertEqual(r["multi_sku_products_with_distinct_prices"], 1)

    def test_price_range_without_variant_prices_is_fallback(self):
        r = evaluate([sku("a", None, price_min=299, price_max=399), sku("b", None)],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["status"], "fallback_product_price_bounds")
        self.assertEqual(r["fallback_product_count"], 1)
        self.assertEqual(r["fallback_range_count"], 1)
        self.assertEqual(r["multi_sku_products"], 0)

    def test_only_minimum_price_is_fallback(self):
        r = evaluate([sku("a", None, price_min="NT$299")],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["status"], "fallback_product_price_bounds")
        self.assertEqual(r["fallback_min_only_count"], 1)
        self.assertEqual(r["fallback_max_only_count"], 0)

    def test_only_maximum_price_is_fallback(self):
        r = evaluate([sku("a", None, max_price=599)],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["status"], "fallback_product_price_bounds")
        self.assertEqual(r["fallback_max_only_count"], 1)

    def test_inverted_range_is_not_fallback(self):
        r = evaluate([sku("a", None, price_min=599, price_max=299)])
        self.assertEqual(r["status"], "insufficient_evidence")
        self.assertEqual(r["fallback_product_count"], 0)

    def test_variant_level_takes_priority_over_product_range(self):
        r = evaluate([sku("a", 299, price_min=299, price_max=599),
                      sku("b", 599, price_min=299, price_max=599)],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["status"], "complete_multi_sku_price_evidence")
        self.assertEqual(r["fallback_product_count"], 0)

    def test_region_and_currency_must_match(self):
        r = evaluate([sku("a", 299, currency="BRL"), sku("b", 399)])
        self.assertEqual(r["multi_sku_products"], 0)
        self.assertEqual(r["reject_reasons"].get("not_taiwan_twd"), 1)

    def test_missing_variant_name_or_url_fails(self):
        r = evaluate([sku("a", 299, sku_name=""), sku("b", 399, product_url="https://example.com")])
        self.assertEqual(r["valid_sku_rows"], 0)

    def test_same_price_but_two_models_still_variant_level(self):
        r = evaluate([sku("a", 299), sku("b", 299)],
                     datetime(2026, 11, 15, tzinfo=timezone.utc))
        self.assertEqual(r["multi_sku_products"], 1)
        self.assertEqual(r["multi_sku_products_with_distinct_prices"], 0)

    def test_json_join_products_and_skus(self):
        data = {"products":[{"item_id":"456","shop_id":"123","site":"TW",
                             "currency":"TWD","product_url":"https://shopee.tw/product/123/456",
                             "captured_at":"2026-11-15T00:00:00+08:00"}],
                "skus":[{"item_id":"456","sku_id":"a","sku_name":"小","price":299},
                        {"item_id":"456","sku_id":"b","sku_name":"大","price":399}]}
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "sample.json"
            p.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
            rows = records_from_path(p)
            self.assertEqual(len(rows), 2)
            self.assertEqual(evaluate(rows)["multi_sku_products"], 1)

    def test_old_prices_flagged(self):
        r = evaluate([sku("a", 299), sku("b", 399)],
                     datetime(2026, 12, 20, tzinfo=timezone.utc))
        self.assertEqual(r["multi_sku_products_older_than_seven_days"], 1)


if __name__ == "__main__":
    unittest.main()
