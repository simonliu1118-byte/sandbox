"""Offline regression tests for isolated Feebee/Coupang lookup."""
import os
import unittest
from unittest.mock import patch
from app import app
from feebee_search import make_search_url, parse_feebee_results


def card(title, price, shop="Coupang 酷澎", href="/p/sample"):
    return (f'<div><a href="{href}">{title}</a><span>{price}</span>'
            f'<span>{shop}</span><a href="/go/x">前往購買</a></div>')


class FeebeeTests(unittest.TestCase):
    def setUp(self):
        self.url = make_search_url("3m 1530-1")
        self.client = app.test_client()

    def test_fixed_host_and_encoding(self):
        self.assertEqual(self.url, "https://feebee.com.tw/s/3m%201530-1/")

    def test_extract_only_coupang_prices(self):
        html = (card("3M 通氣膠帶 12捲", "259", "momo購物網") +
                card("3M 1530-1 紙膠帶 1個", "179") +
                card("3M 1530-1 紙膠帶 12捲 3盒", "395"))
        state, rows = parse_feebee_results(html, self.url)
        self.assertEqual(state, "ok")
        self.assertEqual([x["price"] for x in rows], [179, 395])
        self.assertEqual(rows[0]["title"], "3M 1530-1 紙膠帶 1個")
        self.assertTrue(rows[0]["url"].startswith("https://feebee.com.tw/p/"))

    def test_reject_promo_and_title_numbers(self):
        html = ('<a href="/p/x">3M 1530-1 一盒12捲</a>'
                '<span>促銷 99 元</span><span>Coupang 酷澎</span>'
                '<a>前往購買</a>')
        self.assertEqual(parse_feebee_results(html, self.url), ("no_price", []))

    def test_reject_other_merchants(self):
        self.assertEqual(parse_feebee_results(
            card("3M 1530-1 一盒", "250", "蝦皮商城"), self.url), ("no_match", []))

    def test_untrusted_link_falls_back_to_feebee_search(self):
        state, rows = parse_feebee_results(
            card("3M 1530-1 一盒", "300", href="javascript:alert(1)"), self.url)
        self.assertEqual(state, "ok")
        self.assertEqual(rows[0]["url"], self.url)

    def test_feebee_lookup_does_not_call_momo(self):
        result = {"status": "ok", "results": [{"title": "3M 1530-1 一盒",
                 "price": 179, "url": self.url, "source": "飛比／酷澎"}],
                 "source_url": self.url}
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_feebee_search", return_value=result) as feebee, \
             patch("app.safe_search") as momo:
            page = self.client.get("/?source=feebee&feebee_keyword=3m+1530-1")
        feebee.assert_called_once_with("3m 1530-1")
        momo.assert_not_called()
        self.assertIn("第三方參考價".encode(), page.data)
        self.assertIn(b"NT$ 179", page.data)
        self.assertNotIn(b'id="analysis"', page.data)

    def test_momo_lookup_does_not_call_feebee(self):
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_search", return_value={"status": "no_match", "results": []}), \
             patch("app.safe_feebee_search") as feebee:
            page = self.client.get("/?keyword=3m+1530-1")
        feebee.assert_not_called()
        self.assertEqual(page.status_code, 200)
        self.assertIn(b'name="feebee_keyword"', page.data)

    def test_failure_has_no_fake_price(self):
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_feebee_search", return_value={"status": "no_price", "results": [],
                   "source_url": self.url}):
            page = self.client.get("/?source=feebee&feebee_keyword=3m+1530-1")
        self.assertIn("未找到能可靠配對".encode(), page.data)
        self.assertNotIn(b"NT$ 0", page.data)


if __name__ == "__main__":
    unittest.main()
