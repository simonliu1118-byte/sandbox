# ScratchGame 工作交接

更新日期：2026/09/18

## 0. 新對話先看這裡

Repository：`simonliu1118-byte/sandbox`

正式基準：`main`  
最新正式版本：**V0.5.5 / Build 5**  
正式 Tag：`ScratchGame-v0.5.5-build5`

目前開發線：

```text
Version: V0.5.6 Build 0
Branch: scratchgame/feature-gametype3
Draft PR: #9
Topic: GameType 3「三個相同」
```

ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是 ScratchGame 附屬 EXE，不是獨立產品。

新對話不要依舊聊天記憶直接改。先讀最新 `main`、根治理文件、`PROJECT_RULES.md`、`TODO.md`、本檔、VERSION / BUILD；ScratchPack / GameType 工作再讀 `SCRATCHPACK_SPEC.md` / `GAMETYPE_SPEC.md`。只再讀當次工作直接相關 source。

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

V0.5.5 Build 5 已正式結案；不要再從舊 UI 驗收 branch 接續開發。

---

# 2. V0.5.6 Build 0 / GameType 3 目前狀態

GameType 3 契約唯一權威：`GAMETYPE_SPEC.md`。

使用者已定案：

- 玩法為同金額恰好三個即中該金額，不是三倍獎金。
- `zoneCount` 3～25。
- custom decoy amounts 是正式正獎金額的補充，不得與任何 Prize Tier 重疊。
- Near Miss 支援可設定機率與 Pair 數；預設 75% / 1 組。
- Near Miss 只改盤面刺激感，不改 Prize Pool、中獎率或兌獎。
- 第一版不做三個中獎格的專屬高亮；沿用共通中獎結果流程。

第一階段已完成並由 Windows CI Run #202 全部 PASS：

- Model / Loader / Validator / Generator。
- authoritative `GameType3Rules` 同時供 ScratchGame / PackEditor 使用。
- dedicated GameType 3 regression。
- 原 GameType 1 / 2 regression、兩個 EXE build / startup smoke、完整 Portable 均未被破壞。

第二階段已接入目前 Draft PR #9：

- `GameType3RenderModel`。
- `MainWindow.ScratchPackV1` 的 GameType 3 runtime renderer。
- 每個 Type 3 scratch zone 只顯示 `$1,000` 類型金額；整張票共用相同字級規則。
- Generator → RenderModel regression。

目前下一個 gate：**PR #9 最新 Windows CI 全部 PASS → 提供 V0.5.6 Build 0 Portable 與獨立 GameType 3 測試 ScratchPack給使用者實機驗收。**

使用者驗收前：不 merge、不 Release、不 Tag。

---

# 3. 已完成基準

- GameType 1：完成。
- GameType 2：完成；V0.5.4 Build 2 / Run #192 PASS。
- V0.5.5 UI 驗收線 Build 0～5：完成；Build 5 / Run #200 PASS，正式 Release 完成。
- Canonical ThreeStar TestPack：800 bytes；SHA-256 `2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a`。
- 正式 runtime assets 位於 `RuntimeAssets/Live`，portable 由 repository source + manifest integrity gate 重建。

**TestPacks 保留到 V1.0.0 正式驗收完成；到 release gate 主動提醒使用者，再由使用者決定是否移除。**

---

# 4. ScratchPack / PackEditor 原則

- ScratchPack schema：`SCRATCHPACK_SPEC.md`。
- GameType 規則：`GAMETYPE_SPEC.md`。
- `ScratchPackV1Loader` 是主程式 / Importer / PackEditor 共用 validator 權威。
- BuiltIn 與 Imported 使用相同 `.scratchpack` 格式，只差 installation source。
- PackEditor 是 create-only；基本 GameType 全部完善前，不做正式 UI / preview / validation UX 收尾。

---

# 5. 接續順序

1. 完成 PR #9 GameType 3 Windows CI 與使用者實機驗收。
2. 驗收後才決定 merge / 正式版本處理。
3. 再依 `GAMETYPE_SPEC.md` 逐一完成 GameType 4～6。
4. GameType 完成後回 PackEditor 正式 UI / preview / validation UX。
5. Decoration / 整體遊戲體驗。
6. V0.9.x stabilization。
7. V1.0.0 Feature Freeze / 正式驗收 / release gate。

GitHub Actions 原則：集中修改 → 靜態檢查 → final Windows CI；不要把 Actions 當逐步 debugger。

V1.0.0 release gate 時，**必須再次提醒使用者決定是否移除 `TestPacks/`，不可自行提前刪除。**
