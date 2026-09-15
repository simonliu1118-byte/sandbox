# ScratchGame Project Rules

本檔只保存 ScratchGame 的固定專案規則；repository 共通版本、開發、CI、Local-first 與 GitHub-final verification 規則依根 `REPOSITORY_RULES.md`。

## 1. 平台與發行

- 正式技術線：C# / .NET 8 / WPF。
- 目標平台：Windows x64。
- 正式發行以 self-contained portable folder 為目標，不要求使用者另外安裝 .NET。
- portable folder 至少包含 `ScratchGame.exe` 與正式 UI / Ticket / Audio 等外部資源；可替換美術不得強制嵌死在 EXE。
- Windows 視窗標題預設為「刮刮樂」。
- 主視窗只提供固定預設尺寸與最大化兩種正常使用狀態；禁止使用者任意拖曳邊框改變視窗尺寸。最大化時內容等比例放大，還原時回固定預設尺寸。

## 2. UI 與視覺責任邊界

- 主畫面以彩券本體為視覺核心；正式彩券與主要場景優先使用外部 PNG 美術，WPF 負責動態內容、刮除層、互動、文字與動畫。
- 主彩券舞台維持「背景 -> 彩券 -> 動態刮區/符號/票號 -> 硬幣 -> 中獎效果/結果框」的單純層級；不得再以多層 Border、托盤框、重複裝飾框壓在彩券背後。
- 每一個視覺資料只能有單一 owner：靜態美術由 PNG 負責；票號、程式面額、刮膜、符號、硬幣與結果效果由程式負責。禁止舊版文字/底塊先畫一次，再由 enhancement layer 覆蓋或刪除的補丁式做法。
- Dialog / Modal 應使用 ScratchGame 自畫風格，正常使用流程的 Info / Warning / Error / Confirm 不使用 Windows `MessageBox`。只有程式連自家 UI 都無法初始化的致命 fallback 才允許 Windows MessageBox。
- 若 Dialog 已有「取消」或「關閉」按鈕，不再額外放右上角重複 X。
- 文字與圖示不得因 DropShadowEffect 或整體 rasterization 變糊；陰影與文字本體應分層處理，文字優先使用清楚的 WPF text rendering。
- **彩券設計座標 Viewbox 只允許承載 1080×882 票面座標系內容。程式擁有的 Result Modal、按鈕、一般 UI 文字與其他視窗像素 UI 必須放在 Viewbox 外，以原生 WPF 尺寸呈現；不得跟著票面做非整數縮放。**
- 重要程式 UI 的 Viewbox ownership 必須有可機器驗證的 regression check；不得只依賴人工觀察文字是否模糊。
- 設定頁與長列表必須預留明確的垂直 ScrollBar 欄位；ScrollBar 外觀需符合深紅/金色主題，不可因資料量增加而造成內容左右跳動。

## 3. Canvas 與版型座標

- ScratchPack 使用官方固定 Canvas code；代碼一旦發布就永遠不得改變其尺寸語意，未來只新增新的 code。
- `canvas = 1` 固定代表 **1080 x 882**，作為目前基本橫式刮刮樂版型。
- 所有票面座標、刮區、符號、票號與程式面額都以彩券 Canvas 的設計座標描述，再由主程式整體等比例縮放；不得依目前視窗像素硬編排。
- 同一刮區的符號、 ScratchSurface 與銀膜必須共用同一份 layout geometry，不得各自維護另一套近似座標。

## 4. 使用者、Wallet 與遊玩統計

- 每個使用者資料彼此獨立。
- 新使用者初始 Wallet 固定為 `$100,000`。
- Wallet 是實際可購買彩券的可用資金；Wallet 不等於遊玩損益。
- 本機版只保存累積統計，不保存逐張玩家歷史，不保存彩券名稱、Package ID、GameType、批次、票號或逐張獎金快照。
- 累積統計至少包含：`wallet_balance`、`completed_ticket_count`、`win_count`、`total_spent`、`total_redeemed`、`max_prize`、`grant_count`、`grant_total_amount`。
- 勝率由 `win_count / completed_ticket_count` 即時計算；總損益由 `total_redeemed - total_spent` 即時計算，不另外保存重複 derived data。
- Wallet grant / 外部資金補充不算中獎、不算勝率、不算累計中獎，也不列入遊玩總損益；人物、圖片與文案只是 cosmetic presentation，資料層維持 generic grant 命名。
- 使用者切換視窗只負責使用者選擇、改名與 Wallet 摘要；詳細累積統計使用獨立「遊玩紀錄」Modal，不把完整統計塞在切換使用者列表。
- 每個使用者最多同時存在一張 Pending Ticket。
- Pending Ticket 必須綁定建立它的使用者；不得丟失或轉移到其他使用者。
- 正常關閉程式或切換使用者時，若目前有未完成彩券，必須用自畫 Confirm modal 說明並在使用者確認後直接揭曉原票、完成兌獎，再執行關閉或切換。
- Crash、停電或強制結束留下 Pending Ticket 時，下次該使用者進入時直接恢復原本已決定獎項的票並自動揭曉/兌獎；不得重抽。

