# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.5 / Build 2**

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

## UI / Portable / GameType

- V0.5.2：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄、Stage 中獎結果 UI、銀膜碎屑／刮痕等 UI 批次。
- 永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**
- V0.5.3：repo runtime assets → complete Portable automation、canonical TestPack、automated regression 完成；Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成；Run #192 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版；Run #193 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果 resume pill 下移、PackEditor 正確 ICON master；Run #194 PASS。

Canonical TestPack：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

## PackEditor ICON 基準

核可 master：

```text
apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png
256×256
size: 19093 bytes
SHA-256: 557eabf2d0ea9f87a2d3525408206a1065ef42be3f4cc2295a4a0fe0d0971ed6
Git blob: 11f808d513564c52e9c3959857bc77a1a17dcde4
visual: 紅色彩券 + 金槌
```

---

# 2. V0.5.5 Build 2 — 本次實機返修

本批延續 V0.5.5 UI 驗收，不開新 Patch；VERSION 維持 0.5.5，BUILD 升 2。

### A. 暫時隱藏中獎結果的動畫終點

Build 1 把 `ResultResumeButton` 往下移到接近 Stage / Footer 金線，但 `MainWindow.Build4.cs` 的 transition chip 仍用舊固定 `edge = 18` 計算飛行終點，因此動畫會飛向舊位置，最後按鈕才出現在新位置。

Build 2：

- `GetResultTransitionTarget()` 不再使用固定 18px。
- 直接讀 `ResultResumeButton.Margin.Bottom` 計算動畫終點。
- 隱藏與恢復使用同一 target，因此雙向動畫均與實際 resume pill 位置一致。
- 不改 MainWindow.xaml、Stage/Footer row size 或整體 layout boundary。

### B. 設定選單主清單

使用者實機回報：

- 表頭背景左邊突出、右邊未填滿。
- 原始平面 table 視覺較粗糙。
- 三角形 accordion 展開在未來彩券數增加後會造成列表高度跳動，使用體驗不佳。

Build 2 改為：

- 表頭背景完整填滿清單外框；表頭文字欄位另依 scrollbar 與 row 內距對齊，不再用背景本身做右側補空。
- 每張彩券固定一列 compact row card，圓角、細框、hover；列表高度不因查看詳細資料改變。
- 移除三角形、`IsExpanded`、`ExpandGlyph`、row accordion。
- 最右欄改為「資訊」按鈕。
- 按「資訊」才呼叫 `GetTicketDetailAsync()`，並以 anchored Popup 顯示小型彩券資訊卡：
  - Pack 來源 / 玩法 / 批次
  - 面額 / 中獎率 / 剩餘張數
  - 總發行 / 每本 / 總本數
  - 獎池分配
  - 解除安裝
- Imported Pack 可在資訊卡解除安裝；Built-in Pack 按鈕 disabled，並提示只能隱藏。
- 匯入 ScratchPack 後只重新整理清單，不再強迫展開任何列。

### C. 本批驗證要求

1. ScratchGame / PackEditor / Regression build PASS。
2. GameType 1 / 2 regression PASS。
3. shell icon / startup smoke PASS。
4. complete Portable 19 files PASS。
5. 使用者實機確認：
   - 暫時隱藏動畫確實飛到目前低位的「顯示中獎結果」。
   - 設定清單表頭左右背景完整。
   - 多張彩券時主清單不因看資訊而改變高度。
   - 資訊卡與解除安裝操作符合預期。

---

# 3. ScratchPack / GameType 權威

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- GameType 1：完成。
- GameType 2：完成。
- GameType 3～6：規格已定，尚待實作。

---

# 4. PackEditor 暫停點

PackEditor 是 create-only：不開啟、修改、覆寫既有 `.scratchpack`；每個新 draft 使用新的 UUID v4 packageId；輸出必須經正式 Loader round-trip。

使用者已定案：**基本 GameType 全部完善前，不回 PackEditor 做正式 UI 收尾。**

---

# 5. 後續順序

1. 完成 V0.5.5 Build 2 實機驗收。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
