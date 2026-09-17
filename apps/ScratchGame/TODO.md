# ScratchGame TODO / Roadmap

更新日期：2026/09/18

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.5 / Build 1**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2：完整 Portable automation 已完成，Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」核心、Renderer、Runtime 與 regression 已完成，Run #192 全部 PASS。
- V0.5.4 Build 2 Portable：`ScratchGame-V0.5.4-Build2-win-x64.zip`，SHA-256 `46513302980227b3d48e2c287830ea1666bf7ed83827c70419a36987eb85fb05`。
- V0.5.5 Build 0：彩券小舖卡片排版完成，Run #193 全部 PASS，進入使用者實機驗收。
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
- `GameType2RenderModel` 直接使用同一份 `scratch.zones` geometry，不建立第二套座標。
- Windows CI Run #192：Type 2 + 原 GameType 1 regression、兩個 EXE build / smoke、完整 Portable 全部 PASS。

## V0.5.5 — 彩券小舖卡片排版

Build 0 已完成並由 Run #193 全部驗證通過：

- `TicketCardItem` 外框仍固定 182×252。
- 彩券縮圖移到最上方，顯示區 132px。
- 名稱與面額移到縮圖下方。
- 名稱、面額、總中獎率採相同 nominal 字級；總中獎率使用較低調顏色。
- 「最高獎金」標題縮小並降低視覺權重；最高獎金金額以大字、亮桃紅顯示。
- 發行日期與批次不再於彩券小舖卡片顯示；底層資料未刪除。
- 不修改卡片外框、NewTicketDialog 視窗尺寸或主畫面 Header / Stage / Footer 邊界。

## V0.5.5 Build 1 — 實機驗收返修

本批只處理使用者實機回報，不開新功能：

1. **選擇玩家初次顯示裁切**
   - 現象：第一次開啟時玩家卡片邊框被裁切；按一次「修改名稱」後即完整。
   - 判定：初次 ListBox item realization / Measure / Arrange 未完成；先前只加 Margin/Padding 是治標。
   - 修正：UserDialog Loaded 時主動讓 ListBox 與目前 selected container 完成一次 Measure / Arrange / UpdateLayout，等價於使用者點編輯後觸發的重新 layout，但不改玩家卡片尺寸。

2. **「顯示中獎結果」下移**
   - 維持 Stage UI owner，不搬進 Footer。
   - 只把 resume pill 往下貼近 Stage / Footer 間金線；不改 Header / Stage / Footer row size 或整體 layout boundary。

3. **PackEditor ICON master 回復**
   - 已 byte-level 確認目前 repo 的 `PackEditor-icon-source.png`（4178 bytes，Git blob `f826f0aa88c023e0548538e44ac8dd630e49ccb2`）其實就是素材庫的 `PackEditor-icon-48.png`，先前誤把預覽／縮小版本當 master。
   - 改回核可 256×256 master：紅色彩券＋金槌，19093 bytes，SHA-256 `557eabf2d0ea9f87a2d3525408206a1065ef42be3f4cc2295a4a0fe0d0971ed6`。
   - 仍沿用既有 `GenerateWindowsIcon.ps1` 產生 multi-size ICO，並由 Windows CI 驗證 shell icon。

本批完成條件：

1. VERSION 維持 `0.5.5`，BUILD `1`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression 不退化。
4. ScratchGame / PackEditor startup smoke PASS。
5. PackEditor generated ICO / EXE associated icon verification PASS。
6. 完整 V0.5.5 Build 1 Portable 成功產生。
7. 玩家卡片初次開啟與兩個視覺修正仍需使用者實機確認。

## GameType 主線

基本 GameType 原則：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：完成。
- GameType 2：完成。
- GameType 3～6：規格已定，尚待逐一實作。

V0.5.5 Build 1 實機驗收完成後，下一個獨立開發項目依 `GAMETYPE_SPEC.md` 進入 **GameType 3**。

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
