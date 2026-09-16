# ScratchGame 工作交接

更新日期：2026/09/17

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前基準：**V0.5.3 Build 1**

Build 1 source commit：`56722c0fe0c22a96af6a9976188168db98ee419a`

最近 Windows CI：**Run #181 PASS**。

ScratchGame 與 PackEditor 共用同一 VERSION / BUILD；PackEditor 是 ScratchGame 附屬 EXE，不是獨立產品。

新對話不要依舊 V0.5.0 / V0.5.1 handoff 直接做。先讀：

1. 最新 branch HEAD。
2. `AGENTS.md` / `REPOSITORY_RULES.md` / `REPO_POLICY.md`。
3. `apps/ScratchGame/PROJECT_RULES.md`。
4. `apps/ScratchGame/TODO.md`。
5. `apps/ScratchGame/RUNTIME_PACKAGE.md`。
6. `apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`。
7. VERSION / BUILD。
8. ScratchPack / GameType 工作需要時再讀 `SCRATCHPACK_SPEC.md` / `GAMETYPE_SPEC.md`。
9. 只再讀當次工作直接相關 source；不要為了延續工作重掃整個 repo。

---

# 1. V0.5.2 UI 修改已完成，但延後集中驗收

V0.5.2 Build 1 已完成但尚未由使用者逐項實機驗收：

- Header 按鈕「選擇玩家」。
- 新增玩家：按新增 → Modal → 輸入 → 確認才建立。
- 遊玩紀錄入口移到 Footer 右側。
- 遊玩紀錄使用卷軸式 Modal，由 Footer 飛入中央，關閉飛回。
- 遊玩紀錄標題置中；玩家名稱與「目前錢包 $XXXXXX」各一行置中；移除餘額說明小字。
- 「顯示中獎結果」移到 Stage 底邊中央，相關隱藏 / 顯示動畫終點同步修改。
- ScratchSurface 已有銀膜碎屑粒子與不規則刮痕邊緣；使用者已先表示這兩項效果不錯。
- 選擇玩家內的玩家卡底框裁切改在 `UserDialog` 內處理。
- 曾誤把 Footer 高度從 104 改大；V0.5.2 Build 1 已完整恢復 Footer 原尺寸。

永久 UI 規則已寫入 `PROJECT_RULES.md`：**未經使用者當次明確要求，不得自行變動 Header / Stage / Footer 的尺寸或整體區域邊界。** UI 問題必須優先在區域內部處理。

使用者指示：**這批 UI 之後一起驗收；目前先完成 portable / automation / 文件。**

---

# 2. V0.5.3 Portable Packager — Build 1 已完成

Build 0 commit：

`6612f88d7542d5b7558baca9cf8cc6894071dc87`

Build 1 source commit：

`56722c0fe0c22a96af6a9976188168db98ee419a`

Windows CI：**Run #181 PASS**。

目前正式 packager 已完成：

- `tools/package_portable.py` 同時要求 `ScratchGame.exe` + `PackEditor.exe`。
- 支援 `--assets <dir>`。
- 支援 `--assets-zip <approved portable / asset bundle>`。
- `runtime-assets.json` 記錄 runtime assets 的 relative path / byte size / SHA-256。
- required asset 缺檔、空檔、size mismatch、SHA mismatch 都 FAIL。
- output ZIP 建立後重新開啟，核對檔案集合與 source bytes identity。
- 不直接繼承舊 package 的 EXE 或任意額外檔案。
- `testPacks` 已成為 manifest 正式區段。
- `TestPacks/ThreeStar-Test.scratchpack` 是 required TestPack；缺失 / size / SHA 不符都 FAIL。
- `verify_output()` 已不再禁止整個 `TestPacks/`；仍只允許 manifest 白名單檔案。
- `tools/build_reference_testpack.py` 可由 repo reference source deterministic 建立 canonical ThreeStar-Test。
- packager Python static compile PASS；unit tests **7/7 PASS**。

Canonical ThreeStar-Test baseline：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 必須一直保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 時主動提醒使用者，再由使用者決定移除；不得提前自行刪除。**

---

# 3. Portable 資產策略

目前 runtime PNG / WAV 並非完整保存於 repo；歷史設計就是外部 runtime assets。

V0.5.3 packager 的方向是：

- repo 保存 packager + exact hash manifest；
- package 時使用已核准的 asset directory 或完整 asset ZIP；
- packager 只抽取 manifest 宣告的資源，避免「拿舊 ZIP 換 EXE」的人工作法；
- ThreeStar-Test binary 由 repo reference source deterministic 重建，manifest 鎖定 exact size / SHA；
- TestPack 在 0.x / V1 驗收前是**刻意需要的測試資源**，不是應被過濾的垃圾檔。

