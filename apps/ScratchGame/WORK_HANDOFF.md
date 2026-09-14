# ScratchGame 工作交接

更新日期：2026-09-14

> 本檔保存目前 ScratchGame 的實際開發狀態與下一輪接手順序。它不是永久 governance 規則；永久規則仍以根目錄治理文件與 `apps/ScratchGame/PROJECT_RULES.md` 為準。

## 1. 目前基準

- Repository：`simonliu1118-byte/sandbox`
- 開發分支：`scratchgame/build3-ui-rebuild`
- PR：#6
- 版本：`V0.3.0 Build 5`
- `BUILD = 5`
- Build 4 最後 handoff 基準：`ec0655d15eccd84f9f2d1817f077297264c883c0`
- Build 5 是 Build 4 驗收返修，不是新功能版本。

## 2. Build 5 本輪主背景決策

使用者已取消「預設 Theme 嵌入 EXE」方案。

目前正式方向：**所有可替換 Theme 美術由 EXE 外部資源讀取，不內嵌。**

現階段仍沿用既有 runtime 路徑：

```text
ScratchGame.exe
UI/
├─ topbar_bg.png
├─ stage_bg.png
└─ footer_bg.png
```

本輪正式尺寸：

- Header / `topbar_bg.png`：1920×144
- Stage / `stage_bg.png`：1920×900
- Footer / `footer_bg.png`：1920×156

外觀概念名稱：

- **介面框架（Frame Theme）**：Header + Footer 成套；目前預設「新春紅金」。
- **舞台主題（Stage Theme）**：中央 Stage；目前預設「招財好運」。

未來 Theme 商店／多主題資料夾格式尚未實作；Build 5 不為此提前增加 manifest 或資料模型。

## 3. Build 5 主畫面變更

`MainWindow.xaml`：

- Header 美術已負責「刮刮樂／刮出好運・樂在每一刻」，所以舊 WPF 標題 StackPanel 在本輪隱藏，避免重複 visual owner。
- 玩家資訊、使用者、設定仍由 WPF 負責。
- `StagePanelOverlay` 在 Build 5 驗收包設為 `Collapsed`；本輪先直接檢查純 Stage 背景，不畫中央咖啡色暗板。
- Ticket / Scratch / result / coin / footer action 等互動層維持既有程式責任。

## 4. Build 5 驗收包資源

本輪測試包必須在 EXE 同層帶上：

- `UI/topbar_bg.png`
- `UI/stage_bg.png`
- `UI/footer_bg.png`

Theme 缺檔代表測試／portable package 不完整；不再設計 EXE 內嵌 fallback。

## 5. 尚未完成／仍需驗收

Build 5 這輪只處理主背景分層與外部 Theme 方向，以下 Build 4 待驗收項目仍保留：

1. 最終 `Tickets/ThreeStar/ticket.png`：1080×882、無假延伸區、無右側裁切。
2. ticket.png 定稿後重新量測九宮格 geometry，移除臨時 +1 px 依賴。
3. denomination selected border 是否仍裁切。
4. 設定 row action / scrollbar 實測。
5. 獎項測試票未出現在挑選清單的實際 DB / Active batch / remaining 原因。
6. UserDialog 框架實測。
7. coin / debris 手感實測。
8. result hide/show 與按鈕視覺實測。
9. Windows EXE / 工作列 / 檔案總管 icon 實測。

## 6. ScratchPack V1

完整設計仍在 docs-only 分支：

- Branch：`docs/scratchpack-v1-plan`
- `apps/ScratchGame/SCRATCHPACK_V1_PLAN.md`
- `apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`

Build 5 UI 驗收完成前，**不要開始 ScratchPack V1 功能實作**。

既有重要定案不可倒退：

- `canvas=1 = 1080×882`
- ScratchPack V1 沒有 `thumbnail.png`
- V1 沒有 universal `contentBox`
- Maker 必須 GameType-aware
- ScratchPack 只放資料／美術，不帶 executable code

## 7. 下一個接手順序

1. 先確認 Build 5 Windows CI 成功。
2. 用包含三張外部 UI PNG 的完整測試包做使用者驗收。
3. 若主背景分層通過，再回到最終 ThreeStar ticket.png 與 geometry。
4. 之後依序處理設定、測試票 DB 原因、UserDialog、coin/debris/result/icon。
5. Build 4/5 UI correction round 全部驗收後，才進 ScratchPack V1。
