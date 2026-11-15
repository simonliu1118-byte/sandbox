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
python apps/ScraplingProbe/scrapling_probe.py --url https://shopee.tw/product/293681214/23632340277 --output probe-result.json
~~~

預設樣本是公開搜尋可找到的普通文具商品，僅為提供可重現的非個人化測試目標。若商品下架或需驗證，輸出將忠實呈現；不可將失敗視為 0 元。

單元測試：

~~~bash
python -m unittest discover -s apps/ScraplingProbe -p 'test_*.py' -v
~~~

## GitHub Actions

`.github/workflows/scrapling-probe.yml` 支援 Pull Request 與手動觸發。PR 建立後應自動執行一次，結果在該 run 的 `scrapling-probe-v0.1.0` Artifact（保存 3 天）。

## 預期資料

- `status`：`price_candidates_found`、`no_price_found`、`blocked_or_challenged` 或 `fetch_error`
- `http_status`、`title`
- `rendered_price_candidates`、`api_price_candidates`（皆未核對）
- `price_verified: false`

本實驗不收集帳密、Cookie、原始頁面或背景 API 完整回應。公開 Repo 中不得提交私人查價清單、企業營運資料或敏感資訊。

Copyright © 2026 C.C. Liu. All Rights Reserved.
