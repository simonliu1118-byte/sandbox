# ScratchGame Project Rules

本檔只保存 ScratchGame 的固定專案規則；repository 共通版本、開發、CI、Local-first 與 GitHub-final verification 規則依根 `REPOSITORY_RULES.md`。

ScratchPack schema / ResourceRef / Built-in registry 的唯一權威為 `SCRATCHPACK_SPEC.md`；GameType 契約的唯一權威為 `GAMETYPE_SPEC.md`。本檔只保存會長期反覆適用的產品原則，不平行複製完整 schema 或玩法規格。

## 1. 產品、平台與版本線

- 正式技術線：C# / .NET 8 / WPF；目標平台 Windows x64。
- 正式發行以 self-contained portable folder 為目標，不要求使用者另外安裝 .NET。
- `ScratchGame.exe` 是玩家主程式；`PackEditor.exe` 是 ScratchGame 的附屬製作工具，source 必須位於 `apps/ScratchGame/` 底下，不建立另一個產品專案。
- ScratchGame 與 PackEditor 共用同一份 `apps/ScratchGame/VERSION` / `BUILD`；PackEditor 不得另建自己的 VERSION、BUILD、PROJECT_RULES、TODO 或另一條產品版號。
- 可替換 UI / Ticket / Audio / Theme 等美術資源維持 EXE 外部資源，不強制嵌死在 EXE。
- Windows 主視窗標題預設為「刮刮樂」。

## 2. UI 與視覺責任邊界

- 主畫面以彩券本體為視覺核心；正式彩券與主要場景優先使用外部 PNG 美術，WPF 負責動態內容、刮除層、互動、文字與動畫。
- 每一個視覺資料只能有單一 owner：靜態美術由 PNG 負責；票號、程式面額、刮膜、符號、硬幣、結果效果與程式 UI 由程式負責。
- 票面設計座標可放在 `Viewbox` 內等比例縮放；程式 UI / Modal / 狀態文字等需要清晰文字的元素不得依附票面 Viewbox 做 fractional scaling。
- 文字與圖示不得因 DropShadowEffect 或父層 rasterization 變糊；陰影與文字本體應分層處理。
- 正常使用流程的 Info / Warning / Error / Confirm 使用 ScratchGame 自畫 Dialog / Modal；只有程式連自家 UI 都無法初始化的致命 fallback 才允許 Windows MessageBox。
- Header 負責程式層級功能；Footer 負責玩家狀態、遊戲操作與唯一的 `StatusText` 呈現。Stage 不另外建立第二套一般狀態列。

## 3. Canvas 與版型座標

- ScratchPack 使用官方固定 Canvas code；代碼一旦發布就不得改變其尺寸語意，只能新增新 code。
- `canvas = 1` 固定代表 **1080 × 882**。
- 所有票面座標、刮區、符號、票號與程式面額都以彩券 Canvas 設計座標描述，再由主程式等比例縮放；不得依目前視窗像素硬編。
- 同一刮區的符號、ScratchSurface 與銀膜必須共用同一份 geometry，不得維護第二套近似座標。

## 4. 使用者、Wallet 與累積統計

- 每個使用者資料彼此獨立；每個使用者最多同時存在一張 Pending Ticket，且 Pending Ticket 必須綁定建立它的使用者，不得轉移。
- 新使用者初始 Wallet 為 `$100,000`；購票餘額不足時不得建立新 Pending Ticket。
- 本機版只保存**累積統計**，不保存逐張玩家遊玩歷史，也不因 Pack 解除安裝而回滾既有累積統計。
- 目前累積欄位包含：`wallet_balance`、`completed_ticket_count`、`win_count`、`total_spent`、`total_redeemed`、`max_prize`、`grant_count`、`grant_total_amount`。
- 勝率與總損益等可推導資料不另存；總損益為 `total_redeemed - total_spent`。
- Wallet grant / 外部資金補充不算中獎、不增加 `win_count` / `total_redeemed`，也不納入遊玩損益；程式與 database 維持 generic wallet grant 命名。
- 詳細逐張歷史只有未來若發展成連線／server-side 稽核需求時才另行設計，不得反過來讓目前單機版依賴逐張 history。

## 5. ScratchPack 身分與 lifecycle

