# ScratchGame UI Asset Pack

本檔描述 ScratchGame 主程式共通 UI 美術與目前 `ThreeStar` 測試／內建票的 portable-folder 資源。這些檔案不是 ScratchPack 規格的一部分；ScratchPack 的正式格式仍以 `SCRATCHPACK_SPEC.md` 為準。

## Portable folder

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
      ├─ thumbnail.png
      ├─ thumbnail-100.png
      └─ silver-star.png
```

## 圖片處理原則

- 正式 UI 與彩券底圖使用 PNG。
- 發行包建立時不得把 PNG 轉成 JPG、不得降低尺寸、不得重新取樣。
- 可做 PNG 的 lossless deflate/metadata 最佳化，但不得改變像素內容。
- 程式載入圖片時使用原始檔案，不先另存成較低畫質版本。
- `canvas=1` 固定為 **1080×882**；主程式只做等比例顯示，不另存縮放後版本。

## 主程式 UI 資源

| 路徑 | 用途 | 建議尺寸 |
| --- | --- | --- |
| `UI/topbar_bg.png` | 主視窗頂部資訊列 | 1920×180 |
| `UI/stage_bg.png` | 中央彩券舞台；中央不得烤入托盤／大框 | 1920×900 |
| `UI/footer_bg.png` | 底部操作區 | 1920×240 |
| `UI/dialog_bg.png` | 舊/其他對話窗共通背景；新 Dialog 優先由 WPF 單層容器繪製 | 1200×800 |
| `UI/Denominations/*.png` | 挑選彩券的面額鈔票圖示 | 180×96 |

主程式 UI 資源屬於 ScratchGame 本體，不隨單張彩券 ScratchPack 更換。

## ThreeStar 目前資源

| 路徑 | 用途 |
| --- | --- |
| `Tickets/ThreeStar/ticket.png` | 1080×882 的 $500／測試票基礎底圖 |
| `Tickets/ThreeStar/ticket-100.png` | 1080×882 的舊 $100 票底圖 |
| `Tickets/ThreeStar/thumbnail*.png` | 挑選彩券縮圖 |
| `Tickets/ThreeStar/silver-star.png` | 九宮格銀膜材質 |

票號由程式動態繪製，底圖不得烤入固定票號或舊票號底塊。Scratch geometry、符號與銀膜必須使用同一組設計座標。

## 互動規則

- 九宮格仍是 9 個獨立遮罩，各自保存刮除比例與約 78% 完成狀態。
- 78% 只代表該區邏輯完成，不自動移除剩餘銀膜。
- 使用者按住滑鼠時，可從一個刮區連續拖到另一個刮區；硬幣游標與刮除必須共用同一個 mouse pipeline 與同一個座標點。
- 9 個必要區域全部完成後才自動判定／兌獎；「全部刮開」則直接揭除所有區域並結算。
