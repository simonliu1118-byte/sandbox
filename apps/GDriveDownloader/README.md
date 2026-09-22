# GDriveDownloader

個人用 Google Drive 影片下載自動化工具，取代原本手動開瀏覽器、確認畫質、按下載的流程。

## 功能

- 貼上 Google Drive 影片分享連結，加入佇列，程式依序自動下載。
- 用 WebView2 實際播放影片、透過 Chrome DevTools Protocol 監聽播放器真正產生的 `videoplayback` 請求分類音訊／視訊與畫質，自動選最高可用畫質；找到多個同畫質來源時，速度過慢會自動改試下一個。
- 支援公開分享的影片，也支援使用者本人有權限檢視的私人影片：透過內嵌瀏覽器視窗登入 Google 一次，之後不需重複登入；實際下載沿用同一個已登入的瀏覽器 session，不透過複製 cookie 給外部工具的方式（會被 Google 判定非瀏覽器連線而 403）。

## 使用方式

1. 開啟 `GDriveDownloader.exe`。
2. 若要下載私人影片，先按「登入 Google 帳號」，在跳出的視窗完成 Google 登入，再按「完成登入」。
3. 選擇下載資料夾（預設為 `Downloads\GDriveDownloader`）。
4. 貼上單一 Google Drive 影片連結，按「加入佇列」；可重複貼上多個連結，逐一加入。
5. 按「開始下載佇列」，程式會依加入順序逐一自動下載：先開一個分析視窗實際播放影片並偵測畫質，再背景下載視訊／音訊並合併。

首次執行時，程式會自動下載 `ffmpeg.exe`（用於合併視訊與音訊）至本機 `%LOCALAPPDATA%\GDriveDownloader\tools`，需要網路連線。

## 系統需求

- Windows 10/11 x64。
- Microsoft Edge WebView2 Runtime（新版 Windows 通常已內建）。
- 首次執行需要網路連線以下載 `ffmpeg`。

## 開發

- 技術線：C# / .NET 8 / WinForms + WebView2（透過 Chrome DevTools Protocol 分析畫質與下載）。
- Source：`src/GDriveDownloader/`。
- `legacy/`：舊版（Bun + 受控 Chrome/Edge CDP）實作快照，僅供追溯，不是目前技術線。
- 詳細專案規則見 `PROJECT_RULES.md`；共通版本與 CI 規則見根目錄 `REPOSITORY_RULES.md`。
