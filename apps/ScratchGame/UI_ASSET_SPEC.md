# ScratchGame UI / Runtime Asset Spec

本檔描述 ScratchGame 主程式外部視覺資源與目前內建 ThreeStar 測試票資源。ScratchPack 正式格式仍以 `SCRATCHPACK_SPEC.md` 與 docs-only V1 設計文件為準。

## 1. Portable runtime structure

```text
ScratchGame.exe
Themes/
└─ Default/
   ├─ Frame/
   │  ├─ header_bg.png
   │  └─ footer_bg.png
   └─ Stage/
      └─ stage_bg.png
Tickets/
└─ ThreeStar/
   ├─ ticket.png
   ├─ ticket-100.png
   └─ silver-star.png
Audio/
├─ small-win-manual.wav
├─ small-win-auto.wav
├─ big-win-manual.wav
└─ big-win-auto.wav
```

Theme / ticket / audio 都是 EXE 外部資源；目前不把可替換 Theme 嵌入 `ScratchGame.exe`。

## 2. Theme model

- **介面框架（Frame Theme）**：Header + Footer 成套，不允許跨框架混搭。
- **舞台主題（Stage Theme）**：中央 Stage，可獨立於 Frame Theme 裝備。
- 預設 Frame Theme：**新春紅金**。
- 預設 Stage Theme：**招財好運**。

正式圖尺寸：

| 檔案 | 尺寸 | 責任 |
| --- | ---: | --- |
| `Themes/Default/Frame/header_bg.png` | 1920×144 | 紅金 Header、美術字 `刮刮樂` / `刮出好運・樂在每一刻`；不含動態玩家資料與按鈕 |
| `Themes/Default/Stage/stage_bg.png` | 1920×900 | 招財貓、燈籠、花、金幣、右側好運常在與中央舞台；不含彩券與功能列 |
| `Themes/Default/Frame/footer_bg.png` | 1920×156 | Footer 紅金底；不包含操作按鈕 |

## 3. Header / Stage / Footer 邊界

Header、Stage、Footer 是三個獨立圖片區。兩條粗金色分隔線由 WPF 程式繪製，**不屬任何 Theme PNG**，目前標準高度為 **4 px**：

```text
Header image
program gold separator
Stage image
program gold separator
Footer image
```

圖片不可跨 Row 壓到 separator。Header 與 Footer 正式圖應保持純圖片，不得自行烤入額外程式邊界線；真正邊界一律以程式 separator 為準。

## 4. Stage empty state

- 不使用整片中央咖啡色 panel。
- 無彩券時，只在星號、提示文字與「挑一張彩券」按鈕後方放小範圍半透明淺色卡片。
- Stage 其他區域保持完整可見。

## 5. ThreeStar runtime ticket art

- `canvas=1` 固定 1080×882。
- `ticket.png`：目前 $500 / 測試票底圖。
- `ticket-100.png`：目前 $100 票底圖。
- `silver-star.png`：163×101 九宮格銀膜材質。
- 挑選彩券不要求獨立 thumbnail；直接由 ticket artwork 縮圖顯示。
- TicketBackgroundImage、symbol、ScratchSurface、hit geometry 必須使用同一 1080×882 設計座標。

## 6. Audio

- `< $50,000`：small win。
- `>= $50,000`：big win。
- 手動刮完使用 `*-manual.wav`；全部刮開 / 系統自動揭曉使用 `*-auto.wav`。
- Audio 缺失不得視為完整 portable package。

## 7. Packaging

GitHub Actions 只產出並上傳 `exe-only` artifact。完整 portable package 必須由 `tools/package_portable.py` 驗證 required runtime resources 後建立；缺任一 Theme / Ticket / Audio 檔即失敗。
