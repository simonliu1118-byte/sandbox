import unittest
from coupang_detail_probe import assess


class CoupangDetailProbeTests(unittest.TestCase):
    def test_detect_regular_and_promo_price_markers_without_claiming_price(self):
        s = "<html><h1>3M Nexcare 1530-1</h1><span>酷澎售價 $165</span><p>首購折扣價 $99</p><div>商品主要規格</div></html>"
        r=assess(s)
        self.assertTrue(r["has_product_title"])
        self.assertIn("酷澎售價",r["price_markers"])
        self.assertIn("首購折扣價",r["price_markers"])
        self.assertNotIn("price",r)
    def test_hidden_script_data_does_not_count(self):
        r=assess("<script>3M Nexcare 1530-1 酷澎售價 $165</script>")
        self.assertFalse(r["has_product_title"])


if __name__=="__main__":
    unittest.main()
