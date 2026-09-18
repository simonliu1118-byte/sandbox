# ScratchPack V1 交接索引

更新日期：2026/09/18

本檔只做 ScratchPack / PackEditor 目前狀態索引，不重複 schema 或 GameType 核心規則。永久規則以 `PROJECT_RULES.md` 為準；目前整體工作狀態以 `WORK_HANDOFF.md` / `TODO.md` 為準。

## 權威文件

- ScratchPack schema / ResourceRef / Built-in registry：`SCRATCHPACK_SPEC.md`
- GameType 契約：`GAMETYPE_SPEC.md`
- 永久專案規則：`PROJECT_RULES.md`
- Roadmap：`TODO.md`
- 整體工作交接：`WORK_HANDOFF.md`
- Portable：`RUNTIME_PACKAGE.md`

## 正式版本與目前開發線

正式版本：

```text
V0.5.5 Build 5
formal baseline: main
tag: ScratchGame-v0.5.5-build5
Windows CI: Run #200 PASS
user real-machine acceptance: PASS
```

目前開發：

```text
V0.5.6 Build 0
branch: scratchgame/feature-gametype3
draft PR: #9
work: GameType 3
phase 1 CI: Run #202 PASS
```

## ScratchPack V1 runtime

- V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 schema parse / validation 單一 owner。
- Importer、runtime、PackEditor round-trip 共用正式 loader / validator。
- Built-in / Imported Pack 格式相同；差別只在 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 Pack update / replace。
- Built-in ticket ResourceRef 依 GameType namespace；foil 使用共用 namespace。
- `scratch.zones` 是玩法 geometry / ScratchSurface 的權威來源；GameType-specific mapping 依 `GAMETYPE_SPEC.md`。
- price / serial 使用 `priceDisplayArea` / `serialDisplayArea`。
- thumbnail 是 runtime cache，可重建，不屬 ScratchPack schema。

GameType 實作狀態：

- GameType 1：完整 runtime 已完成。
- GameType 2：完整 runtime 已完成。
- GameType 3：V0.5.6 Build 0 開發中；Model / Loader / Validator / Generator 已由 Run #202 驗證，RenderModel / runtime renderer 已接入 Draft PR #9，等待最新 CI 與實機驗收。
- GameType 4～6：契約已定，尚未實作。

## PackEditor lifecycle

PackEditor 是 ScratchGame 附屬 EXE，與 ScratchGame 共用 VERSION / BUILD。

固定規則：create-only；不開啟／修改／覆寫／另存既有 `.scratchpack`；新 draft 使用新的 UUID v4 packageId；輸出前以正式 `ScratchPackV1Loader` round-trip 驗證。

GameType 3 的 authoritative validation 已由 `GameType3Rules` 提供給 ScratchGame / PackEditor 共用，但 PackEditor 的 Type 3 正式編輯 UI 仍依既定決策延後；基本 GameType 全部完善後再集中做正式 UI / preview / validation UX。

## TestPacks

目前 canonical `ThreeStar-Test`：

```text
TestPacks/ThreeStar-Test.scratchpack
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

GameType 3 實機驗收使用獨立測試 ScratchPack；它不是正式 Built-in Pack，也不改變 ScratchPack schema。正式 repository TestPack 納管若後續需要，再依 `runtime-assets.json` integrity gate 處理。

`TestPacks/` 必須保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。

## 目前接續點

PR #9 最新 Windows CI PASS 後，提供 V0.5.6 Build 0 Portable + GameType 3 測試 ScratchPack做實機刮除與 Near Miss 驗收。使用者驗收前不 merge / Release / Tag。
