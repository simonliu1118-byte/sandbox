"""No-network tests for the candidate extraction contract."""
import unittest

from scrapling_probe import price_candidates_from_text, price_fields_from_json, valid_target


class ProbeTests(unittest.TestCase):
    def test_rendered_price_candidates(self):
        self.assertEqual(price_candidates_from_text("活動價 $50 原價 NT$1,290"), ["50", "1290"])

    def test_api_fields_are_raw_and_sku_scoped(self):
        data = {"data": {"item": {"price_min": 5000000, "models": [{"price": 6000000}]}}}
        self.assertEqual(
            price_fields_from_json(data),
            [
                {"field": "data.item.price_min", "raw": "5000000"},
                {"field": "data.item.models[0].price", "raw": "6000000"},
            ],
        )

    def test_only_public_shopee_https(self):
        self.assertTrue(valid_target("https://shopee.tw/product/1/2"))
        self.assertFalse(valid_target("http://shopee.tw/product/1/2"))
        self.assertFalse(valid_target("https://shopee.tw.example.com/product/1/2"))


if __name__ == "__main__":
    unittest.main()
