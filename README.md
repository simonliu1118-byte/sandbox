# sandbox

個人自用程式、實驗工具與小型專案的集中 repository。

## 目錄結構

每個程式放在 `apps/<ProjectName>/` 下，彼此獨立，不將多個程式的 source、資源或版本檔混在 repository 根目錄。

```text
sandbox/
├─ README.md
├─ AGENTS.md
├─ REPOSITORY_RULES.md
├─ REPO_POLICY.md
├─ COMMON_RULES_VERSION
├─ GOVERNANCE_VERSION
├─ LICENSE
├─ .gitignore
└─ apps/
   ├─ README.md
   └─ <ProjectName>/
      ├─ PROJECT_RULES.md
      ├─ VERSION
      ├─ BUILD
      └─ ...
```

## 治理與版本

- 共通開發／版本／CI／Release 規則由 AITeam 的共通母本同步。
- sandbox 自己的個人用途、安全、資料隔離與 copyright 規則在 `REPO_POLICY.md`。
- 各 APP 的特殊規則只放在自己的 `PROJECT_RULES.md`。
- 各程式自行維護 `VERSION` 與 `BUILD`，彼此不共用產品版號。

詳細規則請從根 `AGENTS.md` 依序閱讀正式三層規則。
