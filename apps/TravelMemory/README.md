# 旅行記憶 — TravelMemory

復原基準：V1.1.0，Build 0。由使用者於 2026/10/14 提供的 TravelMemory.zip 保存原始碼，未修改應用程式邏輯。

## 已恢復

- App/main.py、travel_memory 模組與既有地理／地圖／圖示資源。
- V1.1.0 匯入進度、多日 RITI 分組與既有資料庫遷移程式。
- 原版本說明與第三方聲明。

## 未包含與待恢復

- 原 Windows 監督式 EXE 啟動器 source、完整依賴鎖定與封裝腳本未在提供檔案中找到。這次不是可重建 EXE 的完整開發包。
- V1.1.1 source 已遺失；本次入庫仍為 V1.1.0。待補回一次性 fit 指令與使用者操作取消、地圖圖示置中、軌跡右鍵定位、旅程／軌跡／設定下拉選單、定位點與起訖點狀態圖示。
- 未重新執行 Windows GUI／EXE 啟動驗收，未建立 Release。
- 實際 GPS 樣本、使用者資料庫、設定、備份、Runtime、EXE、cache 與 log 均排除，不公開。

## 本次檢查

- Python 檔案 AST 語法檢查通過。
- 本地私有 RITI 固定樣本驗證：260530.txt 21 點／略過 290 列；260602.txt 482 點／略過 25 列；首點座標符合既有基準。樣本不納入 Git。
- 入庫原始碼應與上傳 ZIP 對應檔案 byte-for-byte 一致。

授權依 repository 根 LICENSE；Copyright © 2026 C.C. Liu. All Rights Reserved.
