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

## 目前版本線

```text
V0.5.5 Build 4
branch: scratchgame/feature-scratchpack-v1-runtime
previous verified source: 7713013c74a7c2798137b304aa4e95f7513211fe
previous Windows CI: Run #197 PASS
current Build 4: settings alignment + centered info modal acceptance repair, awaiting final Windows CI / user verification
```

## ScratchPack V1 runtime

- V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 schema parse / validation 單一 owner。
- Importer、runtime、PackEditor round-trip 共用正式 loader / validator。
- Built-in / Imported Pack 格式相同；差別只在 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 Pack update / replace。
- Built-in ticket ResourceRef 依 GameType namespace；foil 使用共用 namespace。
- GameType 1 / 2 runtime 已完成；GameType 3～6 契約已定，實作進度依 `TODO.md`。
- `scratch.zones` 是玩法 geometry / ScratchSurface 的權威來源；GameType-specific mapping 依 `GAMETYPE_SPEC.md`。
- price / serial 使用 `priceDisplayArea` / `serialDisplayArea`。
- thumbnail 是 runtime cache，可重建，不屬 ScratchPack schema。

## PackEditor lifecycle

PackEditor 是 ScratchGame 附屬 EXE，與 ScratchGame 共用 VERSION / BUILD。

固定規則：

- create-only。
- 不開啟 / 修改 / 覆寫 / 另存既有 `.scratchpack`。
- 新 draft 使用新的 UUID v4 packageId。
- 不建立 `.scratchproject`。
- 輸出前建立真實 `.scratchpack`，再以 `ScratchPackV1Loader` round-trip 驗證。

目前四步驟 UI：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

PackEditor 正式收尾依使用者決定延後：**基本 GameType 全部完善後，再回來集中完成正式 UI / preview / validation UX。**

## ThreeStar-Test

`ThreeStar-Test` 是 reference / test Pack，不是正式 Built-in Pack，但使用正式 ScratchPack schema / loader / GameType pipeline。

Repository 保存 reference source：

```text
reference-packs/ThreeStar-Test/manifest.json
reference-packs/ThreeStar-Test/ticket.json
```

`tools/build_reference_testpack.py` deterministic 產生 portable 所需：

```text
TestPacks/ThreeStar-Test.scratchpack
```

目前 canonical fixture：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

`TestPacks/` 必須保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。

## 目前接續點

- GameType 1：完成。
- GameType 2：完成。
- V0.5.5 Build 4：設定頁 UI 驗收返修中。
- V0.5.5 UI 驗收完成後再進 GameType 3。
- GameType 3～6 完成後，再回 PackEditor 做正式 UI / preview / validation UX 收尾。
