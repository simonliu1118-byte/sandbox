# ScratchPack V1 交接索引

更新日期：2026/09/17

本檔只做 ScratchPack / PackEditor 目前狀態索引，不重複 schema 或 GameType 核心規則。

## 權威文件

- ScratchPack schema / ResourceRef / Built-in registry：`SCRATCHPACK_SPEC.md`
- GameType 契約：`GAMETYPE_SPEC.md`
- 永久專案規則：`PROJECT_RULES.md`
- Roadmap：`TODO.md`
- 整體工作交接：`WORK_HANDOFF.md`
- Portable：`RUNTIME_PACKAGE.md`

## 目前版本線

```text
V0.5.3 Build 1
branch: scratchgame/feature-scratchpack-v1-runtime
source commit: 56722c0fe0c22a96af6a9976188168db98ee419a
Windows CI: Run #181 PASS
```

## ScratchPack V1 runtime

- V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 schema parse / validation 單一 owner。
- Importer、runtime、PackEditor round-trip 共用正式 loader / validator。
- Built-in / Imported Pack 格式相同；差別只在 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 Pack update / replace。
- Built-in ticket ResourceRef 依 GameType namespace；foil 使用共用 namespace。
- GameType 1 由 Pack game / zones / prizes / ResourceRef 驅動。
- `scratch.zones` 是 symbol / foil / ScratchSurface geometry 的共同來源。
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

PackEditor 正式收尾已依使用者決定延後：**先完成 automation、基本 GameType；基本 GameType 全部完善後再回來完成 PackEditor 正式 UI / preview / validation UX。**

## ThreeStar-Test

`ThreeStar-Test` 是目前 reference / test Pack，不是正式 Built-in Pack，但使用正式 ScratchPack schema / loader / GameType pipeline。

Repository 保存 reference source：

```text
reference-packs/ThreeStar-Test/manifest.json
reference-packs/ThreeStar-Test/ticket.json
```

`tools/build_reference_testpack.py` deterministic 產生 portable 所需：

```text
TestPacks/ThreeStar-Test.scratchpack
```

V0.5.3 Build 1 baseline：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

目前使用者明確要求：

- 開發 / 測試 portable **必須包含 TestPacks**。
- TestPack 已納入 `runtime-assets.json` `testPacks` 區段與 size / SHA-256 integrity verification。
- TestPack 缺失、hash 不符都必須 FAIL；正確檔案必須進輸出 ZIP。
- 未宣告 TestPack 不得因來源 ZIP / folder 中存在就被繼承。
- 一直保留到 V1.0.0 正式驗收完成。
- 到 V1.0.0 release gate 再主動提醒使用者，確認後才移除；不得提前移除。

Build 1 packager unit tests **7/7 PASS**；Windows CI **Run #181 PASS**。

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

V0.5.3 Build 1 已完成正式 packager 核心：兩個 EXE、runtime asset manifest、TestPack manifest、size / SHA verification、post-ZIP verification，以及 canonical TestPack builder。

下一步順序：

1. 建立完整 automated regression：Build → Portable → Pack load → Import → finite pool → buy → pending → scratch → redeem → Wallet / stats。
2. 加入 PackEditor → `.scratchpack` → ScratchGame Importer round-trip regression。
3. 完成 ICON binary SOP 共通治理。
4. 基本 GameType 逐一完成。
5. 最後再回 PackEditor 正式收尾。
