# GDriveDownloader Project Rules

本檔只保存 GDriveDownloader 的固定專案規則；repository 共通版本、開發、CI、Local-first 與 GitHub-final verification 規則依根 `REPOSITORY_RULES.md`。

## 1. 產品、平台與版本線

- 正式技術線：C# / .NET 8 / WinForms + WebView2；目標平台 Windows x64。
- 正式發行以 self-contained single-file（`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`）為目標，不要求使用者另外安裝 .NET runtime；發行資料夾只保留 `GDriveDownloader.exe` 與 `ffmpeg.exe` 兩個檔案，不得讓使用者面對成堆散落的 DLL。
- `GDriveDownloader.exe` 是本專案唯一產品識別，不建立第二個平行專案或另一條版號。
- 本專案為個人用途的下載自動化工具，不代表任何公司或組織。

## 2. 畫質策略

- **畫質解析權威固定為「實際播放 + 真實網路請求分析」，不得回頭改用 `yt-dlp` 或其他靜態猜測畫質清單的做法。** 這是為了修正舊版「無法正確辨識畫質」的根本病灶：單純複製 cookie 給外部 HTTP client（含 `yt-dlp --cookies`）在 Google Drive 的 `videoplayback` 端點上會被判定為非瀏覽器連線，實測回應 HTTP 403。
- 正確流程固定為：用 WebView2 開啟 Drive 播放頁、觸發播放、被動監聽播放器實際產生的 `videoplayback` 請求，依網址 `itag` 參數分類音訊／視訊與畫質；不得自行呼叫 Drive 內部 API 猜測畫質清單。
- 畫質偵測（監聽請求）固定使用 WebView2 原生的 `WebResourceRequested`（搭配 `AddWebResourceRequestedFilter`），不得使用 CDP `Network` domain 做這件事：實測 Google Drive 播放器可能在跨來源 iframe／worker 內發出 `videoplayback` 請求，CDP 對單一 top-level target 的 `Network.enable` 監聽不到這類請求，`WebResourceRequested` 是涵蓋整頁（含 iframe）的原生瀏覽器層級 hook，才能可靠偵測到。
- 下載一律「自動選擇偵測到的最高可用畫質」；同一畫質若偵測到多個候選來源，下載速度過慢（預設 < 50 KB/s，暖機期預設 8 秒）時依序改試下一個候選，全部候選都慢速時改用第一個候選以慢速下載到底，不得因為慢速就整體失敗。
- 實際下載一律使用同一個已登入的 WebView2 瀏覽器 session，透過 CDP 的 `Fetch` domain 在 Response 階段攔截目標 `videoplayback` 請求並用 `IO.read` 串流寫檔；不得另外用複製出來的 cookie／URL 交給外部 HTTP client（`HttpClient`、`yt-dlp` 等）發送請求，避免重新踩到 403 的坑。
- 候選來源網址只需移除 `range` 與 `ump` 這兩個查詢參數即可取得完整串流網址；其餘參數（含 `rn`、`rbuf` 等）維持原樣，不得額外增刪。

## 3. Google 登入與私人影片授權

- 私人影片授權採「內嵌 WebView2 開啟獨立登入視窗」模式，不使用讀取使用者主要瀏覽器（Chrome/Edge 等）既有 cookie 的方式。
- WebView2 使用固定的持久化 profile 目錄（本機 `%LOCALAPPDATA%\GDriveDownloader\WebView2Profile`），登入狀態預期可跨程式重啟保留，使用者不需每次重新登入。
- 登入狀態一律即時查詢該持久化 profile 目前是否存在 Google 登入 session cookie（domain 為 google.com、cookie 名稱包含 `SID`／`LSID` 等關鍵字，不得限縮成少數幾個固定 cookie 名稱的白名單），不得依賴另外匯出到磁碟的 cookie 檔案作為登入狀態的唯一依據。
- 登入 session 過期或失效時，由使用者手動重新點擊「登入 Google 帳號」更新登入狀態；程式不主動明碼保存帳號密碼，也不需要另外匯出、保存 cookie 檔案。

## 4. 佇列與下載流程

- 佇列模式固定為「使用者每次貼上單一連結後加入佇列，程式依加入順序逐一下載」；不提供批次貼上多連結一次匯入的介面。
- 佇列處理為循序（同一時間僅下載一項），可由使用者中途要求停止；停止只影響尚未開始的項目，進行中項目視為取消。

## 5. 第三方工具依賴

- `ffmpeg.exe`（僅用於將分開下載的視訊／音訊合併為單一 mp4）由 CI 在 Windows runner 上下載並與 `GDriveDownloader.exe` 一起放進正式發行 ZIP，使用者不需另外等待程式首次執行時才下載；不隨 Git 提交，也不內嵌於 `GDriveDownloader.exe` 本身（維持獨立 exe，方便單獨更新）。
- 程式啟動時優先使用與 `GDriveDownloader.exe` 同層的 `ffmpeg.exe`；找不到時才退回本機 `%LOCALAPPDATA%\GDriveDownloader\tools` 執行期下載作為保底（例如開發時直接執行未封裝的 build），不得反過來把执行期下載當成正式發行的主要取得方式。
- 畫質偵測與實際媒體下載不依賴任何第三方下載工具（不使用 `yt-dlp` 或其他外部下載器），一律透過 WebView2 自身的 Chrome DevTools Protocol 完成，見第 2 節。
- 上述 CI 下載或執行期保底下載的第三方工具二進位檔，不屬於根 `REPOSITORY_RULES.md` 第 5.1 節「Binary asset source integrity SOP」規範對象；該節僅規範提交進 Git、影響產品輸出的專案自有 binary source。
- 若上游下載來源失效，屬已知風險並應於下次維護時檢視，不視為需要把第三方工具二進位檔改為提交進 Git 的理由。

## 6. Runtime 資料與隱私

- WebView2 profile 目錄、下載佇列狀態與任何登入相關資料屬使用者 runtime 個資，一律保存於本機 `%LOCALAPPDATA%\GDriveDownloader`，不得提交至 Git、記錄於 log 範例或作為測試 fixture。
- 下載完成的影片檔案預設輸出至使用者本機資料夾（預設 `Downloads\GDriveDownloader`），不隨程式或 Git 一併保存。

## 7. 視覺風格

- UI 採「Modern Business Desktop」風格：乾淨、低裝飾、原生控制項優先、文字優先於圖示。
- 主要字體 `Microsoft JhengHei UI`，找不到時由 Windows GDI+ 自動 fallback 到系統預設字型，不額外處理例外。
- 主題色為 Teal（`#0D9488` 系列），Danger 另用獨立紅色（`#B43737` 系列），不與主題色混用。
- 本專案的視覺樣式（`Theme.cs`）為獨立設計、獨立實作，僅在風格方向上參考「Modern Business Desktop、Teal 主題」等一般性設計語彙；不引用、不複製任何其他 repository 的原始碼、色票常數或品牌識別。

## 8. 平台範圍

- 本專案僅支援 Windows 10/11 x64；不建立 macOS、Linux 或跨平台版本。
