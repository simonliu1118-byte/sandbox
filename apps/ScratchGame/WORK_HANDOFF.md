# ScratchGame 工作交接

更新日期：2026/09/20

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

目前正式基準：`main`

最新正式版本：**V0.5.5 / Build 5**

正式 Tag：`ScratchGame-v0.5.5-build5`

ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是 ScratchGame 附屬 EXE，不是獨立產品。

新對話不要依舊聊天記憶直接改。先讀：

1. 最新 `main` HEAD 與 ScratchGame 最新正式 Release / CI。
2. 根 `AGENTS.md` / `REPOSITORY_RULES.md` / `REPO_POLICY.md`。
3. `apps/ScratchGame/PROJECT_RULES.md`。
4. `apps/ScratchGame/TODO.md`。
5. `apps/ScratchGame/RUNTIME_PACKAGE.md`。
6. `apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`。
7. VERSION / BUILD。
8. ScratchPack / GameType 工作需要時再讀 `SCRATCHPACK_SPEC.md` / `GAMETYPE_SPEC.md`。
9. 只再讀當次工作直接相關 source；不要重掃整個 repo。

V0.5.5 Build 5 已完成實機驗收與正式 Release。GameType 3 目前在 `scratchgame/feature-gametype3` / Draft PR #9 開發，V0.5.6 Build 0 的 Generator / Renderer / Runtime 已通過 Run #203；V0.5.6 Build 1 將固定 TestPack 打包政策納入開發 Artifact。

---

# 1. 最新正式 Release

```text
Version: V0.5.5 Build 5
Tag: ScratchGame-v0.5.5-build5
Tag target: 0926e2c69c500340d38f33f09f390e0e7ce24b63
Release date: 2026/09/18
Portable: ScratchGame-V0.5.5-Build5-win-x64.zip
SHA-256: eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459
Acceptance CI: Run #200 PASS
User real-machine acceptance: PASS
Release asset verification: Run #7 PASS
```

正式 GitHub Release 已公開發布；Portable ZIP 與 `SHA256SUMS.txt` 均已上傳，GitHub asset 回報的 ZIP size / SHA-256 與正式基準完全一致。

正式發布說明：`RELEASE_NOTES_V0.5.5_BUILD5.md`。

---

# 2. 已完成基準

- V0.5.2：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄、Stage 中獎結果 UI、銀膜碎屑／刮痕等 UI 批次。
- 永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**
- V0.5.3：repo runtime assets → complete Portable automation、canonical TestPack、automated regression；Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成；Run #192 PASS。
- V0.5.5 Build 0～5：UI 驗收線完成；Build 5 Run #200 PASS、使用者驗收 PASS、正式 Release asset verification Run #7 PASS。

## 固定 TestPacks

開發／測試 Portable 固定包含：

```text
TestPacks/ThreeStar-Test.scratchpack
TestPacks/GameType2-Test.scratchpack
TestPacks/GameType3-Test.scratchpack
```

固定 hash 與產生方式記錄於 `RUNTIME_PACKAGE.md` / `runtime-assets.json`。之後開發版固定使用這三包做實機測試；新增 GameType 時可再新增對應固定 TestPack。

**正式 Release ZIP 一律排除整個 `TestPacks/`。** TestPack 留在 repository / CI，不發給正式使用者。此規則已取代先前「保留到 V1.0.0 再決定」的舊暫行政策。

---

# 3. ScratchPack / GameType 權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- GameType 1：完成。
- GameType 2：完成。
- GameType 3：V0.5.6 開發中；core / Renderer / Runtime 已通過 Run #203，待實機驗收。
- GameType 4～6：規格已定，尚待實作。

---

# 4. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。**

---

# 5. 接續順序

1. 完成 GameType 3 實機驗收／必要修正並整合 Draft PR #9。
2. 再逐一完成 GameType 4～6。
3. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
4. Decoration / 整體遊戲體驗。
5. V0.9.x stabilization。
6. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

任何正式 Release：**先以通過 CI 的開發基準建立 release candidate，再移除 `TestPacks/`、重新驗證 ZIP 與 SHA-256 後才能發布。**
