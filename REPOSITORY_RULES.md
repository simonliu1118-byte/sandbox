# Sandbox Repository Rules

本 repository 用於集中保存自用的簡單程式與小工具。

## 1. 專案目錄

- 每個程式必須放在 `apps/<ProjectName>/`。
- 不同程式的 source、資源、設定與版本檔不得混放。
- repository 根目錄只放 repository 共通文件與共通設定。
- 每個可執行／可發行的程式目錄必須至少包含 `VERSION` 與 `BUILD`。

## 2. 版本號規則

本規則沿用 CYapps 的版本推進邏輯，但 sandbox 內各程式完全獨立計版，不與 CYapps、CYapps_pvt、AITeam 或其他 repository 同步。

### VERSION

- `VERSION` 內容只放基礎版本 `X.Y.Z`。
- 使用者可見格式為 `VX.Y.Z`。
- 各程式各自維護自己的 `VERSION`。

### BUILD

- `BUILD` 為整數，表示同一工作項目的返修次數。
- `0`：不顯示 Build。
- `1`：顯示 `Build 1`，依此類推。
- 新專案建立時預設 `BUILD = 0`。
- 當 `BUILD > 0` 時，使用者可見格式為 `VX.Y.Z Build N`。

### X / Y / Z 推進

- `X`（Major）：重大產品世代變更。只有使用者可以決定升 Major；AI 不得自行升 Major。
- `Y`（Minor）：使用者能明顯感受到的新能力、完整功能階段或具份量的功能升級。符合時可升 Minor；升 Minor 時 `Z = 0`、`BUILD = 0`。
- `Z`（Patch）：日常開發的預設遞增單位。開始新的獨立修改項目、新 bug、新需求或小型功能時，通常 `Z + 1`，並將 `BUILD = 0`。

### 同一項目返修

同一個 Patch 所代表的工作項目若第一次交付／測試後仍未達成原要求，後續修正不再升 Patch，只增加 Build。

例如：

```text
V0.1.2
V0.1.2 Build 1
V0.1.2 Build 2
```

當上一個工作項目完成後，開始另一個獨立項目時，才進入下一個 Patch；若新工作達到 Minor 標準，可改升 Minor。

### 判定原則

「同一項目返修」包括：

- 上一版尚未修好的同一 bug。
- 同一功能驗收失敗。
- 同一原需求尚未完整達成。

「新項目」包括：

- 原要求完成後提出的新修改。
- 不同 bug。
- 不同功能。
- 新增需求。

界線不清楚時，先向使用者確認，不自行猜測。

### 其他

- 單純重跑完全相同 source 的 CI、重新下載 artifact、runner 或網路失敗後 retry，不修改 `VERSION` 或 `BUILD`。
- workflow run number 不等同於本規則的 Build N。
- 尚未達 1.0 的程式可使用 `0.Y.Z`。
- 由 `0.x.x` 升為 `1.0.0` 視為 Major 決策，由使用者決定。
- 已存在的正式 tag／Release 不覆寫；後續修改需建立新的版本身分。

## 3. Git 基本安全

- 不提交密碼、API Key、token、OAuth secret、private key 或其他機密。
- 不提交執行期 LOG、Cache、暫存檔、使用者資料或本機私有設定，除非該程式有明確需要。
- EXE、DLL、ZIP、MSI 等建置產物原則上不直接提交 source tree；需要發行時使用 GitHub Release 或 Actions artifact。
- `.gitignore` 只能避免未來誤提交；若機密已進入 Git 歷史，必須另外處理歷史清理與憑證更換。

## 4. 規則範圍

- 本檔只規範 sandbox repository 的共通事項。
- 個別程式若真的需要例外，可在該程式目錄加入 `PROJECT_RULES.md`，但不應為簡單需求過度建立治理文件。
- 使用者當次明確指示永遠優先於本檔。
