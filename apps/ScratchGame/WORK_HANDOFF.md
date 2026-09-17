# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

主開發分支：`scratchgame/feature-scratchpack-v1-runtime`

目前工作版本線：**V0.5.5 / Build 5**

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

- V0.5.2：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄、Stage 中獎結果 UI、銀膜碎屑／刮痕等 UI 批次。
- 永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**
- V0.5.3：repo runtime assets → complete Portable automation、canonical TestPack、automated regression 完成；Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成；Run #192 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版；Run #193 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果 resume pill 下移、PackEditor 正確 ICON master；Run #194 PASS。
- V0.5.5 Build 2：中獎結果 transition target 與 resume pill 同步；設定清單固定列 + 資訊 Popup；Run #196 PASS。
- V0.5.5 Build 3：設定清單操作欄合併、資料列化與文件校正；Run #197 PASS。
- V0.5.5 Build 4：設定清單右側對齊與中央資訊 Modal；Run #199 PASS。

Canonical TestPack：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

## PackEditor ICON 基準

```text
apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png
256×256
size: 19093 bytes
SHA-256: 557eabf2d0ea9f87a2d3525408206a1065ef42be3f4cc2295a4a0fe0d0971ed6
Git blob: 11f808d513564c52e9c3959857bc77a1a17dcde4
visual: 紅色彩券 + 金槌
```

---

# 2. V0.5.5 Build 5 — UI 收尾 + 簡易玩家刪除

本批仍屬 V0.5.5 同一條實機驗收線，不開新 Patch；VERSION 維持 `0.5.5`，BUILD 升為 `5`。

## A. 設定清單表頭

- Build 4 實機可見表頭色塊在上方圓角仍露出方角。
- Build 5 將表頭背景本身設為與外層清單一致的上方圓角，避免色塊突出圓角外框。
- 不改欄寬、操作欄或資料列結構。

## B. 選擇玩家錢包標示

- 玩家卡右側「錢包」由 11px 輔助字提升為 16px Bold。
- 字色提升為接近 Footer 玩家名片主文字的亮色。
- Wallet 金額仍維持 20px 金色粗體。

## C. 簡易刪除玩家

使用者要求先做功能版，之後再重新設計玩家管理介面。

- 每張玩家卡右上角新增小型紅色 `×`。
- 刪除前用 ScratchGame 自訂 Confirm Modal 二次確認。
- 確認內容明示：該玩家錢包與累積統計會永久刪除，無法復原。
- 目前正在使用的玩家不可直接刪除；先切換到別的玩家後才能刪除。
- 至少保留一個玩家。
- 有未完成 Pending Ticket 的玩家不可刪除。
- DB 刪除使用 transaction；成功後立即從 UserDialog 清單移除。
- 若被刪除列原本是目前選取列，清單回到現行玩家或第一個可用玩家。

## D. 本批驗證要求

1. VERSION `0.5.5` / BUILD `5`。
2. ScratchGame / PackEditor / Regression build PASS。
3. GameType 1 / 2 regression PASS。
4. shell icon / startup smoke PASS。
5. complete Portable PASS。
6. 使用者實機確認：
   - 設定清單表頭上圓角乾淨。
   - UserDialog「錢包」標示辨識度。
   - 右上刪除 `×` 位置、確認流程、目前玩家 / 最後一位 / Pending 保護。

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

1. 完成 V0.5.5 Build 5 實機驗收。
2. 下一個獨立項目依 `GAMETYPE_SPEC.md` 進 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
