# MediaMerge Project Rules

- 專案正式名稱：`MediaMerge`。
- 本專案是個人／sandbox 工具，不屬於 CY 系列程式；程式名稱、UI、檔名、路徑與版本資訊不得使用 `CY`、`CYMediaMerge` 或 Chihyuan 公司品牌識別。
- Windows 發行檔固定命名為 `MediaMerge.exe`。
- 專案路徑固定為 `apps/MediaMerge/`。
- 發行目標為 Windows x64 單檔 EXE；FFmpeg 採 LGPL build，於 CI 建置時下載並嵌入，不將 `ffmpeg.exe` 提交至 Git。
- 應用程式 icon 的唯一來源為 `assets/MediaMerge.svg`；CI 由此產生多尺寸 ICO 並嵌入最終 EXE，不提交衍生 ICO。
- 視訊預設採 stream copy，不重新壓縮；所選音訊取代原視訊音軌。若音訊無法直接封裝，才自動轉為容器相容格式。
- 輸出檔預設放在來源視訊同一資料夾；不得覆蓋既有檔案，同名時自動加流水尾碼。
