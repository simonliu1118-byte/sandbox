# ScratchGame portable runtime package

ScratchGame 的 Theme、彩券美術與音效採 **EXE 外部資源**。GitHub Actions 的 Windows artifact 只代表可執行檔編譯成功，不是可直接交付的完整 portable package。

正式測試／交付 ZIP 必須通過 `tools/package_portable.py` 驗證，至少包含：

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

## 規則

- Header / Footer / Stage Theme 不內嵌 EXE。
- Header + Footer 屬同一 Frame Theme；Stage Theme 獨立。
- 缺少任一 required runtime asset 時，portable package 必須直接失敗，不得交付半套 ZIP。
- 程式執行時若找不到 Theme / ticket thumbnail source，會寫入 `%LOCALAPPDATA%\ScratchGame\logs\runtime-assets.log`。
- GitHub Actions artifact 名稱明確標示 `exe-only`，避免誤認為完整 portable package。
