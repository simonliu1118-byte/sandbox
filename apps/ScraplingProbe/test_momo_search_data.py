import unittest
from momo_search_data import price_value, read_goods


class MomoSearchDataTests(unittest.TestCase):
    def test_multiple_products_with_individual_prices(self):
        raw = {"success": True, "rtnSearchData": {"goodsInfoList": [
            {"goodsCode": "9956761", "goodsName": "羅技 M331 黑", "goodsPrice": "$699"},
            {"goodsCode": "9956762", "goodsName": "羅技 M331 白", "goodsPrice": "799"},
            {"goodsCode": "9956763", "goodsName": "羅技 M331 黑", "goodsPrice": "請登入"},
            {"goodsCode": "9956761", "goodsName": "羅技 M331 黑", "goodsPrice": "$699"},
            {"goodsCode": "9999999", "goodsName": "其他滑鼠", "goodsPrice": "299"}]}}
        result = read_goods(raw, "羅技 M331")
        self.assertEqual(result["raw_goods_count"], 5)
        self.assertEqual(len(result["products"]), 3)
        self.assertEqual(result["products"][0]["price_twd"], 699)
        self.assertEqual(result["products"][1]["price_twd"], 799)
        self.assertIsNone(result["products"][2]["price_twd"])

    def test_response_shape_changes_not_success(self):
        result = read_goods({"items": [{"name": "羅技 M331", "price": 699}]}, "羅技 M331")
        self.assertEqual(result["status"], "unexpected_json_shape")
        self.assertEqual(result["products"], [])

    def test_price_rejects_ranges_and_invalid(self):
        for value in ("$299-$599", "99~199", "null", None, 0, -8, True):
            self.assertIsNone(price_value(value))
        self.assertEqual(price_value("1,299"), 1299)


    def test_sale_price_from_live_style_fixture(self):
        row = {"goodsCode": "9956761", "goodsName": "羅技 M331 滑鼠",
               "SALE_PRICE": "699", "goodsPrice": "$799"}
        result = read_goods({"success": True, "rtnSearchData": {"goodsInfoList": [row]}}, "羅技 M331")
        self.assertEqual(result["status"], "products_with_prices")
        self.assertEqual(result["products"][0]["price_twd"], 699)

    def test_site_explicit_failure_is_not_success(self):
        result = read_goods({"success": False, "rtnSearchData": {"goodsInfoList": [
            {"goodsCode": "9956761", "goodsName": "羅技 M331", "SALE_PRICE": "699"}]}}, "羅技 M331")
        self.assertEqual(result["status"], "provider_response_not_successful")

    def test_missing_product_id_not_success(self):
        result = read_goods({"success": True, "rtnSearchData": {"goodsInfoList": [
            {"goodsName": "羅技 M331", "goodsPrice": "299"}]}}, "羅技 M331")
        self.assertEqual(result["status"], "no_matching_products")


if __name__ == "__main__":
    unittest.main()
