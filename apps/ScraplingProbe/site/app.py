"""Protected, single-page momo keyword search preview for Vercel."""
import hmac
import os
import re
import threading
import time
import unicodedata
from datetime import datetime, timezone

import never_primp as primp
from flask import Flask, make_response, render_template, request

app = Flask(__name__)
ENDPOINT = "https://apisearch.momoshop.com.tw/momoSearchCloud/moec/textSearch"
PRICE_RE = re.compile(r"^\s*(?:NT\$|\$)?\s*([1-9]\d{0,2}(?:,\d{3})*|[1-9]\d{0,6})\s*$")
_LOCK = threading.Lock()
_CACHE = {}
_LAST_REQUEST = 0.0
_BLOCKED_UNTIL = 0.0


def normalize_keyword(raw):
    keyword = " ".join(str(raw).split())
    if not 2 <= len(keyword) <= 80 or any(ord(c) < 32 for c in keyword):
        raise ValueError("關鍵字必須為 2～80 個字元")
    return keyword


def parse_price(raw):
    if raw is None or isinstance(raw, bool):
        return None
    found = PRICE_RE.fullmatch(str(raw))
    if not found:
        return None
    amount = int(found.group(1).replace(",", ""))
    return amount if 0 < amount <= 10000000 else None


def parse_results(data, keyword):
    if not isinstance(data, dict) or data.get("success") is not True:
        return "provider_refused", []
    search = data.get("rtnSearchData")
    goods = search.get("goodsInfoList") if isinstance(search, dict) else None
    if not isinstance(goods, list):
        return "format_changed", []
    seen, found = set(), []
    tokens = unicodedata.normalize("NFKC", keyword).casefold().split()
    model_terms = [t for t in tokens if any(c.isdigit() for c in t) and len(t) >= 3]
    for item in goods:
        if not isinstance(item, dict):
            continue
        code = str(item.get("goodsCode") or "").strip()
        name = str(item.get("goodsName") or "").strip()
        if not re.fullmatch(r"\d{5,14}", code) or code in seen or not name:
            continue
        # Preserve the products momo returned; relevance is only an ordering hint.
        normalized = unicodedata.normalize("NFKC", name).casefold()
        if all(token in normalized for token in tokens):
            relevance, match_label = 3, "完全符合"
        elif any(token in normalized for token in model_terms):
            relevance, match_label = 2, "型號相關"
        elif any(token in normalized for token in tokens):
            relevance, match_label = 1, "部分相關"
        else:
            relevance, match_label = 0, "momo 推薦"
        seen.add(code)
        price = parse_price(item.get("SALE_PRICE"))
        if price is None:
            price = parse_price(item.get("goodsPrice"))
        found.append({"title": name[:220], "code": code, "price": price,
                      "relevance": relevance, "match_label": match_label,
                      "url": "https://www.momoshop.com.tw/product/" + code})
        if len(found) == 20:
            break
    return ("ok" if any(row["price"] is not None for row in found)
            else "no_price" if found else "no_match"), found


def fetch_once(keyword, page=1):
    """One ordinary request; no retries on denial or verification."""
    payload = {
        "host": "ecmobile", "flag": "searchEngine",
        "data": {
            "maxPage": 30, "cateLevel": -1, "serviceCode": "MT01", "platform": 16,
            "has3P": "Y", "NAM": "N", "china": "N", "cp": "N", "first": "N",
            "freeze": "N", "prefere": "N", "stockYN": "N", "superstore": "N",
            "threeHours": "N", "tomorrow": "N", "tvshop": "N", "video": "N",
            "cycle": "N", "cod": "N", "superstorePay": "N",
            "moCoinFeedback": "N", "superstoreFree": "N", "discount": "N",
            "isBrandSeriesPage": False, "isShowAdShop": False,
            "curRecommendedWordsCnt": 0, "searchValue": keyword, "curPage": page
        }
    }
    report = {"status": "network_error", "results": [], "page": page,
              "raw_count": 0, "checked_at": datetime.now(timezone.utc).isoformat()}
    try:
        with primp.Client(
            impersonate="chrome_142", impersonate_os="windows",
            timeout=18.0, http2_only=True,
            headers={"content-type": "application/json",
                     "origin": "https://m.momoshop.com.tw",
                     "referer": "https://m.momoshop.com.tw/"}
        ) as client:
            response = client.post(ENDPOINT, json=payload)
            if response.status_code in (403, 429):
                report["status"] = "blocked"
                return report
            if response.status_code != 200:
                report["status"] = "http_error"
                return report
            if any(x in response.text[:1400].lower() for x in
                   ("captcha", "access denied", "流量驗證")):
                report["status"] = "blocked"
                return report
            try:
                data = response.json()
            except ValueError:
                report["status"] = "format_changed"
                return report
            section = data.get("rtnSearchData") if isinstance(data, dict) else None
            raw = section.get("goodsInfoList") if isinstance(section, dict) else None
            report["raw_count"] = len(raw) if isinstance(raw, list) else 0
            if os.getenv("MOMO_DIAGNOSTICS") == "1" and isinstance(raw, list):
                # Public product metadata only; never persist raw HTTP data.
                report["source_diagnostics"] = [
                    {"goodsCode": str(item.get("goodsCode") or "")[:24],
                     "goodsCodeId": str(item.get("goodsCodeId") or "")[:24],
                     "goodsName": str(item.get("goodsName") or "")[:100],
                     "sale": str(item.get("SALE_PRICE") or "")[:24],
                     "keys": sorted(item.keys())[:24]}
                    for item in raw[:20] if isinstance(item, dict)
                ]
            report["status"], report["results"] = parse_results(data, keyword)
    except Exception as exc:
        report["error_type"] = type(exc).__name__
    return report


