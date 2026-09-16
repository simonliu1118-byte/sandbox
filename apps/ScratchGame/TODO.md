# ScratchGame TODO / Roadmap

更新日期：2026/09/17

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前版本：**V0.5.3 Build 1**
- Build 1 source commit：`56722c0fe0c22a96af6a9976188168db98ee419a`
- Windows CI：**Run #181 PASS**
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.2 Build 1 的 UI / 玩家 / 遊玩紀錄 / 刮獎效果修改尚待使用者集中實機驗收，不阻擋目前先做 packaging / automation / 文件治理。

## 已完成但待集中實機驗收 — V0.5.2

- Header「使用者」改為「選擇玩家」。
- 新增玩家改為：按新增 → Modal 輸入 → 確認才建立。
- 遊玩紀錄入口移至 Footer 右側；卷軸式 Modal 由 Footer 飛入中央、關閉飛回。
- 遊玩紀錄標題、玩家名稱、目前錢包置中；移除「可用於購買彩券的餘額」小字。
- 「顯示中獎結果」移到 Stage 靠底邊正中央，收合 / 展開動畫終點同步修改。
- 刮銀膜加入碎屑粒子與較自然的不規則刮痕邊緣。
- 「選擇玩家」玩家卡底框裁切修正於 UserDialog 內處理。
- 曾誤改 Footer 高度，V0.5.2 Build 1 已恢復原 Footer 尺寸；`PROJECT_RULES.md` 已明定未經使用者要求不得改 Header / Stage / Footer 尺寸。

## V0.5.3 — 正式 Portable Packager

### Build 0 已完成

- `tools/package_portable.py` 正式要求 `ScratchGame.exe` + `PackEditor.exe`。
- 支援 `--assets <dir>` 與 `--assets-zip <approved package / asset bundle>`。
- `runtime-assets.json` 保存可打包 runtime assets 的 path / byte size / SHA-256 baseline。
- 打包前逐檔驗證；輸出後重新開 ZIP 驗證檔案集合與 bytes identity。
- 舊 EXE 與未宣告檔案不得從 asset source 被誤繼承。
- packager unit tests 已建立。

### Build 1 已完成 — TestPacks 納入 portable

Build 0 曾錯誤禁止 `TestPacks/`。V0.5.3 Build 1 已依使用者要求完成修正：

1. `TestPacks/ThreeStar-Test.scratchpack` 納入 `runtime-assets.json` 的 `testPacks` manifest。
2. TestPack 受 path / byte size / SHA-256 integrity gate 保護。
3. directory source 與 ZIP source 缺少 TestPack 都會 FAIL。
4. TestPack bytes / hash 不符會 FAIL。
5. 正確 TestPack 必須存在輸出 ZIP；未宣告的 TestPack 不會被白名單外繼承。
6. 移除 `verify_output()` 對整個 `TestPacks/` 的 forbidden-output 邏輯。
7. 新增 `tools/build_reference_testpack.py`，從 repo reference source deterministic 建立 canonical `ThreeStar-Test.scratchpack`。
8. packager unit tests **7/7 PASS**；Python static compile PASS。
9. Windows CI **Run #181 PASS**。

Canonical ThreeStar-Test baseline：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

**TestPacks 保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 時必須主動提醒使用者，再由使用者確認後移除 TestPacks；不可提前移除。**

## 下一階段 — 自動驗證 / Regression

Portable Build 1 完成後，現在優先做完整自動驗證，不先做 PackEditor 正式收尾。

目標鏈：

`Build → Portable Package → ScratchPack load → Import → finite pool → buy → pending → scratch/result → redeem → Wallet/statistics`

至少涵蓋：

- ScratchGame / PackEditor build、publish、startup smoke test。
- portable completeness + runtime asset / TestPack hash verification。
- 建立 approved runtime asset bundle 的長期來源，使 CI 能真正產生完整 portable artifact。
- ScratchPackV1Loader validation。
- Built-in / Imported installation path。
- GameType 1 finite pool、購票、Pending Ticket、換票限制、刮獎、兌獎。
- Wallet / cumulative statistics transaction correctness。
- PackEditor → `.scratchpack` → ScratchGame Importer round-trip regression。

## 文件 / Governance — Automated Regression 後立即整理

- 持續同步 `TODO.md`、`WORK_HANDOFF.md`、`SCRATCHPACK_V1_HANDOFF.md`、`RUNTIME_PACKAGE.md`。
- 清除仍殘留的舊 V0.5.0 / V0.5.1、五步 PackEditor、ICON blocker 等過期狀態描述。
- PackEditor ICON 事件整理成 repository 共通 binary / icon SOP：
  - intended source 先人工目視；
  - 記錄 byte size / SHA-256 / dimensions；
  - binary-safe upload（正常 git 或 Git Data API base64 blob）；
  - repo read-back SHA identity；
  - 必要時 render repo read-back；
  - source gate 通過後才產 ICO；
  - multi-size ICO / EXE associated icon / Windows Explorer-taskbar-window 實機驗收。
- 這套 SOP 應透過 `simonliu1118-byte/AITeam` governance branch 升為 repository 共通規則，不建立 ScratchGame 平行永久規則文件。

## GameType 主線

完成 portable + automation + 文件治理後，優先逐一完善基本 GameType。

每個 GameType 必須一起完成：

`Spec → Generator → Validator → Renderer → Runtime → regression`

PackEditor 在這段只做支援 GameType 開發所必要的最低限度修改，不做正式 UI 收尾。

## PackEditor 正式收尾 — 延後

**等基本 GameType 全部完善後再回來一次完成。**

屆時再集中做：

- 所有正式 GameType 的完整編輯 UI。
- 正式銀膜 clipping / symbol / outcome preview。
- geometry drag / resize 與邊界限制完善。
- 驗證錯誤導向對應步驟 / 欄位。
- 各 Canvas / GameType 的正式 UX、排版與表格統一。
- 最終 PackEditor → Pack → Import round-trip 使用者驗收。

目前 PackEditor 已有四步驟流程：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

沒有可見「新增 Pack」按鈕；啟動即建立新 draft；輸出位於最後一步「完成並輸出」。

## 後續產品階段

- GameType 基本集合完成。
- PackEditor 正式收尾。
- Decoration Shop / cosmetic 收藏與裝備。
- 中獎 / 大獎 / 頭獎演出、音效 slot、角色與整體遊戲體驗。
- V0.9.x：migration / backup / 壞 Pack / 大量票池 / 多玩家 / Pending recovery / DPI / performance / memory regression。
- V1.0.0：Feature Freeze → 一整輪只修 bug → 使用者正式驗收 → 發布。

## V1.0.0 Release Gate

至少要求：

- ScratchPack V1 runtime 穩定。
- 預定基本 GameType 完成。
- PackEditor 能完整建立所有正式 GameType Pack。
- portable package 可重建且完整驗證。
- automated round-trip regression 通過。
- database migration / backup / 舊資料驗證通過。
- Feature Freeze 後完成正式實機驗收。
- **最後提醒並確認移除開發用 `TestPacks/`，不得自行提前刪除。**
