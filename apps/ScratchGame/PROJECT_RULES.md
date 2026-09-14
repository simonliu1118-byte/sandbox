# ScratchGame Project Rules

本檔只保存 ScratchGame 的固定專案規則；repository 共通版本、開發、CI、Local-first 與 GitHub-final verification 規則依根 `REPOSITORY_RULES.md`。

ScratchPack schema 的唯一正式來源是 `SCRATCHPACK_SPEC.md`；GameType 規則的唯一正式來源是 `GAMETYPE_SPEC.md`。本檔不重複欄位表或各 GameType 詳細規則。

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
- PNG / WAV 等外部資源對主程式是 opaque render asset；主程式可驗證檔案存在、格式、尺寸與能否解碼，但不得靠 OCR、像素分析、檔名或圖片文字推導業務資料。
- 圖片可含宣傳性固定文字；這只屬視覺內容。遊戲邏輯與統計只認 ScratchPack 結構化資料與 runtime database。
- Dialog / Modal 使用 ScratchGame 自畫風格；正常流程的 Info / Warning / Error / Confirm 不使用 Windows `MessageBox`。只有自家 UI 無法初始化的致命 fallback 才允許 Windows MessageBox。
- 若 Dialog 已有「取消」或「關閉」按鈕，不再額外放右上角重複 X。
- 文字與圖示不得因 DropShadowEffect 或整體 rasterization 變糊；陰影與文字本體分層處理，文字優先使用清楚的 WPF text rendering。
- 設定頁與長列表必須預留明確的垂直 ScrollBar 欄位；ScrollBar 外觀符合深紅/金色主題，不可因資料量增加造成內容左右跳動。

## 3. Canvas 與版型座標

- ScratchPack 使用官方固定 Canvas code；代碼一旦發布就永遠不得改變其尺寸語意，未來只新增新的 code。正式 code 對照以 `SCRATCHPACK_SPEC.md` 為準。
- 所有票面座標、刮區、符號、票號與程式面額以彩券 Canvas 的設計座標描述，再由主程式整體等比例縮放；不得依目前視窗像素硬編排。
- 同一刮區的符號、ScratchSurface 與銀膜共用同一份 layout geometry，不得各自維護另一套近似座標。

## 4. 使用者

- 使用者檔案彼此獨立保存累計投入、累計兌獎、累計損益與遊玩歷史。
- 不做初始資金、補充資金或餘額不足限制；損益可為負值。
- 每個使用者最多同時存在一張 Pending Ticket。
- Pending Ticket 必須綁定建立它的使用者；不得丟失或轉移到其他使用者。
- 使用者歷史至少保存日期時間、彩券、面額、批次與最終獎金。
- 正常關閉程式或切換使用者時，若目前有未完成彩券，必須用自畫 Confirm modal 說明並在使用者確認後直接揭曉原票、完成兌獎，再執行關閉或切換。
- Crash、停電或強制結束留下 Pending Ticket 時，下次該使用者進入時直接恢復原本已決定獎項的票並自動揭曉/兌獎；不得重抽。

## 5. 彩券定義、發行量與票號

- 正式彩券的定義以 ScratchPack 為來源；主程式不得為某一張正式彩券保留第二套專屬 schema 或硬編碼 Prize Tier。
- 一張正式彩券的必要資料與格式由 `SCRATCHPACK_SPEC.md` 定義；玩法契約由 `GAMETYPE_SPEC.md` 定義。
- 彩券一旦建立第一批，核心定義即鎖定；若要不同面額、獎項機率、張數或核心規則，建立新的彩券款式，不修改既有款式。
- 同一彩券每個新批次使用完全相同的固定定義；新批次只重新建立完整票池。
- 已發行過的彩券不得真正刪除，只能依產品規則停用；歷史必須可追溯。
- 虛擬票號格式固定為 `款式編號-本號-本內序號`；各段至少三位補零，超過三位自然增加，不截斷、不循環。
- 彩券款式編號與 `gameType` 是不同概念；多款彩券可共用同一 GameType。

### 內建基礎 Pack

- 第一款「三星連線」是 Built-in Base Pack，不是主程式硬編碼特例。
- Built-in Base Pack 必須完整符合與外部 Pack 相同的 `SCRATCHPACK_SPEC.md` + `GAMETYPE_SPEC.md`，並走相同 loader / validator / engine / renderer pipeline。
- Built-in Base Pack 隨程式提供、免使用者手動匯入，且不可刪除／解除安裝。
- Built-in Base Pack 允許停用／重新啟用；停用只影響新票選擇，不移除 Pack、批次或歷史資料。
- `BuiltIn` / `Imported` 是本機 runtime 安裝來源狀態，不是 ScratchPack 可自行宣稱的 schema 欄位。

## 6. 批次與售罄

- 每張彩券同時間只能有一個 Active 批次。
- 建立新批次會結束原 Active 批次；舊批次之後不可再抽票。
- 發行新批次屬高影響操作，設定頁每張彩券列內提供「發行新一批」操作，執行前必須經兩次自畫確認。
- 批次售罄後不得自動建立下一批；由使用者手動決定是否發行新批次。
- 結算後如果目前同款 Active 批次已無剩餘票，左側動作按鈕 Disabled，文字顯示 **「本批次已售完」**；另一顆按鈕顯示 **「挑其他款」**。
- 正常仍有票時，結算按鈕文字為 **「再來一張」** 與 **「挑其他款」**。

