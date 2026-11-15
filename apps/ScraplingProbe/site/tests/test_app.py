"""Offline checks for password protection and genuine momo price parsing."""
import base64
import os
import unittest
from unittest.mock import patch
from app import app, normalize_keyword, parse_results


def payload():
    return {"success": True, "rtnSearchData": {"goodsInfoList": [
        {"goodsCode": "9956761", "goodsName": "羅技 M331 黑", "SALE_PRICE": "699"},
        {"goodsCode": "4318322", "goodsName": "羅技 M331 白", "SALE_PRICE": "599"},
        {"goodsCode": "4318322", "goodsName": "羅技 M331 白", "SALE_PRICE": "599"}]}}


class OnePageTests(unittest.TestCase):
    def setUp(self):
        self.client = app.test_client()

    def test_keyword_validation(self):
        self.assertEqual(normalize_keyword(" 羅技   M331 "), "羅技 M331")
        with self.assertRaises(ValueError):
            normalize_keyword(" ")

    def test_real_provider_shape(self):
        status, rows = parse_results(payload(), "羅技 M331")
        self.assertEqual(status, "ok")
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["price"], 699)

    def test_3m_search_keeps_official_nonliteral_matches(self):
        data = {"success": True, "rtnSearchData": {"goodsInfoList": [
            {"goodsCode":"1234567", "goodsName":"3M Micropore 1530-0 通氣膠帶", "SALE_PRICE":"259"},
            {"goodsCode":"1234568", "goodsName":"Nexcare 1530-1 紙膠帶", "SALE_PRICE":"190"},
            {"goodsCode":"1234569", "goodsName":"3M 透氣膠帶 1吋", "SALE_PRICE":"54"},
            {"goodsCode":"1234570", "goodsName":"醫療膠帶 12入", "SALE_PRICE":"160"}]}}
        status, rows = parse_results(data, "3m 1530")
        self.assertEqual(status, "ok")
        self.assertEqual(len(rows), 4)
        self.assertEqual([r["relevance"] for r in rows], [3, 2, 1, 0])

    def test_pagination_navigation(self):
        info = {"status":"ok", "results":parse_results(payload(),"羅技 M331")[1],
                "raw_count":20, "checked_at":"2026-11-15T00:00:00+00:00"}
        with patch.dict(os.environ, {"VERCEL": ""}), patch("app.safe_search", return_value=info) as mocked:
            first = self.client.get("/?keyword=%E7%BE%85%E6%8A%80+M331")
            second = self.client.get("/?keyword=%E7%BE%85%E6%8A%80+M331&page=2")
        self.assertIn("下一頁".encode(), first.data)
        self.assertIn("上一頁".encode(), second.data)
        self.assertEqual(mocked.call_args.args, ("羅技 M331", 2))

    def test_no_next_page_on_short_results(self):
        info = {"status":"ok", "results":parse_results(payload(),"羅技 M331")[1],
                "raw_count":2}
        with patch.dict(os.environ, {"VERCEL": ""}), patch("app.safe_search", return_value=info):
            result = self.client.get("/?keyword=%E7%BE%85%E6%8A%80+M331")
        self.assertNotIn("下一頁".encode(), result.data)

    def test_actual_3m_1530_marketplace_ids_kept(self):
        data = {"success": True, "rtnSearchData": {"goodsInfoList": [
            {"goodsCode":"14011391", "goodsName":"3M 1530-0 通氣膠帶 6 組", "SALE_PRICE":"259"},
            {"goodsCode":"TP00013930000165", "goodsName":"3M 1530-0 紙膠帶 12 入", "SALE_PRICE":"$190"},
            {"goodsCode":"TP00065910000469", "goodsName":"3M Nexcare 1530CP 膠帶", "SALE_PRICE":"$54"},
            {"goodsCode":"TP00052130002280", "goodsName":"來而康 1530-0 透氣膠帶", "SALE_PRICE":"$406"},
            {"goodsCode":"TP00071080000060", "goodsName":"3M Nexcare 1530CP-1", "SALE_PRICE":"$69"},
            {"goodsCode":"TP00094060000571", "goodsName":"3M 1530 盒裝", "SALE_PRICE":"$100"}]}}
        status, rows = parse_results(data, "3m 1530")
        self.assertEqual(status, "ok")
        self.assertEqual(len(rows), 6)
        self.assertEqual([r["price"] for r in rows], [259,190,54,406,69,100])
        self.assertTrue(rows[1]["url"].endswith("GoodsDetail.jsp?i_code=TP00013930000165"))
        self.assertTrue(rows[0]["url"].endswith("/product/14011391"))

    def test_explicit_provider_failure(self):
        self.assertEqual(parse_results({"success": False}, "羅技 M331"), ("provider_refused", []))

    def test_home_has_keyword_form(self):
        with patch.dict(os.environ, {"VERCEL": ""}):
            page = self.client.get("/")
        self.assertEqual(page.status_code, 200)
        self.assertIn(b'name="keyword"', page.data)
        self.assertIn("查詢價格".encode(), page.data)

    def test_no_export_import_controls(self):
        # Entire CI must remain offline: use a normal search result fixture.
        result = {"status": "ok", "results": parse_results(payload(), "羅技 M331")[1],
                  "raw_count": 2, "checked_at": "2026-11-15T00:00:00+00:00"}
        with patch.dict(os.environ, {"VERCEL": ""}), patch("app.safe_search", return_value=result):
            page = self.client.get("/?keyword=%E7%BE%85%E6%8A%80+M331")
        self.assertIn(b'id="catalog-data"', page.data)
        self.assertNotIn(b'id="export-rules"', page.data)
        self.assertNotIn(b'id="import-rules"', page.data)
        self.assertNotIn("匯入分類備份".encode(), page.data)

    def test_no_secret_denies_remote(self):
        with patch.dict(os.environ, {"VERCEL": "1", "APP_ACCESS_PASSWORD": ""}):
            response = self.client.get("/")
        self.assertEqual(response.status_code, 503)

    def test_password_required(self):
        with patch.dict(os.environ, {"VERCEL": "1", "APP_ACCESS_PASSWORD": "secret"}):
            self.assertEqual(self.client.get("/").status_code, 401)
            auth = base64.b64encode(b"viewer:secret").decode()
            response = self.client.get("/", headers={"Authorization": "Basic " + auth})
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["X-Robots-Tag"], "noindex, nofollow")

    def test_search_with_mock_results(self):
        with patch.dict(os.environ, {"VERCEL": ""}), patch("app.safe_search",
               return_value={"status": "ok", "results": parse_results(payload(), "羅技 M331")[1]}):
            page = self.client.get("/?keyword=%E7%BE%85%E6%8A%80+M331")
        self.assertEqual(page.status_code, 200)
        self.assertIn(b"NT$ 599", page.data)
        self.assertIn(b"NT$ 699", page.data)


if __name__ == "__main__":
    unittest.main()
