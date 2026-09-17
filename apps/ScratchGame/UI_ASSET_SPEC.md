# ScratchGame UI / Runtime Asset Spec

更新日期：2026/09/18

本檔描述 ScratchGame 主程式外部視覺、音效與 Windows application icon 資源。ScratchPack 正式格式、ResourceRef 與 Built-in ticket / foil registry 仍以 `SCRATCHPACK_SPEC.md` 為唯一權威；本檔不另建 Pack schema 或 Built-in registry。

## 1. Portable runtime structure

```text
ScratchGame.exe
PackEditor.exe
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
TestPacks/
└─ ThreeStar-Test.scratchpack
```

Theme、UI effect、ticket / foil Built-in asset 與 audio 都是 EXE 外部資源；可替換的視覺／音效不強制嵌入 `ScratchGame.exe`。`TestPacks/` 是目前開發／測試 portable 的驗證 fixture，保留規則見 `PROJECT_RULES.md` / `RUNTIME_PACKAGE.md`。

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
- `Tickets/ThreeStar/` 僅可作舊本機資料相容資源，不是現行 ScratchPack 的正式第二套票面來源。

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

目前 application icon 使用 **repository 內的 256×256 master PNG → build-time generated multi-size ICO**，不再維護 hand-edited `.ico` 作 source of truth。

ScratchGame master：

```text
apps/ScratchGame/src/ScratchGame/Assets/ScratchGame-icon-source.png
```

PackEditor master：

```text
apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png
```

共同規則：

- 兩個 master PNG 都必須是 256×256；CI 會直接驗證尺寸。
- `tools/GenerateWindowsIcon.ps1` 在 build 前產生 `obj/*.generated.ico`。
- `ScratchGame.csproj` / `PackEditor.csproj` 的 `ApplicationIcon` 指向各自 generated ICO；不得平行維護第二套 application icon source。
- generated ICO 必須包含 Windows native sizes：`16, 20, 24, 28, 32, 40, 48, 64, 72, 80, 96, 128, 256`。
- Windows CI 直接解析 generated ICO directory，確認尺寸與 image offset 合法。
- publish 後的 `ScratchGame.exe` / `PackEditor.exe` 必須能由 Windows `ExtractAssociatedIcon` 解析出 application icon。
- **禁止在 single-file publish 完成後，再用 `BeginUpdateResource`、resource editor 或其他方式改寫最終 EXE。** .NET single-file bundle 將 runtime / application payload 附加於 PE；publish 後重寫 PE 可能截掉 bundle並造成 EXE 無法啟動。
- CI 必須驗證 untouched published EXE：self-contained bundle 大小合理、無意外 loose DLL、Windows associated icon 可解析、兩個 EXE 都通過 startup smoke。

## 9. Packaging

完整 portable 由 `tools/package_portable.py` 組裝並驗證；缺任一 required Theme、Built-in resource、UI effect、Audio 或 required TestPack 即失敗。

目前 Windows CI 會直接：

1. publish `ScratchGame.exe` / `PackEditor.exe`；
2. 建立 deterministic `ThreeStar-Test.scratchpack`；
3. 驗證 `runtime-assets.json` 的 size / SHA-256；
4. 組完整 portable；
5. post-ZIP 再驗證內容；
6. 上傳完整 `ScratchGame-V<version>-BuildN-portable-win-x64` Actions Artifact。

因此舊的「Actions 只上傳 exe-only artifact」敘述已廢止；目前 CI 的產品驗證單位是**完整 portable package**。