# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.5 / Build 0**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 已完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」核心、Renderer、Runtime 與 regression 已完成，Run #192 全部 PASS。
- V0.5.4 Build 2 Portable：`ScratchGame-V0.5.4-Build2-win-x64.zip`，SHA-256 `46513302980227b3d48e2c287830ea1666bf7ed83827c70419a36987eb85fb05`。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 由 CI 直接組完整 Portable。
- AITeam Common Rules 2.5.0 / sandbox Governance 1.2.1 已同步。
- V0.5.2 的 UI / 玩家 / 遊玩紀錄 / 刮獎效果與後續 UI 微調仍留待使用者集中實機驗收。

**TestPacks 必須保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 必須主動提醒使用者，再由使用者決定是否移除；不可提前移除。**

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
- `GameType2RenderModel` 直接使用同一份 `scratch.zones` geometry，不建立第二套座標。
- Windows CI Run #192：Type 2 + 原 GameType 1 regression、兩個 EXE build / smoke、完整 Portable 全部 PASS。

## V0.5.5 — 彩券小舖卡片排版微調

目前工作項目：只調整 `NewTicketDialog` 彩券卡片內部排版，**卡片外框仍固定 182×252，不改視窗、Header / Stage / Footer 或其他整體版面邊界**。

本批內容：

- 彩券縮圖移到卡片最上方，並由原 121px 增加為 132px 顯示區。
- 彩券名稱與面額移到縮圖下方。
- 名稱、面額、總中獎率採相同 nominal 字級；總中獎率使用較低調顏色。
- 「最高獎金」標題改成較小、低調文字。
- 最高獎金金額改成大字、亮桃紅醒目顯示。
- 移除卡片中的發行日期與批次顯示；資料本身沒有刪除，只是不在此 UI 呈現。
- 內容區 row 高度總和仍為 244px，不改卡片外框尺寸。

本批完成條件：

1. 正式工作 branch 只增加一個乾淨 V0.5.5 commit，不帶暫存 WIP commit 歷史。
2. ScratchGame / PackEditor / Regression build 不退化。
3. 原 GameType 1 / GameType 2 regression 全部 PASS。
4. ScratchGame / PackEditor startup smoke PASS。
5. 完整 V0.5.5 Portable 成功產生。
6. 卡片最終視覺仍需使用者實機確認；若只需視覺微調，依版本規則續用 V0.5.5 Build N。

## GameType 主線

基本 GameType 原則：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：完成。
- GameType 2：完成。
- GameType 3～6：規格已定，尚待逐一實作。

V0.5.5 卡片排版完成後，下一個獨立開發項目依 `GAMETYPE_SPEC.md` 進入 **GameType 3**。

## PackEditor 正式收尾 — 延後

基本 GameType 全部完善後，再集中完成所有 GameType 的完整編輯 UI、preview、geometry 操作、錯誤導向、正式 UX 與最終 round-trip 使用者驗收。

## 後續產品階段

- GameType 3～6。
- PackEditor 正式收尾。
- Decoration Shop / cosmetic 收藏與裝備。
- 中獎 / 大獎 / 頭獎演出、音效 slot、角色與整體遊戲體驗。
- V0.9.x stabilization。
- V1.0.0 Feature Freeze → 使用者正式驗收 → 發布。

## V1.0.0 Release Gate

至少要求：ScratchPack V1 runtime、預定基本 GameType、PackEditor、完整 portable 重建、automated round-trip regression、database migration / backup、Feature Freeze 後實機驗收全部通過。

**最後主動提醒並由使用者確認是否移除開發用 `TestPacks/`；不得自行提前刪除。**