目前 Windows CI 的 artifact 仍是 executable-only；因 approved external asset bundle 尚未建立長期 CI 來源，所以不能稱為完整 portable package。

下一階段 automation 要一併決定 approved runtime asset bundle 的長期存放 / CI 取得方式，讓 CI 最終直接產生完整 portable artifact。

---

# 4. Portable 完成後的優先順序

使用者已定案：

1. **Portable Packager — 已完成 Build 1。**
2. **現在做完整自動驗證 / regression。**
3. **更新 ICON binary SOP 與各種狀態 / handoff 文件。**
4. **開始逐一完善基本 GameType。**
5. **基本 GameType 全部完善後，才回 PackEditor 做正式收尾。**
6. Decoration / 遊戲體驗完整化。
7. V0.9.x 穩定化。
8. V1.0.0 正式 Gate。

不要把 PackEditor 正式 UI 收尾重新提前。

---

# 5. 下一階段：完整自動驗證 / Regression

目標不是只測 compiler，而是建立：

`Build → Portable Package → ScratchPack load → Import → finite pool → buy → pending → scratch/result → redeem → Wallet/statistics`

至少包含：

- ScratchGame / PackEditor build / publish / startup smoke。
- portable completeness / asset hash / TestPack hash。
- approved runtime asset bundle 長期來源與 CI 完整 portable artifact。
- ScratchPackV1Loader validation。
- Built-in / Imported install path。
- GameType 1 finite pool。
- 購票扣 Wallet。
- Pending Ticket。
- 未開始刮獎的換票 transaction。
- 開始刮後不可換票。
- 中獎 / 未中獎結算。
- redemption / Wallet / cumulative statistics。
- PackEditor → `.scratchpack` → ScratchGame Importer round-trip。

遵守 token / CI 使用原則：**集中修改 → source/static/unit validation → 最後一次 Windows CI**。不要把 GitHub Actions 當逐項 debugger。

---

# 6. PackEditor 現況與暫停點

PackEditor 是 create-only：

- 不開啟、修改、覆寫既有 `.scratchpack`。
- 每次啟動建立新 draft / UUID v4 packageId。
- 不建立 editor-only `.scratchproject`。
- 輸出必須用正式 `ScratchPackV1Loader` round-trip validation。

目前 UI：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

其他已完成：

- 沒有可見「新增 Pack」按鈕。
- `.scratchpack` 輸出移到最後一步，按鈕為「完成並輸出」。
- 每步有上一步 / 下一步；下一步驗證當前步驟，上一步不驗證。
- 預覽 placeholder：「設定彩券內容後顯示」。
- built-in ticket 顯示名稱：內建票面 A - 藍 / A - 紅 / B - 紅。
- foil：素面銀膜 / 三星銀膜。
- Scratch Grid / Price / Serial 已有基本拖曳 / resize。

**PackEditor 正式收尾延後到基本 GameType 全部完善後。** 現階段只做新 GameType 開發必需的最低限度支援。

---

# 7. ICON binary SOP / 待升級共通治理

PackEditor ICON 已不是目前 blocker。後續要保存的是這次形成的 binary SOP。

### Gate 1 — Source Integrity

- intended PNG 先人工目視正確。
- 記錄 byte size / SHA-256 / dimensions。
- binary-safe upload：正常 git 或 Git Data API `create_blob` base64。
- 從 repo read-back 後 SHA-256 必須與 intended source完全一致。
- 必要時 render repo read-back 再目視。

### Gate 2 — Windows Embedding

- Gate 1 通過後才產 multi-size ICO。
- 驗證 ApplicationIcon / associated icon / shell extraction。
- 最後 Windows Explorer / taskbar / window 實機驗收。

不要再把「PNG 可解碼、CI 能產 ICO」當成 source 圖像正確。

這套規則不應只留在 ScratchGame。後續要透過 `simonliu1118-byte/AITeam` governance branch 加入 repository 共通規則，更新 COMMON_RULES_VERSION，再同步下游 repo。

---

# 8. 文件現況

V0.5.3 Build 1 完成後已同步：

- `TODO.md`
- `WORK_HANDOFF.md`
- `SCRATCHPACK_V1_HANDOFF.md`
- `RUNTIME_PACKAGE.md`

---

# 9. 下一個工作直接執行

不要先做 V0.5.2 UI 驗收，也不要先做 PackEditor 正式收尾。

直接從：

> **Automated Regression — 建立 Build → Portable → ScratchPack / finite pool / buy / scratch / redeem / Wallet/statistics 全鏈自動驗證，並加入 PackEditor round-trip。**

開始。
