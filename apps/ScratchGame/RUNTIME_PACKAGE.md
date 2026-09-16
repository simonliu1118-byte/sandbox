# ScratchGame portable runtime package

ScratchGame 的 Theme、彩券美術、音效與 Built-in assets 採 **EXE 外部資源**。`ScratchGame.exe` 與 `PackEditor.exe` 必須位於同一個 portable 根目錄並共用同一組外部資源。

## 正式 portable 結構

```text
ScratchGame/
├─ ScratchGame.exe
├─ PackEditor.exe
├─ Themes/
├─ BuiltInAssets/
│  ├─ Tickets/
│  └─ Foils/
├─ BuiltInPacks/        # 有正式 Built-in Pack 時才存在
├─ UI/
├─ Audio/
├─ Tickets/             # pre-V0.4 legacy，相容檔存在時才攜帶
└─ PACKAGE_CONTENTS.txt
```

`TestPacks/`、`TEST_STEPS.txt`、舊版 EXE 或其他測試檔 **不得**因為拿舊完整包當 asset source 而被帶入正式 portable。

## Runtime assets 的保存方式

外部美術／音效 **不作為 repository binary source**。Repository 只保存：

- `tools/package_portable.py`：正式打包工具。
- `runtime-assets.json`：目前可打包 runtime assets 的精確 byte size / SHA-256 基準。

正式打包時可使用兩種 asset source：

```text
--assets <runtime asset 資料夾>
```

或：

```text
--assets-zip <上一個已驗收完整 portable ZIP / 專用 asset bundle ZIP>
```

`--assets-zip` 不是「拿舊包直接改 EXE」；packager 只會重新抽取 `runtime-assets.json` 宣告的白名單資源。來源 ZIP 裡的舊 `ScratchGame.exe`、舊 `PackEditor.exe`、`TestPacks/`、`TEST_STEPS.txt` 等都不會繼承。

## Integrity Gate

`runtime-assets.json` 對 required / optional legacy / Built-in Pack 記錄：

- relative path
- byte size
- SHA-256

正式 package 建立前，packager 必須逐檔核對 size + SHA-256。任一 required resource：

- 缺檔
- 空檔
- size 不符
- SHA-256 不符

都必須直接中止，不建立可交付 ZIP。

Optional legacy asset 若來源中不存在可以略過；若存在，仍必須通過 manifest 的 size + SHA-256。

## Package verification

`tools/package_portable.py` 現在同時要求：

```text
ScratchGame.exe
PackEditor.exe
```

輸出 ZIP 後會重新開啟 ZIP 並驗證：

1. 兩個 EXE 都存在於同一個 `ScratchGame/` 根目錄。
2. Required runtime assets 全部存在。
3. Packaged EXE / asset bytes 與輸入來源 SHA-256 一致。
4. `PACKAGE_CONTENTS.txt` 完整。
5. 沒有未宣告的額外檔案。
6. 不含 `TestPacks/`、`TEST_STEPS.txt` 或 build-only marker。

因此「ZIP 成功寫出」本身不代表成功；只有 post-package verification 通過才回傳成功。

## PACKAGE_CONTENTS.txt

正式輸出會記錄：

- VERSION / BUILD（呼叫端有提供時）
- runtime asset baseline
- asset source 類型
- `runtime-assets.json` SHA-256
- 實際封裝檔案清單

方便後續追查某一份 portable 到底使用哪一組資源。

## 必要 runtime assets

目前 required asset 清單及其精確 hash 以 `runtime-assets.json` 為準。類型至少包含：

- Default Header / Footer / Stage Theme
- GameType 1 Built-in ticket art
- Built-in foil art
- grant overlay
- small / big win audio
- wallet grant audio
- lose audio

正式 Built-in Pack 若加入發行，應新增至 `runtime-assets.json` 的 `builtInPacks`，不能只把任意 `.scratchpack` 丟進來源資料夾就自動進正式包。

## CI 邊界

目前 Windows CI 負責 build / publish / icon / startup smoke test，產出的 executable artifact **不是完整 portable package**，因外部 runtime binary assets 不保存在 repository。

V0.5.3 的正式 portable 必須使用 CI 產出的兩個新 EXE，再經 `package_portable.py` 與 `runtime-assets.json` 組裝／驗證。

下一階段的自動驗證工作再決定 runtime asset bundle 的長期儲存位置，以及是否讓 CI 可以直接取得 approved asset bundle 並產生最終 portable artifact；在那之前不得把 executable-only artifact 當成完整交付包。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 相容資源。若 asset source 內存在，packager 會依 `runtime-assets.json` 驗證後攜帶；它們不再是 ScratchPack ticket-definition pipeline 的權威資料。
