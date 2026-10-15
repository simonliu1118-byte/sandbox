# Sandbox Governance Changelog

## 1.2.7 — 2026/10/15

- `apps/GDriveDownloader/PROJECT_RULES.md` 第 4 節補充兩項固定規則：(1) 同一項目內視訊／音訊兩條串流改為並行下載（各自獨立 `StreamFetcher`），不得回退成先視訊後音訊的循序做法，此規則與「跨項目循序、同一時間僅下載一個項目」並不衝突；(2) 主介面任務清單改採「一個項目一張卡片」呈現，每張卡片需同時顯示執行狀態與視訊／音訊各自的進度條，並提供各自的停止／移除按鈕；下載過程中的位元組數／速度等細節只能呈現在卡片進度條上，不得持續灌進執行紀錄文字區塊。

## 1.2.6 — 2026/09/22

- `apps/GDriveDownloader/PROJECT_RULES.md` 改寫畫質策略與登入狀態判斷權威：不得再使用 `yt-dlp` 或其他靜態猜測畫質清單、複製 cookie 給外部 HTTP client 的做法（實測會被 Google 判定非瀏覽器連線並回應 403）；改為固定用 WebView2 的 Chrome DevTools Protocol 實際播放並監聽 `videoplayback` 請求分類畫質，下載也透過同一個已登入瀏覽器 session 的 CDP `Fetch`/`IO` 串流完成。
- 登入狀態判斷不得再限縮成少數固定 cookie 名稱白名單，改為即時查詢持久化 profile 是否存在 google.com 網域、名稱含 `SID`/`LSID` 的 session cookie。
- 新增 `apps/GDriveDownloader/legacy/`：舊版（Bun + 受控 Chrome/Edge CDP）發行包可還原原始碼快照，僅供追溯，不屬於正式技術線。

## 1.2.5 — 2026/10/14

- 納入個人旅行規劃程式 `apps/TravelPlan`，匯入既有 V0.8.5 測試版 source、Version 歷史及接續文件。
- 新增唯一 `PROJECT_RULES.md` 與 `VERSION`（0.8.5）、`BUILD`（0）；產品版本沿用匯入基準，不因單純入庫升版。
- 原始碼與既有文字文件原樣保存；未提交 EXE、ZIP、執行資料或私密設定。缺少原 assets、Windows 實機尚待驗證的狀態寫入 README。

## 1.2.4 — 2026/09/29

- 新增個人專案 `apps/MediaMerge`：建立唯一 `PROJECT_RULES.md`、`VERSION`（0.1.0）、`BUILD`（0）。
- 固定 `MediaMerge.exe` 為 Windows x64 單檔發行名稱；FFmpeg 於 CI 下載 LGPL build 後內嵌，不將執行檔提交至 Git。
- 固定 `assets/MediaMerge.svg` 為應用程式 icon 唯一來源，由 Windows CI 產生多尺寸 ICO 並嵌入最終 EXE。
- 新增 MediaMerge Windows Build workflow，採 pull request／manual 驗證與 sandbox 預設 3 天 Artifact retention。

## 1.2.3 — 2026/09/22

- `REPO_POLICY.md` 新增 repo-level 預設：本 repo 開發測試包 Actions Artifact 的 `retention-days` 統一預設為 3 天（覆蓋共通母本 14 天的一般預設），適用所有 `apps/<Project>` Build workflow；個別專案需要更長保留時間須在自己的 `PROJECT_RULES.md` 明確例外。
- `ScratchGame`（既有）與 `GDriveDownloader`（另案 PR 同步調整）的 Build workflow 皆已設為 `retention-days: 3`，與此預設一致。

## 1.2.2 — 2026/09/22

- 新增專案 `apps/GDriveDownloader`：建立唯一 `PROJECT_RULES.md`、`VERSION`（0.1.0）、`BUILD`（0），與 `apps/ScratchGame` 各自獨立計版、彼此不共用資源。
- 依共通規則，新增 `apps/*/PROJECT_RULES.md` 屬 repository governance 變更，經 `governance/*` branch 提交本次治理紀錄。

