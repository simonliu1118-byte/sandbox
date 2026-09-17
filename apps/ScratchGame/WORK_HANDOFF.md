# ScratchGame 工作交接

更新日期：2026/09/18

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

V0.5.5 Build 5 已完成實機驗收與正式 Release；下一個獨立工作是 GameType 3。新開發應由最新 `main` 建立新的 ScratchGame branch，不要把既有 Build 5 UI 驗收 branch 當成尚未完成的工作線繼續堆疊。

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

正式 GitHub Release 已公開發布；Portable ZIP 與 `SHA256SUMS.txt` 均已上傳，GitHub asset 回報的 ZIP size / SHA-256 與正式基準完全一致。一次性 Release recovery workflow 已在完成後移除，不作為後續開發流程的一部分。

正式發布說明：`RELEASE_NOTES_V0.5.5_BUILD5.md`。

## V0.5.5 Build 5 最終內容

- 設定清單表頭上圓角收邊完成。
- UserDialog「錢包」標示提升辨識度。
- 玩家卡右上角提供簡易刪除 `×`。
- 刪除玩家使用 ScratchGame Confirm Modal 二次確認。
- 目前正在使用的玩家、最後一位玩家、仍有 Pending Ticket 的玩家不可刪除。
- DB 刪除使用 transaction；刪除會永久移除該玩家 Wallet 與累積統計。
- Run #200：ScratchGame / PackEditor / Regression、GameType 1 / 2 regression、Windows icons、兩個 EXE startup smoke、完整 Portable 全部 PASS。
- Release Run #7：正式 Tag ref、已發布 Release、Portable ZIP / checksum assets 與 ZIP digest 驗證全部 PASS。

---

# 2. 已完成基準

- V0.5.2：選擇玩家、新增玩家 Modal、Footer 遊玩紀錄、Stage 中獎結果 UI、銀膜碎屑／刮痕等 UI 批次。
- 永久 UI 規則：**未經使用者當次明確要求，不得自行改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體邊界。**
- V0.5.3：repo runtime assets → complete Portable automation、canonical TestPack、automated regression 完成；Run #189 PASS。
- V0.5.4 Build 2：GameType 2「中獎號碼」完成；Run #192 PASS。
- V0.5.5 Build 0：彩券小舖卡片排版；Run #193 PASS。
- V0.5.5 Build 1：玩家初次 layout、結果 resume pill 下移、PackEditor 正確 ICON master；Run #194 PASS。
- V0.5.5 Build 2：中獎結果 transition target 與 resume pill 同步；設定清單固定列 + 資訊 Popup；Run #196 PASS。
- V0.5.5 Build 3：設定清單操作欄合併、資料列化與文件校正；Run #197 PASS。
- V0.5.5 Build 4：設定清單右側對齊與中央資訊 Modal；Run #199 PASS。
- V0.5.5 Build 5：UI 收尾 + 簡易玩家刪除；Run #200 PASS；2026/09/18 使用者驗收 PASS；正式 Release asset verification Run #7 PASS。

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

# 5. 接續順序

1. 由最新 `main` 建立新的 ScratchGame 開發 branch。
2. 依 `GAMETYPE_SPEC.md` 實作 GameType 3。
3. 再逐一完成 GameType 4～6。
4. 基本 GameType 全部完成後回 PackEditor 正式 UI / preview / validation UX 收尾。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**