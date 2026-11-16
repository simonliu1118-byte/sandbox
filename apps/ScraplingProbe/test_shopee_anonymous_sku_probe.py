"""Offline tests: partial SKU data must never be counted as per-model price."""
import json
import unittest
from shopee_anonymous_sku_probe import analyse_json, evaluate_html, valid_price

def fixture(models, price_min=None, price_max=None):
    item={"shop_id":1133901773, "item_id":28163998437, "models":models,
          "price_min": price_min, "price_max":price_max}
    return {"initialState":{"DOMAIN_PDP":{"data":{"PDP_BFF_DATA":{
        "cachedMap":{"test":{"item":item}}}}}}}

class AnonymousSkuProbeTests(unittest.TestCase):
    def test_valid_two_models_have_independent_prices(self):
        a=fixture([
            {"model_id":11,"name":"0.5吋 24入","price":20000000},
            {"model_id":12,"name":"1吋 12入","price":25000000}])
        report=analyse_json(a)
        self.assertEqual(report["status"],"complete_multi_sku_price_evidence")
        self.assertEqual(report["models_with_independent_price"],2)
        self.assertEqual([x["display_price_twd"] for x in report["samples"]],[200,250])

    def test_four_models_require_four_independent_prices(self):
        rows = [{"model_id": 101+i, "name": f"規格{i}", "price": (200+i*10)*100000}
                for i in range(4)]
        report = analyse_json(fixture(rows))
        self.assertEqual(report["models_total"], 4)
        self.assertEqual(report["models_with_full_evidence"], 4)
        self.assertEqual(report["status"], "complete_multi_sku_price_evidence")
        rows[2]["price"] = None
        report = analyse_json(fixture(rows))
        self.assertEqual(report["models_with_full_evidence"], 3)
        self.assertEqual(report["status"], "partial_or_incomplete_sku_evidence")

    def test_price_range_and_models_without_prices_are_not_complete(self):
        a=fixture([
            {"model_id":11,"name":"0.5吋 24入","price":None},
            {"model_id":12,"name":"1吋 12入","price":None}],20000000,25000000)
        report=analyse_json(a)
        self.assertEqual(report["status"],"variant_structure_without_prices")
        self.assertEqual(report["models_with_independent_price"],0)
        self.assertTrue(report["parent_price_min_present"])

    def test_one_model_price_missing_is_partial(self):
        a=fixture([
            {"model_id":11,"name":"0.5吋 24入","price":20000000},
            {"model_id":12,"name":"1吋 12入","price":None}])
        self.assertEqual(analyse_json(a)["status"],"partial_or_incomplete_sku_evidence")

    def test_unmatched_item_is_not_accepted(self):
        self.assertEqual(analyse_json(fixture([]),expected_item=9999)["status"],"unmatched_item")

    def test_synthetic_html_blob(self):
        a=fixture([
            {"model_id":11,"name":"0.5吋 24入","price":20000000},
            {"model_id":12,"name":"1吋 12入","price":25000000}])
        html='<script type="text/mfe-initial-data">'+json.dumps(a)+'</script>'
        self.assertEqual(evaluate_html(html)["models_with_full_evidence"],2)

    def test_empty_product_shell(self):
        self.assertEqual(evaluate_html("<html><title>Shopee</title></html>")["status"],
                         "html_without_product_blob")

    def test_invalid_prices_are_not_zero_or_full_evidence(self):
        self.assertIsNone(valid_price(None))
        self.assertIsNone(valid_price(0))
        self.assertIsNone(valid_price(True))

if __name__=="__main__":
    unittest.main()
