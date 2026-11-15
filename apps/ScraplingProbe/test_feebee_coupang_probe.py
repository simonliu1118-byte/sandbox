import unittest
from feebee_coupang_probe import describe

class FeebeeCoupangProbeTests(unittest.TestCase):
    def test_merchant_and_public_price_candidate_detection(self):
        html = ('<html><script>Coupang 酷澎 $0</script><div>3M 1530-1 12捲 1盒'
                '<b>115</b><span>Coupang 酷澎</span></div></html>')
        report = describe(html)
        self.assertEqual(report["coupang_merchant_mentions"], 1)
        self.assertGreater(report["mentions_1530"], 0)
        self.assertGreater(report["price_number_candidates"], 0)

if __name__ == "__main__":
    unittest.main()
