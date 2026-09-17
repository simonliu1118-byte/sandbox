# ScratchGame 工作交接

更新日期：2026/09/17

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.4 Build 1**

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

# 1. 已完成基準

## V0.5.2 UI 批次

已完成：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄卷軸 Modal、Stage 底部中央顯示中獎結果、銀膜碎屑／不規則刮痕、玩家卡裁切修正。

使用者決定延後一次集中實機驗收，目前不要逐項返回修改。

永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**

## V0.5.3 Portable / Regression

- Portable packager、runtime asset manifest、canonical TestPack、完整 automated regression 已完成。
- 正式 runtime PNG / WAV 已進 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 已 byte-for-byte 驗證。
- Windows CI 直接以 repo Live assets + deterministic TestPack + 當次 publish EXE 組完整 Portable。
- Run #189 PASS，完整 Portable Artifact 已成功產生。
- `ScratchGame.Regression` 已驗證 GameType 1、Imported / BuiltIn、有限票池、購票、Pending、換票、scratch gate、兌獎、Wallet / statistics、PackEditor round-trip。

Canonical TestPack：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

## 共通治理

- AITeam Common Rules：**2.5.0**。
- sandbox Governance：**1.2.1**。
- 長期 ScratchGame branch 已在 V0.5.4 Build 0 對齊上述正式治理基準。

---

# 2. V0.5.4 — GameType 2「中獎號碼」

## Build 0 — Core：已通過

正式工作 branch commit：`931e87502e8e92c64c1465382bdadba2b0528e2b`

Windows CI Run #190：PASS。

已完成：

- `GAMETYPE_SPEC.md`：前 `winningNumberCount` 個 `scratch.zones` = 中獎號碼；後 `playNumberCount` 個 = 你的號碼。
- `allowPrizeAmountRepeat = true | false`。
- `ScratchPackModels` / `ScratchPackV1Loader` / `GameType2Rules` / `GamePayloadFactory` Type 2 core。
- `payoutSource=play|winning`。
- 兩組內各自唯一；跨組相同即命中。
- 未中獎票固定 0 命中。
- 正獎命中格獎金總和必須精確等於 Prize Tier。
- PackEditor project 直接 link authoritative `GameType2Rules.cs`。
- Type 2 core regression + 原 Type 1 regression + complete portable 全部 PASS。

Run #190 Portable：

```text
ScratchGame-V0.5.4-win-x64.zip
size: 142,636,827 bytes
SHA-256: 5592610c63c62fd4bbfbe4972b3562a1eb717f3f45e9303f823eccbe1151b09f
Artifact: ScratchGame-V0.5.4-portable-win-x64
Artifact ID: 10505080192
```

## Build 1 — Renderer / Runtime：目前批次

目前先在暫存 branch `scratchgame/tmp-gametype2-renderer` 集中修改，完成靜態檢查後才會整理成一個乾淨 commit 推進正式工作 branch。

已完成暫存實作：

- 新增 `GameType2RenderModel.cs`：payload → zone render cell 的純邏輯模型。
- render model 驗證號碼範圍、同組唯一、payoutSource、displayPrizeAmounts、禁止重複規則與實際命中獎金總和。
- `MainWindow.ScratchPackV1.cs` 已加入 Type 2 Renderer。
- 有獎金一側：大號數字在上、小號 `$金額` 在下；無獎金一側只顯示大號數字。
- 所有 winning / play zones 都依同一 `scratch.zones` geometry 建立 `ScratchSurface`。
- 手動刮、全部刮開、開始刮後禁止換票、全部必要區完成才兌獎，直接沿用現有通用 runtime pipeline。
- Type 1 / Type 2 的 ScratchSurface 建立共用同一 helper；Type 1 原參數不變。
- `ScratchPackV1Loader` 已移除 Type 2 臨時 `allowUnrenderedGameTypes` gate；正常 Loader / Importer 開放 Type 2。
- `GameType2Regression` 已擴充：正常 Loader、兩種 payoutSource、payload / render mapping、正常 Importer、Runtime Service reload、installed payload render。
- `BUILD` 已由 0 → 1；VERSION 維持 0.5.4，符合「同一工作項目續修」版本規則。

本批禁止事項仍有效：

- 不修改 Header / Stage / Footer 尺寸、Grid 區域尺寸或整體版面邊界。
- 不另建 Type 2 geometry；Renderer 必須使用 `scratch.zones`。
- 不建立 Editor-only Type 2 規則副本。

Build 1 完成判定：

1. 正式工作 branch 只收一個乾淨 commit，不帶暫存 WIP commit 歷史。
2. ScratchGame / PackEditor / Regression build PASS。
3. 原 Type 1 / finite pool / Wallet / BuiltIn / PackEditor round-trip regression PASS。
4. Type 2 loader / importer / validator / payload / renderer runtime regression PASS。
5. ScratchGame / PackEditor shell icon、startup smoke PASS。
6. 完整 V0.5.4 Build 1 Portable 成功產生。

---

# 3. ScratchPack / GameType 權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 update / replace framework。
- GameType 1 使用 `scratch.zones` row-major 標準網格與通用 `prizes`。
- GameType 2 使用同一 `scratch.zones` 陣列以前段 / 後段區分 winning / play，不另建平行 geometry mapping。

---

# 4. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

目前流程：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。** 現階段只做 GameType 開發所需最低限度支援。

---

# 5. 後續順序

1. V0.5.4 Build 1 final Windows CI；若 PASS，GameType 2 核心實作完成。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後才回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x 穩定化。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → **一次 final Windows CI**；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
