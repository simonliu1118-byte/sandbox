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

    def test_explicit_provider_failure(self):
        self.assertEqual(parse_results({"success": False}, "羅技 M331"), ("provider_refused", []))

    def test_home_has_keyword_form(self):
        with patch.dict(os.environ, {"VERCEL": ""}):
            page = self.client.get("/")
        self.assertEqual(page.status_code, 200)
        self.assertIn(b'name="keyword"', page.data)
        self.assertIn("查詢價格".encode(), page.data)

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
