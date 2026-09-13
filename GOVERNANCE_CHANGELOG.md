# Sandbox Governance Changelog

## 1.0.0 — 2026/09/13

- sandbox 正式納入 AITeam 共通規則同步體系，採用 Common Rules 2.3.0。
- 建立三層治理架構：共通 `REPOSITORY_RULES.md`、sandbox `REPO_POLICY.md`、`apps/<Project>/PROJECT_RULES.md`。
- sandbox 明確定位為個人用途 Public repository，不承載與個人專案無關的公司、客戶、營運資料或其他組織秘密。
- Wade–Giles（威妥瑪）依共通母本適用於所有需要中文羅馬拼音的個人專案；不引入其他 repository 的公司固定名稱或 company copyright。
- 個人 copyright 預設為 `Copyright © <YEAR> C.C. Liu. All Rights Reserved.`，並新增根 `LICENSE`。
- 建立 `COMMON_RULES_VERSION`、`COMMON_RULES_CHANGELOG.md`、`RULES_INDEX.md`、Governance Check 與手動 Common Rules sync workflow。
- ScratchGame 重複的 Local-first／Token-efficient 規則改由共通母本統一維護，PROJECT_RULES 只保留 ScratchGame 專案特例。
- ScratchGame Windows Build workflow 改為 PR／manual 驗證，避免 merge 後又因 main push 重複建置同一批內容。
