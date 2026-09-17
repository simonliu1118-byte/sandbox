# ScratchGame 工作交接

更新日期：2026/09/17

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.4 Build 2**

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

已完成：Spec / Model / Loader / Validator / Generator / PackEditor shared authority / core regression。

## Build 1 — Renderer / Runtime：產品契約 PASS，CI 因測試清理失敗

正式工作 branch commit：`e669e658f7838ff134ba7d820bd4aa8c3089ca79`

Windows CI Run #191：FAIL，但失敗範圍已定位。

Run #191 已確認：

- ScratchGame build：PASS，0 errors。
- PackEditor build：PASS，0 errors；只有既有 `_lastFullValidationPassed` CS0414 warning。
- Regression runner build：PASS，0 errors。
- Type 2 正常 Loader / Importer / Validator / Payload / Renderer / Runtime Service contract：**PASS**。
- 真正失敗點在 Type 2 regression 結束後：測試用 SQLite connection pool 暫時仍占用 `%LOCALAPPDATA%\ScratchGame`，原 cleanup 靜默吞掉刪除失敗；接著既有 `Program.Main` 的安全檢查發現該目錄仍存在，拒絕重用並 FAIL。
- 因 step 17 FAIL，後續 icon / publish / smoke / portable / artifact steps 被 GitHub Actions 跳過。

這不是產品 Renderer / Loader / Runtime 邏輯失敗，不回頭重做 UI 或 Type 2 規則。

Build 1 產品實作包含：

- `GameType2RenderModel.cs`：payload → zone render cell，並再次驗證 payout 與 payload integrity。
- `MainWindow.ScratchPackV1.cs`：正式 Type 2 Renderer。
- 有獎金一側大號數字 + 小號 `$金額`；無獎金一側只顯示大號數字。
- winning / play 全部 zones 共用 `scratch.zones` geometry 與既有 `ScratchSurface` pipeline。
- 手動刮、全部刮開、scratch-start gate、完成計數、自動兌獎直接沿用既有通用流程。
- 正常 Type 2 Loader / Importer 已開放；臨時 `allowUnrenderedGameTypes` gate 已移除。
- 未修改 Header / Stage / Footer 尺寸、Grid 區域尺寸或整體版面邊界。

## Build 2 — 目前 corrective 批次

只修 CI regression isolation cleanup，不改產品邏輯：

- `GameType2Regression` cleanup 先執行 `SqliteConnection.ClearAllPools()`。
- regression 自己建立的 `%LOCALAPPDATA%\ScratchGame` 目錄採有限次重試刪除，避免 Windows / SQLite pool 短暫 file handle 造成下一段測試誤判。
- 若自己的測試目錄仍無法刪除，改成明確 FAIL，不再靜默吞掉 cleanup error。
- VERSION 維持 `0.5.4`；BUILD 由 1 → 2，屬同一工作項目驗證返修。

Build 2 完成判定：

1. 正式工作 branch 只增加一個乾淨 Build 2 corrective commit，不帶暫存 WIP 歷史。
2. ScratchGame / PackEditor / Regression build PASS。
3. Type 2 loader / importer / validator / payload / renderer runtime contract PASS。
4. cleanup 完成後，原 GameType 1 / finite pool / Wallet / BuiltIn / PackEditor round-trip regression 接續 PASS。
5. shell icon / startup smoke PASS。
6. 完整 V0.5.4 Build 2 Portable + Actions Artifact 成功產生。

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

1. V0.5.4 Build 2 corrective Windows CI；若 PASS，GameType 2 核心實作完成。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後才回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x 穩定化。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；Run #191 已提供明確單一 cleanup failure，因此 Build 2 只做該 corrective fix，不擴大修改範圍。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
