# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.5 / Build 5**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成，Run #192 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版完成，Run #193 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果按鈕位置、PackEditor ICON master 修正完成，Run #194 PASS。
- V0.5.5 Build 2：動畫 target 與設定資訊 Popup 完成，Run #196 PASS。
- V0.5.5 Build 3：設定清單操作欄合併、資料列化與文件同步完成，Run #197 PASS。
- V0.5.5 Build 4：設定清單右側對齊、中央資訊 Modal 完成，Run #199 PASS。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 由 CI 直接組完整 Portable。
- AITeam Common Rules 2.5.0 / sandbox Governance 1.2.1 已同步。

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
- Windows CI Run #192：Type 2 + 原 GameType 1 regression、兩個 EXE build / smoke、完整 Portable 全部 PASS。

## V0.5.5 UI 驗收線

### Build 0
- 彩券小舖卡片重新排版；Run #193 PASS。

### Build 1
- UserDialog 初次 layout、結果 resume pill 下移、PackEditor ICON master 修正；Run #194 PASS。

### Build 2
- `GetResultTransitionTarget()` 與 resume pill 位置同步。
- 設定清單移除 accordion，改成固定高度 row card + 資訊 Popup。
- Run #196 PASS。

### Build 3
- 「顯示 / 批次操作 / 資訊」合併為單一「操作」欄。
- 操作順序：資訊 / 發行下一批 / 隱藏。
- 清單改成平面資料列 + 底部分隔線，移除整列 hover / click 視覺。
- Run #197 PASS。

### Build 4

- ScrollBar 改為需要時才顯示的 overlay，移除固定深色色帶。
- 表頭 / 資料欄位右側安全距離統一，資料列分隔線延伸至最右側。
- 彩券資訊改為中央 Modal；背景遮罩、資訊置中、完整獎池、右上解除安裝、底部中央關閉。
- Run #199 PASS。

### Build 5 — 本批

1. **設定清單表頭圓角收邊**
   - 使用者實機確認 Build 4 後，表頭色塊仍會在外層圓角頂端露出方角。
   - 表頭背景改為與外層清單一致的上方圓角，避免色塊突出圓角外框。
   - 不改欄寬、操作按鈕或資料列結構。

2. **選擇玩家錢包標示**
   - UserDialog 玩家卡右側「錢包」由 11px 輔助字提升為 16px Bold。
   - 字色改為與 Footer 玩家名片主要文字接近的亮色。
   - Wallet 金額維持既有 20px 金色粗體。

3. **簡易刪除玩家**
   - 每張玩家卡右上角放小型紅色 `×` 刪除按鈕；先以功能清楚為主，後續再重新設計玩家管理介面。
   - 刪除前必須使用 ScratchGame 自訂 Confirm Modal 二次確認，並明確提示錢包與累積統計會永久刪除。
   - 目前正在使用的玩家不可直接刪除，必須先切換到其他玩家。
   - 至少保留一個玩家。
   - 有未完成 Pending Ticket 的玩家不可刪除。
   - 成功刪除後立即從 UserDialog 清單移除並回到目前玩家或第一位可用玩家。

本批完成條件：

1. VERSION 維持 `0.5.5`，BUILD `5`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression 不退化。
4. ScratchGame / PackEditor startup smoke PASS。
5. 完整 V0.5.5 Build 5 Portable 成功產生。
6. 使用者實機確認：
   - 設定清單表頭色塊不再突出上方圓角。
   - 選擇玩家中的「錢包」標示大小與辨識度符合預期。
   - 玩家卡右上刪除按鈕、確認流程與刪除限制正常。

## GameType 主線

基本 GameType 原則：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：完成。
- GameType 2：完成。
- GameType 3～6：規格已定，尚待逐一實作。

V0.5.5 UI 驗收完成後，下一個獨立開發項目依 `GAMETYPE_SPEC.md` 進入 **GameType 3**。

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
