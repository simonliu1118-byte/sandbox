# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.5 / Build 4**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成，Run #192 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版完成，Run #193 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果按鈕位置、PackEditor ICON master 修正完成，Run #194 PASS。
- V0.5.5 Build 2：動畫 target 與設定資訊 Popup 完成，Run #196 PASS。
- V0.5.5 Build 3：設定清單操作欄合併、資料列化與文件同步完成，Run #197 PASS。
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

### Build 4 — 本批

1. **設定清單右側對齊**
   - ScrollBar 改為需要時才顯示，並以 overlay 方式放在清單右側，不再保留固定深色色塊 gutter。
   - 表頭背景維持完整填滿外框；表頭內容與資料欄位統一保留相同右側安全距離。
   - 資料列底部分隔線由整列 Border 負責，必須延伸到清單最右側外框。
   - 右側操作欄維持「資訊 / 發行下一批 / 隱藏」。

2. **彩券資訊改為中央 Modal**
   - 不再使用貼著資訊按鈕的 anchored Popup。
   - 按「資訊」後，設定頁背景加半透明遮罩；資訊卡由畫面中央淡入並輕微上移到位。
   - 資訊卡標題、來源 / 玩法 / 批次與六個摘要數值採偏置中排版。
   - 獎池分配完整顯示，不使用內層 ScrollBar；空間不足時由資訊卡本身向下擴充。
   - 右上角只保留小型紅色「解除安裝」；Built-in Pack 不顯示此按鈕。
   - 移除原右上角 `X` 與解除安裝旁說明小字。
   - 底部正中央放一般「關閉」按鈕，與解除安裝位置明確分離，降低誤按風險。
   - Imported Pack 解除安裝仍保留二次確認與 Pending Ticket 保護。

本批完成條件：

1. VERSION 維持 `0.5.5`，BUILD `4`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression 不退化。
4. ScratchGame / PackEditor startup smoke PASS。
5. 完整 V0.5.5 Build 4 Portable 成功產生。
6. 使用者實機確認：
   - 表頭色塊與外框貼齊，右側無固定深色色帶。
   - 每列分隔線延伸至最右側。
   - 資訊卡位於畫面中央，背景被遮罩且底層不可誤操作。
   - 獎池完整顯示且無 ScrollBar。
   - 右上解除安裝與底部中央關閉按鈕位置、大小與操作符合預期。

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
