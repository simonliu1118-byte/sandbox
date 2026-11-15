"""Offline tests of Feebee Shopee/Mall separation and safe parsing."""
import unittest
from feebee_shopee_probe import make_url, parse_search_html

URL = make_url("3m 1530-1")


def card(title, price, seller, href="/p/sample"):
    return ('<div><a href="' + href + '">' + title + '</a>'
            '<span>' + price + '</span><span>' + seller + '</span>'
            '<a href="/go/sample">前往購買</a></div>')


class ShopeeFeebeeTests(unittest.TestCase):
    def test_correct_url(self):
        self.assertEqual(URL, "https://feebee.com.tw/s/3m%201530-1/")
        with self.assertRaises(ValueError):
            make_url(" ")

    def test_both_markets_not_mixed(self):
        html = (card("3M 1530-1 12捲", "300", "蝦皮商城 - 瑞昌藥局") +
                card("3M 1530-1 12入", "215", "蝦皮購物 - 亮亮生活") +
                card("3M 1530-1 12入", "179", "Coupang 酷澎"))
        result = parse_search_html(html, URL)
        self.assertEqual(result["status"], "paired")
        self.assertEqual(result["matched_count"], 2)
        self.assertEqual(result["market_counts"], {"mall": 1, "marketplace": 1})
        self.assertEqual([x["reference_price_twd"] for x in result["samples"]], [300, 215])
        self.assertEqual(result["samples"][0]["seller"], "瑞昌藥局")

    def test_promo_numbers_are_not_prices(self):
        result = parse_search_html(card("3M 1530-1 12入", "限時現折100", "蝦皮購物"), URL)
        self.assertEqual(result["status"], "unpaired")
        self.assertEqual(result["matched_count"], 0)

    def test_site_header_not_a_product(self):
        html = ('<header><a href="/">Feebee 飛比價格</a></header>'
                '<span>3M 1530-1 12捲</span><span>300</span>'
                '<span>蝦皮商城 - 瑞昌藥局</span><a>前往購買</a>')
        self.assertEqual(parse_search_html(html, URL)["matched_count"], 0)

    def test_separate_sellers(self):
        html = (card("3M 1530-1 1捲", "60", "蝦皮購物 - 店A") +
                card("3M 1530-1 12捲", "190", "蝦皮購物 - 店B"))
        self.assertEqual(parse_search_html(html, URL)["matched_count"], 2)

    def test_safe_link_rejects_javascript(self):
        html = card("3M 1530-1 12捲", "190", "蝦皮購物 - 店A", "javascript:alert(1)")
        self.assertEqual(parse_search_html(html, URL)["samples"][0]["feebee_link"], URL)

    def test_other_merchants_excluded(self):
        result = parse_search_html(card("3M 1530-1 12捲", "190", "momo購物網"), URL)
        self.assertEqual(result["status"], "no_shopee")


if __name__ == "__main__":
    unittest.main()
