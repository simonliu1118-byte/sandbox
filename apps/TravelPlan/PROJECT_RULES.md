# TravelPlan Project Rules

本檔是 TravelPlan 唯一的專案永久規則；共通規則與 repository 政策依根 `REPOSITORY_RULES.md`、`REPO_POLICY.md`。

- 專案正式名稱為 `TravelPlan`，屬個人 Windows 旅行規劃桌面程式。
- 全部專案檔案集中在 `apps/TravelPlan/`；保留個別版本與資料隔離。
- 發行目標為 Windows x64 可攜式程式；解壓最外層資料夾固定為 `TravelPlan`，主執行檔固定為 `TravelPlan.exe`。
- 產品基礎版本與返修次數依本目錄 `VERSION`、`BUILD`；初次匯入沿用既有版本，不因單純 Git 入庫改版。
- UI 可採原生或非原生呈現，穩定性、資料安全與可維護性優先。更換技術棧或整套重寫仍依共通規則先取得使用者明確同意。
- 新增／修改的 workingTrip 草稿、正式儲存邊界與 UI lifecycle 應清楚維持；修改前先確認目前 source 的資料模型與控制項 ownership。
- 使用者旅行資料、備份、匯出、log、OAuth 憑證與本機設定不納入 Git。
- README、接續紀錄、測試說明及 Version 文件為狀態／歷史參考，不構成平行永久規則。
