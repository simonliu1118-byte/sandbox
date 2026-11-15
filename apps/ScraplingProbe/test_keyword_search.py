import unittest
from keyword_search import keyword_value, fits, momo_id, parse_momo_listing, import_shopee_keyword_results, html_report


class KeywordSearchTests(unittest.TestCase):
    def test_keyword_rules(self):
        self.assertEqual(keyword_value(" 羅技  M331 "), "羅技 M331")
        for s in ("", "A", "x" * 81):
            with self.assertRaises(ValueError):
                keyword_value(s)

    def test_keyword_match(self):
        self.assertTrue(fits("羅技 M331", "Logitech 羅技 M331 靜音"))
        self.assertFalse(fits("羅技 M331", "羅技 M350"))

    def test_momo_ids(self):
        self.assertEqual(momo_id("https://www.momoshop.com.tw/product/9956761"), "9956761")
        self.assertEqual(momo_id("https://www.momoshop.com.tw/goods/GoodsDetail.jsp?i_code=9956761"), "9956761")
        self.assertIsNone(momo_id("https://evil.test/product/9956761"))

    def test_momo_cards(self):
        page = ('<a href="/product/9956761">羅技 M331 滑鼠 <b>$699</b></a>'
                '<a href="/product/9956761">羅技 M331 $699</a>'
                '<a href="/product/999999">別的滑鼠 $120</a>')
        r = parse_momo_listing(page, "羅技 M331", "https://www.momoshop.com.tw/search/x")
        self.assertEqual(len(r["products"]), 1)
        self.assertEqual(r["products"][0]["price_twd"], 699)

    def test_no_price_does_not_become_zero(self):
        r = parse_momo_listing('<a href="/product/9956761">羅技 M331</a>',
                               "羅技 M331", "https://www.momoshop.com.tw/search/x")
        self.assertIsNone(r["products"][0]["price_twd"])

    def test_shopee_dataset_dedup_and_filter(self):
        records = [
            {"title": "羅技 M331", "productUrl": "https://shopee.tw/product/1/2",
             "shopId": 1, "itemId": 2, "currency": "TWD", "price": 499},
            {"title": "羅技 M331", "productUrl": "https://shopee.tw/product/1/2",
             "shopId": 1, "itemId": 2, "price": 499},
            {"title": "羅技 M331", "productUrl": "https://unsafe.example/product/3",
             "price": 999},
        ]
        r = import_shopee_keyword_results("羅技 M331", records)
        self.assertEqual(len(r["products"]), 1)
        self.assertEqual(r["products"][0]["price_twd"], 499)

    def test_html_is_escaped(self):
        r = [{"source": "test", "status": "ready", "products": [
            {"platform": "momo", "title": "<script>bad</script>",
             "price_twd": None, "product_url": "https://www.momoshop.com.tw/product/1",
             "price_status": "no_price"}]}]
        html = html_report(r)
        self.assertIn("&lt;script&gt;", html)
        self.assertNotIn("<script>", html)


if __name__ == "__main__":
    unittest.main()
