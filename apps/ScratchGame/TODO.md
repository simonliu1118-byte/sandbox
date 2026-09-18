# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前正式基準

- 正式基準：`main`。
- 最新正式版本：**V0.5.5 / Build 5**。
- 正式 Tag：`ScratchGame-v0.5.5-build5`。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成，Run #192 PASS。
- V0.5.5 Build 0～5：UI 驗收線完成；Build 5 Run #200 PASS，使用者實機驗收 PASS，正式 Release 已發布。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 由 CI 組完整 Portable。
- AITeam Common Rules 2.5.0 / sandbox Governance 1.2.1 已同步。

正式 Portable：

```text
ScratchGame-V0.5.5-Build5-win-x64.zip
SHA-256: eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459
```

**TestPacks 必須保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 必須主動提醒使用者，再由使用者決定是否移除；不可提前移除。**

## 已完成 — GameType 1 / GameType 2

### GameType 1

- finite pool / purchase / Pending / swap gate / scratch / redeem / Wallet / statistics regression 已完成。
- Imported / BuiltIn installation 與 PackEditor round-trip 已納入 regression。

### GameType 2「中獎號碼」

- Loader / Model / Validator / Generator / Renderer / Runtime / Importer 全部完成。
- Windows CI Run #192：Type 2 + 原 GameType 1 regression、兩個 EXE build / smoke、完整 Portable 全部 PASS。

## V0.5.6 Build 0 — GameType 3「三個相同」

Active branch：`scratchgame/feature-gametype3`  
Draft PR：#9

GameType 核心契約仍只以 `GAMETYPE_SPEC.md` 為權威，本段只記錄實作狀態。

已完成：

- V0.5.6 / Build 0 開發線建立。
- Type 3 Model / Loader / Validator / Generator。
- `zoneCount`、custom decoy amounts 與 Near Miss 進階設定支援。
- Near Miss 預設 75% / 1 組 Pair；只影響盤面生成，不改 Prize Tier / 中獎率 / 兌獎結果。
- PackEditor 與主程式共用 authoritative Type 3 validation source。
- 第一階段 Windows CI Run #202：build / regression / startup smoke / complete Portable 全部 PASS。
- Type 3 RenderModel 與玩家端 Runtime renderer 已接入目前 Draft PR；每格只顯示固定格式金額，不加入專屬中獎高亮。
- GameType 3 regression 已擴充到 Generator → RenderModel 對應驗證。

目前驗收 gate：

- Draft PR #9 的最新 Windows CI 必須維持全部 PASS。
- CI PASS 後提供 V0.5.6 Build 0 Portable + Type 3 測試 ScratchPack 做實機刮除／Near Miss 驗收。
- 使用者實機驗收前不 merge、不建立正式 Release / Tag。

## PackEditor 正式收尾 — 延後

基本 GameType 全部完善後，再集中完成所有 GameType 的完整編輯 UI、preview、geometry 操作、錯誤導向、正式 UX 與最終 round-trip 使用者驗收。

## 後續產品階段

- 完成 GameType 3 實機驗收。
- GameType 4～6。
- PackEditor 正式收尾。
- Decoration Shop / cosmetic 收藏與裝備。
- 中獎 / 大獎 / 頭獎演出、音效 slot、角色與整體遊戲體驗。
- V0.9.x stabilization。
- V1.0.0 Feature Freeze → 使用者正式驗收 → 發布。

## V1.0.0 Release Gate

至少要求：ScratchPack V1 runtime、預定基本 GameType、PackEditor、完整 portable 重建、automated round-trip regression、database migration / backup、Feature Freeze 後實機驗收全部通過。

**最後主動提醒並由使用者確認是否移除開發用 `TestPacks/`；不得自行提前刪除。**
