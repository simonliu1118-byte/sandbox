# ScratchPack V1 交接索引

更新日期：2026/09/16

本檔只做目前狀態與交接入口，不重複 schema、Prize Tier、Built-in registry 或 GameType 核心規則。

## 權威文件

- ScratchPack 封裝 / schema / ResourceRef / Built-in registry：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 契約：`apps/ScratchGame/GAMETYPE_SPEC.md`
- 永久產品原則：`apps/ScratchGame/PROJECT_RULES.md`
- Roadmap / TODO：`apps/ScratchGame/TODO.md`

## 目前版本線

```text
V0.5.0
branch: scratchgame/feature-scratchpack-v1-runtime
PR: #7
```

V0.5.0 已進入 **PackEditor** 階段；PackEditor 是 ScratchGame 的附屬 EXE，共用同一份 VERSION / BUILD，不另設產品版號。

## ScratchPack V1 runtime 現況

- V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 schema 解析／驗證單一 owner；Importer、runtime 與 PackEditor 輸出驗證共用。
- Built-in / Imported Pack 使用相同 schema、ResourceRef、GameType 契約與 runtime pipeline。
- BuiltIn / Imported 只屬 runtime installation source，不是 Pack 欄位。
- Built-in ticket ResourceRef 依 GameType namespace；foil 使用全域 namespace。
- GameType 1 runtime 由 Pack game / zones / prizes / ResourceRef 驅動。
- `scratch.zones` 是 symbol / foil / ScratchSurface geometry 的共同來源。
- 程式面額／票號使用 `priceDisplayArea` / `serialDisplayArea`。
- runtime thumbnail cache 在 Pack 安裝後產生，可遺失重建，不屬 ScratchPack schema。

## packageId 與 create-only lifecycle

`manifest.packageId` 是 Pack 的永久唯一身分。

目前單機版規則：

- Importer 發現相同 packageId 已安裝時直接拒絕，不把它當 Pack update。
- Built-in Pack 若 packageId 不變但內容 hash 改變，直接拒絕並要求新 packageId。
- **PackEditor 只建立新 Pack，不開啟、不修改、不覆寫、不另存既有 `.scratchpack`。**
- 每次「新增 Pack」自動產生新的 UUID v4 packageId；一般使用者不手動指定 packageId。
- 若要不同內容，建立新的 Pack / packageId；0.x 不做 Pack update / replace framework。

這個設計避免不同電腦或不同檔案以同 packageId 表示不同內容，也避免匯入端需要處理 Pack 版本升級與 replacement semantics。

## Built-in Pack

Built-in Pack 與 Imported Pack 的 `.scratchpack` 內容沒有差別。

正式製作流程：

```text
PackEditor 新增 Pack
→ 產出新的 packageId
→ 輸出 .scratchpack
→ 使用者確認／測試
→ 最終檔原樣放入 BuiltInPacks/
→ ScratchGame 啟動由正式 loader / validator / installer 註冊
```

主程式不手寫第二套 Built-in ticket definition，也不在 Pack schema 增加 BuiltIn 欄位。

## PackEditor V0.5.0

位置：`apps/ScratchGame/src/PackEditor/`

目前已完成 GameType 1 第一個可測流程：

- 新增 Pack + UUID v4 packageId。
- 名稱／作者／面額／Canvas／GameType。
- Built-in ticket / foil 或自訂 PNG。
- Canvas 1 自訂票面精確 1080×882 驗證。
- 3×3 / 4×4 / 5×5 grid geometry，自動產生 row-major zones。
- Price Area / Serial Area。
- Prize Pool amount / count；合法線數由 GameType 1 規則衍生。
- 中獎率、平均獎金（期望值）、獎金回饋率等 derived preview。
- 輸出前以正式 `ScratchPackV1Loader` round-trip 驗證。

PackEditor 不保存 editor-only schema，也不建立 `.scratchproject`。

## ThreeStar-Test

ThreeStar-Test 是 reference / test Pack，不是正式 Built-in Pack。它與正式 Pack 使用同一 schema / loader / GameType pipeline。

已接受的 Canvas 1 / GameType 1 幾何：

```text
zone size: 191 × 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

## Thumbnail cache

ScratchPack V1 不保存 `thumbnail.png`。

主程式流程：

```text
Pack 安裝成功
→ 由正式 Pack/runtime definition 產生 360×294 thumbnail cache
→ 挑券頁直接讀 cache
→ cache 遺失／損壞時重建
```

PackEditor 的中央預覽直接依 draft 即時疊圖，不使用 runtime thumbnail cache。

## Windows 驗證狀態

- V0.5.0 CI Run #161：ScratchGame build / publish / smoke test **PASS**。
- V0.5.0 CI Run #161：PackEditor build / publish / smoke test **PASS**。
- CI artifact 同時產出 `ScratchGame.exe` 與 `PackEditor.exe`。

## 下一步

1. 實機驗 PackEditor：新增 → 編輯 → round-trip 驗證 → 輸出 → ScratchGame 匯入。
2. 移除 PackEditor 骨架中 disabled 的「開啟／儲存」按鈕，避免誤導成可修改舊 Pack。
3. 做 preview drag / resize、銀膜 clipping、動態符號示意與驗證頁細化。
4. 更新 portable packager，正式把 `PackEditor.exe` 納入 shared-resource package。
5. 建立 PackEditor → Pack → Importer regression。
