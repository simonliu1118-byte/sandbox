# ScratchGame 工作交接

更新日期：2026-09-14

> 本檔保存目前 ScratchGame **實際開發狀態、已完成的 WIP source、尚未完成項目與下一輪接手順序**。它不是永久 governance 規則；永久規則仍以根目錄治理文件與 `apps/ScratchGame/PROJECT_RULES.md` 為準。

## 1. 目前基準與真實狀態

- Repository：`simonliu1118-byte/sandbox`
- 開發分支：`scratchgame/build3-ui-rebuild`
- PR：#6
- 目前版本：`V0.3.0 Build 4`
- `BUILD = 4`
- Build 3 基準 commit：`a40bbcb7d78a0dc7c6d4fc054dfb5db38d5a4e9a`
- Build 4 第一輪 source commit：`4d240e3aa2436af120c59aefa5d45aa0b3650eed`
- Build 4 第二輪 source / icon commit：`7d8d98f84c80b6aa18b91a47f7a6ab251424d2dc`

### Build 4 CI

`7d8d98f84c80b6aa18b91a47f7a6ab251424d2dc` 已通過 Windows CI：

- ScratchGame Build Run #43 / ID `34813775535`：success
- Governance Check Run #27：success
- Artifact ID：`10335581732`
- Artifact：`ScratchGame-V0.3.0 Build 4-win-x64`
- Artifact digest：`sha256:5da824f44a1a378c50676fe5e40764d8778f71051cafe427f3cfd678e5bcd53f`

**重要：CI 成功只代表編譯／發行成功，不代表這輪 UI 已經過使用者驗收。Build 4 仍是 staging / WIP，不可直接視為完成。**

---

## 2. ScratchPack / Maker / V1.0.0 玩法設計

完整已討論設計保存在 docs-only 分支：

- Branch：`docs/scratchpack-v1-plan`
- 完整文件：`apps/ScratchGame/SCRATCHPACK_V1_PLAN.md`
- 完整設計 commit：`33975fc85e998eb728cf99b1550f50e8230b7141`
- 快速交接索引：`apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`
- 索引 commit：`a5f411f92790f31bbaadad9e11afc616142a414b`

後續接手 **必須先讀這份文件**，不要依聊天記憶重新發明 ScratchPack 規格。

已鎖定 V1.0.0 六種基礎 GameType：

1. `1` 星星連線
2. `2` 中獎號碼
3. `3` 三個相同
4. `4` 符號計數
5. `5` 賓果
6. `6` 比大小

### 特別避免倒退的定案

- `canvas=1` 永遠是 `1080×882`。
- ScratchPack V1 **完全沒有 `thumbnail.png`**；主程式由 `ticket.png` 產生縮圖。
- V1 **沒有通用 `contentBox`**；刮區只管幾何，內容排版由 GameType Renderer 控制。
- Maker 必須 GameType-aware，不做完全自由的空白刮區編輯器。
- Maker 專案檔（例如 `.scratchproj`）目前只保留概念，不列 TODO、不列正式開發計畫。
- V1 ScratchPack 不提供自訂中獎音效／動畫；由主程式／玩家 cosmetic 系統負責。
- 正式彩券最後應逐步 ScratchPack 化；開發測試票可保留 built-in。
- Build 4 UI 驗收完成前，不開始 ScratchPack V1 功能實作。

---

## 3. Build 4 目前已實作但尚未驗收的 source

以下已經在 branch 上，不是只有討論。

### A. 主舞台 WPF overlay

`MainWindow.xaml` 已加入：

- `StagePanelOverlay`
- 中央暗色舞台改由 WPF Border 疊上，而非依賴 ticket layer。
- `TicketBackgroundImage.Stretch` 改為 `Uniform`。
- Viewbox 加安全 margin，降低右側裁切風險。

**但新的 `UI/stage_bg.png` 尚未替換。**目前 repository 沒有新的完整純背景素材，因此使用者「一張完整背景、中央咖啡塊不要烤進圖」的需求只完成了 WPF 分層，素材本身仍待做。

