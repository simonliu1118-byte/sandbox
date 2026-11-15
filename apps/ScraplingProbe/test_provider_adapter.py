import unittest
from datetime import datetime, timezone
from provider_sample_adapter import assess, money, to_records
from sku_vendor_validator import evaluate

NOW = datetime(2026, 11, 15, tzinfo=timezone.utc)
PRODUCT_URL = "https://shopee.tw/product/123/456"


def khalid(item, **kwargs):
    return {
        "region": "tw", "currency": "TWD", "shop_id": 123,
        "item_id": 456, "url": PRODUCT_URL,
        "scrapedAt": "2026-11-15T00:00:00+08:00",
        "status": "success",
        "raw": {"get_pc": {"data": {"item": item}}},
        **kwargs,
    }


def sian(models, **kwargs):
    return {
        "_rowType": "product", "market": "Taiwan", "currency": "TWD",
        "shopId": 123, "itemId": 456, "productUrl": PRODUCT_URL,
        "updatedAt": "2026-11-15T00:00:00+08:00",
        "status": "success", "skuModels": models, **kwargs,
    }


class ProviderSampleAdapterTests(unittest.TestCase):
    def test_khalid_two_named_sku_prices(self):
        p = khalid({"price_min": 29900000, "price_max": 59900000,
                    "models": [
                        {"model_id": 11, "name": "小", "price": 29900000},
                        {"model_id": 12, "name": "大", "price": 59900000}]})
        result = evaluate(to_records("khalid", [p]), NOW)
        self.assertEqual(result["status"], "complete_multi_sku_price_evidence")
        self.assertEqual(result["multi_sku_products"], 1)
        self.assertEqual(result["fallback_product_count"], 0)

    def test_khalid_missing_models_fallback(self):
        p = khalid({"price_min": 29900000, "price_max": 59900000, "models": []})
        result = evaluate(to_records("khalid", [p]), NOW)
        self.assertEqual(result["status"], "fallback_product_price_bounds")
        self.assertEqual(result["fallback_range_count"], 1)

    def test_khalid_hidden_model_prices_fallback(self):
        p = khalid({"price_min": 29900000, "price_max": 59900000,
                    "models": [{"model_id": 11, "name": "小", "price": None},
                               {"model_id": 12, "name": "大", "price": None}]})
        self.assertEqual(evaluate(to_records("khalid", [p]), NOW)["multi_sku_products"], 0)

    def test_sian_two_named_prices(self):
        p = sian([{"model_id": 21, "name": "黑", "price": 320},
                  {"model_id": 22, "name": "白", "price": 350}],
                 priceMin=320, priceMax=350)
        result = evaluate(to_records("sian", [p]), NOW)
        self.assertEqual(result["status"], "complete_multi_sku_price_evidence")

    def test_sian_example_without_variant_name_does_not_pass(self):
        p = sian([{"model_id": 21, "price": 599}],
                 priceMin=499, priceMax=599)
        result = evaluate(to_records("sian", [p]), NOW)
        self.assertEqual(result["status"], "fallback_product_price_bounds")
        self.assertEqual(result["multi_sku_products"], 0)

    def test_sian_without_bounds_or_named_models_fails(self):
        p = sian([{"model_id": 21, "price": 599}])
        self.assertEqual(evaluate(to_records("sian", [p]), NOW)["status"],
                         "insufficient_evidence")

    def test_not_taiwan_fails(self):
        p = khalid({"price_min": 29900000}, region="br", currency="BRL")
        self.assertEqual(evaluate(to_records("khalid", [p]), NOW)["status"],
                         "insufficient_evidence")

    def test_failed_actor_not_a_price(self):
        p = khalid({"price_min": 29900000}, status="failed")
        self.assertEqual(to_records("khalid", [p]), [])

    def test_do_not_infer_sku_price_from_range(self):
        p = khalid({"price_min": 29900000, "price_max": 59900000,
                    "models": [{"model_id": 11, "name": "小"},
                               {"model_id": 12, "name": "大"}]})
        result = evaluate(to_records("khalid", p), NOW)
        self.assertEqual(result["multi_sku_products"], 0)
        self.assertEqual(result["fallback_range_count"], 1)

    def test_invalid_prices_and_scale(self):
        self.assertIsNone(money(None))
        self.assertIsNone(money(True))
        self.assertIsNone(money("not-supplied"))
        self.assertEqual(money(500000, 100000), "5")
        self.assertEqual(money(7386000, 100000), "73.86")

    def test_unknown_provider_rejected(self):
        with self.assertRaises(ValueError):
            assess("unknown", [])

    def test_source_classification_is_not_runtime_proof(self):
        report = assess("khalid", [])
        self.assertEqual(report["source_proof"], "unverified_vendor_output")


if __name__ == "__main__":
    unittest.main()
