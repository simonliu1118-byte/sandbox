# TravelMemory Project Rules

本專案依根 REPOSITORY_RULES.md 與 REPO_POLICY.md；本檔是唯一專案規則。

- 正式名稱 TravelMemory，中文名稱「旅行記憶」；個人用途 Windows x64 桌面程式。
- 原始碼與資源保存於 apps/TravelMemory/；不與 TravelPlan 或其他專案混用。
- 保持既有 Python 3.12、PySide6、QtWebEngine、SQLite、MapLibre 技術架構。
- Data、Config、Runtime、Cache、Logs、Diagnostics、個人 GPS 軌跡與備份不得提交 Git；App/Samples 的實際定位紀錄亦不公開。
- App/travel_memory/resources/Icons/TravelMemory.png 與 TravelMemory.ico 是本次復原的必要既有 binary assets；保留原始 bytes，後续變更遵守根 binary integrity 規則。
- 版本以 VERSION 與 BUILD 管理；復原入庫不得冒充較新版本或已完成 Windows 發行驗證。
