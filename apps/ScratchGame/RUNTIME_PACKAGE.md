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
- TestPack 不是任意全資料夾繼承；必須列入 `runtime-assets.json`，以 path / byte size / SHA-256 驗證。
- 目前 required TestPack 為 `TestPacks/ThreeStar-Test.scratchpack`。
- **TestPacks 一直保留到 V1.0.0 正式驗收完成。**
- 到 V1.0.0 release gate 時，必須主動提醒使用者，再由使用者確認是否移除；不得提前自行刪除。

V0.5.3 Build 1 已修正 Build 0 錯誤：`package_portable.py` 不再禁止 `TestPacks/`，而是只接受 manifest 明確宣告的 TestPack。未宣告的 TestPack 仍不會被繼承到輸出 ZIP。

## ThreeStar-Test canonical package

Repository 保存 `reference-packs/ThreeStar-Test/manifest.json` 與 `ticket.json` 作為 reference source；不把 `.scratchpack` binary 當成 Git binary release 檔保存。

`tools/build_reference_testpack.py` 會以固定 JSON canonicalization、固定 ZIP member order / timestamp / attributes / compression 建立可重現的：

```text
TestPacks/ThreeStar-Test.scratchpack
```

V0.5.3 Build 1 baseline：

```text
byte size: 800
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

`test_package_portable.py` 會重新從 reference source 建立 canonical TestPack，並核對上述 manifest baseline，避免 reference source 與 packaging manifest 靜默漂移。

## Runtime assets 保存方式

外部美術 / 音效目前不作為 repository binary source。Repository 保存：

- `tools/package_portable.py`
- `tools/build_reference_testpack.py`
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

## V0.5.3 Build 1 已完成

Build 1 source commit：`56722c0fe0c22a96af6a9976188168db98ee419a`

Windows CI：**Run #181 PASS**。

已完成：

- `runtime-assets.json` 新增 `testPacks` 區段。
- `ThreeStar-Test.scratchpack` 納入 path / byte size / SHA-256 integrity gate。
- directory source 與 ZIP source 都把 required TestPack 視為必要檔案。
- 移除 `verify_output()` 對整個 `TestPacks/` 的禁止條件；仍只允許 manifest whitelist 內容。
- 新增 deterministic reference TestPack builder。
- unit tests 覆蓋 TestPack 缺失、hash mismatch、正確輸出、ZIP-source round-trip 與 canonical baseline；**7/7 PASS**。
- Python static compile (`py_compile`) PASS。
- Windows build / publish / shell icon / startup smoke / artifact：Run #181 PASS。

## CI 邊界 / 下一階段

目前 Windows workflow 仍主要負責 build / publish / icon / startup smoke test。因 approved external asset bundle 尚未建立長期 CI 取得方式，executable-only artifact 仍不能稱為完整 portable package。

下一階段 automated regression 要完成 approved asset bundle 長期來源與 CI 最終 portable artifact，並建立：

`Build → Portable Package → ScratchPack load → Import → finite pool → buy → pending → scratch/result → redeem → Wallet/statistics`

同時加入 PackEditor → `.scratchpack` → ScratchGame Importer round-trip regression。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 相容資源。來源存在時依 manifest 驗證後攜帶；它們不是 ScratchPack ticket-definition pipeline 的權威資料。