### B. Ticket 顯示與 Scratch geometry WIP

已加入 `MainWindow.Build4.cs`：

- 將現有 ScratchSurface / symbol 同步往右下移 1 design px，維持同一 geometry owner。
- 票號暫移到較靠上的 footer 位置。

**但 `Tickets/ThreeStar/ticket.png` 尚未替換。**目前 branch 仍沒有新的 1080×882 最終票面資產，所以：

- 下方多餘紅色延伸區尚未從美術根源解決。
- 九宮格最終座標仍應在新 ticket.png 定稿後重新量測。
- 現在 +1 px 只是 Build 4 WIP 校正，不應視為最後規格。

### C. 硬幣視覺與刮屑

已實作：

- 硬幣 visual 從 Build 3 放大。
- 刮動仍走既有主要 mouse pipeline。
- 新增 `ScratchDebrisLayer`。
- 新增程式生成銀灰色短生命週期碎屑，無影片素材。
- 粒子有限量並自動淡出。

尚需使用者實測確認：

- 硬幣可見大小是否真的和筆刷手感一致。
- 粒子密度／方向／效能是否合適。

### D. 中獎結果層

已實作：

- 結果卡改回高不透明度深色背景。
- 「再來一張／挑其他款」按鈕加強字體與 chrome。
- 加入「暫時隱藏中獎提示」。
- 加入「顯示中獎結果」按鈕重新叫回結果層。

尚需使用者實測美觀與位置。

### E. 面額列 / 挑選彩券

已實作：

- denomination filter item 的大小／padding 做過調整，目標是避免 selected 金框右側被裁。
- 選票 row 做得較緊湊，讓更多票能在視窗內出現。
- `ResolveThumbnail()` 已改成直接用 ticket artwork，不再依賴 built-in `thumbnail.png`。

但「測試票未出現在列表」**根因仍未證實已解決**：

- `NewTicketDialog` 本身沒有去重。
- `CatalogService.GetAvailableTicketsAsync()` 仍只回 `enabled=1 + Active batch + available_count>0`。
- 若獎項測試票仍有 Active 庫存卻未出現，必須再查資料庫狀態／SQL。
- 若測試票其實售罄，則目前 SQL 正常；要再確認使用者希望售罄票是否仍列出。

不要因為 thumbnail 改成 ticket.png 就宣稱此問題已確定修好。

### F. 設定視窗

已對 XAML / code-behind 做 Build 4 修正：

- 固定 themed scrollbar lane 的方向已實作。
- row action 與 accordion click 做過隔離處理。
- enable / new batch 後不再刻意強制重新展開該列。

尚需使用者實測：

- ScrollBar 在只有少量資料時是否真的可見。
- 點狀態／發行按鈕是否完全不會觸發展開。
- 展開資料後 ScrollBar 是否正常。

### G. UserDialog

已重寫為較乾淨的單層 modal：

- 移除舊 `dialog_bg.png` 疊框思路。
- 使用單一 WPF outer frame。
- user card 簡化。
- 新增使用者與改名 TextBox 改成 themed style。
- 底部按鈕重新排列／統一。

尚需使用者實測框架是否已符合預期。

### H. Application ICON

已完成 source integration：

- `apps/ScratchGame/src/ScratchGame/Assets/ScratchGame.ico`
- `ScratchGame.csproj` 已設定 `ApplicationIcon`。
- MainWindow 已設定 `Icon="Assets/ScratchGame.ico"`。
- Windows CI 已能正常 build / publish。

下一輪只需由使用者檢查 EXE、工作列／視窗與檔案總管實際顯示效果；若設計不滿意再換圖，不需重做整個 icon plumbing。

---

## 4. 使用者 Build 3 實測提出的原始修正清單

以下是本輪需求來源，後續不可漏掉。

### 1. 主背景

- 要一張完整中央舞台背景圖，尺寸維持 `1920×900`。
- 不包含彩券，不包含上下功能列。
- 中央咖啡色暗區不烤進 PNG，改由 WPF 疊上。

