# ScratchGame 工作交接

更新日期：2026/09/17

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前版本線：**V0.5.3 Build 2**

ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是 ScratchGame 附屬 EXE，不是獨立產品。

新對話不要依舊聊天記憶直接改。先讀：

1. 最新 branch HEAD 與該 HEAD 的最新 Windows CI。
2. 根 `AGENTS.md` / `REPOSITORY_RULES.md` / `REPO_POLICY.md`。
3. `apps/ScratchGame/PROJECT_RULES.md`。
4. `apps/ScratchGame/TODO.md`。
5. `apps/ScratchGame/RUNTIME_PACKAGE.md`。
6. `apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`。
7. VERSION / BUILD。
8. ScratchPack / GameType 工作需要時再讀 `SCRATCHPACK_SPEC.md` / `GAMETYPE_SPEC.md`。
9. 只再讀當次工作直接相關 source；不要重掃整個 repo。

---

# 1. V0.5.2 UI 批次 — 已完成、延後集中實機驗收

已完成：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄卷軸 Modal、Stage 底部中央顯示中獎結果、銀膜碎屑／不規則刮痕、玩家卡裁切修正。

使用者已決定這批之後一次集中實機驗收，目前不要逐項返回修改。

永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**

---

# 2. V0.5.3 Build 1 — Portable Packager 基礎已完成

Build 1 已完成並通過 Windows CI Run #181：

- `ScratchGame.exe` + `PackEditor.exe` 同時納入正式 packager。
- `runtime-assets.json` 以 path / size / SHA-256 鎖定 runtime assets。
- 支援 approved asset directory / ZIP source。
- 輸出 ZIP 重新開啟驗證檔案集合與 bytes identity。
- 不繼承來源中的舊 EXE 或未宣告檔。
- `testPacks` 已納入正式 manifest。
- canonical `ThreeStar-Test.scratchpack` 由 repo reference source deterministic 建立。

Canonical TestPack：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

---

# 3. V0.5.3 Build 2 — Automated Regression

Build 2 新增 `src/ScratchGame.Regression/`。它不是第二個產品，只是 Windows CI 的自動測試執行器。

設計原則：**測產品規則，不測 UI 座標。** 一般 UI 改版、動畫調整、內部重構，只要產品規則沒變，不應為了測試而改整套 regression。

目前自動鏈已涵蓋：

`ScratchPack load → Imported install → finite pool → buy → pending → pre-scratch swap → scratch gate → result payload → redeem → Wallet/statistics`

並額外涵蓋：

- 同玩家第二張 Pending 必須拒絕。
- swap 不得再次扣 Wallet，也不得改變 Remaining 總數。
- scratch 開始後 swap 必須拒絕。
- canonical ThreeStar-Test 全批 8 張全部完成。
- 全批 outcome 必須剛好為 1 張未中 + 100 / 500 / 1,000 / 2,500 / 5,000 / 10,000 / 100,000 各一張。
- 最終固定統計：8 completed、7 wins、spent 4,000、redeemed 119,100、max 100,000、wallet 215,100。
- BuiltIn install source 與相同內容重裝行為。
- PackEditor 建立新 Pack → 真實 `.scratchpack` → authoritative Loader → ScratchGame normal Importer round-trip。
- Python packager static/unit tests與 canonical TestPack rebuild 都納入同一 Windows workflow。

Regression 只允許在 GitHub Actions 專用 gate 下執行；runner 資料用完即清理，避免碰使用者正式玩家資料。

繼續下一步前，先確認最新 branch HEAD 對應的 Windows workflow 已 PASS；不要另外為文件再 push 一次造成重複 CI。

---

# 4. Production portable 尚餘一個外部資產來源問題

目前正式 runtime PNG / WAV 仍依既有設計放在 repo 外；repo 保存 `runtime-assets.json` exact hash，不把全部 binary 當 Git source。

本次已從 Library 中先前保存的 `ScratchGame_V0.5.2_Build1_FULL_Test_Package.zip` 核對 manifest 的 **15 個 required production assets：15/15 path / size / SHA-256 完全一致**。因此目前已知正式素材 baseline 沒有漂移。

但 ChatGPT Library 不是 GitHub Actions 的長期下載來源，所以仍缺：

> 一個 GitHub Actions 可以穩定取得、且受控的 approved runtime asset bundle 位置。

在這個來源確定前：

- CI 可以驗證 packager 邏輯、TestPack、完整遊戲交易流程與 EXE。
- CI **不能**宣稱已產生 production-complete portable ZIP。
- 不要用假素材冒充正式 portable，也不要把 100+ MB 舊完整包直接提交到 Git。

Automation 收尾的最後一件事，就是建立／指定這個 CI 可讀的 asset bundle 來源，再由正式 `package_portable.py` 產出完整 artifact。

---

# 5. ScratchPack / GameType 目前權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 update / replace framework。
- GameType 1 使用 `scratch.zones` row-major 標準網格與通用 `prizes`。

ThreeStar-Test accepted geometry：

```text
zone: 191×138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

---

# 6. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

目前流程：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。** 現階段只做 GameType 開發所需的最低限度支援。

---

# 7. 接下來順序

1. 收尾 automated regression 的 production asset bundle 長期 CI 來源／完整 portable artifact。
2. ICON binary SOP 透過 `simonliu1118-byte/AITeam` governance branch 升成共通規則。
3. 基本 GameType 逐一：`Spec → Generator → Validator → Renderer → Runtime → regression`。
4. 基本 GameType 全部完成後才回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x 穩定化。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 使用原則：集中修改 → 本地／靜態檢查 → **一次 final Windows CI**。不要把 Actions 當逐步 debugger。

---

# 8. ICON binary SOP 待升級共通治理

後續要保留的 SOP：intended PNG 人工目視 → byte size / SHA / dimensions → binary-safe upload → repo read-back SHA identity → 必要時 render read-back → multi-size ICO → EXE associated icon → Windows Explorer / taskbar / window 實機驗收。

這套 SOP 應進 AITeam 共通治理，不新增 ScratchGame 平行永久規則文件。
