# GDriveDownloader Project Rules

本檔只保存 GDriveDownloader 的固定專案規則；repository 共通版本、開發、CI、Local-first 與 GitHub-final verification 規則依根 `REPOSITORY_RULES.md`。

## 1. 產品、平台與版本線

- 正式技術線：C# / .NET 8 / WinForms + WebView2；目標平台 Windows x64。
- 正式發行以 self-contained portable folder 為目標，不要求使用者另外安裝 .NET runtime。
- `GDriveDownloader.exe` 是本專案唯一產品識別，不建立第二個平行專案或另一條版號。
- 本專案為個人用途的下載自動化工具，不代表任何公司或組織。

## 2. 畫質策略

- 下載畫質策略固定為「自動選擇可用最高畫質」：解析交由 `yt-dlp` 處理（`-f "bv*+ba/b"`，`ffmpeg` 可用時合併為 mp4）。
- `ffmpeg` 若無法取得，退回不需合併的最高可用單一串流（`-f "b"`），不得為了合併失敗而讓下載整體失敗。
- 不得自行重寫 Google Drive 影片畫質清單解析邏輯；解析權威固定交給 `yt-dlp`，避免重蹈舊版「無法正確辨識畫質」的問題。

## 3. Google 登入與私人影片授權

- 私人影片授權採「內嵌 WebView2 開啟獨立登入視窗」模式，不使用讀取使用者主要瀏覽器（Chrome/Edge 等）既有 cookie 的方式。
- WebView2 使用固定的持久化 profile 目錄（本機 `%LOCALAPPDATA%\GDriveDownloader\WebView2Profile`），登入狀態預期可跨程式重啟保留，使用者不需每次重新登入。
- 使用者完成登入後，程式將登入視窗當下的 Google 相關 cookie 匯出為 Netscape 格式 `cookies.txt`，供 `yt-dlp` 於公開與私人影片下載時一併使用；公開影片下載不因附帶 cookies 而失敗。
- 登入 session 過期或失效時，由使用者手動重新點擊「登入 Google 帳號」更新 `cookies.txt`；程式不主動明碼保存帳號密碼。

## 4. 佇列與下載流程

- 佇列模式固定為「使用者每次貼上單一連結後加入佇列，程式依加入順序逐一下載」；不提供批次貼上多連結一次匯入的介面。
- 佇列處理為循序（同一時間僅下載一項），可由使用者中途要求停止；停止只影響尚未開始的項目，進行中項目視為取消。

## 5. 第三方工具依賴

- `yt-dlp.exe` 與 `ffmpeg.exe` 屬執行期依賴，於程式第一次需要時自動下載至本機 `%LOCALAPPDATA%\GDriveDownloader\tools`，不隨 Git 提交，也不內嵌於本專案 build 產物中。
- 上述執行期自動下載的第三方工具二進位檔，不屬於根 `REPOSITORY_RULES.md` 第 5.1 節「Binary asset source integrity SOP」規範對象；該節僅規範提交進 Git、影響產品輸出的專案自有 binary source。
- 若上游下載來源失效，屬已知風險並應於下次維護時檢視，不視為需要把第三方工具二進位檔改為提交進 Git 的理由。

## 6. Runtime 資料與隱私

- `cookies.txt`、WebView2 profile 目錄、下載佇列狀態與任何登入相關資料屬使用者 runtime 個資，一律保存於本機 `%LOCALAPPDATA%\GDriveDownloader`，不得提交至 Git、記錄於 log 範例或作為測試 fixture。
- 下載完成的影片檔案預設輸出至使用者本機資料夾（預設 `Downloads\GDriveDownloader`），不隨程式或 Git 一併保存。

## 7. 平台範圍

- 本專案僅支援 Windows 10/11 x64；不建立 macOS、Linux 或跨平台版本。
