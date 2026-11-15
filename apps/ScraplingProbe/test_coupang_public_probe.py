"""Offline regression tests for direct Coupang public search diagnostics."""
import unittest
from coupang_public_probe import BASE, describe, search_url


class CoupangPublicProbeTests(unittest.TestCase):
    def test_search_url_is_official_and_keyword_encoded(self):
        self.assertEqual(search_url("3m膠帶"),
                         BASE + "/srp/3m%E8%86%A0%E5%B8%B6")
        with self.assertRaises(ValueError):
            search_url(" ")

    def test_separate_regular_and_first_purchase_candidates(self):
        html = (
            '<a href="/products/3m-123?itemId=444&vendorItemId=555">'
            '<div>3M Nexcare 1530-1 12捲 1盒</div>'
            '<span>首購折扣價</span><del>$165</del><b>$99</b>'
            '<small>($8.25/1入)</small></a>'
            '<a href="/products/3m-124"><div>3M 醫療膠帶 6捲</div>'
            '<span>$139</span></a>'
        )
        report = describe(200, html, BASE + "/srp/3m")
        self.assertEqual(report["product_count"], 2)
        self.assertEqual(report["product_cards_with_currency"], 2)
        self.assertEqual(report["product_cards_with_promo_marker"], 1)
        self.assertEqual(report["product_cards_with_price_candidates"], 2)
        self.assertEqual(report["sample_price_candidates"][0]["amount_candidates_twd"],
                         [165, 99])
        self.assertEqual(report["sample_price_candidates"][0]["price_kind"],
                         "first_purchase_promotion")
        self.assertEqual(report["sample_price_candidates"][1]["price_kind"],
                         "unclassified_display")

    def test_variants_with_distinct_item_id_are_distinct(self):
        html = ('<a href="/products/3m-123?itemId=1">膠帶 1盒 $99</a>'
                '<a href="/products/3m-123?itemId=2">膠帶 2盒 $198</a>')
        self.assertEqual(describe(200, html, BASE)["product_count"], 2)

    def test_foreign_links_and_unassociated_amounts_ignored(self):
        html = ('<a href="https://evil.example/products/x">假商品 $10</a>'
                '<div>$500 站內廣告</div>'
                '<a href="/products/3m-234">3M 1530-1 1盒</a>')
        report = describe(200, html, BASE)
        self.assertEqual(report["product_count"], 1)
        self.assertEqual(report["product_cards_with_price_candidates"], 0)
        self.assertEqual(report["sample_price_candidates"][0]["amount_candidates_twd"], [])

    def test_no_html_product_link_not_marked_as_success(self):
        report = describe(200, "<body>酷澎 3M 膠帶 $99</body>", BASE)
        self.assertEqual(report["status"], "html_without_product_links")
        self.assertEqual(report["product_count"], 0)


if __name__ == "__main__":
    unittest.main()
