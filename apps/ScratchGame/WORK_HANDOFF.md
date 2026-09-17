# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.5 / Build 1**

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

已完成：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄卷軸 Modal、Stage 底部中央顯示中獎結果、銀膜碎屑／不規則刮痕。

玩家卡裁切在 V0.5.2 Build 1 曾以 Margin/Padding 處理，但使用者於 V0.5.5 實機確認：第一次開啟仍會裁切，點一次「修改名稱」後才完整，因此該舊修正屬治標；V0.5.5 Build 1 改為主動完成初次 ListBox layout。

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

## V0.5.5 Build 0 — 彩券小舖卡片排版

正式 commit：`b7746f1486ad243f640dfe50ffc4597549c6c74d`。

Run #193 全部 PASS。

內容：

- `TicketCardItem` 外框 182×252 不變。
- 縮圖移到最上方；名稱、面額改到縮圖下方。
- 名稱 / 面額 / 總中獎率 nominal 字級一致，總中獎率顏色降低權重。
- 最高獎金標題縮小；最高獎金金額以大字亮桃紅顯示。
- 發行日期與批次從彩券小舖卡片移除；底層資料未刪除。

## 共通治理

- AITeam Common Rules：**2.5.0**。
- sandbox Governance：**1.2.1**。
- 長期 ScratchGame branch 已對齊正式治理基準。

---

# 2. V0.5.5 Build 1 — 使用者實機返修

本批是 Build 0 實機驗收後的同一工作項目返修，不開新 Patch。

### A. 選擇玩家卡片初次裁切

使用者回報：第一次打開「選擇玩家」時卡片邊框仍被切；只要按一次「修改名稱」按鈕，卡片就會立即完整。

目前判定：不是卡片寬高本身錯，而是第一次 ListBox item realization / Measure / Arrange 尚未穩定；編輯模式切換 Visibility 後觸發第二次 layout，因此恢復正常。

Build 1 修正：

- 不再加大 Margin / Padding。
- UserDialog 在 Loaded 時主動 `InvalidateMeasure` / `InvalidateArrange` / `UpdateLayout`。
- 對目前 selected item `ScrollIntoView` 後再取得 item container，完成第二次 layout / visual invalidation。
- 不改 UserDialog 視窗尺寸與玩家卡片設計尺寸。

### B. 「顯示中獎結果」往下貼近金線

使用者要求暫時隱藏結果後的「顯示中獎結果」按鈕再往下，靠近 Stage / Footer 之間金線。

Build 1 修正：

- 按鈕仍屬 Stage UI，不搬入 Footer。
- 只調整 resume pill 的 bottom margin，使其下移約 46px，視覺上貼近金線。
- 不修改 Header / Stage / Footer row size、整體 Grid 邊界或 Footer 功能配置。

### C. PackEditor ICON 回復真正 master

已 byte-level 查核：

- 目前 repo `apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png`
  - 4178 bytes
  - Git blob：`f826f0aa88c023e0548538e44ac8dd630e49ccb2`
- 素材庫 `PackEditor-icon-48.png`
  - 4178 bytes
  - 計算 Git blob 同樣是 `f826f0aa88c023e0548538e44ac8dd630e49ccb2`

因此先前確實把縮小／預覽圖誤當成 master 放回 repo。

Build 1 改回核可原始 master：

```text
PackEditor-icon-source.png
256×256
size: 19093 bytes
SHA-256: 557eabf2d0ea9f87a2d3525408206a1065ef42be3f4cc2295a4a0fe0d0971ed6
visual: 紅色彩券 + 金槌
```

後續仍由既有 `GenerateWindowsIcon.ps1` 生成 Windows multi-size ICO；CI 必須通過 generated ICO size、associated EXE icon 與 startup smoke 驗證。

本批完成判定：

1. VERSION 維持 `0.5.5` / BUILD `1`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression PASS。
4. generated icon / shell associated icon 驗證 PASS。
5. ScratchGame / PackEditor startup smoke PASS。
6. 完整 Portable PASS。
7. 使用者再實機確認三項視覺結果。

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

ICON 修正屬產品資源錯誤修復，不代表提前開始 PackEditor 正式 UI 收尾。

---

# 5. 後續順序

1. 完成 V0.5.5 Build 1 三項實機返修並由使用者確認。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
