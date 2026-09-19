# ScratchPack V1 交接索引

更新日期：2026/09/20

本檔只做 ScratchPack / PackEditor 目前狀態索引，不重複 schema 或 GameType 核心規則。永久規則以 `PROJECT_RULES.md` 為準；目前整體工作狀態以 `WORK_HANDOFF.md` / `TODO.md` 為準。

## 權威文件

- ScratchPack schema / ResourceRef / Built-in registry：`SCRATCHPACK_SPEC.md`
- GameType 契約：`GAMETYPE_SPEC.md`
- 永久專案規則：`PROJECT_RULES.md`
- Roadmap：`TODO.md`
- 整體工作交接：`WORK_HANDOFF.md`
- Portable / TestPack 發布政策：`RUNTIME_PACKAGE.md`

## 目前正式版本線

```text
V0.5.5 Build 5
formal baseline: main
tag: ScratchGame-v0.5.5-build5
acceptance source: 556aaa1152ed5939bd15b67aa581cf8d1029c2a9
Windows CI: Run #200 PASS
user real-machine acceptance: PASS
next development: GameType 3 / V0.5.6
```

Build 5 已結束 V0.5.5 UI 驗收線；GameType 3 使用新的 feature branch / Draft PR #9。

## ScratchPack V1 runtime

- V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 schema parse / validation 單一 owner。
- Importer、runtime、PackEditor round-trip 共用正式 loader / validator。
- Built-in / Imported Pack 格式相同；差別只在 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 Pack update / replace。
- Built-in ticket ResourceRef 依 GameType namespace；foil 使用共用 namespace。
- GameType 1 / 2 runtime 已完成。
- GameType 3 core / Renderer / Runtime 已進入 V0.5.6，Run #203 PASS，待實機驗收。
- GameType 4～6 契約已定，實作進度依 `TODO.md`。
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

PackEditor 正式收尾依使用者決定延後：**基本 GameType 全部完善後，再回來集中完成正式 UI / preview / validation UX。**

## 固定 TestPacks

開發／測試 Portable 固定使用三包：

```text
ThreeStar-Test.scratchpack   # GameType 1
GameType2-Test.scratchpack   # GameType 2
GameType3-Test.scratchpack   # GameType 3
```

- `ThreeStar-Test` 由 `reference-packs/ThreeStar-Test` + `tools/build_reference_testpack.py` deterministic 產生。
- Type 2 / Type 3 固定測試包由 `tools/build_fixed_testpacks.py` deterministic 產生。
- 三包均由 `runtime-assets.json` 固定 size / SHA-256。
- 開發 Artifact 的 `TestPacks/` 固定攜帶三包，供每版實機回歸。
- **正式 Release ZIP 不包含 `TestPacks/`；TestPack 只留在 repository / CI。**

## 目前接續點

- GameType 1：完成。
- GameType 2：完成。
- GameType 3：V0.5.6 開發／驗收中。
- GameType 4～6：待實作。
- GameType 3～6 完成後，再回 PackEditor 做正式 UI / preview / validation UX 收尾。
