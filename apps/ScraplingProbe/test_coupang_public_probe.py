import unittest
from coupang_public_probe import describe, BASE

class CoupangPublicProbeTests(unittest.TestCase):
    def test_product_card_and_promo_detection(self):
        html = '<a href="/products/3m-123"><div>3M 1530-1 12捲 1盒</div><span>首購折扣價</span><del>$165</del><b>$99</b></a>'
        r = describe(200, html, BASE+"/srp/3m")
        self.assertEqual(r["product_count"], 1)
        self.assertEqual(r["product_cards_with_currency"], 1)
        self.assertEqual(r["product_cards_with_promo_marker"], 1)

    def test_nonproduct_links_are_ignored(self):
        r = describe(200, '<a href="https://evil.example/products/x">Fake $99</a>', BASE)
        self.assertEqual(r["product_count"], 0)

if __name__ == "__main__":
    unittest.main()
