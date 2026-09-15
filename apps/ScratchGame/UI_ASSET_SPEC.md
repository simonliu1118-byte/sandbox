# ScratchGame UI / Runtime Asset Spec

本檔描述 ScratchGame 主程式外部視覺與音效資源。ScratchPack 正式格式、ResourceRef 與 Built-in ticket / foil registry 仍以 `SCRATCHPACK_SPEC.md` 為唯一權威；本檔不另建 Pack schema 或 Built-in registry。

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
BuiltInAssets/
├─ Tickets/
│  └─ gameType1/
│     ├─ 01-red.png
│     ├─ 01-blue.png
│     └─ 02.png
└─ Foils/
   ├─ brushed-silver-plain.png
   └─ brushed-silver-three-star.png
UI/
└─ grant-overlay-01.png
Audio/
├─ small-win-manual.wav
├─ small-win-auto.wav
├─ big-win-manual.wav
├─ big-win-auto.wav
├─ wallet-grant.wav
└─ lose.wav
```

Theme、UI effect、ticket / foil Built-in asset 與 audio 都是 EXE 外部資源；可替換的視覺／音效不強制嵌入 `ScratchGame.exe`。

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

## 5. Ticket runtime artwork

- `canvas=1` 固定 1080×882。
- ScratchPack Built-in ticket / foil 實體資源位於 `BuiltInAssets/`，公開 ref 與解析規則仍只由 `SCRATCHPACK_SPEC.md` 定義。
- 挑券縮圖不是獨立 Pack 檔案；主程式在 Pack 安裝後產生 360×294 runtime cache，遺失時可重建。
- TicketBackgroundImage、symbol、ScratchSurface、hit geometry 必須使用同一份 Pack Canvas / `scratch.zones` 設計座標。
- `Tickets/ThreeStar/` 僅可作舊本機資料相容資源，不是 V0.4 ScratchPack 的正式第二套票面來源。

## 6. Wallet grant Stage effect

`UI/grant-overlay-01.png` 是目前「黃仁勳給我錢」的 Stage overlay 素材；人物／文案只是目前 UI skin，資料層與程式欄位維持 generic wallet grant 命名，未來可替換成其他人物或文案而不改 schema。

播放規則：

- 按下主畫面錢包旁的 grant 按鈕後，錢包交易成功才播放。
- 效果層限制在 Stage，不覆蓋 Header / Footer。
- PNG 本身保持單張透明素材；動畫由 WPF 程式控制，不製作 GIF 或影片。
- 總長約 **3 秒**。
- 前約 2 秒以左右晃動、輕微平移與週期性放大縮小呈現；2～3 秒逐步 fade out。
- 動畫播放期間 grant 按鈕暫時不可再次觸發，結束後恢復。
- UI effect / audio 缺失不得影響已成功完成的錢包交易，但正式 portable package 驗證仍要求資源齊全。

## 7. Audio

- `< $50,000`：small win。
- `>= $50,000`：big win。
- 手動刮完使用 `*-manual.wav`；全部刮開 / 系統自動揭曉使用 `*-auto.wav`。
- `lose.wav`：未中獎結果音效；目前使用裁切後的短笑聲。
- `wallet-grant.wav`：錢包 grant Stage effect 音效；目前使用裁切後的金幣聲。
- Audio failure 屬 cosmetic failure，不得回滾兌獎或錢包交易；但正式 portable package 缺必要音效仍視為不完整。

## 8. Application icon

ScratchGame application icon 的程式建置唯一入口：

```text
apps/ScratchGame/src/ScratchGame/Assets/ScratchGame.ico
```

規則：

- 目前正式圖示為紅金刮刮樂票＋金幣圖案。
- 正式圖示的高解析 master PNG 保存在 Asset Library；repository 的 `.ico` 是 Windows / C# resource compiler 實際驗證通過的 runtime 版本，目前包含 **16×16 與 32×32 的 32-bit DIB entries**。Windows 可依 Shell / DPI 需求縮放顯示；若未來增加更高尺寸，必須先通過同一套 Windows Build / Shell / startup CI，不能再次使用非標準 ICO。
- `ScratchGame.csproj` 的 `ApplicationIcon` 與主視窗 XAML `Icon` 都引用同一個 `ScratchGame.ico`。桌面捷徑、開始功能表與工作列不得另維護第二份 icon 圖檔。
- Windows 桌面捷徑、開始功能表與工作列一律由最終 EXE 的 application icon 取得。
- **禁止在 single-file publish 完成後，再用 `BeginUpdateResource`、resource editor 或其他方式改寫最終 EXE。** .NET single-file bundle 將 runtime / application payload 附加於 PE；publish 後重寫 PE 可能截掉 bundle並造成 EXE 無法啟動。
- CI 必須直接驗證 untouched published EXE：self-contained bundle 大小合理、Windows 能解析 associated icon，且 EXE 能通過實際 startup smoke test。
- Asset Library 的 `AppIcon/` 保存 approved master PNG 與同版 ICO 作為美術素材備份；程式建置仍只讀 repository 的 `Assets/ScratchGame.ico`。

## 9. Packaging

GitHub Actions 只產出並上傳 `exe-only` artifact。完整 portable package 必須由 `tools/package_portable.py` 驗證 required runtime resources 後建立；缺任一必要 Theme、Built-in resource、UI effect 或 Audio 檔即失敗。
