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

## 臺灣蝦皮逐規格報價驗收工具（V0.2.4）

新增純離線的 `sku_vendor_validator.py`，適用供應商試用 CSV 或 JSON，**不含任何自動登入或網路請求**。驗收重點：

1. 市場必須為 `TW` 且幣別為 `TWD`。
2. 同一個 `shop_id + item_id` 至少有兩個不同 `sku_id/model_id`，每個都要有規格名稱與獨立的數值售價。
3. 必須包含臺灣蝦皮商品網址、帶時區的資料擷取時間。
4. 只有價格區間 `price_min/price_max`，沒有 SKU 各自的價格，歸類為**備用方案**，不得視為完整逐規格報價。
5. 同一個商品兩種規格同價仍可證明 SKU 價格欄位完整，另記錄有不同價格的商品組數。
6. 超過七天的樣本另標示過舊；即使通過欄位驗證，也不代表現在的即時售價或結帳價。

只顯示彙總驗收結果，不在 CI 或 GitHub 儲存供應商原始資料。使用前先將供應商欄位對應到以下標準欄位（或程式支援的 camelCase / 常見別名）：

`site,currency,shop_id,item_id,sku_id,sku_name,sku_price,product_url,captured_at`

本機離線執行：

~~~bash
python apps/ScraplingProbe/sku_vendor_validator.py --input vendor_sample.csv --output local-validation.json
~~~

本輪研究的重要供應商差異：

