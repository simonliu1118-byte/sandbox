# ScratchGame 工作交接

更新日期：2026-09-14

> 本檔是目前 ScratchGame **實際開發狀態與下一輪工作交接**，不是永久 governance 規則。
> 永久規則仍以根目錄治理文件與 `apps/ScratchGame/PROJECT_RULES.md` 為準。

## 1. 目前基準

- Repository：`simonliu1118-byte/sandbox`
- Build 3 開發分支：`scratchgame/build3-ui-rebuild`
- Build 3 基準 commit：`a40bbcb7d78a0dc7c6d4fc054dfb5db38d5a4e9a`
- PR：#6 `ScratchGame V0.3.0 Build 3 UI rebuild`
- 目前版本：`V0.3.0 Build 3`
- Build 3 Windows CI 已成功，使用者已開始實測。
- Build 3 尚未驗收通過，不可合併為正式完成版。

### 版本處理

本輪是 Build 3 實測後的同一工作項目修正。
依版本規則，完成一個連貫修正批次後應維持 `V0.3.0`，將 `BUILD` 從 `3` 增為 `4`；不要每修一個小項就增加一次 Build。

目前這份交接文件本身不代表 Build 4 已完成，也不應僅因文件更新就改 `BUILD`。

---

## 2. ScratchPack / Maker / V1.0.0 玩法設計

完整已討論設計保存在另一條 docs-only 分支：

- Branch：`docs/scratchpack-v1-plan`
- File：`apps/ScratchGame/SCRATCHPACK_V1_PLAN.md`
- 最新完整設計 commit（截至本交接）：`33975fc85e998eb728cf99b1550f50e8230b7141`

後續接手 **必須先讀這份文件**，不要依舊聊天記憶重建 ScratchPack 規格。

已鎖定 V1.0.0 基礎 GameType：

1. `1` 星星連線
2. `2` 中獎號碼
3. `3` 三個相同
4. `4` 符號計數
5. `5` 賓果
6. `6` 比大小

該計畫已包含 ScratchPack V1 結構、Canvas、priceDisplay、票號、Scratch Zone、Prize Pool、Maker GameType-aware 流程、Importer 驗證、六種玩法的盤面生成與派彩規則。

### 特別避免倒退的定案

- `canvas=1` 永遠是 `1080×882`。
- ScratchPack V1 **完全沒有 `thumbnail.png`**；主程式由 `ticket.png` 產生縮圖。
- V1 **沒有通用 `contentBox`**；刮區只管幾何，內容排版由 GameType Renderer 控制。
- Maker 專案檔（例如 `.scratchproj`）目前只保留概念，不列 TODO、不列正式開發計畫。
- V1 ScratchPack 不提供自訂中獎音效／動畫；這些由主程式／玩家 cosmetic 系統負責。
- 正式彩券最後應逐步 ScratchPack 化；開發測試票可保留 built-in。
- Build 3 驗收完成前，不開始 ScratchPack V1 功能實作。

---

## 3. Build 3 使用者實測：本輪必修

以下均已由使用者在 Build 3 實際畫面確認，不是猜測。

### A. 主舞台背景分層

使用者要求：

- `stage_bg.png` 要是一張**完整中央舞台背景圖**，尺寸維持目前 UI spec 的 `1920×900`。
- 背景圖不包含彩券。
- 背景圖不包含上方／下方功能列。
- 背景圖**不能預先烤入中央咖啡色大塊**。
- 中央較暗、方便閱讀彩券的咖啡色／暗色舞台區應由 WPF 後續疊上，可調透明度、圓角與邊距。

目前程式：`MainWindow.xaml` 的中央區先放 `StageBackgroundImage`，可在其上加單一 WPF stage overlay；不要再回到多層托盤框。

需要重新製作／替換 `UI/stage_bg.png`。正式資源原則仍遵守 `UI_ASSET_SPEC.md`。

### B. 面額鈔票選取金框右側被裁切

畫面：`NewTicketDialog` 面額列。

現況：所有面額卡片的 selected 金框右側都偏緊／會被切，不只有「全部」。

目前 `PriceNoteItem`：

- Width 112
- Height 68
- Margin 5
- SelectionChrome BorderThickness 2 / Padding 3

修正方向：

- 為每個 item 增加足夠外部安全空間，不讓 selected / hover chrome 超出容器裁切。
- 同時驗證正常、hover、selected、disabled 四種狀態。
- 不以只修第一張「全部」為特例。

### C. 設定頁按鈕不應觸發列展開

使用者要求：

- 點「啟用中／已停用」只切狀態。
- 點「發行新一批」只執行發行確認。
- 只有點該列其他區域才展開／收起。

已查到目前結構：

- Row 用 `MouseLeftButtonUp="TicketRow_OnClick"`。
- Button handler 裡 `e.Handled = true` 處理的是 Button 的 `Click` routed event，不能可靠阻止之後 row 自己收到 `MouseLeftButtonUp`。

修正方向：