## 1.2.1 — 2026/09/17

- 修正 Governance Check 對共通母本同步的判定：`REPOSITORY_RULES.md`、`COMMON_RULES_VERSION`、`COMMON_RULES_CHANGELOG.md` 的 AITeam common-rules sync 與 sandbox 自己的 repo/project governance 變更分開驗證。
- 共通母本同步仍必須使用 `governance/*` 並一次同步三個 common files，但不再錯誤要求為單純同步升 sandbox `GOVERNANCE_VERSION`；真正修改 `REPO_POLICY.md`、`PROJECT_RULES.md`、Governance workflow 等本地治理時，仍必須同步更新 `GOVERNANCE_VERSION` 與 `GOVERNANCE_CHANGELOG.md`。
- 同步 AITeam Common Rules 2.5.0；新增 binary asset source integrity / derived resource SOP。

## 1.2.0 — 2026/09/14

- 重整 `apps/ScratchGame/PROJECT_RULES.md`，把目前已定案的 ScratchGame 永久規則正式寫入，移除已過時的 Reservation／放棄本張／舊版型假設。
- 固定 `canvas=1` 為 1080×882 基本橫式刮刮樂座標系；所有刮區、符號、票號與程式面額統一使用 Canvas 設計座標。
- 固定主舞台與 Dialog 的視覺責任邊界：主舞台只保留背景→彩券→動態層；正常提示／確認／警告／錯誤改用 ScratchGame 自畫 modal，Windows MessageBox 只保留致命 fallback。
- 固定有限票池改為發行即扣 Remaining、不再使用業務層 Reserved；換票、異常恢復與正常關閉／切換使用者的 Pending Ticket 行為重新定義。
- 固定 gameType / ScratchPack 相容性原則、`gameType="1"` 星星連線規則、`issueSize % ticketsPerBook == 0`、票號格式、`priceDisplay`、設定頁列內操作、硬幣單一 mouse pipeline、結算按鈕與售罄文案等 ScratchGame 專案規則。

## 1.1.0 — 2026/09/13

- 共通規則同步至 2.4.0：一般 Build／Test workflow 統一採 `pull_request` + `workflow_dispatch`；Draft PR 也可正常驗收，不再把 Draft／Ready 當 CI 開關。
- ScratchGame Windows Build workflow 移除 Draft 阻擋與多餘的 `ready_for_review` 觸發；PR 建立、reopen 或新 commit 才會執行必要驗收。
- Local-first、集中修改、少 push、path filter 與 concurrency 繼續作為節省 Token／Actions 的主要方法；不再依賴 manual-only CI。
- sandbox 個人用途定位、公司內容隔離、Wade–Giles、個人 copyright 與 ScratchGame 功能規則均未變更。

## 1.0.0 — 2026/09/13

- sandbox 正式納入 AITeam 共通規則同步體系，採用 Common Rules 2.3.0。
- 建立三層治理架構：共通 `REPOSITORY_RULES.md`、sandbox `REPO_POLICY.md`、`apps/<Project>/PROJECT_RULES.md`。
- sandbox 明確定位為個人用途 Public repository，不承載與個人專案無關的公司、客戶、營運資料或其他組織秘密。
- Wade–Giles（威妥瑪）依共通母本適用於所有需要中文羅馬拼音的個人專案；不引入其他 repository 的公司固定名稱或 company copyright。
- 個人 copyright 預設為 `Copyright © <YEAR> C.C. Liu. All Rights Reserved.`，並新增根 `LICENSE`。
- 建立 `COMMON_RULES_VERSION`、`COMMON_RULES_CHANGELOG.md`、`RULES_INDEX.md`、Governance Check 與手動 Common Rules sync workflow。
- ScratchGame 重複的 Local-first／Token-efficient 規則改由共通母本統一維護，PROJECT_RULES 只保留 ScratchGame 專案特例。
- ScratchGame Windows Build workflow 改為 PR／manual 驗證，避免 merge 後又因 main push 重複建置同一批內容。

