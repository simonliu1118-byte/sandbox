# ScratchGame portable runtime package

更新日期：2026/09/17

ScratchGame 的 Theme、彩券美術、音效與 Built-in assets 採 EXE 外部資源。`ScratchGame.exe` 與 `PackEditor.exe` 必須位於同一 portable 根目錄並共用同一組外部資源。

## 開發 / 測試階段目標結構

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
├─ Tickets/             # pre-V0.4 legacy，相容檔存在時
└─ PACKAGE_CONTENTS.txt
```

## TestPacks 規則

目前仍在持續開發 / 測試，使用者已明確要求：

- portable package 必須包含目前核准的 `TestPacks/`。
- TestPack 不是任意全資料夾繼承；應列入 manifest，以 path / byte size / SHA-256 驗證。
- 至少目前的 `TestPacks/ThreeStar-Test.scratchpack` 必須隨測試 portable 提供。
- **TestPacks 一直保留到 V1.0.0 正式驗收完成。**
- 到 V1.0.0 release gate 時，必須主動提醒使用者，再由使用者確認是否移除；不得提前自行刪除。

### V0.5.3 Build 0 已知 gap

目前 HEAD `6612f88d7542d5b7558baca9cf8cc6894071dc87` 的 `package_portable.py` 仍錯誤地把 `TestPacks/` 視為 forbidden output。這是 Build 0 在使用者補充規格前形成的錯誤假設。

**下一步 V0.5.3 Build 1 必須修正 source / tests / manifest；本文件描述的是已確認的目標規格，不代表 Build 0 已符合。**

## Runtime assets 保存方式

外部美術 / 音效目前不作為 repository binary source。Repository 保存：

- `tools/package_portable.py`
- `runtime-assets.json`

打包可使用：

```text
--assets <runtime asset directory>
```

或：

```text
--assets-zip <approved portable / asset bundle ZIP>
```

使用 `--assets-zip` 時不得直接沿用舊 package 結構。Packager 只抽取 manifest 明確宣告的 runtime / legacy / built-in / test resources；舊 `ScratchGame.exe`、舊 `PackEditor.exe`、舊說明檔或其他未宣告檔案不得被繼承。

## Integrity Gate

`runtime-assets.json` 對可打包資源記錄：

- relative path
- byte size
- SHA-256

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

應記錄：

- VERSION / BUILD
- runtime asset baseline
- asset source 類型
- asset manifest SHA-256
- 實際封裝檔案清單

## V0.5.3 Build 0 已完成

- packager 同時接受 ScratchGame.exe / PackEditor.exe。
- `--assets` / `--assets-zip`。
- runtime asset size + SHA-256 gate。
- output ZIP structure / bytes read-back verification。
- packager unit tests。
- Windows CI Run #179 PASS。

## V0.5.3 Build 1 待做

- 把 `ThreeStar-Test.scratchpack` 納入 manifest / hash gate。
- 移除 TestPacks forbidden-output 邏輯。
- 新增 TestPack missing / hash mismatch / successful packaging tests。
- 用 approved full asset source 實際重建完整 portable。
- static / unit validation 後只跑一次 Windows CI。

## CI 邊界

Build 0 的 Windows CI 仍主要負責 build / publish / icon / startup smoke test。因 approved external asset bundle 尚未建立長期 CI 取得方式，executable-only artifact 仍不能稱為完整 portable package。

後續 automated regression 階段再完成 asset bundle 長期來源與 CI 最終 portable artifact，使完整 package 可以從固定來源重建，不再人工用舊包替換 EXE。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 相容資源。來源存在時依 manifest 驗證後攜帶；它們不是 ScratchPack ticket-definition pipeline 的權威資料。