- Row handler 必須檢查 `OriginalSource` 是否位於 Button 內；若是 Button 就直接 return，或改用明確獨立的展開 hit area。
- 不要只依賴 Button `Click` 的 `e.Handled`。

### D. 啟用中的測試票未出現在「挑選彩券」清單

使用者看到設定頁有：

- 三星連線 $100
- 三星連線 $500
- 三星連線（獎項測試） $500

但選票清單未出現測試票。

目前已查：

- `NewTicketDialog` 本身沒有依名稱／面額做去重；它直接列 `_tickets`。
- `CatalogService.GetAvailableTicketsAsync()` 只回傳：`enabled=1`、有 Active batch、且 `batch_prize_state.available_count > 0` 的 ticket。

因此下一輪**不要先在 UI 亂補一張**。要先確認測試票目前 active batch 的 `available_count` 與 batch 狀態；若仍有庫存卻未回傳，再追 SQL / data state。若已售罄，則需釐清使用者期待與「只列仍可買票」規則是否一致。

### E. 彩券畫面尺寸／底圖錯誤

使用者指出目前彩券：

- 下方多出一大塊沒有玩法意義的紅色延伸圖。
- 右邊仍有裁切。
- 新 Canvas 已正式固定，不需要再用多餘圖塊去「填滿區域」。

固定規則：

- `canvas=1 = 1080×882`。
- `TicketStage`、`TicketOverlayCanvas`、`CelebrationLayer`、`CoinCursorLayer` 都應在同一 `1080×882` 設計座標。
- 彩券底圖不可靠額外假延伸區填滿 Canvas。
- 顯示時等比縮放、置中，不裁右側。

目前 `MainWindow.xaml` 的 `TicketStage` 已是 1080×882，但 `Tickets/ThreeStar/ticket.png` 本身仍含使用者不接受的下方延伸設計；需要重做票面資產／重新核對原票面比例，而不是再以 WPF 補一塊遮掉。

### F. 銀膜與刮區有輕微錯位

使用者截圖確認九宮格銀膜沒有精準貼齊底下刮區。

固定規則：

> 同一刮區的底層動態內容、ScratchSurface、銀膜與 hit testing 必須共用單一 geometry source。

目前程式已有 `ThreeLinePositions` + `ThreeLineCellWidth/Height` 共用座標，但位置本身仍需依最終 ticket artwork 重新量測。

目前 Build 3 值：

```text
cell = 163×101
(281,225) (466,225) (652,225)
(281,354) (466,354) (652,354)
(281,484) (466,484) (652,484)
```

修正順序：**先定最終票面 PNG，再一次量測並更新 geometry**。不要先對舊 PNG 微調，之後換底圖又重做。

### G. 硬幣圖樣與實際刮筆大小不符

目前 `ScratchSurface.BrushRadius = 20`，實際擦除直徑約 40 design px；目前 Scratch coin 視覺是細長 24×66，使用者覺得視覺比實際筆刷小。

要求：

- 硬幣略放大，讓可見接觸範圍與筆刷感受接近。
- 硬幣與刮除仍必須共用同一個 mouse pipeline。
- 按下刮獎時硬幣要跟著游標移動；不能只有擦除在動。

Build 3 已把 coin update 放在同一 `PreviewMouseMove`；下一輪只需校正視覺尺寸／offset，不另加第二套 mouse handler。

### H. 中獎結果層恢復較實背景＋可暫時隱藏

目前 `ResultOverlay` 背景是半透明 `#73130907`，使用者認為透明效果不好。

要求：

- 恢復較接近前一版的深色實底／高不透明度。
- 「再來一張」「挑其他款」按鈕重新美化，現在太簡陋，中文字體呈現也不理想。
- 可增加「暫時隱藏／先看票面」功能：只收起結果提示，不改變已兌獎結果。
- 收起後要有清楚且不干擾票面的方式重新叫回結果層。

不要用 Windows MessageBox 代替。

### I. 刮膜碎屑效果（排在核心修正之後）

使用者詢問不用影片能否有「刮下來的渣渣」感；已確認可做。

建議：

- 不引入影片。
- WPF 程式產生短生命週期銀灰色粒子／小碎片。
- 由刮動速度／方向控制少量散射。
- 幾百毫秒淡出，不永久堆積。
- 必須設粒子上限，避免影響刮獎流暢度。
- 第一階段可完全程式生成，不需額外 PNG。

此項優先級低於背景、Canvas、銀膜、列表與 Dialog 修正。

### J. 設定視窗 ScrollBar 固定 lane 仍未成功

使用者要求設定頁清單右側固定保留 ScrollBar lane，即使目前只有三筆也看得到，不因內容量改變導致左右跳動。

已查到目前 XAML 問題：

- `ThemedScrollViewer` 雖設定 `VerticalScrollBarVisibility="Visible"`。
- 但模板內 `PART_VerticalScrollBar.Visibility` 又綁定 `ComputedVerticalScrollBarVisibility`。
- 當內容不需要捲動時仍可能 Collapse，所以畫面完全看不到。

