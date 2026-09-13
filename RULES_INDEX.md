# Sandbox Rules Index

本檔只定義允許存在的治理／永久規則入口，避免 AI 或日常開發重新建立平行規則系統。

## 永久規則

- `/REPOSITORY_RULES.md` — AITeam 共通母本的同步副本。
- `/REPO_POLICY.md` — sandbox repository-specific 規則。
- `/apps/*/PROJECT_RULES.md` — 各 APP 唯一 project-specific 規則。

## 治理基礎設施

- `/AGENTS.md` — AI 入口與規則索引，不是第四層規則。
- `/COMMON_RULES_VERSION`
- `/COMMON_RULES_CHANGELOG.md`
- `/GOVERNANCE_VERSION`
- `/GOVERNANCE_CHANGELOG.md`
- `/RULES_INDEX.md`
- `/.github/workflows/governance-check.yml`
- `/.github/workflows/sync-common-rules.yml`

README、REQUIREMENTS、TODO、CHANGELOG、版本紀錄、技術規格與設計文件只能保存狀態、需求、歷史或技術說明，不得自行取得永久規則優先權。

禁止重新新增 `VERSIONING.md`、`TEAM_RULES.md`、`DEVELOPMENT_RULES.md`、project-level `AGENTS.md` 或其他平行永久規則入口。
