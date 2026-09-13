# sandbox

自用簡單程式與小工具的集中 repository。

## 目錄結構

每個程式必須放在 `apps/<ProjectName>/` 下，彼此獨立，不將多個程式的 source、資源或版本檔混在 repository 根目錄。

```text
sandbox/
├─ README.md
├─ AGENTS.md
├─ REPOSITORY_RULES.md
├─ .gitignore
└─ apps/
   ├─ README.md
   └─ <ProjectName>/
      ├─ VERSION
      ├─ BUILD
      └─ ...
```

## 版本

各程式自行維護 `VERSION` 與 `BUILD`，彼此不共用版號，也不與 CYapps、CYapps_pvt 或其他 repository 同步。

詳細規則請見 `REPOSITORY_RULES.md`。