### 2. 面額選取金框

- 所有 denomination item 的右側 selected 金框都不能被切。

### 3. 設定 row action

- 點「啟用／停用」不展開。
- 點「發行新一批」不展開。
- 點 row 其他區域才展開／收起。

### 4. 測試票缺席

- 啟用的「三星連線（獎項測試）」應確認為何沒出現在挑選清單。
- 必須先查 Active batch / remaining，不能只做 UI 假補。

### 5. 彩券畫面

- canvas=1 固定 1080×882。
- 下方不應有為了填滿畫面而追加的大塊無意義圖。
- 右邊不能切掉。
- 最終 ticket.png、Overlay、Scratch geometry 都使用同一 Canvas 座標。

### 6. 硬幣

- 視覺尺寸要更接近實際筆刷。
- 刮動時硬幣跟著移動。

### 7. 中獎提示

- 半透明背景不好看，改回較實的背景。
- 可以暫時隱藏，再重新叫回。

### 8. 銀膜對齊

- 銀膜、symbol、ScratchSurface、hit test 共用同一 geometry；不要各自微調。

### 9. 結果按鈕

- 「再來一張／挑其他款」不能像臨時按鈕，字體與 chrome 要整體化。

### 10. 設定 ScrollBar

- 彩券列表右側固定保留 themed scrollbar lane。
- 不因資料少就整條消失。

### 11. 使用者視窗

- 移除多層框中框。
- 單一 modal frame。
- themed TextBox。
- user card 與底部按鈕整理乾淨。

### 12. 刮膜碎屑

- 不用影片也可做。
- 使用程式粒子；有限量、短生命、不能拖慢刮獎。

### 13. 程式 ICON

- 主題方向：刮刮卡＋硬幣，小尺寸辨識優先，深紅／金色系。
- 需嵌入 EXE 與 WPF Window。

---

## 5. 下一輪真正未完成的部分

優先處理：

1. **重新製作並提交真正的 `UI/stage_bg.png`**：完整純背景，不烤中央暗塊。
2. **重新製作並提交最終 `Tickets/ThreeStar/ticket.png`**：1080×882、無假延伸區、無右側裁切。
3. ticket.png 定稿後重新量測九宮格，移除臨時 +1 px 依賴，讓 geometry 精確對齊。
4. 實測 Build 4 denomination selected border 是否仍裁切。
5. 實測設定 row button / scrollbar；若仍錯再針對事件與 template 修。
6. 釐清獎項測試票缺席的實際 DB 原因。
7. 實測 UserDialog 框架。
8. 實測 coin / debris / result hide-show。
9. 實測 EXE / Explorer icon。
10. 這些修正形成 coherent round 後再跑 Windows CI；不要每一個微調都跑 GitHub Actions。

---

## 6. 接手時優先閱讀檔案

- `apps/ScratchGame/PROJECT_RULES.md`
- `apps/ScratchGame/UI_ASSET_SPEC.md`
- `apps/ScratchGame/WORK_HANDOFF.md`
- `apps/ScratchGame/src/ScratchGame/MainWindow.xaml`
- `apps/ScratchGame/src/ScratchGame/MainWindow.xaml.cs`
- `apps/ScratchGame/src/ScratchGame/MainWindow.Enhancements.cs`
- `apps/ScratchGame/src/ScratchGame/MainWindow.Build4.cs`
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

## 7. 不要做的事

- 不要把 Build 4 CI success 當成使用者驗收通過。
- 不要先開始 ScratchPack V1 實作。
- 不要再引入第二套 scratch mouse pipeline。
- 不要用個別 silver/symbol 座標補丁破壞單一 geometry owner。
- 不要為了解決 ticket 1080×882 再畫一塊假的底部填充區。
- 不要因為測試票沒顯示就直接繞過有限票池／Active batch 規則。
- 不要把 docs-only ScratchPack 設計當成目前正式 importer 已支援格式。
