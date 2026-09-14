# ScratchGame 工作交接

更新日期：2026-09-15

## 目前基準

- Repository：`simonliu1118-byte/sandbox`
- 開發分支：`scratchgame/build3-ui-rebuild`
- PR：#6
- 版本：`V0.3.0 Build 6`
- Build 6 是 Build 5 使用者實測返修。

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
- NewTicketDialog 改成 3 欄左右的卡片式列表：ticket artwork 縮圖、名稱、面額、第幾扸、中獎率。
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
