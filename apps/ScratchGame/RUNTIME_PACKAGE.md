# ScratchGame portable runtime package

ScratchGame 的 Theme、彩券美術、音效與 Built-in assets 採 **EXE 外部資源**。V0.5.0 起，`PackEditor.exe` 是 ScratchGame 的附屬程式，正式 portable 方向是與 `ScratchGame.exe` 放在同一資料夾，共用同一組外部資源。

## V0.5.0 目標結構

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
└─ PACKAGE_CONTENTS.txt
```

必要 runtime assets 目前至少包含：

```text
Themes/Default/Frame/header_bg.png
Themes/Default/Frame/footer_bg.png
Themes/Default/Stage/stage_bg.png
BuiltInAssets/Tickets/gameType1/01-red.png
BuiltInAssets/Tickets/gameType1/01-blue.png
BuiltInAssets/Tickets/gameType1/02.png
BuiltInAssets/Foils/brushed-silver-plain.png
BuiltInAssets/Foils/brushed-silver-three-star.png
UI/grant-overlay-01.png
Audio/small-win-manual.wav
Audio/small-win-auto.wav
Audio/big-win-manual.wav
Audio/big-win-auto.wav
Audio/wallet-grant.wav
Audio/lose.wav
```

## EXE 與資源責任

- `ScratchGame.exe` 與 `PackEditor.exe` 共用同一 ScratchGame VERSION / BUILD。
- PackEditor 不另設自己的 portable 根目錄或第二套 BuiltInAssets。
- Header / Footer / Stage Theme 不嵌死在 EXE。
- Built-in ticket / foil 由 `BuiltInAssets/` 提供；`.scratchpack` 可引用它們，但不需複製進每個 Pack。
- 正式 Built-in Pack 若存在，原樣放在 `BuiltInPacks/`。
- 缺少必要 runtime asset 時，正式 portable package 應直接失敗，不交付半套 ZIP。
- runtime 找不到必要資源或音效播放失敗時，應寫 `%LOCALAPPDATA%\ScratchGame\logs\runtime-assets.log`。

## CI / packaging 現況

V0.5.0 Windows CI 已同時 build / publish / smoke-test：

```text
ScratchGame.exe
PackEditor.exe
```

Run #161 已 PASS。

目前 `tools/package_portable.py` 仍是 V0.4 時期的單一 `ScratchGame.exe` packager；它已能驗證目前正式資源，但**尚未把 `PackEditor.exe` 納入參數、檢查與輸出**。這是 V0.5.0 待修工作，在正式 V0.5.0 portable 流程定稿前必須更新。

在 packager 更新前，測試包若人工組裝，仍必須確認兩個 EXE 位於同一資料夾且共享完整 runtime assets；不得把 CI 的 executable-only artifact 誤認成完整 portable package。

## Legacy assets

`Tickets/ThreeStar/*` 屬 pre-V0.4 local database 的 legacy 相容資源；若 package tool 找得到可選擇性攜帶，但它們不再是 V0.4+ ScratchPack ticket-definition pipeline 的權威資料。
