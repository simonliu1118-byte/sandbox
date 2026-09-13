# ScratchGame Project Rules

本檔只保存 ScratchGame 的固定專案規則；repository 共通版本規則依根目錄 `REPOSITORY_RULES.md`。

## 1. 平台與發行

- 正式技術線：C# / .NET 8 / WPF。
- 目標平台：Windows x64。
- 正式發行以 self-contained portable 為目標，不要求使用者另外安裝 .NET。
- Windows 視窗標題預設為「刮刮樂」；目前彩券名稱與面額屬於主畫面票券資訊，不取代程式視窗標題。

## 2. 主畫面與操作

- 主畫面以彩券本體為視覺核心；面額／版型選擇不得長期佔用主票券區。
- 按「新的一張」後選擇面額與彩券；選擇介面應顯示彩券名稱、面額與發行時中獎率。
- 目前彩券名稱與面額應清楚顯示在票券附近，例如「三星連線　$100」。
- 「全部刮開」會直接揭曉並自動完成兌獎。
- 手動刮除達該必要刮獎區約 78%（可經實測微調）時，該區可自動視為完成並清除剩餘遮罩；所有必要區完成後自動兌獎。

## 3. 使用者

- 使用者檔案彼此獨立保存累計投入、累計兌獎、累計損益與遊玩歷史。
- V0.1 不做初始資金、補充資金或餘額不足限制；損益可為負值。
- 每個使用者最多同時存在一張 Pending Ticket。
- Pending Ticket 必須綁定建立它的使用者；切換使用者不會丟失或轉移 Pending Ticket。
- 使用者歷史至少保存日期時間、彩券、面額、批次與最終獎金。
- 「放棄」最終視為普通未中獎，不在使用者歷史另存「是否放棄」。

## 4. 彩券定義

一張彩券定義固定包含：

- 名稱。
- 面額。
- 遊戲規則／版型。
- 總發行張數。
- 完整獎項表（各獎項固定張數）。
- 由獎項表自動計算的發行時中獎率。
- 所需美術、版面與音效設定。

規則：

- 彩券尚未建立任何批次前可修改或刪除。
- 彩券一旦建立第一批，核心定義即鎖定，不得修改面額、總發行量、獎項張數或中獎率。
- 同一彩券的每個新批次都必須使用完全相同的固定獎項表；新批次只代表重新建立完整票池。
- 若要不同獎項機率／張數，必須建立一張新的彩券，而不是修改既有彩券的新批次。
- 已發行過的彩券不得真正刪除，只能停用；停用後不出現在新票選擇清單，但歷史仍可追溯。

## 5. 批次

- 每張彩券同時間只能有一個 Active 批次。
- 建立新批次會結束原 Active 批次；舊批次之後不可再抽票。
- 建立新批次前，舊批次不得存在未解決 Pending Ticket。
- 若存在 Pending Ticket，使用者可以：
  1. 系統全部刮開並正常結算；或
  2. 放棄並依「未中獎」完成該張。
- 舊批次結束後不需永久保存完整可抽獎池；只保留必要的簡單歷史／統計資料。
- 批次售罄後不得自動建立下一批；由使用者手動決定是否發行新批次。

## 6. 有限票池、Reservation 與交易一致性

票池不是無限機率亂數。每個 Active 批次使用固定張數的有限獎項池。

為避免不同使用者的 Pending Ticket 同時抽到唯一頭獎，抽票必須使用 Reservation：

1. 建立 Pending Ticket 時，先從目前 Available 數量依剩餘張數加權抽出一個獎項。
2. 在同一資料庫交易內將該獎項 `Available - 1`、`Reserved + 1`，再建立 Pending Ticket。
3. 正常兌獎時，在同一交易內將原獎項 `Reserved - 1`、`Consumed + 1`，寫入使用者兌獎與歷史，完成 Pending Ticket。
4. 程式異常關閉時不得重抽；下次開啟恢復同一 Pending Ticket 與其 Reservation。
5. 放棄時，不揭曉原本隱藏獎項：先將原獎項 `Reserved - 1`、`Available + 1`，再消耗一張未中獎票（`Available - 1`、`Consumed + 1`），並以獎金 0 完成該張。
6. 如果目前票池已沒有任何未中獎票，則不可使用「放棄」；只能正常揭曉並結算。
7. 所有上述數量與使用者紀錄變更必須以 SQLite transaction 原子完成，避免 crash 造成獎池或金額不一致。

