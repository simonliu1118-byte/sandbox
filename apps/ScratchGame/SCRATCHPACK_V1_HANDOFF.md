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
V0.5.5 Build 3
branch: scratchgame/feature-scratchpack-v1-runtime
previous verified source: 2157ac19d63b6d7da94154a468a06d1d38de7123
previous Windows CI: Run #196 PASS
current Build 3: settings-list acceptance repair, awaiting final Windows CI / user verification
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

目前 canonical fixture（自 V0.5.3 Build 1 packaging baseline 起未變）：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

目前固定要求：

- 開發 / 測試 portable **必須包含 TestPacks**。
- TestPack 納入 `runtime-assets.json` `testPacks` 區段與 size / SHA-256 integrity verification。
- TestPack 缺失、hash 不符都必須 FAIL；正確檔案必須進輸出 ZIP。
- 未宣告 TestPack 不得因來源 ZIP / folder 中存在就被繼承。
- 一直保留到 V1.0.0 正式驗收完成。
- 到 V1.0.0 release gate 再主動提醒使用者，確認後才移除；不得提前移除。

ThreeStar-Test 已接受的 Canvas 1 / GameType 1 geometry：

```text
zone size: 191 × 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

## Portable / automation 關係

目前已完成並納入 Windows CI：

1. ScratchGame / PackEditor Windows x64 build。
2. deterministic ThreeStar-Test builder。
3. complete Portable packager + manifest size / SHA-256 + post-ZIP verification。
4. ScratchGame domain regression：Pack load / Import / finite pool / buy / pending / swap / scratch / redeem / Wallet / stats。
5. PackEditor → `.scratchpack` → ScratchGame Loader / Importer round-trip regression。
6. generated Windows icon / associated shell icon verification。
7. ScratchGame / PackEditor startup smoke。
8. complete Portable Artifact 上傳。

Run #196 已全部 PASS。Build 3 只延續設定清單 UI 實機返修，不改 ScratchPack schema / GameType 契約；完成 final Windows CI 後再更新此處驗證基準。

下一個獨立功能項目：V0.5.5 UI 驗收完成後，依 `GAMETYPE_SPEC.md` 進入 GameType 3；基本 GameType 全部完成後再回 PackEditor 正式收尾。