## 5. 彩券定義、Pack lifecycle、發行量與票號

一張彩券定義至少固定包含：

- 名稱。
- 面額。
- gameType 與玩法參數。
- canvas code。
- 總發行張數 `issueSize`。
- 每本張數 `ticketsPerBook`。
- 完整獎項表（獎金 + 固定張數）。
- 由獎項表自動計算的發行時中獎率。
- 所需美術、版面與音效設定。

規則：

- `issueSize > 0`、`ticketsPerBook > 0`，且 `issueSize % ticketsPerBook == 0`；`bookCount = issueSize / ticketsPerBook` 由程式計算。
- 彩券一旦建立第一批，核心定義即鎖定，不得修改面額、總發行量、每本張數、獎項張數或中獎率。
- 同一彩券的每個新批次都必須使用完全相同的固定獎項表；新批次只代表重新建立完整票池。
- 若要不同獎項機率、張數或核心規則，必須建立新的彩券款式，而不是修改既有彩券。
- Built-in / Imported Pack lifecycle 以 `SCRATCHPACK_SPEC.md` 為唯一權威，不另設平行刪除規則。Imported Pack 即使已有完成遊玩統計仍可解除安裝；只有仍存在該 Pack Pending Ticket 時必須拒絕解除安裝。
- Pack 解除安裝不回滾使用者累積統計，因本機統計不依賴逐張 history 或 Pack snapshot。
- 虛擬票號格式為 `款式編號-本號-本內序號`，初始至少三位補零，例如 `001-023-057`；任一段超過 999 時自然增加位數，不截斷、不循環。
- 彩券款式編號與 `gameType` 是不同概念；多款彩券可共用同一 gameType。

## 6. 批次與售罄

- 每張彩券同時間只能有一個 Active 批次。
- 建立新批次會結束原 Active 批次；舊批次之後不可再抽票。
- Imported Pack 安裝成功時自動建立第 1 批；不要求使用者手動發行首次批次。
- 後續「發行新一批」屬高影響操作，設定頁每張彩券列內提供操作，執行前必須經兩次自畫確認。
- 批次售罄後不得自動建立下一批；由使用者手動決定是否發行新批次。
- 結算後如果目前同款 Active 批次已無剩餘票，左側動作按鈕必須 Disabled，文字顯示 **「本批次已售完」**；另一顆按鈕顯示 **「挑其他款」**。
- 正常仍有票時，結算按鈕文字為 **「再來一張」** 與 **「挑其他款」**。

## 7. 有限票池與交易一致性

- 票池不是無限機率亂數；每個 Active 批次使用固定張數的有限獎項池。
- 建立 Pending Ticket 時，在同一 SQLite transaction 內依剩餘張數加權抽出固定獎項、將該獎項 available count 減 1，並建立包含既定獎項的 Pending Ticket。
- 正常兌獎只完成該 Pending Ticket、更新 Wallet 與累積遊玩統計；不得再次扣獎池，也不得建立逐張 history。
- 尚未開始刮獎前允許「換一張」：同一 transaction 內把舊 Pending 對應獎項放回 available count，再抽取新獎項並建立新 Pending；本次換票不得重複扣 Wallet 或增加 `total_spent`。
- 一旦開始刮獎就不可換票，只能完成原票。
- 所有獎池、Pending Ticket、Wallet、投入、兌獎與累積統計變更必須使用 SQLite transaction 原子完成，避免 crash 造成資料不一致。

## 8. 購票、兌獎與資金

- 建立 Pending Ticket 時立即從 Wallet 扣除面額，並增加 `total_spent`。
- Wallet 小於彩券面額時不得建立 Pending Ticket。
- 程式異常恢復 Pending Ticket 時不得再次扣款或再次計入投入。
- 正常兌獎時將獎金加入 Wallet 與 `total_redeemed`，並更新 `completed_ticket_count`、`win_count`、`max_prize`。
- 最終個人遊玩損益 = 累計兌獎 - 累計投入；wallet grant 不納入此公式。
- 外部資金補充的 UI 名稱可替換或角色化，但內部資料與 service 不使用人物名稱。

## 9. Game Rule / gameType 相容性

- ScratchPack 只選擇主程式已公開支援的 gameType，不得在彩券包內攜帶任意遊戲程式碼。
- `gameType` 使用字串，例如 `"1"`、未來變體可使用 `"1-2"`；不使用 `gameVersion`。
- 已發布 gameType 的核心勝負判定與語意永遠不修改。新的核心規則或會改變勝負判定的變體必須新增新的 gameType，不得偷偷改舊規則。
- 既有 gameType 可以新增不改變核心勝負判定的 optional 參數；舊 ScratchPack 沒有該參數時，必須套用能保持舊行為的明確 default，確保向下相容。
- Prize Pool / Prize Tier 保持通用資料，只保存獎金與張數；玩法引擎負責依抽中的 payout 產生合法結果，不把玩法專屬 outcome 欄位污染通用獎池資料。

### gameType `"1"` — 星星連線

