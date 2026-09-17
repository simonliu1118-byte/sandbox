# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.5 / Build 2**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 已完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」核心、Renderer、Runtime 與 regression 已完成，Run #192 全部 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版完成，Run #193 全部 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果按鈕位置、PackEditor ICON master 修正完成，Run #194 全部 PASS。
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

- 彩券小舖卡片重新排版；`TicketCardItem` 外框仍固定 182×252。
- 不修改 NewTicketDialog 視窗尺寸或主畫面 Header / Stage / Footer 邊界。
- Run #193 PASS。

### Build 1

- UserDialog 第一次開啟主動完成 selected item Measure / Arrange / UpdateLayout。
- 「顯示中獎結果」resume pill 下移貼近 Stage / Footer 金線，Stage ownership 不變。
- PackEditor ICON 回復核可 256×256、19093 bytes master。
- Run #194 PASS。

### Build 2 — 本批

1. **中獎結果暫時隱藏動畫位置同步**
   - Build 1 下移 resume pill 後，transition chip 仍用舊固定 18px 位置，因此動畫終點與按鈕脫鉤。
   - `GetResultTransitionTarget()` 改為直接使用 `ResultResumeButton.Margin.Bottom` 計算終點。
   - 之後若只調整 resume pill bottom margin，隱藏／恢復動畫會自動跟隨。
   - 不改 Header / Stage / Footer row size 或整體 layout boundary。

2. **設定選單彩券清單重整**
   - 修正表頭色塊左右不齊：表頭背景改為完整填滿清單外框，欄位內容再依 scrollbar / row 內距對齊。
   - 移除三角形與 accordion 列內展開，避免彩券數增加時整頁高度反覆被展開內容撐動。
   - 主清單改為固定高度、圓角且有 hover 的 compact row card。
   - 最右欄改成「資訊」按鈕。
   - 按「資訊」載入該彩券 detail，於按鈕旁開小型資訊 Popup：顯示來源、玩法、批次、面額、中獎率、剩餘、發行量／本數與獎池分配。
   - Imported Pack 的「解除安裝 Pack」移入資訊卡；Built-in Pack 保持不可解除安裝，只能隱藏。
   - 匯入 ScratchPack 後不再自動展開清單列。

本批完成條件：

1. VERSION 維持 `0.5.5`，BUILD `2`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression 不退化。
4. ScratchGame / PackEditor startup smoke PASS。
5. 完整 V0.5.5 Build 2 Portable 成功產生。
6. 使用者實機確認動畫落點、設定清單表頭／列視覺與資訊卡操作。

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
