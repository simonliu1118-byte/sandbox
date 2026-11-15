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

    def test_first_item_must_not_use_site_header_as_title(self):
        html = ('<header><a href="/">Feebee 飛比價格</a></header>'
                '<nav><a href="/s/3m/">熱門搜尋</a></nav>'
                + card("3M 1530-1 透氣紙膠帶 1個", "179"))
        status, rows = parse_feebee_results(html, self.url)
        self.assertEqual(status, "ok")
        self.assertEqual(rows[0]["title"], "3M 1530-1 透氣紙膠帶 1個")

    def test_missing_product_link_rejects_card(self):
        html = ('<header><a href="/">Feebee 飛比價格</a></header>'
                '<div>3M 1530-1 1個</div><span>175</span>'
                '<span>Coupang 酷澎</span><a>前往購買</a>')
        status, rows = parse_feebee_results(html, self.url)
        self.assertEqual((status, rows), ("no_price", []))

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
        feebee.assert_called_once_with("3m 1530-1", market="coupang")
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


    def test_shopee_and_coupang_share_one_html_parser(self):
        html = (card("3M 1530-1 十二捲", "300", "蝦皮商城 - 瑞昌藥局") +
                card("3M 1530-1 一個裝", "175", "Coupang 酷澎") +
                card("3M 1530-1 十二入", "230", "蝦皮購物 - 亮亮生活") +
                card("3M 1530-1 十二入", "99", "momo購物網"))
        from feebee_search import parse_all_results
        all_rows = parse_all_results(html, self.url)
        self.assertEqual([r["price"] for r in all_rows["shopee"][1]], [300, 230])
        self.assertEqual([r["price"] for r in all_rows["coupang"][1]], [175])
        self.assertEqual([r["merchant"] for r in all_rows["shopee"][1]],
                         ["蝦皮商城", "蝦皮購物"])
        self.assertEqual(all_rows["shopee"][1][1]["seller"], "亮亮生活")

    def test_identical_keyword_reuses_one_network_fetch_across_markets(self):
        import feebee_search as core
        example = {"status": "ok", "source_url": self.url,
                   "checked_at": "2026-11-16T00:00:00+00:00",
                   "markets": {"coupang": ("ok", [{"title": "酷澎", "price": 175}]),
                               "shopee": ("ok", [{"title": "蝦皮", "price": 230}])}}
        with patch("feebee_search.fetch_once", return_value=example) as query, \
             patch.object(core, "_LAST_REQUEST", 0.0), \
             patch.object(core, "_BLOCKED_UNTIL", 0.0):
            core._CACHE.clear()
            first = core.safe_search("3m 1530-1", market="shopee")
            second = core.safe_search("3m 1530-1", market="coupang")
            core._CACHE.clear()
        query.assert_called_once_with("3m 1530-1")
        self.assertEqual(first["results"][0]["price"], 230)
        self.assertEqual(second["results"][0]["price"], 175)
        self.assertTrue(second["from_cache"])

    def test_shopee_route_only_uses_shopee_panel(self):
        lookup = {"status": "ok", "results": [
            {"title": "3M 紙膠 12捲", "price": 230, "merchant": "蝦皮購物",
             "seller": "亮亮生活", "url": self.url, "source": "飛比／蝦皮購物"}],
            "source_url": self.url}
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_feebee_search", return_value=lookup) as only_feebee, \
             patch("app.safe_search") as momo:
            page = self.client.get("/?source=feebee&market=shopee&feebee_keyword=3m+1530-1")
        self.assertEqual(page.status_code, 200)
        only_feebee.assert_called_once_with("3m 1530-1", market="shopee")
        momo.assert_not_called()
        self.assertIn("蝦皮商城".encode(), page.data)
        self.assertIn("亮亮生活".encode(), page.data)
        self.assertIn(b"NT$ 230", page.data)
        self.assertIn(b'id="feebee-shopee"', page.data)
        self.assertIn(b'id="feebee-coupang"', page.data)
        self.assertNotIn(b'id="analysis"', page.data)

    def test_source_keyword_only_queries_selected_platform(self):
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_feebee_search") as query:
            result = self.client.get("/?source=feebee&market=unsupported&feebee_keyword=123")
        self.assertEqual(result.status_code, 200)
        query.assert_not_called()
        self.assertIn("不支援的查詢來源".encode(), result.data)

    def test_shopee_without_price_never_claims_sku_price(self):
        with patch.dict(os.environ, {"VERCEL": ""}), \
             patch("app.safe_feebee_search", return_value={
                 "status": "no_price", "results": [], "source_url": self.url}):
            page = self.client.get("/?source=feebee&market=shopee&feebee_keyword=3m+1530-1")
        self.assertIn("逐規格".encode(), page.data)
        self.assertNotIn(b"NT$ 0", page.data)


if __name__ == "__main__":
    unittest.main()
