# Google Drive 影片下載器 v1.4.2 可視除錯版

這是舊版 Windows x64 發行包中可還原的原始碼快照，從 `runtime_payload.tar.xz` 原樣取出，未納入 EXE、DLL、FFmpeg、Bun runtime、WebView runtime、封裝 ZIP、log 或使用者資料。

## 內容

| 檔案 | 用途 |
|---|---|
| `app.js` | Bun 後端與下載流程的已封裝 JavaScript 原始碼 |
| `ui.js` | 主介面行為 |
| `ui-worker.js` | UI worker |
| `ui.html` | 主介面結構 |
| `style.css` | 主介面樣式 |
| `使用說明.txt` | v1.4.2 發行版操作與行為說明 |
| `THIRD_PARTY_NOTICES.txt` | 第三方元件聲明 |

## 還原範圍

- 上述檔案與 v1.4.2 發行包內的 runtime payload 位元組一致。
- 原始的未封裝模組樹、build scripts、套件鎖定檔及 Windows launcher 原始碼並未包含於發行包，因此無法由該發行成品完整還原。
- 發行包 SHA-256：`b5e0b8705e2819f438c743d79ae06c27f386296c0ebb57aadef229accf3cdfc0`。

此快照只用於歷史追溯；目前正式程式碼位於 [`../../src/GDriveDownloader/`](../../src/GDriveDownloader/)。