修正方向：

- lane 永遠保留且顯示。
- 無需捲動時 thumb 顯示為不可拖／完整 viewport 狀態即可。
- 不使用突兀的 Windows 原生白灰 ScrollBar。

### K. 使用者視窗框架需重整

使用者認為目前 `UserDialog` 框架「亂七八糟」。

已查到原因：

- `UserDialog.xaml` 已有 WPF 最外層 Border。
- 內部又載入 `UI/dialog_bg.png` (`DialogBackgroundImage`)，該圖片本身帶裝飾框，因此形成多層框中框。
- 使用者卡、新增區也各自再加明顯框線。

修正方向：

- **移除 `DialogBackgroundImage` 這一層**，UserDialog 改成與 SettingsDialog 同樣的單一 WPF modal outer frame。
- 一張使用者只保留一張清楚的 user card。
- 新增使用者 TextBox 改深色主題，不要白色 Windows 原生輸入框。
- 底部維持左側「重置損益」，右側「取消」「切換使用者」，但統一高度、圓角、padding、基線。
- 編輯姓名輸入框也使用同一 themed TextBox。

### L. 程式 ICON

使用者要求新增正式程式 ICON，由助手設計。

設計方向已定：

- 主題：**刮刮卡＋硬幣**。
- 小尺寸辨識優先；不要塞文字，不做像通用彩券網站 logo 的複雜圖。
- 與目前深紅／金色 ScratchGame 視覺一致。

實作要求：

- 產出多尺寸 `.ico`。
- `ScratchGame.csproj` 設定 `ApplicationIcon`。
- MainWindow / executable / 檔案總管 / Windows 捷徑都應顯示同一 ICON。
- CI 發行 EXE 需驗證 ICON 已嵌入。

目前 **ICON 尚未產生，也尚未整合**；後續不可誤認為已完成。

---

## 4. 建議實作順序

為避免座標與美術重做，下一輪建議一個 correction batch 依序處理：

1. 完成新的純 `stage_bg.png`，中央暗色舞台改 WPF overlay。
2. 定稿 ThreeStar `ticket.png`（1080×882、無假延伸區、無右側裁切）。
3. 依最終 ticket.png 重量九宮格 geometry，修銀膜／符號／hit-test 對齊。
4. 修 denomination selected chrome 右側裁切。
5. 修設定 row button 不觸發展開。
6. 查清測試票缺席原因後再修，不猜。
7. 修固定 themed ScrollBar lane。
8. 重整 UserDialog 單層框架與 themed TextBox。
9. 校正 coin 視覺尺寸／offset。
10. 恢復結果層較實背景、美化兩顆結果按鈕、加入暫時隱藏／重新叫回。
11. 設計並整合 App ICON。
12. 核心修正穩定後，再加入刮膜碎屑粒子。
13. Local/static check 完成後一次推成 `V0.3.0 Build 4` correction；只在最後 coherent round 跑 Windows CI。

---

## 5. 已查檔案（接手優先讀）

- `apps/ScratchGame/PROJECT_RULES.md`
- `apps/ScratchGame/UI_ASSET_SPEC.md`
- `apps/ScratchGame/src/ScratchGame/MainWindow.xaml`
- `apps/ScratchGame/src/ScratchGame/MainWindow.xaml.cs`
- `apps/ScratchGame/src/ScratchGame/MainWindow.Enhancements.cs`
- `apps/ScratchGame/src/ScratchGame/App.xaml`
- `apps/ScratchGame/src/ScratchGame/Controls/ScratchSurface.cs`
- `apps/ScratchGame/src/ScratchGame/Views/NewTicketDialog.xaml`
- `apps/ScratchGame/src/ScratchGame/Views/NewTicketDialog.xaml.cs`
- `apps/ScratchGame/src/ScratchGame/Views/SettingsDialog.xaml`
- `apps/ScratchGame/src/ScratchGame/Views/SettingsDialog.xaml.cs`
- `apps/ScratchGame/src/ScratchGame/Views/UserDialog.xaml`
- `apps/ScratchGame/src/ScratchGame/Views/UserDialog.xaml.cs`
- `apps/ScratchGame/src/ScratchGame/Services/CatalogService.cs`
- `apps/ScratchGame/src/ScratchGame/Services/SeedDataService.cs`
- `apps/ScratchGame/src/ScratchGame/ScratchGame.csproj`

---

## 6. 本次中斷時的真實狀態

- 已完成：需求確認、截圖比對、Build 3 source 結構檢查、主要問題原因定位。
- 尚未完成：本輪 UI / asset / icon / particle 的正式 source 修改。
- 尚未完成：Build 4 版本號變更。
- 尚未完成：Windows CI / 新測試包。
- 不要把本文件中的「修正方向」當成已經實作。

因本輪 source 尚未形成可驗證 coherent patch，所以本次先保存**完整 handoff**，不把半寫、未編譯的 source 假裝成完成品提交。
