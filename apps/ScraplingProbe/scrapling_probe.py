"""One-page, public product-price feasibility probe.

All extracted prices are candidates, not verified checkout prices.
No login, CAPTCHA solving, proxy rotation, retries, or checkout actions.
"""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse

from scrapling.fetchers import StealthySession

DEFAULT_URL = "https://shopee.tw/3M-%E7%BE%8E%E7%B4%8B%E7%B4%99%E8%86%A0%E5%B8%B6-2688%E9%81%AE%E8%94%BD%E8%86%A0%E5%B8%B6%E7%84%A1%E6%AE%98%E8%86%A0%E6%89%8B%E6%92%95-%E9%9B%BB%E9%8D%8D%E5%88%86%E8%89%B2%E6%B1%BD%E8%BB%8A%E9%AB%98%E6%BA%AB%E5%99%B4%E6%BC%86%E9%81%AE%E8%94%BD%E5%99%B4%E5%A1%97%E4%BF%9D%E8%AD%B7-%E8%A3%9D%E4%BF%AE%E9%81%AE%E8%94%BD%E7%84%A1%E7%97%95%E8%86%A0%E5%B8%B6%EF%BC%88%E5%8F%AF%E9%96%8B%E7%99%BC%E7%A5%A8%EF%BC%89-i.768064806.19160839433"
PRICE_PATTERN = re.compile(r"(?:NT\$|\$)\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)")
API_PRICE_KEYS = {"price", "price_min", "price_max", "price_before_discount", "price_before_discount_min", "price_before_discount_max"}
BLOCK_WORDS = ("流量驗證", "安全驗證", "robot check", "access denied", "unusual traffic", "captcha")


def price_candidates_from_text(text: str) -> list[str]:
    """Return only textual amount candidates, without assuming product identity."""
    seen: list[str] = []
    for found in PRICE_PATTERN.findall(text):
        value = found.replace(",", "")
        if value not in seen:
            seen.append(value)
        if len(seen) >= 15:
            break
    return seen


def price_fields_from_json(payload: object) -> list[dict]:
    """Collect raw price-like fields; do not guess unit scale or applicable SKU."""
    found: list[dict] = []

    def walk(node: object, path: str, depth: int) -> None:
        if depth > 7 or len(found) >= 20:
            return
        if isinstance(node, dict):
            for key, value in node.items():
                current = f"{path}.{key}" if path else str(key)
                if key in API_PRICE_KEYS and isinstance(value, (int, float, str)) and not isinstance(value, bool):
                    found.append({"field": current[:160], "raw": str(value)[:40]})
                elif isinstance(value, (dict, list)):
                    walk(value, current, depth + 1)
        elif isinstance(node, list):
            for index, value in enumerate(node[:12]):
                walk(value, f"{path}[{index}]", depth + 1)

    walk(payload, "", 0)
    return found


def valid_target(url: str) -> bool:
    parsed = urlparse(url)
    return parsed.scheme == "https" and parsed.hostname in {"shopee.tw", "www.shopee.tw"} and not parsed.username and not parsed.password


def probe(url: str) -> dict:
    """Exactly one top-level navigation, no retry, and no authentication."""
    if not valid_target(url):
        raise ValueError("Only https://shopee.tw public URLs are supported")
    result = {
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "target": url,
        "tool": "Scrapling 0.5.1 / StealthySession",
        "status": "not_started",
        "http_status": None,
        "title": None,
        "rendered_price_candidates": [],
        "api_price_candidates": [],
        "api_responses_seen": 0,
        "price_verified": False,
        "notes": "All prices are unverified candidates, not final checkout prices.",
    }
    try:
        with StealthySession(
            headless=True,
            locale="zh-TW",
            record_requests=True,
            max_recorded_requests=200,
        ) as session:
            page = session.fetch(
                url,
                timeout=45000,
                wait=3000,
                network_idle=False,
                google_search=False,
                solve_cloudflare=False,
            )
            result["http_status"] = page.status
            result["title"] = (
                page.css('meta[property="og:title"]::attr(content)').get()
                or page.css("title::text").get()
            )
            text_nodes = page.css("body *::text").getall()
            visible_text = " ".join(str(value) for value in text_nodes[:1600])[:35000]
            result["rendered_price_candidates"] = price_candidates_from_text(visible_text)

            for network_response in session.network.search(include_static=False, limit=None):
                if result["api_responses_seen"] >= 100:
                    break
                source = urlparse(network_response.url)
                if not (source.hostname or "").endswith(".shopee.tw") and source.hostname != "shopee.tw":
                    continue
                result["api_responses_seen"] += 1
                if not any(word in source.path.lower() for word in ("item", "product", "pdp", "detail")):
                    continue
                try:
                    payload = network_response.json()
                except (ValueError, TypeError, AttributeError):
                    continue
                for candidate in price_fields_from_json(payload):
                    candidate["source_path"] = source.path[:130]
                    result["api_price_candidates"].append(candidate)
                    if len(result["api_price_candidates"]) >= 20:
                        break
                if len(result["api_price_candidates"]) >= 20:
                    break

            page_sample = (str(result["title"] or "") + " " + visible_text[:2500]).lower()
            if page.status in (403, 429) or any(word in page_sample for word in BLOCK_WORDS):
                result["status"] = "blocked_or_challenged"
            elif result["rendered_price_candidates"] or result["api_price_candidates"]:
                result["status"] = "price_candidates_found"
            else:
                result["status"] = "no_price_found"
    except Exception as exc:
        result["status"] = "fetch_error"
        result["error_type"] = type(exc).__name__
        # Avoid writing response bodies, cookies, headers, or exception URLs to public CI logs.
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default=DEFAULT_URL, help="One public Shopee product URL")
    parser.add_argument("--output", default="probe-result.json", help="JSON output path")
    options = parser.parse_args()
    result = probe(options.url)
    destination = Path(options.output)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
