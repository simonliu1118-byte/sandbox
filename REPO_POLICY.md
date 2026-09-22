# Sandbox Repository Policy

本文件只定義 `simonliu1118-byte/sandbox` 的 repository-specific 規則。共通規則以根 `REPOSITORY_RULES.md` 為準；各程式例外以 `apps/<Project>/PROJECT_RULES.md` 為準。

## 1. Repository 身分與用途

- 本 repository 為 **Public、個人用途、source-visible proprietary**。
- 用途是保存使用者個人製作的程式、實驗工具與小型專案；不同程式放在 `apps/<ProjectName>/`，彼此獨立計版與維護。
- 本 repository 不代表任何公司、雇主、客戶或其他組織；個人專案不得自動套用其他 repository 的品牌、公司名稱、公司 copyright 或營運規則。
- Public visibility 不代表 open source；使用、修改、散布與商業權利以根 `LICENSE` 為準。

## 2. 個人專案與資料隔離

- 不得提交任何與個人專案無關的公司內部資料、客戶資料、營運資料、帳務／發票資料、內部帳密、正式 API key、token、private key 或其他機密。
- 若某個工具未來改為主要服務公司營運或必須依賴公司專屬資料／秘密，應先評估移往適當 repository，不應為方便而把公司資料帶入 sandbox。
- 測試資料優先使用自建、匿名化或公開資料；不得把真實敏感資料當作範例、fixture、log 或 screenshot 提交。
- 個人 runtime database、設定、log、cache、備份與使用紀錄不得提交，除非該專案 `PROJECT_RULES.md` 有安全且明確的例外。

## 3. 專案結構

- 每個程式放在 `apps/<ProjectName>/`；不同程式的 source、資源、設定與版本檔不得混放。
- 每個可執行／可發行專案至少包含 `VERSION`、`BUILD` 與 `PROJECT_RULES.md`。
- repository 根目錄只保存共通／repo-level 治理、README、LICENSE、workflow 與必要共通設定。
- 一個 APP 的特殊需求只寫入自己的 `PROJECT_RULES.md`，不得為單次需求另外建立平行規則檔。

## 4. 共通規則同步

- 本 repo 的 `REPOSITORY_RULES.md`、`COMMON_RULES_VERSION`、`COMMON_RULES_CHANGELOG.md` 必須與 AITeam `main` 母本一致。
- AITeam 共通規則變更後，同一輪治理工作應直接以 Git／GitHub API／治理 PR 同步這三個檔，不等待排程 workflow。
- `sync-common-rules.yml` 與 Governance Check 只作第二道保險；同步機制不得覆蓋本 repo `REPO_POLICY.md` 或任何 APP 的 `PROJECT_RULES.md`。
- 任何 AI 接手 sandbox APP 前，先比對本 repo `COMMON_RULES_VERSION` 與 AITeam `main`；若不同或內容有疑義，先同步再開發。
- AITeam 共通母本必須保持組織中性；若未來母本意外加入特定公司名稱、公司 copyright 或其他不適合個人 repo 的內容，sandbox 不應盲目同步，應先在 AITeam 修正母本。

## 5. CI / Artifact / Release

- 採共通 Local-first / GitHub-final verification 原則；不要把 GitHub Actions 當每個微小修改的即時試錯編譯器。
- Windows-specific 專案完成一輪合理修改後，仍須依共通規則使用真正 Windows runner 做必要驗收。
- 開發測試包使用短期 Actions Artifact；正式發行使用明確人工啟動的 GitHub Release，除非專案規則另有明確例外。
- 本 repo 開發測試包 Artifact 的 `retention-days` 預設為 **3 天**（覆蓋共通母本 14 天的一般預設），適用所有 `apps/<Project>` 的 Build workflow；個別專案若需要更長保留時間，須在該專案 `PROJECT_RULES.md` 明確例外並說明理由。
- Public Actions 可以正常使用，但要避免同一 PR／main 重複建置、無關專案一起跑或每個小修正都觸發昂貴工作。

## 6. 個人 Copyright / License

- sandbox 與其中個人程式的預設 copyright notice：`Copyright © <YEAR> C.C. Liu. All Rights Reserved.`
- `<YEAR>` 使用正式 Release 實際發布年份。
- 本 repo 不使用其他組織或公司的 copyright notice，除非使用者未來明確改變該專案歸屬。
- README、Release note、使用說明與 package 內文件不得與根 `LICENSE` 衝突。
