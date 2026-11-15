import unittest
from market_price_probe import parse_product_html, extract_momo_offer


class MarketProbeTests(unittest.TestCase):
    def test_momo_labelled_price(self):
        html = '<html><meta property="og:title" content="羅技 M331 滑鼠"><body>促銷價 <span>699</span> 元 市售價 899元</body></html>'
        result = parse_product_html(html, "momo", "9956761", "/product/9956761")
        self.assertEqual(result["status"], "price_found")
        self.assertEqual(result["price_twd"], 699)
        self.assertEqual(result["price_kind"], "促銷價")

    def test_ecommerce_price_requires_identity(self):
        html = '<meta property="og:title" content="另一商品"><body>促銷價 <span>699</span> 元</body>'
        self.assertEqual(parse_product_html(html, "momo", "9956761", "/product/9956761")["status"],
                         "identity_not_confirmed")

    def test_momo_offers_from_feed(self):
        obj = {"items": [{"name": "某牌氣炸鍋", "platform": "momo", "price": 1990, "url": "https://example.com"},
                         {"name": "其他通路", "platform": "PChome", "price": 1500}]}
        self.assertEqual(extract_momo_offer(obj)[0]["price_twd"], 1990)


if __name__ == "__main__":
    unittest.main()
