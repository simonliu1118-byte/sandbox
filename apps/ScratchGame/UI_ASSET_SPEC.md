# ScratchGame UI Asset Pack

本檔描述 ScratchGame 主程式共通 UI 美術與目前內建 `ThreeStar` 測試票的 portable-folder 資源。這些檔案不是 ScratchPack 規格的一部分；ScratchPack 的正式格式仍以 `SCRATCHPACK_SPEC.md` 為準。

## Portable folder

```text
ScratchGame/
├─ ScratchGame.exe
├─ UI/
│  ├─ topbar_bg.png
│  ├─ stage_bg.png
│  ├─ footer_bg.png
│  └─ dialog_bg.png
└─ Tickets/
   └─ ThreeStar/
      ├─ ticket.png
      ├─ thumbnail.png
      └─ silver-star.png
```

## 圖片處理原則

- 正式 UI 與彩券底圖使用 PNG。
- 發行包建立時不得把 PNG 轉成 JPG、不得降低尺寸、不得重新取樣。
- 可做 PNG 的 lossless deflate/metadata 最佳化，但不得改變像素內容。
- 程式載入圖片時使用原始檔案，不先另存成較低畫質版本。
- `ticket.png` 的設計寬度以 1080 px 級為基準；程式只做等比例縮小或在最大化模式等比例呈現，不應將低解析圖放大後再存檔。

## 主程式 UI 資源

| 路徑 | 用途 | 建議尺寸 |
| --- | --- | --- |
| `UI/topbar_bg.png` | 主視窗頂部資訊列 | 1920×180 |
| `UI/stage_bg.png` | 中央彩券舞台 | 1920×900 |
| `UI/footer_bg.png` | 底部操作區 | 1920×240 |
| `UI/dialog_bg.png` | 使用者／選票等對話窗共通背景 | 1200×800 |

主程式 UI 資源屬於 ScratchGame 本體，不隨單張彩券 ScratchPack 更換。

## ThreeStar 目前測試資源

| 路徑 | 用途 |
| --- | --- |
| `Tickets/ThreeStar/ticket.png` | 三星連線彩券底圖；序號區不得烤入固定編號 |
| `Tickets/ThreeStar/thumbnail.png` | 「新的一張」選票卡片縮圖 |
| `Tickets/ThreeStar/silver-star.png` | 九宮格銀膜材質 |

## 互動規則

- 九宮格仍是 9 個獨立遮罩，各自保存刮除比例與 78% 完成狀態。
- 78% 只代表該區邏輯完成，不自動移除剩餘銀膜。
- 使用者按住滑鼠時，可從一個刮區連續拖到另一個刮區；外層刮獎容器統一追蹤滑鼠軌跡，再把軌跡分派給所經過的獨立遮罩。
- 9 個必要區域全部完成後才自動判定／兌獎；「全部刮開」則直接揭除所有區域並結算。
- 彩券序號由程式動態疊在底圖的序號底板上，不得固定畫在 `ticket.png`。