- Built-in Pack 與 Imported Pack 的 `.scratchpack` 內容格式完全相同；BuiltIn / Imported 只屬 runtime installation source，不寫入 Pack schema。
- `manifest.packageId` 是 Pack 的永久唯一身分。相同 packageId 再次匯入視為同一 Pack 身分衝突，不作為「更新版本」入口。
- **PackEditor 只用來建立新的 Pack，不提供開啟、修改、覆寫或另存既有 `.scratchpack` 的功能。** 每次「新增 Pack」建立新的 draft 並自動產生新的 UUID v4 packageId；一般使用者不得手動指定或重用既有 packageId。
- 0.x 單機版不建立 Pack update / replace / upgrade framework。若要調整既有 Pack 的內容，重新建立一個新 Pack，使用新的 packageId，再由使用者自行決定是否保留／解除安裝舊 Pack。
- 正式 Built-in Pack 也由 PackEditor 建立；使用者確認最終 `.scratchpack` 後，發行流程原樣放入 `BuiltInPacks/`，主程式不得維護另一套 Built-in 專用 Pack schema。
- Imported Pack 可隱藏／取消隱藏／解除安裝；Built-in Pack 可隱藏但不可解除安裝。
- Imported Pack 若仍有 Pending Ticket，解除安裝必須拒絕；沒有 Pending Ticket 時可解除安裝，即使該 Pack 曾有完成遊玩統計也不阻擋。

## 6. 彩券定義、批次與有限票池

- `issueSize > 0`、`ticketsPerBook > 0`，且 `issueSize % ticketsPerBook == 0`；總本數由程式推導。
- 每張彩券同時間只能有一個 Active 批次；新批次使用相同 Pack 定義重新建立完整有限票池。
- 批次售罄後不得自動建立下一批，由使用者手動決定是否發行新批次。
- 票池不是無限機率亂數。建立 Pending Ticket 時即在同一 SQLite transaction 內依 Remaining 權重抽出固定獎項並立即扣減 Remaining。
- 尚未開始刮獎前允許「換一張」：同一 transaction 內把舊 Pending 獎項放回 Remaining，再抽取新獎項；不得重複扣 Wallet 或重複增加 `total_spent`。
- 一旦開始刮獎就不可換票，只能完成原票。
- 兌獎時將既定獎金加入 Wallet / `total_redeemed`，並更新完成張數、中獎張數與最大獎；正常兌獎不得再次扣獎池。
- 票池、Pending Ticket、Wallet 與累積統計的關聯變更必須使用 SQLite transaction 保持一致性。

## 7. GameType 與 ScratchPack 權威邊界

- GameType 核心規則、合法公開參數、Prize Tier mapping、geometry 與 Renderer / Generator 契約只由 `GAMETYPE_SPEC.md` 定義。
- 已發布 GameType 的核心勝負判定與語意不得偷偷改變；需要不同核心規則時新增 GameType / variant。
- ScratchPack 封裝、manifest、ticket schema、ResourceRef、Built-in registry 與 Canvas 規格只由 `SCRATCHPACK_SPEC.md` 定義。
- ScratchPack 不攜帶任意玩法程式碼、DLL、EXE、script 或 macro。
- Prize Pool 保持通用資料；玩法專屬 outcome 不得污染通用 prize schema。

## 8. PackEditor

- PackEditor 是附屬工具，不是第二套 ScratchPack 規則來源。
- PackEditor 與 ScratchGame Importer / runtime 必須共用同一份 ScratchPack model / loader / validator 權威來源；不得複製一份 editor-only validator。
- PackEditor 只能輸出主程式當下正式支援的 Canvas、GameType、ResourceRef 與合法參數。
- PackEditor 輸出前必須建立真實 `.scratchpack`，再以正式 `ScratchPackV1Loader` round-trip 驗證；只有驗證成功才能輸出。
- PackEditor 的中獎率、未中獎張數、最高獎金、平均獎金（期望值）、獎金回饋率等皆為衍生顯示，不寫入 ScratchPack schema。

## 9. 刮獎輸入、結果與音效

- 每個必要刮區使用獨立 ScratchSurface；所有必要區完成後才結算，`全部刮開` 可直接揭曉並完成兌獎。
- Stage 有未兌獎 Pending Ticket 時，游標進入整個 Stage 顯示硬幣；離開 Stage 使用一般 Windows 游標。兌獎完成後應立即恢復一般游標。
- 自畫硬幣與實際刮除必須使用同一 mouse event pipeline / 座標點，不建立第二套 MouseMove 追蹤。
- 中獎／大獎／未中獎／wallet grant 的音效與效果屬 presentation，不得影響中獎判定、Prize Tier、Wallet transaction 或 Pack schema。

## 10. 資料與備份

- 正式資料使用 SQLite，runtime database 預設位於 `%LOCALAPPDATA%\ScratchGame`，不跟 portable folder 綁定；更新程式不得因刪除 portable folder 而刪除使用者資料。
- 一般自動備份：程式啟動時檢查，距最近成功備份達 3 天才建立新備份。
- 備份最多保留最近 5 份。
- database schema migration 前無論距離上次備份多久，都必須先建立安全備份；migration 完成後仍按最多 5 份輪替。
- 使用者 runtime database、log、cache 與備份不得提交至 Git。