## 7. 有限票池與交易一致性

- 票池不是無限機率亂數；每個 Active 批次使用固定張數的有限獎項池。
- 不使用業務層 `Reserved` 狀態。建立 Pending Ticket 時即在同一 SQLite transaction 內從 Remaining 依剩餘張數加權抽出固定獎項、將該獎項 Remaining 立即減 1，並建立包含既定獎項的 Pending Ticket。
- 正常兌獎只完成該 Pending Ticket、寫入兌獎與歷史；不得再次扣獎池。
- 尚未開始刮獎前允許「換一張」：同一 transaction 內把舊 Pending 對應獎項放回 Remaining，再抽取新獎項並建立新 Pending；本次換票不得重複計入投入。
- 一旦開始刮獎就不可換票，只能完成原票。
- 所有獎池、Pending Ticket、投入/兌獎與歷史變更必須使用 SQLite transaction 原子完成，避免 crash 造成獎池或金額不一致。

## 8. 新票建立與投入

- 建立 Pending Ticket 時立即計入該使用者的投入金額（面額）。
- 程式異常恢復 Pending Ticket 時不得再次計入投入。
- 正常兌獎才計入兌獎金額。
- 最終個人損益 = 累計兌獎 - 累計投入。

## 9. GameType 相容性

- ScratchPack 只能選主程式已公開支援的 GameType，不得攜帶任意遊戲程式碼。
- 已發布 GameType 的核心勝負判定與語意永遠不修改；改變核心規則時新增新的 GameType / variant ID。
- 既有 GameType 可以新增不改變核心勝負判定的 optional 參數；舊 Pack 缺少新欄位時必須套用保持舊行為的明確 default。
- Prize Pool / Prize Tier 保持通用資料；玩法專屬 outcome 不污染通用 Prize Pool。
- 同一資料只能有一個權威來源；GameType 詳細契約只存在 `GAMETYPE_SPEC.md`。

## 10. ScratchPack 與資源

- ScratchPack 詳細 schema、Canvas code、資源欄位、Scratch Zone、Prize Pool 與驗證規則只存在 `SCRATCHPACK_SPEC.md`。
- 美術欄位只指向包內資源，不承擔 Prize Tier、中獎率、最高獎金或其他平行業務資料。
- 主程式不得從資源檔名或圖片內容反推業務資料。
- ScratchPack 不允許執行任意程式碼、DLL、EXE 或 script。

## 11. 刮獎輸入與硬幣

- 每個刮獎區必須是獨立 ScratchSurface；例如九宮格就是 9 個互不干擾的刮膜區。
- 單一刮獎區達約 78%（可經實測微調）時，只標記該區已完成，不自動清除剩餘銀膜；使用者仍可繼續刮乾淨。
- 只有所有必要刮獎區都達完成門檻後，才執行自動判定與兌獎。
- 「全部刮開」直接清除所有必要刮膜並完成兌獎。
- 自畫硬幣與實際刮除使用同一個 mouse event pipeline 與同一座標點；不得再建立第二套 MouseMove 追蹤造成硬幣與刮點分離。
- 待機時顯示硬幣正面；按住刮獎時切換成側立／傾斜刮獎視覺；滑鼠移動期間硬幣持續跟著實際刮點。

## 12. 設定與挑選彩券 UI

- 挑選彩券預設顯示所有啟用且有 Active 批次的彩券；面額篩選提供「全部、100、200、300、500、1000、2000、5000」等官方選項。
- 面額篩選不得使用傳統 RadioButton / ComboBox 外觀；正式版以一致的小型鈔票圖示／卡片呈現。
- 設定頁彩券列採 accordion；點一列展開詳細資料，再點同一列收起；點其他列時原列收起、新列展開。
- 展開內容的獎池只顯示「獎金 / 發行張數 / 剩餘」。
- 「啟用中 / 已停用」直接做成每列狀態按鈕。
- 「發行新一批」直接放在每列操作區；底部只保留真正全域性的操作，例如匯入 ScratchPack、立即備份、關閉。

## 13. 中獎音效與效果

- 手動把所有刮區自行刮完時，播放去除前置提示「逼」的中獎音效版本。
- 「全部刮開」或系統自動揭曉／兌獎時，播放保留前置提示「逼」的版本。
- 小獎與大獎音效可分開；目前大獎門檻可依現行設計使用 50,000 元以上，未來若改為依 Prize Rank 管理不得破壞既有票面邏輯。
- 頭獎與二獎必須有不同的預設 WPF 中獎效果；結果框背景保留足夠透明度，讓玩家仍看得到背後完整中獎票面。

## 14. 資料與備份

- 正式資料使用 SQLite，runtime database 預設位於 `%LOCALAPPDATA%\ScratchGame`，不跟 EXE 資料夾綁定，更新程式時不得因刪除 portable folder 而刪除使用者資料。
- 一般自動備份：程式啟動時檢查，距最近成功備份達 3 天才建立新備份。
- 備份最多保留最近 5 份，超過即刪除最舊一份。
- 資料庫 schema migration 前無論距離上次備份多久，都必須先建立安全備份；完成後仍以最多 5 份輪替。
- 使用者執行資料與備份不得提交至 Git。
