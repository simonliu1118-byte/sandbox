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

## 第二種模式：Google Chrome 有畫面測試（2026-11-15）

依使用者本次指示，一次性比較 `StealthySession(real_chrome=True, headless=False)`，在 Linux Xvfb 使用 Runner 提供的 Google Chrome。這並非 CAPTCHA 解法；一旦仍遇到流量驗證就停止。測試記錄只保留最終路徑、HTTP 狀態與價格候選值。

- [GitHub Actions Run #3](https://github.com/simonliu1118-byte/sandbox/actions/runs/43672133799)：套件安裝與單元測試通過；採用 Xvfb、系統 Google Chrome、`headless=False`、`real_chrome=True` 完成一次公開商品載入。
- 同樣導向 `/verify/traffic/error`，回傳 HTTP 200，結果為 `blocked_or_challenged`，`api_responses_seen=9`，無商品名稱與價格候選值。
- 已移除 PR 同步事件的線上測試步驟，避免未來 PR 更新重複讀取受限制的頁面。

## 預期資料

- `status`：`price_candidates_found`、`no_price_found`、`blocked_or_challenged` 或 `fetch_error`
- `browser_mode`、`http_status`、`final_path`（最終跳轉路徑）、`title`
- `rendered_price_candidates`、`api_price_candidates`（皆未核對）
- `price_verified: false`

本實驗不收集帳密、Cookie、原始頁面或背景 API 完整回應。公開 Repo 中不得提交私人查價清單、企業營運資料或敏感資訊。

Copyright © 2026 C.C. Liu. All Rights Reserved.

## 多平台第一次實測結果（2026/11/15）

GitHub Actions [Run #5](https://github.com/simonliu1118-byte/sandbox/actions/runs/43689419536) 實際執行三個公開資料來源的各一次請求：

- **momo 成功**：Logitech 羅技 M331 SilentPlus 靜音滑鼠（黑），品號 `9956761`，HTTP 200，促銷價 **NT$699**。
- 酷澎失敗：HTTP 403，拒絕存取。
- ECPRO 第三方 API 失敗：單次連線逾時。

此次取得的價格是公開促銷價，並非已核實的購物車、會員或結帳實付價。此結果也不代表每筆 momo 商品都能讀取。

### 可重複使用的 momo 單頁價格工具

V0.2.0 Build 1 將實驗程式收斂為可輸入任意 momo `/product/<id>` 網址的單頁查價程式。以品號確認商品身分，並從明確標示的「促銷價／活動價／優惠價」取得價格。

```bash
python apps/ScraplingProbe/market_price_probe.py --url https://www.momoshop.com.tw/product/9956761 --output momo-result.json
```

每次命令只送出一次公開 HTTPS 請求；不登入、不重試、不處理驗證、不使用代理。結果為 JSON，包含時間、來源、品號、標題、價格及狀態，失敗不會以 NT$0 代表。CI 的 PR 更新僅執行離線測試，不再自動抓取購物平台。
## 多平台公開價資料測試（V0.2.0）

2026/11/15 新增 `market_price_probe.py`，以獨立公共來源各單次請求實驗：

- momo：公開 M331 滑鼠商品詳情頁（`/product/9956761`）。必須取得商品識別、名稱與「促銷價」或明確價格 metadata。
- 酷澎：公開 M331 滑鼠商品詳情頁（`/products/463932540010509`），只辨識有標籤的商品售價，不將首購價誤作一般價。
- ECPRO：免金鑰公開比價 API，查詢少量「氣炸鍋」結果，僅辨識含 momo 通路、名稱與價格的報價。資料屬第三方，引用時標示 ECPRO 來源；正式商業大量使用需向提供者確認授權。

每個來源只執行一次正常公開 GET，不登入、不重試、不解驗證。若來源網頁不含完整商品售價、產品辨識失敗、或回傳阻擋，就記錄失敗。成功抓到公開頁面價格也不代表結帳實付價。


## 蝦皮公開搜尋列表研究（V0.2.1）

嘗試對已公開索引的 `/list/Logitech羅技/M331` 進行單次匿名 HTTPS 讀取，檢查 HTML 是否含有同時具備商品連結、shop/item ID 與價格的列表卡片。此次只作有限的可行性觀察，不登入、不換 IP、不重試、不解析驗證；不儲存原始 HTML 或完整回應。GitHub PR 觸發是一次性的，驗證結束後移除。

搜尋引擎索引曾顯示 M331 商品價格，但索引資料可能過時，不能視作即時成交價。第三方 Apify 搜尋結果服務與蝦皮聯盟 API 為備案，尚未用我們的憑證驗證。

### 實際測試結果：公開列表未能取得商品卡片

2026-11-15 GitHub Actions [Run #7](https://github.com/simonliu1118-byte/sandbox/actions/runs/43689834708)：
- 單次讀取 `https://shopee.tw/list/Logitech羅技/M331`，回應 HTTP 200，最終路徑未改變。
- 讀取回應約 216 KB，但 HTML 解析的可見文字 token 為 0；`linked_price_cards=[]`，`status=no_linked_price_cards`。
- 不得把 HTTP 200 解讀為抓價成功，也不能推論是被允許存取的完整商品列表；可能為純 JS 殼或不可解的頁面。
- 已移除 PR 更新時的網路測試步驟，後續更新只跑離線單元測試。
- 目前沒有逐規格價格、最低價或最高價的可靠實測結果。官方 Seller Shop API 的 model list 需要有資格及賣場授權，第三方公開資料也可能只有商品區間，不能當作競品逐規格價格來源。

## 蝦皮逐規格價格：第三方來源研究（V0.2.2）

已查到兩家 Apify Actor 宣稱可提供臺灣蝦皮的 SKU 規格 ID 與個別售價：

- [khalid_alsaba/shopee-product-scraper](https://apify.com/khalid_alsaba/shopee-product-scraper)：文件示例含 `raw.get_pc.data.item.models[].{model_id,name,price}`，原始價格數值需除以 100000。
- [sian.agency/shopee-product-scraper](https://apify.com/sian.agency/shopee-product-scraper)：文件示例含 `skuModels[].{model_id,price}` 與 coverage；公開示例只有一個規格，不能證明多規格價格。
- [xtracto/shopee-product-detail](https://apify.com/xtracto/shopee-product-detail)：明言 2026 年公開商品資料的逐規格價格通常隱藏，只能取商品價格區間。
- 蝦皮 Open Platform 的 `get_model_list` 需要賣場授權，不能假設能直接查競爭對手。

新增 `variant_provider_probe.py`：**不呼叫蝦皮、也不啟動付費 Actor**，僅對上述兩家 Apify 的公開 metadata 及公開 last-dataset 範例執行一次 GET，若無權限或沒有可公開讀取的多規格價格便記錄結果，絕不虛構成功。所讀公開輸出僅記錄欄位完整度統計，不儲存其他人的原始 Dataset。

驗收以臺灣商品、至少兩個不同的 Model ID、逐筆非 null 的價格、可辨識的規格名稱為基準；只得到 `price_min` / `price_max`、只有規格名稱或單一 SKU 範例都不算完整成功。外部服務若需註冊、付費或授權，必須另外確認資格和條款。


### 第三方 SKU 資料實測結果（2026-11-15）

[GitHub Actions Run #9](https://github.com/simonliu1118-byte/sandbox/actions/runs/43698583202)：
- khalid_alsaba 的 Apify metadata HTTP 200；最後一筆執行的公開 Dataset API 回傳 HTTP 401。
- sian.agency 的 Apify metadata HTTP 200；最後一筆執行的公開 Dataset API 回傳 HTTP 401。
- 因無法匿名讀取實際結果，兩者均無足以驗證臺灣商品逐規格價格的樣本；**不得把供應商網頁中的示例 JSON 當成我們實測成功**。
- 14 項離線測試通過（含 5 個 SKU 驗收測試）。本次無付費執行、無蝦皮請求、無憑證。
- Apify 供應商的實際商品擷取能力仍待有授權、可控成本的正式試用核對。第三方結果也需要實際比對蝦皮頁面及確認資料更新時間。
- [Rebrowser Shopee Dataset](https://github.com/rebrowser/shopee-dataset) 提供臺灣商品資料集及 `models` 欄位規格，但公開免費樣本將 `models`、`price` 等欄位以 `[PREMIUM]` 遮蔽，無法用免費樣本證明逐規格價；其 Hugging Face 樣本採 `CC BY-NC 4.0`，不可直接當商業競品監測資料來源。
- 已移除 GitHub PR `synchronize` 的一時性第三方網路探針，恢復只執行離線單元測試。

## Octoparse 公開 SKU 樣本查驗（V0.2.3）

2026-11-15 由於 Octoparse 的免費按需試用需提交需求（公開網站上無可直接執行的臺灣試用 API），先檢查其 Hugging Face 公開資料集 [Octoparse/shopee-product-sku-data-sample](https://huggingface.co/datasets/Octoparse/shopee-product-sku-data-sample)。

- 只存取 Hugging Face 公開 Dataset Server 的 split metadata 與每個 split 最多 100 筆示例，不向蝦皮直接請求、不申請帳號、不啟動付費抓取。
- 檢查欄位 `sku_id`、`spu_id`、`sku_real_price` 並計算同商品是否至少有兩個不同的規格價格。
- 巴西市場的 SKU 價格只能驗證資料形式，不能證明臺灣 Shopee 可取得同樣的逐規格售價。
- 正式臺灣試用需向 Octoparse 申請指定商品的免費樣本，或使用 Apify 免費方案登入後執行單一測試商品。

### 2026-11-15 GitHub Actions Run #11 實測結果

[公開資料集實測 Run #11](https://github.com/simonliu1118-byte/sandbox/actions/runs/43701628587)：
- Hugging Face Dataset Server `/splits` 及 `/rows` 均 HTTP 200。
- 公開資料包含 `products`、`skus` 兩種 split/config；讀取每組最多 100 筆樣本。
- SKU 示例中有 100/100 筆同時具備商品識別、SKU 識別及數值售價。
- 將 SKU 按所屬商品群組後，15 組具有至少兩個 SKU；其中 15 組都存在不同個別價格。
- 所有已核對 SKU 的市場代碼為 `br`（巴西），**不是臺灣**；`taiwan_multi_price_groups=0`。
- 這證明 Octoparse 公開樣本確實提供逐規格價格資料結構，但**不證明臺灣蝦皮商用查價可行**。臺灣需進一步取得合法試用資料。
- 無需登入、付費或啟動 Actor；此次沒有向蝦皮發送連線。
- 已移除一次性資料集連線 CI，後續 GitHub PR 更新仍只執行離線單元測試。
