# ScratchGame 工作交接

更新日期：2026-09-15

## 目前基準

- Repository：`simonliu1118-byte/sandbox`
- 開發分支：`scratchgame/build3-ui-rebuild`
- PR：#6
- 版本：`V0.3.0 Build 7`
- Build 7 是 Build 6 使用者實測返修，重點是修正 Header 邊界、Empty-state 清晰度、挑彩券 runtime XAML 錯誤與卡片樣式。

## Build 5 實測確認的問題

1. Header 底部裝飾與區塊邊界視覺混在一起。
2. Header / Stage / Footer 沒有真正的程式 separator，造成重疊 / 空隙感。
3. UserDialog 底部按鈕區受到既有 Button margin 與欄寬影響，右側視覺被切。
4. 挑彩券仍是一整橫列，且 Build 5 test package 缺 `Tickets/`，因此 thumbnail 空白。
5. Build 5 test package 缺 `Tickets/`，因此主畫面只看到 scratch overlay，沒有 ticket artwork。
6. Stage 大咖啡 panel 已移除，但 empty state 尚未改成小型半透明淺色底卡。
7. Build 5 test package 缺 `Audio/`，因此中獎音效消失。

## Build 6 修正方向

- Theme 正式改用外部：
  - `Themes/Default/Frame/header_bg.png`
  - `Themes/Default/Frame/footer_bg.png`
  - `Themes/Default/Stage/stage_bg.png`
- Header / Stage / Footer 中間各增加 5 WPF px 程式金線，不由圖片提供。
- Header 最底安全帶遮掉預設美術容易誤認為重疊的低位裝飾；Footer 頂端安全帶遮掉舊圖烤入線。
- Empty state 改成只包住提示內容的小型半透明淺色卡。
- NewTicketDialog 改成 3 欄左右的卡片式列表：ticket artwork 縮圖、名稱、面額、第幾批、中獎率。
- `TicketDefinition` 增加 `ActiveBatchNumber` 顯示用欄位；available-ticket SQL 同步取得 Active batch number。
- UserDialog 加寬 / 加高，底部 action 使用明確欄距並覆寫按鈕 Margin，避免右側裁切。
- Runtime package 必須包含 Theme + Tickets + Audio；用 `tools/package_portable.py` 驗證，缺檔直接失敗。
- GitHub Actions artifact 改名 `exe-only`，避免再把單一 EXE 誤當完整 portable package。
- Theme / ticket thumbnail 缺檔寫 `%LOCALAPPDATA%\ScratchGame\logs\runtime-assets.log`。

## Runtime asset recovery source for this Build 6 test package

- ThreeStar ticket art：從已保存的 `ScratchGame-V0.3.0-Build4-test-win-x64.zip` 回收最新 Build 4 ticket / silver-star。
- 四個 win audio：從已保存的 `ScratchGame-V0.3.0-Build3-win-x64.zip` 回收原本已驗證的 WAV。
- Default Theme：使用 Build 5 已確認方向的 Header / Stage / Footer 美術。

## Build 6 驗收後仍待處理

- 最終 ThreeStar `ticket.png` 美術仍需依 canvas=1 重新定稿，再量九宮格 geometry；Build 6 先恢復現有可測票面，不把這項混入本輪資源 / layout 修復。
- 繼續驗 denomination selected border、Settings row action / scrollbar、測試票 DB availability 根因、coin/debris/result/icon。
- Build 6 UI correction 驗收前，不開始 ScratchPack V1 實作。


## 2026-09-15 使用者實測（Build 6 後續）新增問題與處理方針

1. **中間 Empty-state 文字發糊**
   - 根因：Empty-state card 放在 `Viewbox` 內，整體被縮放，文字被一起插值。
   - 處理：把 `TicketPlaceholderPanel` 移出 `Viewbox`，改為覆蓋在 Stage 上方，保留同視覺但不再縮放字。

2. **Header 金線上方多一條紅線 / 區塊邊界不乾淨**
   - 根因：`MainWindow.xaml` 已有 separator，`MainWindow.Build6.cs` 又動態再加一次 separator / mask，造成雙重疊加。
   - 處理：Build6 code-behind 不再新增 separator / mask，只保留 row height 校正。

3. **Header 左側標題美術有殘影 / 疊字**
   - 根因：目前 `Themes/Default/Frame/header_bg.png` 本身左側標題區就有壞圖（可直接從資產看到 `刮刮樂` 疊字）。
   - 處理：更換預設 header asset，保留左側標題、右側留給 WPF 動態玩家與按鈕。

4. **挑彩券視窗卡片樣式不符合最新決策**
   - 根因：Build 6 仍保留舊版窄卡思路，且 `NewTicketDialog.Build6.cs` 用 `XamlReader.Parse(...)` 於 runtime 重寫 UI，維護性差。
   - 處理：卡片 layout 改回 XAML 靜態定義；採橫式卡片（縮圖 + 名稱 + 面額 + 第幾批 + 中獎率），一排可放 2 張左右，之後再微調密度。

5. **挑彩券後錯誤 / 建立彩券後閃退**
   - 目前畫面訊息：`'x' is an undeclared prefix. Line 13, position 17.`
   - 已知高風險來源：runtime `XamlReader.Parse(...)` 做 UI 重寫。
   - 先處理：移除 `NewTicketDialog.Build6.cs` 的 runtime XAML 改寫，先把票券選擇 UI 固定回 XAML 版本，縮小錯誤面。
   - 若仍存在，再回頭追主票面建立流程的動態 XAML / control factory。
