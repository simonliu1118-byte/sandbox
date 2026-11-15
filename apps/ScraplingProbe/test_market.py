import unittest
from market_price_probe import parse_product_html, product_id_from_url


class MomoPriceTests(unittest.TestCase):
    def test_product_url(self):
        self.assertEqual(product_id_from_url("https://www.momoshop.com.tw/product/9956761"), "9956761")
        with self.assertRaises(ValueError):
            product_id_from_url("https://shopee.tw/product/9956761")

    def test_momo_promotional_price(self):
        html = ('<html><head><meta property="og:title" content="羅技 M331 滑鼠"></head>'
                '<body><span>品號：9956761</span><div>相關商品：紅699 元</div>'
                '<div>促銷價</div><div>699</div><div>元</div><div>市售價</div>799元</body></html>')
        found = parse_product_html(html, "9956761")
        self.assertEqual((found["status"], found["price_twd"], found["price_kind"]),
                         ("price_found", 699, "促銷價"))

    def test_mismatched_product_or_no_price(self):
        html = "<html><title>商品</title><body>品號：123 促銷價 699</body></html>"
        self.assertEqual(parse_product_html(html, "9956761")["status"], "identity_not_confirmed")
        html = "<html><title>商品</title><body>品號：9956761 促銷價 未提供</body></html>"
        self.assertEqual(parse_product_html(html, "9956761")["status"], "price_not_found")


if __name__ == "__main__":
    unittest.main()
