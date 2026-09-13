# ScratchGame

Windows 單機刮刮樂娛樂程式。

- 顯示名稱：`刮刮樂`（Windows 視窗標題）
- 技術：C# / .NET 8 / WPF
- 發行：Windows x64、self-contained、portable
- 版本：見 `VERSION` / `BUILD`

## 核心方向

主畫面以票券為核心，中央保留最大可用空間給目前彩券；目前彩券名稱與面額顯示在票券區附近。面額與版型不常駐主畫面，按「新的一張」後再選擇。

程式支援：

- 多使用者，各自獨立統計投入、兌獎與損益。
- 多面額、多彩券定義與有限獎項池。
- 每張彩券固定發行量、固定獎項表與固定公布中獎率。
- 同一彩券可建立新批次；新批次只重置相同獎池，不改變獎項機率。
- 每張彩券同時間只有一個 Active 批次。
- 每個使用者最多一張 Pending Ticket。
- 支援內建通用版型與 `.scratchpack` 外部彩券包。
- SQLite 本機資料；每 3 天自動備份，最多保留 5 份。

詳細固定規則見 `PROJECT_RULES.md`，V0.1 功能範圍見 `REQUIREMENTS.md`，外部彩券包規格見 `SCRATCHPACK_SPEC.md`。