## 7. 新票建立與投入

- 建立 Pending Ticket 時立即計入該使用者的投入金額（面額）。
- 程式異常恢復 Pending Ticket 時不得再次計入投入。
- 正常兌獎才計入兌獎金額。
- 最終個人損益 = 累計兌獎 - 累計投入。

## 8. 版型與 Game Rule

V0.1 先實作三種內建通用玩法：

1. 幸運號碼配對。
2. 三星連線／連線型。
3. 三個相同符號或金額。

架構必須允許後續增加更多版型。彩券定義與主程式核心分離；既有 Game Rule 可透過資料／ScratchPack 建立新彩券，不需為每張新彩券更新 EXE。

需要全新遊戲邏輯、現有 Game Rule 無法表達時，才升級主程式。

## 9. ScratchPack

- 外部彩券包副檔名為 `.scratchpack`。
- 唯一正式格式規格為 `SCRATCHPACK_SPEC.md`。
- 規格必須足以讓不同 AI 或第三方在沒有本對話的情況下製作可匯入彩券包。
- ScratchPack 只允許資料、美術與音效，不允許攜帶或執行任意程式碼、DLL、EXE 或 script。

## 10. 資料與備份

- 正式資料使用 SQLite。
- 一般自動備份：程式啟動時檢查，距最近成功備份達 3 天才建立新備份。
- 備份最多保留最近 5 份，超過即刪除最舊一份。
- 資料庫 schema migration 前無論距離上次備份多久，都必須先建立安全備份；完成後仍以最多 5 份輪替。
- 使用者執行資料與備份不得提交至 Git。

## 11. 開發工作流：Local-first development, GitHub-final verification

本專案預設採「節省 Token、但不降低開發可靠度」流程。GitHub 是正式來源與版本紀錄；GitHub Actions 是 Windows 驗收層，不是每一次微小修改的即時編譯器。

### 必要讀檔原則

- 每輪優先只讀本次修改真正需要的檔案。
- 已確認且沒有更新的治理文件、README、CHANGELOG、workflow 或完整 source，不得無理由重讀。
- 只有在新對話／新工作階段接手、治理規則更新、main 或 branch 基準重大變更、或使用者明確要求完整審查時，才重新做較完整確認。
- GitHub push 後只確認本次 commit / diff 與必要檔案，不重新掃描整個 repository。

### 修改與驗證原則

預設流程：

```text
讀必要檔案
↓
本地修改
↓
本地 test / lint / 可行的 build
↓
集中完成一輪相關修改
↓
一次 commit / push
↓
必要時執行 GitHub Windows CI
↓
CI 成功：確認結果後結束
CI 失敗：只讀必要錯誤區段後修正
```

- 同一輪相關 UI bug、欄位調整、文字修正或同一功能返修應集中處理，不得每修一點就立刻 push。
- 本地環境可執行的單元測試、靜態檢查、lint、資料層測試與非 Windows-specific 驗證應先做完。
- 不因節省 Token 而省略必要測試、編譯、ZIP 或打包。

### GitHub Windows CI 使用時機

以下情況應使用真正 Windows CI：

- 一輪修改已完成，需要正式 Windows 驗收。
- WPF / Windows-specific code 有實質變更且本地環境無法完整驗證。
- icon、resource、manifest 有修改。
- Windows DLL linkage、Registry、printer API、WebView2、PowerShell packaging 或其他 Windows 相依功能有修改。
- 準備 Release。
- 本地環境無法可靠驗證的建置項目。

單純文字、文件、低風險資料設定或已可由本地測試充分驗證的小修改，不需要每次觸發 Actions。

### CI 結果讀取原則

- CI 成功時只確認 build、tests、publish 與 artifact / EXE / ZIP 是否成功，不讀完整 log。
- CI 失敗時先看失敗 step 與必要錯誤區段，不預設拉取整份完整 log。
- 只有在錯誤原因無法判斷時，才逐步擴大 log 閱讀範圍。

### 版本管理

- `main`、development branch、`VERSION`、`BUILD`、CHANGELOG、Release 等仍依 repository 原規則執行。
- 此工作流只降低不必要的讀檔、GitHub 往返、高頻 CI 與上下文消耗，不降低正式版本管理與 Windows build 可靠度。
