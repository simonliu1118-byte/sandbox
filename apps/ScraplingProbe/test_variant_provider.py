import unittest
from variant_provider_probe import assess_variant_data


class VariantProviderTests(unittest.TestCase):
    def test_two_named_distinct_prices(self):
        row = {"region":"tw","currency":"TWD","raw":{"get_pc":{"data":{"item":{"models":[
            {"model_id":1,"name":"小","price":29900000},
            {"model_id":2,"name":"大","price":59900000}]}}}}}
        self.assertEqual(assess_variant_data(row)["status"], "full_sku_pricing")

    def test_price_range_is_not_variant_price(self):
        row = {"region":"tw","currency":"TWD","priceMin":299,"priceMax":599,
               "skuModels":[{"model_id":1,"name":"小","price":None},{"model_id":2,"name":"大","price":None}]}
        self.assertEqual(assess_variant_data(row)["status"], "no_sku_prices")

    def test_id_price_without_name_is_partial(self):
        row = {"market":"Taiwan","currency":"TWD","skuModels":[
            {"model_id":1,"price":299},{"model_id":2,"price":599}]}
        self.assertEqual(assess_variant_data(row)["status"], "partial_sku_pricing")

    def test_two_same_prices_distinguished(self):
        row = {"region":"tw","currency":"TWD","variants":[
            {"modelId":1,"name":"黑","price":500},{"modelId":2,"name":"白","price":500}]}
        self.assertEqual(assess_variant_data(row)["status"], "uniform_sku_pricing")

    def test_other_country_not_taiwan(self):
        row = {"region":"br","currency":"BRL","skuModels":[{"model_id":1,"name":"a","price":99}]}
        self.assertEqual(assess_variant_data(row)["status"], "not_taiwan_product")


if __name__ == "__main__":
    unittest.main()
