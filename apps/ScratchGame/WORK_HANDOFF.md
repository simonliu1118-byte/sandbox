# ScratchGame 工作交接

更新日期：2026/09/17

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.4 / Build 0**

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
- 2.5.0 新增 binary asset source integrity / derived resource SOP。
- 長期 ScratchGame branch 在 V0.5.4 核心批次一併同步到上述正式治理基準，避免另外 push 造成多餘 Windows CI。

---

# 2. V0.5.4 — GameType 2「中獎號碼」

第一批核心範圍：**Spec / Model / Validator / Generator / Regression**；Renderer / Runtime UI 留下一批。

已實作：

- `GAMETYPE_SPEC.md`：
  - 前 `winningNumberCount` 個 `scratch.zones` = 中獎號碼。
  - 後 `playNumberCount` 個 = 你的號碼。
  - `prizeAmountUsage` 已改為 `allowPrizeAmountRepeat = true | false`。
  - 未中獎票必須 0 命中。
- `ScratchPackModels` 加入 Type 2 欄位。
- `ScratchPackV1Loader` 可解析 / 驗證 Type 2。
- `GameType2Rules` 負責：
  - zone / 固定模板驗證。
  - 數字範圍與兩組內唯一性。
  - `payoutSource=play|winning`。
  - `displayPrizeAmounts` / `allowPrizeAmountRepeat`。
  - 每個 Prize Tier 必須可精確生成。
  - 產生正獎與 0 命中的未中獎盤面。
- `GamePayloadFactory` 已接 Type 2。
- `GameTypeCatalog` 已加入「中獎號碼」。
- PackEditor project 直接 link 同一份 `GameType2Rules.cs`，不得建立 Editor-only copy。
- `GameType2Regression` 獨立測 Type 2 core，不大改原有 Type 1 regression。

安全閘：

- Renderer 尚未完成前，正常 `ScratchPackV1Loader.LoadAndValidate` 仍拒絕安裝 Type 2。
- CI regression 才能使用明確 `allowUnrenderedGameTypes: true` 測核心。
- 因此目前不會出現「Type 2 可以買，但舞台只能顯示尚未支援」的半成品。

本批完成判定：

1. 正式工作 branch 只接受一個乾淨 V0.5.4 commit，不帶暫存 WIP commit 歷史。
2. Windows CI ScratchGame / PackEditor / Regression 全部編譯 PASS。
3. Type 1 原 regression PASS。
4. Type 2 core regression PASS。
5. 完整 Portable 仍可成功產生。

---

# 3. Type 2 下一批

本批 CI PASS 後繼續：

`Renderer → Runtime scratch interaction → 完整 Type 2 regression → 移除正常安裝 gate`

注意：

- 不修改 Header / Stage / Footer 尺寸或整體版面邊界。
- 固定 Renderer：有獎金一側「大號數字在上、小號金額在下」；無獎金一側只顯示大號數字。
- 同張票所有 Type 2 格子的尺寸、數字字級、金額字級、比例、間距一致，不做逐格縮放。

---

# 4. ScratchPack / GameType 權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- `manifest.packageId` 是永久唯一身分；0.x 不做 update / replace framework。
- GameType 1 使用 `scratch.zones` row-major 標準網格與通用 `prizes`。
- GameType 2 使用同一 `scratch.zones` 陣列以前段 / 後段區分 winning / play，不另建平行 geometry mapping。

---

# 5. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

目前流程：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。** 現階段只做 GameType 開發所需最低限度支援。

---

# 6. 後續順序

1. 完成 GameType 2。
2. 依 `GAMETYPE_SPEC.md` 逐一完成 GameType 3～6。
3. 基本 GameType 全部完成後才回 PackEditor 正式 UI / preview / validation UX 收尾。
4. Decoration / 整體遊戲體驗。
5. V0.9.x 穩定化。
6. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → **一次 final Windows CI**；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
