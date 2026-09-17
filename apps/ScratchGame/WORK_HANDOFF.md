# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.5 / Build 3**

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
- V0.5.5 Build 2：中獎結果 transition target 與 resume pill 同步；設定清單改為固定高度 row card + 資訊 Popup；Run #196 PASS。

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

# 2. V0.5.5 Build 3 — 設定清單第二次實機返修

本批延續 V0.5.5 UI 驗收，不開新 Patch；VERSION 維持 `0.5.5`，BUILD 升為 `3`。

使用者實機回報 Build 2 的 compact row card 仍不夠俐落，且三個操作欄位拆開後「發行下一批」右側會被裁切。

Build 3 定案：

- 設定清單表頭原本的「顯示 / 批次操作 / 資訊」合併成單一 **「操作」** 欄。
- 操作按鈕固定順序：**資訊 → 發行下一批 → 隱藏**。
- 操作欄改為單一固定寬度區域，由三顆按鈕共同排版，不再分割成三個窄欄，避免按鈕文字被裁切。
- 彩券列不再使用 card 外框、圓角、hover 背景或整列點擊感。
- 每張彩券改成平面資料列；列與列之間只保留底部分隔線。
- 資料列本身沒有 hover / pressed 視覺；只有實際可操作的按鈕維持一般按鈕互動效果。
- 清單外層 Border 繼續負責完整外框，避免每列右側邊框因 scrollbar / margin 看起來沒有畫到底。
- 資訊 Popup、Imported Pack 解除安裝、Built-in Pack 只能隱藏等既有行為不變。
- 不修改 MainWindow Header / Stage / Footer 邊界。

### 本批驗證要求

1. VERSION 維持 `0.5.5`，BUILD `3`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression 不退化。
4. shell icon / startup smoke PASS。
5. complete Portable PASS。
6. 使用者實機確認：
   - 操作欄三顆按鈕完整顯示，順序為資訊 / 發行下一批 / 隱藏。
   - 「發行下一批」右側不再裁切。
   - 資料列只有底部分隔線，沒有 card / hover / click 感。
   - 外層清單右側邊框完整。
   - 資訊 Popup 與解除安裝行為正常。

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

1. 完成 V0.5.5 Build 3 實機驗收。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**