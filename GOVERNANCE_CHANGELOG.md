# Sandbox Governance Changelog

## 1.3.0 — 2026/09/16

- 更新 `apps/ScratchGame/PROJECT_RULES.md`，正式同步目前已實作的 Wallet／累積統計模型，移除「無初始資金／保存逐張歷史」等舊規則。
- 固定 ScratchGame 與附屬 `PackEditor.exe` 共用同一 `apps/ScratchGame/VERSION` / `BUILD`；PackEditor 不建立第二個產品專案、平行版號或另一套 PROJECT_RULES / TODO。
- 固定 PackEditor 為 **create-only**：只建立新的 `.scratchpack`，每次新增自動產生新的 UUID v4 packageId；不提供開啟、修改、覆寫或另存既有 Pack。
- 固定 `packageId` 為 Pack 永久唯一身分；0.x 單機版不把相同 packageId 當更新入口，也不建立 Pack update / replace framework。
- 把 ScratchPack schema / Built-in registry 與 GameType 詳細契約的權威重新收斂到 `SCRATCHPACK_SPEC.md` / `GAMETYPE_SPEC.md`，`PROJECT_RULES.md` 不再平行複製完整玩法 schema。
- 同步 Header / Footer / StatusText、文字清晰度、Wallet grant presentation、Imported Pack lifecycle 等目前穩定產品原則。

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
