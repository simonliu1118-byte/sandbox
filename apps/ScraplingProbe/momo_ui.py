"""Render keyword search price results as a standalone, offline-compatible HTML page."""
from datetime import datetime, timedelta, timezone
from html import escape


def taipei_time(iso_value):
    if not iso_value:
        return "尚未查詢"
    try:
        parsed = datetime.fromisoformat(str(iso_value).replace("Z", "+00:00"))
        if parsed.tzinfo is None:
            return "查詢時間不明"
        return parsed.astimezone(timezone(timedelta(hours=8))).strftime(
            "%Y-%m-%d %H:%M:%S（臺灣時間）"
        )
    except ValueError:
        return "查詢時間不明"


def render_page(keyword="", report=None, message=""):
    """Never represent denied or missing searches as empty-but-successful prices."""
    items = list((report or {}).get("products") or [])
    items.sort(key=lambda item: (
        item.get("price_twd") is None,
        item.get("price_twd") if isinstance(item.get("price_twd"), int) else 1000000000,
        str(item.get("title") or ""),
    ))
    status = str((report or {}).get("status") or "idle")
    labels = {
        "products_with_prices": "已取得公開展示價",
        "products_without_prices": "已取得商品，但無法讀取價格",
        "no_matching_products": "站方沒有符合關鍵字的商品",
        "provider_response_not_successful": "站方未回報查詢成功",
        "http_denied": "站方拒絕連線（不再重試）",
        "http_error": "站方服務回傳錯誤",
        "network_error": "網路連線失敗",
        "unexpected_json_shape": "搜尋資料格式已改變",
        "blocked_or_challenged": "站方要求流量驗證",
        "not_json": "回應不是可辨識的搜尋資料",
        "unexpected_redirect": "搜尋請求遭轉向",
        "idle": "請輸入關鍵字開始查詢",
    }
    status_text = labels.get(status, "未能完成搜尋")
    priced = [item for item in items if isinstance(item.get("price_twd"), int)]
    summary = (
        f"符合關鍵字的商品 {len(items)} 筆，其中 {len(priced)} 筆有公開展示價"
        if report is not None else "不必提供商品網址；直接搜尋商品名稱、品牌或型號"
    )
    if message:
        status_text = message
    rows = []
    for item in items:
        title = escape(str(item.get("title") or ""))
        url = str(item.get("product_url") or "")
        if not url.startswith("https://www.momoshop.com.tw/product/"):
            continue
        value = item.get("price_twd")
        price_text = f"NT$ {value:,}" if isinstance(value, int) else "未取得"
        rows.append(
            "<tr><td><span class='name'>" + title + "</span>"
            "<small>momo 品號：" + escape(str(item.get("product_id") or "")) + "</small></td>"
            "<td class='amount'>" + escape(price_text) + "</td>"
            "<td><a href='" + escape(url, quote=True) +
            "' target='_blank' rel='noopener noreferrer'>查看商品</a></td></tr>"
        )
    results = (
        "<table><thead><tr><th>商品</th><th>公開展示價</th><th>來源</th></tr></thead>"
        "<tbody>" + "".join(rows) + "</tbody></table>"
        if rows else "<p class='empty'>這次沒有取得可列出的商品價格。請查看搜尋狀態，或稍後再試其他關鍵字。</p>"
    )
    checked = taipei_time((report or {}).get("checked_at_utc"))
    key = escape(str(keyword), quote=True)
    badge = "ok" if priced else ("neutral" if report is None else "warn")
    return f"""<!doctype html>
<html lang="zh-Hant">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>momo 免費關鍵字查價｜原型</title>
<style>
:root {{ color-scheme: light; font-family: -apple-system, BlinkMacSystemFont, "Noto Sans TC", "Segoe UI", sans-serif; }}
* {{ box-sizing: border-box; }}
body {{ background:#fcfaf7; color:#342a24; margin:0; }}
main {{ max-width:1050px; margin:48px auto; padding:0 18px 60px; }}
h1 {{ font-size:1.85rem; margin:0 0 9px; }}
p {{ line-height:1.55; }}
.subtitle,small,.meta {{ color:#766a60; }}
form {{ display:flex; gap:10px; margin:26px 0; flex-wrap:wrap; }}
input {{ flex:1 1 280px; min-width:0; font:inherit; padding:15px 18px; border:1px solid #d8cbc1; border-radius:10px; background:white; }}
button {{ background:#a95832; border:0; color:white; font:inherit; font-weight:600; padding:13px 26px; border-radius:10px; cursor:pointer; }}
section {{ border:1px solid #e3d9d0; background:#fff; border-radius:14px; padding:19px; }}
.summary {{ display:flex; flex-wrap:wrap; align-items:center; gap:13px; }}
.status {{ background:#f4ede6; color:#594334; font-size:.83rem; border-radius:50px; padding:7px 12px; }}
.status.ok {{ background:#eaf2e8; color:#315c37; }}
.status.warn {{ background:#fff1dd; color:#8a531d; }}
.meta {{ font-size:.84rem; margin-top:10px; }}
table {{ border-collapse:collapse; width:100%; margin-top:20px; }}
th {{ text-align:left; font-weight:600; color:#6c5d52; font-size:.87rem; background:#fbf6f1; }}
th,td {{ padding:17px 14px; border-bottom:1px solid #eee5dc; }}
td {{ vertical-align:middle; }}
.name {{ display:block; font-weight:550; }}
small {{ display:block; margin-top:6px; font-size:.8rem; }}
.amount {{ font-variant-numeric:tabular-nums; white-space:nowrap; font-weight:700; }}
a {{ color:#9c502f; }}
.empty {{ color:#766a60; padding:14px 0; }}
.notice {{ color:#77695e; font-size:.84rem; margin-top:18px; }}
@media(max-width:600px) {{ main {{margin-top:24px;}} th,td {{padding:12px 8px;}} .amount {{white-space:normal;}} }}
</style>
</head>
<body>
<main>
<h1>momo 免費關鍵字查價</h1>
<p class="subtitle">輸入商品名稱或型號，自動搜尋 momo 的公開商品與價格。無須準備商品連結。</p>
<form action="/" method="get">
<input aria-label="搜尋關鍵字" name="keyword" value="{key}" maxlength="80" required placeholder="例如：羅技 M331、咖啡、3M 膠帶">
<button type="submit">查詢價格</button>
</form>
<section>
<div class="summary"><strong>{escape(summary)}</strong><span class="status {badge}">{escape(status_text)}</span></div>
<div class="meta">查詢時間：{escape(checked)}</div>
{results}
<p class="notice">此處為 momo 搜尋時的公開展示價，不保證與特定規格、會員優惠、購物車或結帳價相同。來源異常時不會顯示虛構價格；原型沒有自動重試與排程。</p>
</section>
</main>
</body></html>"""


if __name__ == "__main__":
    from pathlib import Path
    Path("momo-preview.html").write_text(render_page(), encoding="utf-8")
