# ScratchGame portable runtime package

更新日期：2026/09/17

ScratchGame 的 Theme、彩券美術、音效與 Built-in assets 採 EXE 外部資源。`ScratchGame.exe` 與 `PackEditor.exe` 必須位於同一 portable 根目錄並共用同一組外部資源。

## Canonical runtime assets

正式 PNG / WAV 素材直接保存在 repository：

```text
apps/ScratchGame/RuntimeAssets/Live/
├─ Themes/
├─ BuiltInAssets/
│  ├─ Tickets/
│  └─ Foils/
├─ UI/
└─ Audio/
```

固定規則：

- `RuntimeAssets/Live` 是目前正式上線素材的唯一 canonical source。
- `runtime-assets.json` 逐檔記錄 runtime relative path、byte size、SHA-256。
- 新增或替換正式素材時，必須同步更新 `runtime-assets.json`。
- `Live/README.md` 只說明管理規則，不屬 portable payload。
- CI / packager 只依 manifest whitelist 打包，不把未宣告檔案帶入成品。
- EXE、DLL、ZIP、MSI、log、cache、使用者資料與 `.scratchpack` 不作為 `Live` 素材提交。

目前 15 個 required production assets 已由 V0.5.2 Build 1 FULL Test Package 回復並再次核對，path / size / SHA-256 15/15 一致。

## 開發 / 測試階段 portable 結構

```text
ScratchGame/
├─ ScratchGame.exe
├─ PackEditor.exe
├─ Themes/
├─ BuiltInAssets/
│  ├─ Tickets/
│  └─ Foils/
├─ BuiltInPacks/        # 有正式 Built-in Pack 時
├─ TestPacks/           # V1.0.0 正式驗收完成前必須保留
├─ UI/
├─ Audio/
├─ Tickets/             # optional legacy asset 存在時
└─ PACKAGE_CONTENTS.txt
```

## TestPacks 規則

目前仍在持續開發 / 測試：

- portable package 必須包含目前核准的 `TestPacks/`。
- TestPack 必須列入 `runtime-assets.json`，以 path / byte size / SHA-256 驗證。
- 目前 required TestPack 為 `TestPacks/ThreeStar-Test.scratchpack`。
- **TestPacks 一直保留到 V1.0.0 正式驗收完成。**
- 到 V1.0.0 release gate 時，必須主動提醒使用者，再由使用者確認是否移除；不得提前自行刪除。

Repository 保存 `reference-packs/ThreeStar-Test/manifest.json` 與 `ticket.json` 作為 reference source，不把 `.scratchpack` binary 當 Git source 保存。

`tools/build_reference_testpack.py` 會 deterministic 建立：

```text
TestPacks/ThreeStar-Test.scratchpack
```

目前 canonical baseline：

```text
byte size: 800
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

## CI portable pipeline

Windows workflow 使用 repository 本身作為唯一 runtime asset source：

```text
checkout
→ 建立 canonical ThreeStar-Test.scratchpack
→ build / regression
→ publish ScratchGame.exe + PackEditor.exe
→ startup / shell icon smoke
→ 將 RuntimeAssets/Live 複製到 CI staging
→ 注入當次產生的 TestPacks/ThreeStar-Test.scratchpack
→ package_portable.py
→ manifest integrity gate
→ post-package verification
→ upload complete portable Artifact
```

CI 不需要 Dropbox、Google Drive、ChatGPT Library 或外部 asset bundle。

`RuntimeAssets/Live` 的變更、`runtime-assets.json` 的變更與 reference TestPack source 的變更都必須觸發 ScratchGame Windows workflow。

## Integrity Gate

Required runtime asset / required TestPack 發生下列任一情況必須 FAIL：

- 缺檔
- 空檔
- size 不符
- SHA-256 不符

Optional legacy asset 若不存在可略過；若存在仍必須驗證。

## Package verification

正式 packager 必須要求：

```text
ScratchGame.exe
PackEditor.exe
```

輸出 ZIP 後重新開啟驗證：

1. 兩個 EXE 位於同一 `ScratchGame/` 根目錄。
2. required runtime assets 全部存在。
3. 開發 / 測試階段 required TestPacks 全部存在。
4. packaged EXE / asset / TestPack bytes 與輸入來源一致。
5. `PACKAGE_CONTENTS.txt` 完整。
6. 不含未宣告的舊 EXE / build-only / accidental files。

只有 post-package verification 通過才算打包成功。

## PACKAGE_CONTENTS.txt

記錄：

- VERSION / BUILD
- runtime asset baseline
- asset source 類型
- asset manifest SHA-256
- 實際封裝檔案清單

## Artifact 與正式 Release

開發階段 GitHub Actions 上傳的是完整 Portable Artifact，保留期依 workflow 設定。

正式 GitHub Release 仍遵守 repository 治理：只有使用者明確授權後才建立正式 Release / Tag。Release 使用當下通過完整 CI 驗證的程式與 `RuntimeAssets/Live` 素材，不另維護第二套 release-only 素材來源。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 相容資源。來源存在時依 manifest 驗證後攜帶；它們不是 ScratchPack ticket-definition pipeline 的權威資料。
