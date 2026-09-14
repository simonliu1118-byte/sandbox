# ScratchGame UI Asset Pack

本檔描述 ScratchGame 主程式共通 UI 美術與目前 `ThreeStar` 測試／內建票的 portable-folder 資源。這些檔案不是 ScratchPack 規格的一部分；ScratchPack 的正式格式仍以 `SCRATCHPACK_SPEC.md` 為準。

## Portable folder

目前所有主介面 Theme 美術都由 **EXE 外部資源**讀取，不內嵌進 `ScratchGame.exe`。

```text
ScratchGame/
├─ ScratchGame.exe
├─ UI/
│  ├─ topbar_bg.png
│  ├─ stage_bg.png
│  ├─ footer_bg.png
│  ├─ dialog_bg.png
│  └─ Denominations/
│     ├─ note_all.png
│     ├─ note_100.png
│     └─ ...
├─ Audio/
│  ├─ small-win-manual.wav
│  ├─ small-win-auto.wav
│  ├─ big-win-manual.wav
│  └─ big-win-auto.wav
└─ Tickets/
   └─ ThreeStar/
      ├─ ticket.png
      ├─ ticket-100.png
      └─ silver-star.png
```

## Theme 分層

主畫面視覺固定分成三個獨立區塊：

1. **Header**：頂部固定區。
2. **Stage**：中央可獨立替換的舞台背景。
3. **Footer**：底部固定操作區背景。

未來外觀商店的正式概念名稱：

- **介面框架（Frame Theme）**：Header + Footer 必須成套，不能把不同框架的 Header / Footer 混搭。
- **舞台主題（Stage Theme）**：只控制中央 Stage，可獨立購買、收藏與裝備。

目前預設外觀名稱：

- Frame Theme：**新春紅金**
- Stage Theme：**招財好運**

目前 Build 5 仍使用既有 `UI/` 路徑作為預設外部 Theme 的 runtime 路徑；等真正實作 Theme 選擇／商店時，再把多組 Theme 正式整理到獨立 Theme 資料夾與 manifest。不要為了目前單一預設 Theme 提前增加不必要格式。

## 主畫面三區規格

| 路徑 | 區塊 | 正式尺寸 | 美術責任 |
| --- | --- | ---: | --- |
| `UI/topbar_bg.png` | Header | **1920×144** | 紅金裝飾底、`刮刮樂`、`刮出好運・樂在每一刻`；不得包含玩家資料、使用者／設定按鈕 |
| `UI/stage_bg.png` | Stage | **1920×900** | 招財貓、燈籠、花、金幣、右側「好運常在」與中央純舞台；不得包含 Header、Footer、彩券、狀態文字或中央暗板 |
| `UI/footer_bg.png` | Footer | **1920×156** | 金色上分隔線以下的深紅／金色裝飾；不得包含按鈕、狀態文字或主舞台物件 |

### WPF 責任

以下必須由程式／WPF 負責，不烤進 Theme 圖：

- 目前玩家資訊。
- 使用者、設定與其他互動按鈕。
- 彩券、ScratchSurface、symbol、刮膜、hit geometry。
- 狀態文字、票券資訊。
- Footer 操作按鈕。
- 中獎結果、粒子、硬幣游標與其他動態效果。

Build 5 驗收包 **不顯示 `StagePanelOverlay` 中央暗色 WPF 面板**；先直接確認純 Stage 背景與彩券分層效果。

## 圖片處理原則

- 正式 UI 與彩券底圖使用 PNG。
- 發行包不得把 PNG 轉成 JPG、不得降低尺寸或重新取樣。
- 可做 PNG lossless deflate / metadata 最佳化，但不得改變像素內容。
- `canvas=1` 固定為 **1080×882**；主程式只做顯示縮放，不另存縮放後票面。
- Theme 資源缺失時不應偷偷改用另一張內嵌圖；因目前架構明確採外部資源，缺檔應被視為 portable package 不完整。

## 其他 UI 資源

| 路徑 | 用途 | 建議尺寸 |
| --- | --- | ---: |
| `UI/dialog_bg.png` | 舊／其他對話窗共通背景；新 Dialog 優先由 WPF 單層容器繪製 | 1200×800 |
| `UI/Denominations/*.png` | 挑選彩券的面額鈔票圖示 | 180×96 |

## ThreeStar 目前資源

| 路徑 | 用途 |
| --- | --- |
| `Tickets/ThreeStar/ticket.png` | 1080×882 的 $500／測試票基礎底圖 |
| `Tickets/ThreeStar/ticket-100.png` | 1080×882 的舊 $100 票底圖 |
| `Tickets/ThreeStar/silver-star.png` | 九宮格銀膜材質 |

票號由程式動態繪製，底圖不得烤入固定票號或舊票號底塊。Scratch geometry、符號與銀膜必須使用同一組設計座標。

## 互動規則

- 九宮格仍是 9 個獨立遮罩，各自保存刮除比例與約 78% 完成狀態。
- 78% 只代表該區邏輯完成，不自動移除剩餘銀膜。
- 使用者按住滑鼠時，可從一個刮區連續拖到另一個刮區；硬幣游標與刮除必須共用同一個 mouse pipeline 與同一個座標點。
- 9 個必要區域全部完成後才自動判定／兌獎；「全部刮開」則直接揭除所有區域並結算。
