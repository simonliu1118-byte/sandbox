# ScratchGame RuntimeAssets / Live

本目錄保存 `runtime-assets.json` 正式列管、供 portable/runtime 使用的 canonical PNG / WAV 外部產品資源。

## 固定規則

- 資源必須保留 runtime 相對路徑，例如 `Themes/Default/Frame/header_bg.png`。
- 正式新增或替換素材時，同步更新 `apps/ScratchGame/runtime-assets.json` 的 size 與 SHA-256。
- Portable 組包必須以 manifest 驗證成功才算完成。
- 不得放 EXE、DLL、ZIP、7z、MSI、log、cache、使用者資料、完整測試包、發行包或 `.scratchpack`。
- 同一正式素材只保留一個 canonical runtime 路徑，不建立重複備份副本。

目前正式素材目錄應由 `runtime-assets.json` 決定；此 README 本身不屬 runtime payload，packager 不應複製到 Portable。
