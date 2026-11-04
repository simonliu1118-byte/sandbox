# GDriveDownloader

個人用 Google Drive 影片下載自動化工具，取代原本手動開瀏覽器、確認畫質、按下載的流程。

## 功能

- 貼上 Google Drive 影片分享連結，加入佇列，程式依序自動下載。
- 自動選擇 `yt-dlp` 解析出的最高可用畫質（有 `ffmpeg` 時合併最佳影音，沒有時退回最高可用單一串流）。
- 支援公開分享的影片，也支援使用者本人有權限檢視的私人影片：透過內嵌瀏覽器視窗登入 Google 一次，之後不需重複登入。

## 使用方式

1. 開啟 `GDriveDownloader.exe`。
2. 若要下載私人影片，先按「登入 Google 帳號」，在跳出的視窗完成 Google 登入，再按「完成登入，儲存登入狀態」。
3. 選擇下載資料夾（預設為 `Downloads\GDriveDownloader`）。
4. 貼上單一 Google Drive 影片連結，按「加入佇列」；可重複貼上多個連結，逐一加入。
5. 按「開始下載佇列」，程式會依加入順序逐一自動下載。

首次執行時，程式會自動下載 `yt-dlp.exe` 與 `ffmpeg.exe` 至本機 `%LOCALAPPDATA%\GDriveDownloader\tools`，需要網路連線。

## 系統需求

- Windows 10/11 x64。
- Microsoft Edge WebView2 Runtime（新版 Windows 通常已內建）。
- 首次執行需要網路連線以下載 `yt-dlp` 與 `ffmpeg`。

## 開發

- 技術線：C# / .NET 8 / WinForms + WebView2。
- Source：`src/GDriveDownloader/`。
- 詳細專案規則見 `PROJECT_RULES.md`；共通版本與 CI 規則見根目錄 `REPOSITORY_RULES.md`。

## 歷史版本

舊版實作的可還原原始碼快照保存在 [`legacy/`](legacy/)。Legacy 內容僅供追溯，不屬於目前正式技術線。
