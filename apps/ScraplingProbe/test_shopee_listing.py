import unittest
from shopee_listing_probe import parse_cards


class ListingTests(unittest.TestCase):
    def test_linked_price(self):
        html = ('<a href="/Test-shop-i.123.456">羅技 Logitech M331 無線滑鼠'
                '<span>$577</span></a>')
        cards, count, keyword = parse_cards(html)
        self.assertEqual(len(cards), 1)
        self.assertTrue(keyword)
        self.assertEqual((cards[0]["shop_id"], cards[0]["item_id"], cards[0]["price_candidate_twd"]), ("123", "456", 577))

    def test_no_prices_not_reported(self):
        cards, _, _ = parse_cards('<a href="/Test-shop-i.123.456">Logitech M331</a>')
        self.assertEqual(cards, [])


if __name__ == "__main__":
    unittest.main()
