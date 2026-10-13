# TravelPlan

個人 Windows x64 旅行規劃桌面程式。以 Go + Win32 建立，包含旅行基本資料、航班、多方案住宿／每日行程、本機 JSON 資料與 Word 匯出。

## 匯入基準

2026/10/14 將先前交付的 `TravelPlan_V0.8.5_test.zip` 中原始碼與文字文件匯入 Git。產品版本為 **V0.8.5，Build 0，Windows 實機待驗證版**。

`source/`、`Version/`、`接續紀錄.md`、`測試說明.txt` 的內容與該測試包一致，本次不修改應用程式邏輯。

## 檔案

- `source/`：Go module、source、核心測試、建置時資源輔助程式。
- `Version/`：原有 V0.4.1 至 V0.8.5 的已提供版本紀錄；不是完整每版歷史。
- `接續紀錄.md`、`測試說明.txt`：原先交付的狀態與實機測試項目。
- `VERSION`、`BUILD`、`PROJECT_RULES.md`：依 sandbox 正式規則補齊的專案資訊。

歷史接續紀錄中的暫存工作目錄與 Go 工具鏈路徑不保證仍存在；後續從本目錄正式 source 接續。

## 驗證狀態與待辦

原交付紀錄報告 20 項核心測試、race、go vet 與 Windows amd64 交叉編譯通過。本次 Git 入庫核對原始碼與文字檔案 bytes，不把歷史測試結果冒充此次重新執行。

- Windows GUI 互動、DPI 與實機視覺尚未驗收。
- 尚缺原 `assets/TravelPlan.ico`、`assets/empty_state.bmp`，不得把近似重繪當原素材。
- Google Drive 同步、每日內容的滑鼠拖曳仍未完成。
- 目前 source 中版本字串是匯入前既有 literal；下一次產品修改須統一接入專案 `VERSION`／`BUILD`。
- `embed_icon.py` 是匯入前的 PE post-processing 工具，保留歷史 source 不代表核准正式發行例外；未來正式 build 應依共通資源規則使用可驗證的 compiler/resource pipeline。
- 本次沒有新增正式 build/release workflow，也沒有發布 tag 或 Release。

後续開發先讀根 AGENTS.md 的規則鏈，再讀本專案 PROJECT_RULES.md、README 與 source。產品細節以目前正式 source 與後續明確定案為準。

## 開發者參考

在 `source/` 使用 Go 1.23 或相容工具鏈：

```sh
go test ./...
go test -race ./...
go vet ./...
```

Windows GUI 可在 Windows 使用 `go build -buildvcs=false -ldflags="-H=windowsgui" -o ../TravelPlan.exe .` 建置；這只產生工程執行檔，完整發行仍需原 assets、版本／資源與真正 Windows 驗證。

Copyright © 2026 C.C. Liu. All Rights Reserved. 權利依 repository 根 LICENSE。
