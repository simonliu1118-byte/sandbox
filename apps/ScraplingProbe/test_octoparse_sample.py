import unittest
from octoparse_public_sample import inspect_rows


class OctoparseSampleTests(unittest.TestCase):
    def test_two_prices_same_product(self):
        rows = [{"row": {"spu_id": "p1", "sku_id": "a", "sku_real_price": "299", "market": "TW"}},
                {"row": {"spu_id": "p1", "sku_id": "b", "sku_real_price": "399", "market": "TW"}}]
        result = inspect_rows(rows)
        self.assertEqual(result["taiwan_multi_price_groups"], 1)
        self.assertEqual(result["groups_with_distinct_prices"], 1)

    def test_minmax_only_does_not_count_as_skus(self):
        rows = [{"row": {"spu_id": "p1", "min_price": "299", "max_price": "399"}}]
        self.assertEqual(inspect_rows(rows)["sku_rows_with_numeric_price_and_parent"], 0)

    def test_wrong_region_not_taiwan(self):
        rows = [{"row": {"spu_id": "p1", "sku_id": "a", "sku_real_price": 299, "market": "BR"}},
                {"row": {"spu_id": "p1", "sku_id": "b", "sku_real_price": 399, "market": "BR"}}]
        self.assertEqual(inspect_rows(rows)["taiwan_multi_price_groups"], 0)
        self.assertEqual(inspect_rows(rows)["multi_sku_product_groups_in_sample"], 1)


if __name__ == "__main__":
    unittest.main()
