# ScratchGame portable runtime package

更新日期：2026/09/20

ScratchGame 的 Theme、彩券美術、音效與 Built-in assets 採 EXE 外部資源。`ScratchGame.exe` 與 `PackEditor.exe` 必須位於同一 portable 根目錄並共用同一組外部資源。

## 最新正式 Portable

ScratchGame V0.5.5 Build 5：

```text
Tag: ScratchGame-v0.5.5-build5
Tag target: 0926e2c69c500340d38f33f09f390e0e7ce24b63
File: ScratchGame-V0.5.5-Build5-win-x64.zip
Size: 142755863 bytes
SHA-256: eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459
Acceptance CI: Run #200 PASS
User real-machine acceptance: PASS
Release asset verification: Run #7 PASS
```

GitHub Actions 原始 Artifact archive：

```text
Artifact: ScratchGame-V0.5.5-Build5-portable-win-x64
Artifact ID: 10519115830
Archive SHA-256: 4b96f75733e48c23b3e2e0657f683c9ee6eaa7a1dc4a97f1719f561a0f622e3a
```

正式 GitHub Release 掛載的是 Artifact 內已通過 post-package verification 的 `ScratchGame-V0.5.5-Build5-win-x64.zip`，不是 Actions 外屈 Artifact archive。

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
├─ TestPacks/           # 只存在開發／測試 Artifact
├─ UI/
├─ Audio/
├─ Tickets/             # optional legacy asset 存在時
└─ PACKAGE_CONTENTS.txt
```

## 固定 TestPacks

自 V0.5.6 Build 1 起，開發／測試 Portable 固定攜帶三個 TestPack：

```text
TestPacks/ThreeStar-Test.scratchpack
  GameType 1
  size: 800
  SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a

TestPacks/GameType2-Test.scratchpack
  GameType 2
  size: 2668
  SHA-256: 9705daecf5ac6bb57157a7d98a6df429cf05caa519620bcf93293a9e41723c6f

TestPacks/GameType3-Test.scratchpack
  GameType 3
  size: 2336
  SHA-256: 58a6cd148d6e5245e30718e70d614dd0b7b37d6453d7fe5dbf26de5f8851618f
```

用途：

- 每次開發 Windows CI 都重建並由 `runtime-assets.json` 驗證 size / SHA-256。
- 每次開發 Portable Artifact 都固定包含三個 TestPack，供實機回歸測試。
- `ThreeStar-Test` 由 `tools/build_reference_testpack.py` deterministic 建立。
- `GameType2-Test` / `GameType3-Test` 由 `tools/build_fixed_testpacks.py` deterministic 建立；packageId、票面、參數與輸出 hash 固定，不隨每次 build 改變。
- `.scratchpack` binary 不作為 Git source 提交；CI 由 canonical source / builder 產生。

### 正式 Release 排除規則

**正式 GitHub Release 的下載 ZIP 不得包含 `TestPacks/`。**

TestPack 是開發與驗收工具，不屬正式使用者 payload。正式發布時必須由已通過 Windows CI 的版本建立 release candidate，移除整個 `TestPacks/` 後重新產生正式 ZIP、重新計算 SHA-256，並在發布前驗證 ZIP 內不存在任何 `*/TestPacks/*` 或 `.scratchpack` 測試包。Repository 與開發 CI 中的固定 TestPack 仍保留，下一版繼續使用。

此規則取代先前「TestPacks 保留到 V1.0.0 再決定是否移除」的暫行政策。

## CI portable pipeline

Windows workflow 使用 repository 本身作為唯一 runtime asset source：

```text
checkout
→ 建立 canonical ThreeStar-Test.scratchpack
→ 建立固定 GameType2-Test / GameType3-Test.scratchpack
→ build / regression
→ publish ScratchGame.exe + PackEditor.exe
→ startup / shell icon smoke
→ 將 RuntimeAssets/Live 複製到 CI staging
→ 注入三個固定 TestPacks
→ package_portable.py
→ manifest integrity gate
→ post-package verification
→ upload complete development Portable Artifact
```

CI 不需要 Dropbox、Google Drive、ChatGPT Library 或外部 asset bundle。

`RuntimeAssets/Live`、`runtime-assets.json`、TestPack builder / reference source 的變更都必須觸發 ScratchGame Windows workflow。

## Integrity Gate

Required runtime asset / 開發階段 required TestPack 發生下列任一情況必須 FAIL：

- 缺檔
- 空檔
- size 不符
- SHA-256 不符

Optional legacy asset 若不存在可略過；若存在仍必須驗證。

## Package verification

開發 Portable packager 必須要求：

```text
ScratchGame.exe
PackEditor.exe
```

輸出 ZIP 後重新開啟驗證：

1. 兩個 EXE 位於同一 `ScratchGame/` 根目錄。
2. required runtime assets 全部存在。
3. 三個固定 TestPacks 全部存在。
4. packaged EXE / asset / TestPack bytes 與輸入來源一致。
5. `PACKAGE_CONTENTS.txt` 完整。
6. 不含未宣告的舊 EXE / build-only / accidental files。

只有 post-package verification 通過才算開發 Artifact 打包成功。

正式 Release 另加一道 gate：**`TestPacks/` 必須完全不存在。**

## PACKAGE_CONTENTS.txt

記錄：

- VERSION / BUILD
- runtime asset baseline
- asset source 類型
- asset manifest SHA-256
- 實際封裝檔案清單

## Artifact 與正式 Release

開發階段 GitHub Actions 上傳的是包含固定 TestPacks 的完整 Portable Artifact，保留期依 workflow 設定。

正式 GitHub Release 只有使用者明確授權後才建立正式 Release / Tag；正式 ZIP 不直接照搬開發 Artifact，必須依上面的 Release 排除規則移除 TestPacks、重新驗證並記錄新的 SHA-256。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 相容資源。來源存在時依 manifest 驗證後攜帶；它們不是 ScratchPack ticket-definition pipeline 的權威資料。
