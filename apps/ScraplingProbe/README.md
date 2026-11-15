# ScraplingProbe

使用 [Scrapling](https://github.com/D4Vinci/Scrapling) 對**一筆公開臺灣蝦皮商品頁**進行最小可行性測試。專案是個人技術實驗，不是持續查價服務。

## 目的

1. 在 GitHub Actions 的 Ubuntu runner 安裝 Python、Scrapling 及 Chromium。
2. 僅載入一筆公開商品頁一次（不登入、不重試、不解驗證）。
3. 檢查頁面標題、可見價格候選值，以及瀏覽器背景回應中的原始價格欄位。
4. 輸出 JSON Artifact，讓人判斷能否拿到可核對的商品價格。

本專案**不保證可通過蝦皮的反爬蟲限制**；被拒絕、未取得價格或遇到驗證時，依實際情況記錄。所列價格均不是已核實的成交價。

## 專案操作

安裝（Python 3.12）：

~~~bash
python -m pip install -r apps/ScraplingProbe/requirements.txt
python -m patchright install chromium
~~~

本機或 Runner 執行：

~~~bash
python apps/ScraplingProbe/scrapling_probe.py --url https://shopee.tw/3M-%E7%BE%8E%E7%B4%8B%E7%B4%99%E8%86%A0%E5%B8%B6-2688%E9%81%AE%E8%94%BD%E8%86%A0%E5%B8%B6%E7%84%A1%E6%AE%98%E8%86%A0%E6%89%8B%E6%92%95-%E9%9B%BB%E9%8D%8D%E5%88%86%E8%89%B2%E6%B1%BD%E8%BB%8A%E9%AB%98%E6%BA%AB%E5%99%B4%E6%BC%86%E9%81%AE%E8%94%BD%E5%99%B4%E5%A1%97%E4%BF%9D%E8%AD%B7-%E8%A3%9D%E4%BF%AE%E9%81%AE%E8%94%BD%E7%84%A1%E7%97%95%E8%86%A0%E5%B8%B6%EF%BC%88%E5%8F%AF%E9%96%8B%E7%99%BC%E7%A5%A8%EF%BC%89-i.768064806.19160839433 --output probe-result.json
~~~

預設樣本是公開搜尋可找到的普通文具商品，僅為提供可重現的非個人化測試目標。若商品下架或需驗證，輸出將忠實呈現；不可將失敗視為 0 元。

單元測試：

~~~bash
python -m unittest discover -s apps/ScraplingProbe -p 'test_*.py' -v
~~~

## GitHub Actions

`.github/workflows/scrapling-probe.yml` 支援 Pull Request 與手動觸發。首次建立 PR 才進行一筆實際公開頁面讀取；後續更新 PR 僅執行離線單元測試，避免對遭阻擋目標重試。需要另一次公開頁面測試時，須先確認授權與執行方式。實際抓取結果在當次 Run 的 Artifact（保存 3 天）。

## 首次實測結果（2026-11-15）

[GitHub Actions Run #1](https://github.com/simonliu1118-byte/sandbox/actions/runs/43670891870) 完成套件安裝、Chromium 安裝與三項單元測試；對一筆已公開索引的商品頁只發送一次 top-level navigation。

- 瀏覽器最終導向 `/verify/traffic/error`（流量驗證頁），回傳 HTTP 200。
- 擷取到背景請求資訊 14 筆，但沒有商品價格候選值，也沒有取得可核對的商品名稱。
- 第一版誤將此情況報為 `no_price_found`；Build 1 修正為能識別驗證頁的 `blocked_or_challenged`，以離線單元測試覆蓋。為尊重限制，沒有在修正時重新發送現場請求。
- 結論：Scrapling 的 GitHub 執行環境可用；臺灣蝦皮商品頁匿名查價在本次條件下**不可行**。不代表所有時間、商品、網路條件下都必然失敗。

## 預期資料

- `status`：`price_candidates_found`、`no_price_found`、`blocked_or_challenged` 或 `fetch_error`
- `http_status`、`final_path`（最終跳轉路徑）、`title`
- `rendered_price_candidates`、`api_price_candidates`（皆未核對）
- `price_verified: false`

本實驗不收集帳密、Cookie、原始頁面或背景 API 完整回應。公開 Repo 中不得提交私人查價清單、企業營運資料或敏感資訊。

Copyright © 2026 C.C. Liu. All Rights Reserved.
