# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.5 / Build 0**

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

使用者決定延後一次集中實機驗收。

永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**

## V0.5.3 Portable / Regression

- Portable packager、runtime asset manifest、canonical TestPack、完整 automated regression 已完成。
- 正式 runtime PNG / WAV 已進 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 已 byte-for-byte 驗證。
- Windows CI 直接以 repo Live assets + deterministic TestPack + 當次 publish EXE 組完整 Portable。
- Run #189 PASS。

Canonical TestPack：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

## V0.5.4 — GameType 2「中獎號碼」：完成

核心 commit：`931e87502e8e92c64c1465382bdadba2b0528e2b`，Run #190 PASS。

Renderer / Runtime commit：`e669e658f7838ff134ba7d820bd4aa8c3089ca79`；Run #191 產品契約 PASS，但 regression cleanup 因 SQLite pool 造成後段失敗。

Corrective commit：`f352be90f9d12bdcd60f862cb8cf0371a1fe150a`，Run #192 **全部 PASS**。

Run #192 已確認：

- ScratchGame / PackEditor / Regression build PASS。
- Type 2 Loader / Importer / Validator / Payload / Renderer / Runtime contract PASS。
- 原 GameType 1 finite pool / purchase / swap / scratch / redeem / Wallet / statistics PASS。
- BuiltIn install / idempotent reinstall PASS。
- PackEditor round-trip PASS。
- shell icon / startup smoke PASS。
- 完整 Portable 19 files PASS。

V0.5.4 Build 2 Portable：

```text
ScratchGame-V0.5.4-Build2-win-x64.zip
size: 142,638,339 bytes
SHA-256: 46513302980227b3d48e2c287830ea1666bf7ed83827c70419a36987eb85fb05
Artifact: ScratchGame-V0.5.4-Build2-portable-win-x64
Artifact ID: 10506373436
```

## 共通治理

- AITeam Common Rules：**2.5.0**。
- sandbox Governance：**1.2.1**。
- 長期 ScratchGame branch 已對齊正式治理基準。

---

# 2. V0.5.5 — 彩券小舖卡片排版微調

使用者在進 GameType 3 前要求先調整挑選彩券卡片。

修改範圍只在：

- `apps/ScratchGame/src/ScratchGame/Views/NewTicketDialog.xaml`
- `apps/ScratchGame/src/ScratchGame/Views/NewTicketDialog.xaml.cs`

版型要求：

- `TicketCardItem` 外框 **182×252 維持不變**。
- 卡片內部 244px 內容區維持相同總高度。
- 縮圖移到最上方，顯示區由 121px 改為 132px。
- 名稱與面額移到縮圖下方。
- 名稱、面額、總中獎率 nominal 字級一致（14.5）；總中獎率顏色較低調。
- 「最高獎金」標題使用較小字級與總中獎率同系低調顏色。
- 最高獎金金額使用大字、亮桃紅醒目顯示。
- 發行日期與批次從此卡片移除，不在彩券小舖卡片顯示；底層批次資料沒有刪除。
- 不修改 NewTicketDialog 視窗尺寸，也不修改 Header / Stage / Footer 或主畫面 Grid 邊界。

目前先在暫存 branch `scratchgame/tmp-ticket-card-layout-v055` 集中修改，完成靜態檢查後再壓成單一正式 commit 推進主開發 branch，避免 WIP 歷史與多輪 CI。

V0.5.5 完成判定：

1. VERSION `0.5.5` / BUILD `0`。
2. 正式工作 branch 只增加一個乾淨 commit。
3. ScratchGame / PackEditor / Regression build PASS。
4. GameType 1 / 2 regression 不退化。
5. startup smoke / 完整 Portable PASS。
6. 卡片最終視覺仍需使用者實機確認；若只需同一需求的視覺返修，續用 V0.5.5 Build N。

---

# 3. ScratchPack / GameType 權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- GameType 1：已完成。
- GameType 2：已完成。
- GameType 3～6：規格已定，尚待實作。

---

# 4. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。**

---

# 5. 後續順序

1. 完成 V0.5.5 彩券小舖卡片排版並實機驗收。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