- `gridSize` 目前允許 3、4、5。
- N x N 必須完整 N 顆星成一線：3x3 = 三星一線、4x4 = 四星一線、5x5 = 五星一線。
- 只計算所有完整橫列、完整直列與兩條完整大對角線。
- 短斜線、局部連線、Wildcard 或其他替代規則不屬於 gameType `"1"`。
- 可使用不改變中獎判定的生成參數，例如 `allowNearMiss`；缺省值與正式契約以 `GAMETYPE_SPEC.md` 為準。

## 10. ScratchPack 與資源

- ScratchPack schema / ResourceRef / Built-in registry 以 `SCRATCHPACK_SPEC.md` 為唯一權威，本檔不複製第二套 schema。
- 外部彩券包副檔名為 `.scratchpack`。
- Built-in Pack 與 Imported Pack 的 `.scratchpack` 內容格式相同；Built-in / Imported 只屬 runtime installation source。
- ScratchPack 只允許資料與資源，不允許攜帶或執行任意程式碼、DLL、EXE 或 script。
- `priceDisplay = 0` 代表底圖已包含完整面額美術，主程式完全不畫；`priceDisplay = 1` 時主程式依 Pack 提供的正式 area 繪製標準面額徽章。
- 票號只由程式動態繪製，不得把舊票號文字燒在正式 PNG 裡。
- Built-in ticket resource 依 GameType namespace；foil 為跨 GameType 共用資源。具體 ResourceRef 語意只以 `SCRATCHPACK_SPEC.md` 為準。

## 11. 刮獎輸入與硬幣

- 每個刮獎區必須是獨立 ScratchSurface；例如九宮格就是 9 個互不干擾的刮膜區。
- 單一刮獎區達完成門檻時只標記該區已完成，不強制清除剩餘銀膜；使用者仍可繼續刮乾淨。
- 只有所有必要刮獎區都達完成門檻後，才執行自動判定與兌獎。
- 「全部刮開」直接清除所有必要刮膜並完成兌獎。
- 自畫硬幣與實際刮除必須使用同一個 mouse event pipeline 與同一個座標點；不得建立第二套 MouseMove 追蹤造成硬幣與刮點分離。
- Stage 上存在尚未兌獎 Pending Ticket 時，游標進入整個 Stage 範圍即顯示硬幣正面；離開 Stage 使用一般 Windows 游標。
- 實際按住刮獎時硬幣切換成側立/傾斜狀態；兌獎完成後必須立刻恢復一般 Windows 游標。

## 12. 設定與挑選彩券 UI

- 挑選彩券預設顯示所有啟用且 Active 批次仍有庫存的彩券；面額篩選提供「全部、100、200、300、500、1000、2000、5000」等官方選項。
- 面額篩選使用緊湊卡片呈現，不另外放重複的「面額」標籤；篩選器不得不必要地佔用彩券列表垂直空間。
- 挑彩券卡固定資訊順序為：**彩券名稱 -> `$面額` -> 縮圖 -> `最高獎金 N 元!` -> `中獎率` + 數值 -> `YYYY/MM/DD．第N批`**。
- 彩券名稱不得以 ellipsis 截斷；優先換行，必要時只縮小字級/整體文字以完整顯示。
- 發行日期直接使用目前 Active Batch 的 `started_utc` 衍生顯示，不另存重複發行日期欄位。
- 目前挑券視窗以每排 5 張、完整可見 2 排為基準；10 張卡片不應因版型浪費空間而出現垂直 ScrollBar。
- 設定頁彩券列採 accordion：點一列展開詳細資料，再點同一列收起；點其他列時原列收起、新列展開。
- Pack lifecycle UI 使用「解除安裝／隱藏／解除隱藏」語意，不再建立停用/刪除等平行概念；Built-in Pack 不允許解除安裝。

## 13. 中獎音效與效果

- 手動把所有刮區自行刮完與「全部刮開」／系統自動揭曉可使用不同提示版本，避免手動刮獎時提前暴雷。
- 小獎與大獎音效可分開；目前大獎門檻可依現行設計使用 50,000 元以上，後續音效 cosmetic slots 依 TODO 演進，不改變 Prize 判定。
- 頭獎與二獎可有不同預設 WPF 中獎效果；結果框背景需保留足夠透明度，讓玩家仍看得到背後中獎票面。
- 結果框與其文字屬程式 UI，必須在票面 Viewbox 外呈現，不得以縮放後文字換取定位方便。

## 14. 資料與備份

- 正式資料使用 SQLite，runtime database 預設位於 `%LOCALAPPDATA%\ScratchGame`，不跟 EXE 資料夾綁定，更新程式時不得因刪除 portable folder 而刪除使用者資料。
- 一般自動備份：程式啟動時檢查，距最近成功備份達 3 天才建立新備份。
- 備份最多保留最近 5 份，超過即刪除最舊一份。
- 資料庫 schema migration 前無論距離上次備份多久，都必須先建立安全備份；完成後仍以最多 5 份輪替。
- 0.x 測試階段若使用者明確同意舊測試資料可捨棄，不為開發期逐張歷史建立不必要的 migration 特例。
- 使用者執行資料與備份不得提交至 Git。
