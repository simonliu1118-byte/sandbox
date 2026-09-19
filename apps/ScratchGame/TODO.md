# ScratchGame TODO / Roadmap

更新日期：2026/09/20

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前正式基準

- 正式基準：`main`。
- 最新正式版本：**V0.5.5 / Build 5**。
- 正式 Tag：`ScratchGame-v0.5.5-build5`。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成，Run #192 PASS。
- V0.5.5 Build 0～5：UI 驗收線完成；Build 5 Run #200 PASS，使用者實機驗收 PASS，正式 Release 完成。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 由 CI 組完整 Portable。
- AITeam Common Rules 2.5.0 / sandbox Governance 1.2.1 已同步。

正式 Portable：

```text
ScratchGame-V0.5.5-Build5-win-x64.zip
SHA-256: eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459
```

## 固定 TestPacks 政策

自 V0.5.6 Build 1 起，開發／測試 Portable 固定包含：

- `ThreeStar-Test.scratchpack`（GameType 1）
- `GameType2-Test.scratchpack`（GameType 2）
- `GameType3-Test.scratchpack`（GameType 3）

三個 TestPack 由 CI deterministic 重建並以 `runtime-assets.json` 驗證固定 size / SHA-256，之後各版本持續沿用做實機回歸。

**正式 GitHub Release 的下載 ZIP 一律不包含 `TestPacks/`。** TestPacks 保留在 repository / CI 開發流程，不隨正式 Release 發給一般使用者。詳細流程以 `RUNTIME_PACKAGE.md` 為準。

## 已完成 — GameType 1 / GameType 2

### GameType 1

- finite pool / purchase / Pending / swap gate / scratch / redeem / Wallet / statistics regression 已完成。
- Imported / BuiltIn installation 與 PackEditor round-trip 已納入 regression。

### GameType 2「中獎號碼」

- `scratch.zones` 前 `winningNumberCount` 格 = 中獎號碼；後 `playNumberCount` 格 = 你的號碼。
- `allowPrizeAmountRepeat = true | false`。
- `payoutSource=play|winning`。
- 未中獎票固定 0 命中；正獎命中格獎金總和精確等於 Prize Tier。
- Loader / Model / Validator / Generator / Renderer / Runtime / Importer 全部完成。
- Windows CI Run #192：Type 2 + 原 GameType 1 regression、兩個 EXE build / smoke、完整 Portable 全部 PASS。

## V0.5.5 UI 驗收線 — 已完成

- Build 0：彩券小舖卡片重新排版；Run #193 PASS。
- Build 1：UserDialog 初次 layout、結果 resume pill 下移、PackEditor ICON master 修正；Run #194 PASS。
- Build 2：結果 transition target 與設定資訊 Popup；Run #196 PASS。
- Build 3：設定清單操作欄合併、資料列化與文件校正；Run #197 PASS。
- Build 4：ScrollBar / 對齊 / 中央資訊 Modal；Run #199 PASS。
- Build 5：表頭圓角、錢包標示、簡易玩家刪除；Run #200 PASS；2026/09/18 使用者實機驗收 PASS；正式 Release 完成。

## 目前開發：GameType 3

基本 GameType 原則：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：完成。
- GameType 2：完成。
- GameType 3：V0.5.6 開發中；Generator / Validator / Renderer / Runtime 已完成，Windows CI Run #203 PASS，等待使用者實機驗收與後續修正。
- GameType 4～6：規格已定，尚待逐一實作。

V0.5.6 Build 1 另固定開發 TestPacks：GameType 1 / 2 / 3 三包均放入開發 Artifact 的 `TestPacks/`，正式 Release 排除。

## PackEditor 正式收尾 — 延後

基本 GameType 全部完善後，再集中完成所有 GameType 的完整編輯 UI、preview、geometry 操作、錯誤導向、正式 UX 與最終 round-trip 使用者驗收。

## 後續產品階段

- GameType 3 實機驗收。
- GameType 4～6。
- PackEditor 正式收尾。
- Decoration Shop / cosmetic 收藏與裝備。
- 中獎 / 大獎 / 頭獎演出、音效 slot、角色與整體遊戲體驗。
- V0.9.x stabilization。
- V1.0.0 Feature Freeze → 使用者正式驗收 → 發布。

## V1.0.0 Release Gate

至少要求：ScratchPack V1 runtime、預定基本 GameType、PackEditor、完整 portable 重建、automated round-trip regression、database migration / backup、Feature Freeze 後實機驗收全部通過。

正式 Release 一律依 `RUNTIME_PACKAGE.md` 排除 `TestPacks/`；不再等到 V1.0.0 才決定是否移除。