def safe_search(keyword, page=1):
    global _LAST_REQUEST, _BLOCKED_UNTIL
    now = time.monotonic()
    cache_key = (keyword, page)
    with _LOCK:
        cached = _CACHE.get(cache_key)
        if cached and now - cached[0] < 600:
            return {**cached[1], "from_cache": True}
        if now < _BLOCKED_UNTIL:
            return {"status": "blocked_cooldown", "results": []}
        if now - _LAST_REQUEST < 15:
            return {"status": "rate_limited", "results": []}
        _LAST_REQUEST = now
        result = fetch_once(keyword, page)
        if result["status"] in ("blocked", "provider_refused"):
            _BLOCKED_UNTIL = time.monotonic() + 1800
        elif result["status"] in ("ok", "no_match", "no_price"):
            _CACHE[cache_key] = (time.monotonic(), result)
        return result


@app.before_request
def require_preview_password():
    if not os.getenv("VERCEL"):
        return None
    password = os.getenv("APP_ACCESS_PASSWORD", "")
    if not password:
        return "預覽站尚未設定存取密碼，已鎖定。", 503
    auth = request.authorization
    if auth is None or not hmac.compare_digest(auth.password or "", password):
        response = make_response("這是私人測試站，請輸入存取密碼。", 401)
        response.headers["WWW-Authenticate"] = 'Basic realm="momo-price-prototype"'
        return response
    return None


@app.after_request
def security_headers(response):
    response.headers["Cache-Control"] = "private, no-store"
    response.headers["X-Robots-Tag"] = "noindex, nofollow"
    response.headers["X-Content-Type-Options"] = "nosniff"
    response.headers["Referrer-Policy"] = "no-referrer"
    response.headers["Content-Security-Policy"] = (
        "default-src 'none'; style-src 'self'; form-action 'self'; base-uri 'none'"
    )
    return response


@app.get("/")
def homepage():
    raw = request.args.get("keyword", "")
    try:
        page = int(request.args.get("page", "1"))
    except ValueError:
        page = 1
    page = max(1, min(page, 5))
    status, rows, keyword, checked, error, raw_count = "idle", [], "", None, "", 0
    selected_sort = request.args.get("sort", "relevance")
    if selected_sort not in ("relevance", "asc", "desc"):
        selected_sort = "relevance"
    if raw.strip():
        try:
            keyword = normalize_keyword(raw)
        except ValueError as exc:
            error = str(exc)
        else:
            result = safe_search(keyword, page)
            status, rows, checked = (result["status"], result.get("results", []),
                                     result.get("checked_at"))
            raw_count = result.get("raw_count", 0)
            if selected_sort == "relevance":
                rows = sorted(rows, key=lambda r: (
                    -(r.get("relevance", 0)), r["price"] is None,
                    r["price"] if r["price"] is not None else 10**10))
            else:
                priced = [r for r in rows if r["price"] is not None]
                missing = [r for r in rows if r["price"] is None]
                rows = sorted(priced, key=lambda r: r["price"],
                              reverse=(selected_sort == "desc")) + missing
    return render_template("index.html", keyword=keyword, status=status,
                           rows=rows, checked=checked, error=error, page=page,
                           raw_count=raw_count, has_next=(page < 5 and raw_count >= 20),
                           sort=selected_sort)


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=8765, debug=False)