- [Octoparse Shopee Managed Data Service](https://www.octoparse.com/data-service/shopee-data)：公開說明支援臺灣商品與 SKU 價格，接受免費客製樣本申請，但正式代管方案一次性從 US$699 起、持續監控從 US$599／月起。
- [Wellbyte SKU Matrix](https://www.wellbyte.net/en/docs/api/shopee-product-item_sku_matrix_v1)：註冊贈送 500 credits，SKU 類端點成功呼叫 39 credits；但其公開回應描述偏向「規格組合／SKU 模型」，**不能據此認定每種規格均有獨立價格**，待實測。
- [鏡海數據 Shopee API](https://jhzyapi.com/products/shopee/)：臺灣站商品詳情示例含價格區間、規格組合、展示 model ID，但示例並未證實所有規格皆有個別售價。

### 免費試用資料需明確指定的欄位

請求提供臺灣站（TW/TWD）至少三筆多規格商品，包含每筆 `shop_id`、`item_id`、商品網址、擷取日期，以及每個 `model_id`、完整規格名稱、各自的展示售價、原價、庫存狀態、優惠類型。須確認是否為登入前價格／會員價／券後價，並且提供一週或每日更新的費用、最小訂購量、資料商用授權。

若供應商只能提供價格區間或展示最低價，不能滿足本專案指定規格比價需求。

## 2026-11-15 預算決策：Octoparse 排除

**使用者已明確表示 Octoparse 正式服務超出預算，不得再將它推薦為可採用的正式資料來源。** 既有公開巴西 SKU 樣本僅留作技術研究證據；不申請 Octoparse 正式付費服務，也不以其價格規劃未來架構。

後續優先順序（本輪仍未啟動任何新付費 API）：

1. [Wellbyte](https://www.wellbyte.net/en/pricing)：註冊免費 500 credits；Shopee SKU Models / Matrix 端點每次成功 39 credits，按標示換算單次約 US$0.039，可用贈額測約 12 次成功呼叫。**尚未證實具有逐 SKU 非空售價**；必須由授權帳號實際驗收，失敗不當成成功。
2. [Apify](https://apify.com/pricing)：Free 方案提供 US$5 的月度使用額度；但各 Actor 定價與免費方案可用性不同，且過去公開 dataset 查詢 HTTP 401，不能當成已驗證的逐規格臺灣資料來源。
3. 其他可提供合法授權與小額試用的臺灣蝦皮 SKU 價格來源，依逐規格價格欄位驗收結果決定。

本專案不得要求或使用使用者日常蝦皮帳號，亦不得透過 VPN／新帳號反覆規避已顯示的流量限制；所有第三方 API 金鑰不得出現在 public Git、Actions log 或聊天文字。正式採購須另行確認可接受的月費上限、商用授權、最小請求量與資料更新頻率。


## V0.2.5 雙軌價格資料策略（使用者確認）

- **主要方案**：同一臺灣蝦皮商品至少兩個不同 SKU／Model ID，且有對應規格名稱與每一規格的數值售價，報告狀態 `complete_multi_sku_price_evidence`。
- **備用方案**：如果只有商品最低價、最高價，或兩者形成的價格區間，則使用 `fallback_product_price_bounds`；資料仍需為 TW/TWD、可辨識 shop/item ID、蝦皮商品 HTTPS 連結與帶時區的擷取時間。商品最低價只可顯示「最低 NT$… 起」、最高價只可顯示「最高 NT$…」，兩者皆有才顯示區間。
- **禁止混用**：價格區間不能推導出某個 SKU 的售價，不得計算指定規格的精準價差或宣稱最低價對應哪個規格。
- 若同筆資料同時包含合格的逐規格售價與商品層級價格範圍，以主要方案優先，避免重複計數。
- 驗證器現在回報備用商品數、最低價單獨數、最高價單獨數、完整區間數及超過 7 天的資料數；不將個別商品價格輸出到公開 CI。
- Wellbyte／Apify 等供應商若只能提供商品價格上下界，保留備用資格，不直接淘汰；但資料來源仍需經實測，不得把文件範例當成已成功的資料。

## V0.2.6：低成本 Apify 商品詳情資料接入（純離線）

本版為兩個公開標示支援臺灣的按筆計價第三方服務加入 **JSON 輸入轉換器**，不啟動 Actor、不呼叫蝦皮或任何付費 API：

- [Khalid Shopee Product Details](https://apify.com/khalid_alsaba/shopee-product-scraper)：標示 US$6／1,000 筆。預期 `raw.get_pc.data.item.models[].{model_id,name,price}`，原始數字除以 100000 才是 TWD；`item.price_min`／`item.price_max` 同樣按比例處理。若只有產品價格區間，自動列為備用方案。
- [SIÁN Shopee Product Scraper](https://apify.com/sian.agency/shopee-product-scraper)：標示 US$4.40／1,000 筆起，可選商品詳情與 SKU 模型增補（實際費用可能高於基本費率）。`skuModels[].price` 已是一般幣值；需要 SKU ID **以及**規格名稱才能列為主要方案。文件示例只有單一 model、缺少規格名稱時不能誤認為逐規格成功。
- [Zen Studio Shopee Scraper](https://apify.com/zen-studio/shopee-product-scraper)：其文件說明完整 SKU 型號售價只對 BR／ID 開放，TW 僅支援商品層級列表，因此暫列備用候選。
- [Wellbyte Shopee SKU Matrix](https://www.wellbyte.net/zh/docs/api/shopee-product-item_sku_matrix_v1)：39 credits／成功呼叫，但完整 SKU 價格欄位還不確定；狀態頁查不到近期 SKU 端點成功樣本，現階段保留研究，不虛構 API 資料格式。

用法（使用**已獲准的供應商 JSON 樣本**，並確保原始樣本不加入 Public Git）：

~~~bash
python apps/ScraplingProbe/provider_sample_adapter.py --provider khalid --input local-provider-output.json --output validation-summary.json
python apps/ScraplingProbe/provider_sample_adapter.py --provider sian --input local-provider-output.json --output validation-summary.json
~~~

內部呼叫現有 `sku_vendor_validator.evaluate()` 以一致標準判定 `complete_multi_sku_price_evidence`（逐規格主要方案）、`fallback_product_price_bounds`（商品最低／最高價備用）或 `insufficient_evidence`。輸出僅有彙總統計，沒有供應商原始商品／規格內容或 API 金鑰。

驗證器目前只認證資料**格式完整**，不確認臺灣蝦皮當下可否取得該資料，也無法證明商業授權及真正成交價格。兩個 Actor 先前公開 Dataset API 返回 401；後續必須持有獲授權的 Apify 帳號試用，才能驗證真實資料與成本。任何付費執行前應人工設定總預算上限與商品筆數，並確認增補欄位的實際計價。

### 粗估月成本（僅供採購篩選）

假設每天對相同商品各查一次，30 天，不含額外增補、執行費、稅或匯率：

| 商品數量 | Khalid US$0.006／筆 | SIÁN 基本價 US$0.0044／筆 |
|---|---:|---:|
| 50 | US$9 | US$6.60 |
| 100 | US$18 | US$13.20 |
| 500 | US$90 | US$66 |

SIÁN 逐規格 Model 增補可能另計費，因此不能直接以基本價保證含完整 SKU 資料。所有價格都以供應商實際執行當時為準。Octoparse 正式方案已因使用者預算限制排除。

## V0.2.7：備用價格資料只接受長期免費（使用者確認）

已定為永久硬性限制，與前一版「技術上是否有最低／最高價」分開判斷：

1. **主要方案**：有每個 SKU 的個別售價，可評估合理的按次費用及月費上限（尚未指定確切預算）。
2. **備用方案**：只有商品 `price_min`、`price_max` 或區間者，必須**持續零成本**。按筆收費、月租、一次性註冊贈額、付費試用後續扣費者，全部不得正式採用。
3. 有每月重置免費配額的服務，只有在「已核對供應商免費條款」「預計月查詢量低於免費配額」「超額硬性停止、無自動付款」三者皆成立時，才可列為受限免費備用；額度耗盡必須顯示資料過期／查價暫停，不能改用付費額度。
4. 報告中的 `fallback_product_price_bounds` **只代表資料完整度**，不代表符合預算；新增 `fallback_budget_eligibility`、`eligible_zero_cost_fallback_count` 等獨立預算核准欄位。
5. 新參數預設為 `unknown`（未證明免費），因此備用資格預設不通過。需要由操作人員依公開條款與實際預計用量人工確認，不能採信外部供應商 JSON 自行宣稱「免費」。

費用核對範例（只使用供應商合法取得的本機測試資料，不在 Public Git 存放）：

~~~bash
# 未確認費用時，即使有價格區間也一律沒有正式備用資格
python apps/ScraplingProbe/sku_vendor_validator.py --input local-sample.json

# 已確認長期不限量免費，才可明確核准
python apps/ScraplingProbe/sku_vendor_validator.py --input local-sample.json --source-cost-model free_unlimited --free-terms-verified

# 每月重置免費額度，須同時確認免費配額足夠及停止付費超額
python apps/ScraplingProbe/sku_vendor_validator.py --input local-sample.json --source-cost-model free_recurring_limited --free-terms-verified --projected-volume-within-free-limit --automatic-paid-overage-disabled
~~~

Apify Khalid／SIÁN 為按筆計價的來源，預設費用類型 `paid`：若只提供最低／最高價，**不得**進入正式備用方案；只有通過完整逐規格售價驗證，才有資格另外進行主要方案的價格評估。Wellbyte 若僅能提供區間，註冊贈送的 500 credits **不構成持續免費**，除非另外證實有可維持預計查價量的永久免費方案。

## V0.2.8：臺灣蝦皮低成本來源研究（2026-11-15）

本輪是官方條款、定價與資料可用性的研究，沒有實際取得新的臺灣蝦皮售價，也沒有啟動付費 Actor、使用蝦皮帳號、VPN 或使用者的本機／公司網路。

### 供應商候選及價格

| 來源 | 候選用途 | 文件標示費用 | TW 實測 |
|---|---|---|---|
| [Apify Khalid](https://apify.com/khalid_alsaba/shopee-product-scraper) | 主要：SKU ID、規格名稱、個別售價 | US$6 / 1,000 筆 | 我們尚未取得成功的 TW Dataset |
| [Apify SIÁN](https://apify.com/sian.agency/shopee-product-scraper) | 主要：商品詳情與 SKU 增補 | 約 US$4.40 / 1,000 筆起，SKU 增補可能另計 | 公開 Dataset API HTTP 401；單一規格示例不代表多規格成功 |
| [Apify Dami](https://apify.com/dami_studio/shopee-product-scraper) | 免費備用候選：商品卡片展示價、ID、連結 | US$0.235 / 1,000 筆（以 Free 方案額度抵用） | 未有我們的 TW 取得價格證據 |
| [Apify Lergassy Price Tracker](https://apify.com/lergassy/shopee-price-tracker) | 免費備用候選：當前展示價、價格變動紀錄 | Free plan 卡片價約 US$0.40 / 1,000 筆 | 未有我們的 TW 取得價格證據 |
| [Wellbyte SKU](https://www.wellbyte.net/en/docs/api/shopee-product-item_sku_models_v1) | 若有個別 SKU 價格可為主要候選 | 39 credits／成功次，註冊 500 credits | 個別價格仍未證實；若只有區間，註冊贈額不構成長期免費 |
| [蝦皮臺灣聯盟 Open API](https://affiliate.shopee.tw/) | 免費備用研究線，商品最低／最高價 | 加入聯盟免費、Open API 需另核准 | 未驗證臺灣 schema、權限與允許的私有競品監控用途 |
| Octoparse | 已排除 | 超出使用者預算 | BR 已有實測多規格樣本，TW 尚未 |

### 目前明確的新發現：Apify Free 額度可長期維持零付款

[Apify 官方方案](https://apify.com/pricing)：Free 月費 US$0，包含每月 US$5 平台使用額度，免信用卡；[官方限額說明](https://help.apify.com/en/articles/11079614-reaching-platform-limits)：Free 方案額度耗盡後停止使用，不能調高平台限額或自動超額扣款。

因此，只要**保留 Free 方案、沒有付款、預計查價量落在每月重置額度內**，可進入使用者規定的「只接受長期免費」備用候選審查。但這不等於已證明資料準確或已驗證商用授權。

若每天檢查 100 件商品、30 天共 3,000 筆，僅以公告的單件事件價估算：
- Dami：3,000 × US$0.000235 = US$0.705／月
- Lergassy 卡片：3,000 × US$0.0004 = US$1.20／月
- Khalid 詳情：3,000 × US$0.006 = US$18／月，屬可能付費的逐規格主要候選，不可當免費備用

以上只是供應商公告事件費的理論試算，未包含任何可能的啟動、運算、儲存、代理、加值或其他費用。實際消耗量與是否符合免費額度，必須透過一次合法的最小試用及 Billing 紀錄確認。不得直接保證 3,000 筆免費。

**重要語意**：Lergassy 所謂 lowestPrice / highestPrice 是多次觀測的「歷史高低價」，不是目前商品的 price_min / price_max，也不是特定 SKU 價格。Dami 的 price 為商品卡片展示價，不能自動標成當前最低價或指定規格售價。

### 臺灣聯盟 Open API：先取得使用權

臺灣[蝦皮聯盟計畫條款](https://help.shopee.tw/portal/4/article/77310) 有機器人、自動查詢與未經允許的資料擷取限制。加入[分潤計畫](https://shopee.tw/m/affiliateprogram) 雖然免費，也不等於能取得 Open API 權限及把報價用於私有競品監控。

[2026-08 臺灣申請經驗](https://9527.com.tw/journey/shopee-api-waiting/) 指出 affiliate.shopee.tw 後台另有 Open API 審查入口；尚非任意自助取得金鑰。productOfferV2 的 priceMin / priceMax 欄位有其他國家文件佐證，但不能因此假設臺灣端已開放相同 schema 或用途。

### 下一個能真正證明可用的實驗

1. 若使用者自願建立自己的 Apify Free 帳號，且符合使用條款，才可由其安全設定 API token，**不可將 token 直接貼在公開 GitHub 或對話**。
2. 只對一筆臺灣蝦皮、已知具有多規格的公開商品執行第三方 Actor，先核對是否有商品 ID、每個 SKU ID、規格名稱、個別售價、查價時間及來源 URL。
3. 同時核對實際 US$ 費用與當月剩餘 Free 額度；不得自動升級、充值、超量執行或接入消費者蝦皮帳號。
4. 若成功取得獨立 SKU 價格，送入既有 sku_vendor_validator.py 做主要驗收；若只有商品展示價或當前價格上下界，在**零付款且額度可長期支撐**的條件下才列免費備用。
5. 到目前為止，**臺灣蝦皮真實逐規格售價的獨立成功驗證仍為零筆**。不以供應商文件、模擬測試或別國樣本冒充實測成功。

## V0.3.0 — 關鍵字搜尋優先的競品查價原型

**主要入口改為關鍵字**：使用者不必自行收集商品網址。原型維持 `sandbox/apps/ScraplingProbe/` 的獨立工作分支；不合併 CYWEB、不建立正式獨立站、不部署。商品連結只在搜尋結果中提供，供查證。

新增 `keyword_search.py`：

- `--keyword` 接受中文、英文與型號，例如「羅技 M331」，自動建構 momo 站內搜尋 URL。只有明確傳入 `--live-momo` 時才發送**一次**匿名公開關鍵字頁 HTTP GET，嘗試取得多筆商品的品號、名稱、展示價與連結。
- 商品連結由程式自行發現；支援 momo `/product/<id>` 及舊式 `/goods/GoodsDetail.jsp?i_code=<id>`，不要求使用者提供。
- 搜尋結果可能只有商品名稱與連結而沒有價格；不能把「找到商品」當成「已查到即時售價」，更不能將缺價轉換成 0 元。
- 蝦皮關鍵字來源尚待實測。已有支援臺灣 keyword search 的第三方 Actor [Dami Studio](https://apify.com/dami_studio/shopee-product-scraper/input-schema)，輸入 `searchQueries`、`market=tw`、`maxItems`；但需經授權 Apify 帳號且免費額度與條款都需驗證。現階段**不執行付費 Actor**，僅可由 `--shopee-dataset` 匯入經授權的第三方關鍵字結果 JSON。
- `--html` 可輸出獨立靜態 HTML 比較表，與 JSON 一起檢查，不是對外發布的網站。
- 歷史已有一筆 momo 商品頁成功擷取 NT$699，但關鍵字搜尋與列表商品卡片價格必須透過本次實際關鍵字測試另外驗證。

~~~bash
# 離線模式：不對外發送任何請求
python apps/ScraplingProbe/keyword_search.py --keyword '羅技 M331' --output search.json --html search.html

# 一次 momo 關鍵字搜尋，產生搜尋結果表；沒有手動商品網址
python apps/ScraplingProbe/keyword_search.py --keyword '羅技 M331' --live-momo --limit 20 --output search.json --html search.html

# 經授權的蝦皮第三方搜尋結果可匯入，仍不直接請求蝦皮
python apps/ScraplingProbe/keyword_search.py --keyword '羅技 M331' --shopee-dataset local-shopee-results.json --output search.json --html search.html
~~~

下一步：若實測列表只能拿到商品連結，研究能否在合理免費請求量內自動讀取部分結果的 momo 商品頁價格；蝦皮則先取得可用的長期免費 keyword provider 並核對實際臺灣商品。查詢結果要依品牌、型號與規格分類，區間與異常才列入逐規格查價，後者暫不自動執行。

### V0.3.0 Build 1 — momo 公開搜尋資料介面（單次實測）

[Run #19](https://github.com/simonliu1118-byte/sandbox/actions/runs/43707027776) 的 momo 搜尋**網頁**返回 `no_linked_products_found`，實際找到零個商品與零筆價格，故目前不能把第一版當成可用的關鍵字比價。程式單元測試成功不代表網頁取價成功。

另外找到公開社群程式範例，使用 momo 搜尋資料路徑 `https://apisearch.momoshop.com.tw/momoSearchCloud/moec/textSearch`，直接以 POST 查詢中文關鍵字、解析 `rtnSearchData.goodsInfoList[].goodsName/goodsPrice`。參考：[公開 Hugging Face 程式範例](https://huggingface.co/spaces/lin0804/momo_pchome_search2/blob/main/app.py)。

新增 `momo_search_data.py`：由關鍵字自動產生搜尋請求，正確回傳每筆商品的品號、標題、公開展示價，異常價格或無 ID 的商品不能當成功；沒有使用者手動輸入網址。單次公開資料請求、無登入、無重試、無代理、無驗證繞過。這是**未公開承諾穩定性的站內資料介面**，可能調整或封鎖；只有成功取得多筆合法價格才算驗證成功。此輪使用 GH Actions PR 的一次性檢查，完成後移除同步請求步驟。

### V0.3.0 Build 1 實測結論（2026-11-15）

- [Run #19](https://github.com/simonliu1118-byte/sandbox/actions/runs/43707027776)：一次讀取 momo 搜尋網頁得到 `no_linked_products_found`、**0 筆商品／0 筆價格**。
- [Run #20](https://github.com/simonliu1118-byte/sandbox/actions/runs/43707140739)：一次 POST momo 搜尋資料介面得到 `HTTP 403`、`http_denied`、**0 筆商品／0 筆價格**。Python 57/57 單元測試通過只是說明程式處理格式正常，不代表已取得真實價格。
- 先前 [Run #5](https://github.com/simonliu1118-byte/sandbox/actions/runs/43689419536) 有一筆 momo 指定商品頁 NT$699 的成功經驗；但**使用者要求的關鍵字直接搜尋多筆商品售價目前仍未成功**。
- 完整產品目標是「只輸入關鍵字就自動列出多平台商品、賣家、當前售價／區間及查詢時間」，不要求使用者事先提供商品連結。只找出 URL 或返回未驗證的索引價格**不算查價功能成功**。
- 已移除 PR `synchronize` 的一次性 momo 網路檢查，避免後續提交時再向拒絕存取的資料介面反覆發送請求。
- 下一個證據門檻：使用**已核准且不產生費用**的雲端關鍵字搜尋資料服務實測一個關鍵字至少三筆臺灣商品，且每筆有品號、名稱、有效售價和來源時間；蝦皮也需相同標準。若只有搜尋卡片價，仍屬可用的免費商品級備用，不能冒充逐規格價。

### V0.3.0 Build 2 — 採用經開源比價工具使用的 momo 行動版搜尋格式

發現 [coseto6125/mcp-taiwan-price-compare](https://github.com/coseto6125/mcp-taiwan-price-compare)（MIT）實際實作了關鍵字 momo 搜尋，2026-11-05 最近仍有更新；上游搜尋器使用 `never-primp` 的 Chrome profile、momo 行動站的 `host=ecmobile` 及完整搜尋參數，結果讀取 `rtnSearchData.goodsInfoList[].SALE_PRICE`。原本本專案使用錯誤 `host=momoshop` 與不完整 payload，前次 GitHub Runner 曾 HTTP 403。

本版採用同一個公開搜尋資料介面、上游實證的移動端參數與一般瀏覽器 HTTPS 設定，**只執行一次**搜尋「羅技 M331」，不中途換 IP／代理、不登入、不處理 CAPTCHA／驗證、不自動重試。價格來自站方回傳 `SALE_PRICE`，以站方 `success=true` 為前提；若只回傳失敗或任何非 200 HTTP，結果仍標成失敗。實際是否可從 GitHub Runner 存取，須依本輪動態 CI 結果判定。

引用原專案的公開技術作法（MIT）：[src/price_compare/platforms/momo.py](https://github.com/coseto6125/mcp-taiwan-price-compare/blob/master/src/price_compare/platforms/momo.py)，本專案保留獨立程式實作、單次請求、無自動重試。

### V0.3.0 Build 3：momo 關鍵字查價成功，提供獨立原型頁面

[Run #22（2026-11-15）](https://github.com/simonliu1118-byte/sandbox/actions/runs/43708558758) 使用「羅技 M331」關鍵字在 GitHub Ubuntu Runner **成功直接讀取 momo** `HTTP 200`、`success=true`、11 筆來源商品，精準匹配 2 筆（品號 4318322、9956761），兩筆公開展示價均為 **NT$699**。無需事先提供商品連結、不使用 momo 帳號、沒有代理或付費查詢。59 項單元測試通過。

本版加入 `momo_web_app.py` 與 `momo_ui.py`：在本機 Python 3.12 執行，開啟瀏覽器輸入關鍵字即可即時查價並顯示多筆商品與價格；不需要人工準備商品 URL。搜尋依價格由低到高排序，保留品號與商店來源。價格是公開商品展示價，**不是個別 SKU 或會員券後價**。

~~~bash
pip install -r apps/ScraplingProbe/requirements.txt
python apps/ScraplingProbe/momo_web_app.py
# 瀏覽器開啟 http://127.0.0.1:8765
~~~

本機網站僅綁定 `127.0.0.1`，不會對外部署、沒有排程、不會自動重新查價，10 分鐘內相同關鍵字使用記憶體快取，避免短時間重複觸發站方查詢。網路拒絕時會顯示明確失敗，不能將空結果冒充 0 元。

也可在 GitHub Runner／命令列查詢並產生可下載的靜態比價 HTML：

~~~bash
python apps/ScraplingProbe/momo_search_data.py --keyword '咖啡' --limit 20 --output momo-result.json --html momo-result.html
~~~

**原型驗收：**「羅技 M331」已得到兩筆真實價格。此版再針對比較廣泛的「咖啡」作一次 GitHub 雲端查詢，檢查是否可自動得到更多筆價格與可瀏覽的結果表；完成後撤除 PR 同步時的實際網路請求。原型不合併 CYWEB、未部署站點。

### V0.3.0 Build 3 — 第二個關鍵字實測成功，完成互動原型的核心查價流程

[Run #23](https://github.com/simonliu1118-byte/sandbox/actions/runs/43708711368) 使用廣泛關鍵字「咖啡」：直接查詢 momo 搜尋資料介面 `HTTP 200`、`success=true`，站方第一頁回傳 20 筆商品，經名稱包含「咖啡」篩選後 **14 筆**，14 筆都具有正數公開展示價、商品品號、名稱及站內連結。GitHub CI 64/64 單元測試通過，治理檢查通過。

其中可供核對的實測價格（臺灣時間 2026-11-15 19:23 左右）：
- 品號 `15508694`：親愛的 濾掛咖啡－黑夜珍藏 10 入，**NT$195**。
- 品號 `14119110`：G7 純咖啡 50 入，**NT$199**。
- 品號 `15815716`：AGF 即溶黑咖啡任選組，**NT$249**。
- 品號 `15687762`：LITTLE’S 無糖即溶咖啡（50g），**NT$314**。
- 品號 `15598432`：Remote Coffee 精品濾掛咖啡 2 盒組，**NT$365**。

本 Run 上傳兩個有時效的 Artifact：
- `momo-broad-result.json`：真實商品結果、商品名稱、價格、連結、查詢時間。
- `momo-price-report.html`：可開啟的單頁比價表，從真實查詢結果產生並依價格排序。

**已達成的功能**：只輸入關鍵字自動取得 momo 多筆真實商品展示價（兩個不同關鍵字均實測成功）、獨立本機網頁可直接輸入新關鍵字並查看價格排序表；整個過程免費、不需要 momo 商品網址或 momo 帳號。

**尚未達成的部分**：多平台比價、momo 單筆各規格精準價格、關鍵字同品類的規格自動歸一化、長期穩定性與正式站部署；不能拿兩次成功宣稱各個關鍵字永久可用。此功能是可試用的獨立原型，不應直接視為正式商用系統。

[GitHub Run #23 實測與 HTML 報告](https://github.com/simonliu1118-byte/sandbox/actions/runs/43708711368)。

已移除 CI 的一次性 momo 公開查詢步驟，未來 PR 同步只執行離線測試，不重複觸發站方 API。此次沒有對 CYWEB 進行任何修改。


### 酷澎臺灣公開搜尋一次性技術驗證（2026-11-15）

**目標**：跳過 Partners API 申請，直接用匿名公開搜尋頁 `https://www.tw.coupang.com/srp/3m%E9%80%8F%E6%B0%A3%E8%86%A0%E5%B8%B6` 查商品；純測試網站可存取性，不接觸使用者 IP、酷澎帳號或公司網站。

[GitHub Actions Run #28](https://github.com/simonliu1118-byte/sandbox/actions/runs/43729475496) 實測結果 **HTTP 403，0 筆商品、0 筆價格**。這只表示當次 GitHub 雲端出口無法匿名讀取臺灣酷澎 `/srp/` 搜尋頁，不能直接推論所有環境永久不可用。Python 66/66 + 獨立網頁 12/12 測試通過，證明的是程式格式處理，**不是取得酷澎價格成功**。

對照市面上 Apify 的 Coupang 工具，多以 `coupang.com` 韓國站與 KRW 為目標；有工具明確表示不支援臺灣 `tw.coupang.com`，故不能列為臺灣查價已可用方案。Google／其他搜尋引擎可能索引酷澎商品名稱與價格，但索引**不能保證即時，不能直接拿來計算準確市場價格帶**。

技術研究後續順序：
1. 尋找**明確支援臺灣酷澎**而且不需帳號或持續付費的公開商品搜尋資料來源，且能取得實際有效價格、商品識別與時間；不能用韓國站結果代替。
2. 若合法提供的一次性資料樣本或外部授權資料能取得頁面原始商品列表，再研究 HTML／結構化資料解析器，與 momo 共用型號、規格、包裝入數及統計模組。
3. 若公開站點仍回 403，不連續重試、不輪換 IP、不登入或挑戰驗證；不改用使用者本機／公司 IP。
4. 已移除 PR 更新時的自動連網測試。獨立的 `coupang_public_probe.py` 與離線單元測試保留，供未來經明確授權的一次性研究測試；無排程、不部署酷澎功能。

**結論：公開商品搜尋介面存在，但可用的匿名雲端商品價格查詢尚未實證；不能宣稱已與 momo 同等可用。**


### 2026-11-15 最新酷澎價格時效性驗證（關鍵限制）

使用者指出：飛比的收錄價格不一定是酷澎最新正確價格。本研究結論確認此疑慮，不應將第三方搜尋或比較站所見數字直接視為當前可購買的有效報價。

- [Run #29](https://github.com/simonliu1118-byte/sandbox/actions/runs/43730053013)：匿名讀取 [飛比 3M 1530-1 搜尋頁](https://feebee.com.tw/s/3m%201530-1/) 成功，HTTP 200；頁面可見 12 個「Coupang 酷澎」商家標示。**但無法確認其價格資料的上次更新時間、對應的完整規格／入數，以及酷澎目前實際展示價**。這些數字只能用於發現商品，不得納入目前價格統計。
- [Run #30](https://github.com/simonliu1118-byte/sandbox/actions/runs/43730948126)：對已核實存在的[酷澎 3M Nexcare 1530-1 商品頁](https://www.tw.coupang.com/products/460322787786761) 發出一次匿名 GET，GitHub Runner 得到 **HTTP 403，0 筆可驗證價格**。這是不同於搜尋頁的獨立商品詳情路線，但依然被拒絕，沒有再重試。
- 公開搜尋引擎可以讀到這個商品頁的**歷史快取**；不同索引時點曾顯示 NT$165 與 NT$162 等不同展示價格，但不代表現在仍有相同價或有庫存。外部搜索索引頁、飛比、價格歷史站**都不能**作為「即時當前價」。
- 若未來成功直接取得酷澎商品頁，仍須分辨「一般展示價」「首購折扣」「WOW 會員價」「券後價」及可能缺貨；僅把可核對的**一般展示價／當次查詢時間／對應規格數量**放入一般市場價格統計。需要會員、券、首購資格的條件價格另列，不能混算。
- 兩個一次性 PR synchronize 網路步驟已停用，**測試程式 `feebee_coupang_probe.py`、`coupang_detail_probe.py` 及其單元測試全部保留**，日後有新的來源／明確的一次性測試需求再人工啟動，不會在每次程式提交時重複碰站方 403。
- 本次仍**沒有**取得可驗證、可自動更新的臺灣酷澎當前價格，因此不接到 momo 網站，也不把第三方資料放入單價、均價、中位數或標準差。

## V0.5.0 Build 1 — 飛比酷澎獨立參考查價初驗（2026-11-16）

- 已在 `site/` 內新增「酷澎／飛比參考價」獨立關鍵字搜尋；不混合 momo 分類、價格與統計。
- 解析器僅取商品名稱連結、獨立價格節點及 Coupang 酷澎商家能配對的候選結果；否則顯示未取得。
- [第一次真實測試](https://github.com/simonliu1118-byte/sandbox/actions/runs/43741139794) 得到 10 筆候選，其中首筆誤把 Feebee 網站名稱當成商品，並非合格結果。
- 修正後 [第二次真實測試](https://github.com/simonliu1118-byte/sandbox/actions/runs/43741231975) 搜尋 `3m 1530-1` 得到 9 筆候選，抽樣包含不同規格／包裝 NT$175、179、231、395。這些是飛比收錄參考價，並非酷澎即時成交價，亦未全部人工逐筆核對。
- Python 測試與網站測試 22/22、Node 分類測試、Governance Check 均通過；這只是 GitHub Runner 的網路測試，尚非 Vercel Preview 驗收。
- 一次性飛比外部連網 CI 步驟已移除，後續 PR 僅執行離線測試。不記錄價格歷史、不追蹤更新時程、不建立資料庫或排程。
- PR #31 保持 Draft、不合併、不正式部署；現有 Preview 尚未更新。
