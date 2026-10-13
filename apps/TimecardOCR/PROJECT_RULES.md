# TimecardOCR Project Rules

本檔僅記錄 TimecardOCR 持續適用的專案規則。共通規範見 repository 根 `REPOSITORY_RULES.md` 及 `REPO_POLICY.md`。

## 1. 產品用途與執行環境

- TimecardOCR 是個人用途的 Windows 10/11 可攜式打卡照片工時統計工具；本專案不代表任何公司或組織。
- 執行架構固定為 Windows PowerShell 5.1 本機服務、Windows 內建 OCR、HTML/JavaScript 瀏覽器 UI；不要求使用者安裝 Python 或 Node.js。
- 正式發行應為可攜式資料夾；執行檔、ZIP 與 runtime 輸出不提交 Git。

## 2. 工時計算

- 每一日期獨立計算，不跨日；打卡時間以 24 小時制解讀，輸入順序不影響計算。
- 同日兩筆時間依早晚計算一段時數；四筆時間依早晚排序、兩兩配對，再加總當日有效工時。
- 先合計當日有效工作分鐘，再以 15 分鐘為單位向下取整；15 分鐘等於十進位工時 0.25。
- 不完整、不合理或讀取不清的紀錄應提示人工核對，不得自動捏造打卡時間或工時。

## 3. OCR、AI 與個人資料

- Windows 本機 OCR 為預設辨識途徑；Gemini AI 是使用者主動啟用的可選備援，不得預設上傳照片。
- 雲端 AI 傳送前需讓使用者預覽裁切區域並明確同意；AI 候選結果不自動覆寫已核對資料。
- API Key、員工打卡照片、真實個資與 Excel 出勤檔不得寫死在程式碼、提交至 Git 或隨正式測試資料上傳。
- 辨識資料與計算結果須讓使用者在匯出 Excel 前檢查與修正。
