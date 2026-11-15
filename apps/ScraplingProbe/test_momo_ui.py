import unittest
from momo_ui import render_page, taipei_time


class MomoWebUITests(unittest.TestCase):
    def test_form_present_without_product_links(self):
        html = render_page()
        self.assertIn('name="keyword"', html)
        self.assertIn("查詢價格", html)

    def test_real_style_rows_render_and_sorted(self):
        report = {"status": "products_with_prices",
                  "checked_at_utc": "2026-11-15T11:22:02+00:00",
                  "products": [
                      {"platform": "momo", "title": "羅技 M331 黑", "product_id": "9956761",
                       "price_twd": 699, "product_url": "https://www.momoshop.com.tw/product/9956761"},
                      {"platform": "momo", "title": "羅技 M331 白", "product_id": "1111111",
                       "price_twd": 599, "product_url": "https://www.momoshop.com.tw/product/1111111"}]}
        html = render_page("羅技 M331", report)
        self.assertTrue(html.index("NT$ 599") < html.index("NT$ 699"))
        self.assertIn("2026-11-15 19:22:02", html)

    def test_bad_html_escaped(self):
        html = render_page("<script>bad</script>", {"status": "products_with_prices",
            "products": [{"title": "<img src=x onerror=alert(1)>", "product_id": "3",
                          "price_twd": 599, "product_url": "https://www.momoshop.com.tw/product/3"}]})
        self.assertNotIn("<script>bad</script>", html)
        self.assertNotIn("<img src=x onerror=alert(1)>", html)
        self.assertIn("&lt;img src=x", html)

    def test_http_denied_never_shows_fake_price(self):
        html = render_page("羅技 M331", {"status": "http_denied", "products": []})
        self.assertIn("站方拒絕連線", html)
        self.assertNotIn("NT$ 0", html)

    def test_invalid_datetime(self):
        self.assertEqual(taipei_time(None), "尚未查詢")
        self.assertEqual(taipei_time("invalid"), "查詢時間不明")


if __name__ == "__main__":
    unittest.main()
