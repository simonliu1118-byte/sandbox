# ScratchGame TODO / Roadmap

更新日期：2026/09/17

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.4 Build 1**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2 的完整 Portable automation 已完成，Windows CI Run #189 PASS。
- V0.5.4 / Build 0 的 GameType 2 core 已在 Windows CI Run #190 PASS。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 已核對為 byte-for-byte 正確，CI 直接由 repo 組完整 Portable Artifact。
- AITeam Common Rules 2.5.0 / sandbox Governance 1.2.1 已同步到長期 ScratchGame branch。
- V0.5.2 的 UI / 玩家 / 遊玩紀錄 / 刮獎效果修改仍留待使用者集中實機驗收，目前不回頭逐項修改。

## 已完成 — V0.5.3 Portable / Automated Regression

- `tools/package_portable.py` 以 `runtime-assets.json` 鎖定 path / byte size / SHA-256。
- CI 由 repo `RuntimeAssets/Live/` + deterministic TestPack + 當次 publish 的 `ScratchGame.exe` / `PackEditor.exe` 組完整 Portable。
- required asset 缺檔、size mismatch、SHA mismatch、未宣告檔案或 package post-check 不符都會 FAIL。
- canonical `ThreeStar-Test.scratchpack`：800 bytes；SHA-256 `2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a`。
- `ScratchGame.Regression` 已涵蓋 GameType 1、Imported / BuiltIn install、有限票池、購票、Pending、換票、scratch gate、兌獎、Wallet / statistics、PackEditor round-trip。
- Windows CI Run #189 已成功產生完整 Portable Artifact。

**TestPacks 必須保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 必須主動提醒使用者，再由使用者決定是否移除；不可提前移除。**

## V0.5.4 — GameType 2「中獎號碼」

### Build 0 — Core：已通過

- `GAMETYPE_SPEC.md` 固定 Type 2 zone mapping：前 `winningNumberCount` 個 zone = 中獎號碼；後 `playNumberCount` 個 zone = 你的號碼。
- `allowPrizeAmountRepeat = true | false` 為正式欄位。
- `ScratchPackModels` / `ScratchPackV1Loader` / `GameType2Rules` / `GamePayloadFactory` 已支援 Type 2。
- `payoutSource=play|winning` 共用同一 GameType。
- 兩組內部號碼各自唯一；跨組相同即命中。
- 未中獎票固定 0 命中；正獎票所有命中格獎金總和精確等於既定 Prize Tier。
- PackEditor 直接共用 authoritative `GameType2Rules.cs`，不建立 editor-only 規則副本。
- Windows CI Run #190：Type 2 core + 原 Type 1 regression + 完整 Portable 全部 PASS。

### Build 1 — Renderer / Runtime：目前批次

已實作於暫存 branch，待一次 final Windows CI：

- 新增 `GameType2RenderModel`，把 payload 嚴格轉成 zone 對應的 render cells。
- Renderer 依 `scratch.zones` 原座標直接畫動態內容，不新增第二套 geometry。
- 無獎金一側只顯示大號數字；有獎金一側顯示大號數字 + 小號 `$金額`。
- winning / play 全部必要 zones 都建立共用 `ScratchSurface`；手動刮、全部刮開、完成計數與自動兌獎沿用既有通用流程。
- Type 1 的 `ScratchSurface` 建立程式抽成共用 helper，參數與行為不變。
- `GameType2RenderModel` 再驗證：號碼範圍、同組唯一、獎金來源、禁止重複規則、實際命中獎金總和。
- `ScratchPackV1Loader` 的 Type 2 臨時安裝 gate 已移除；正常 Loader / Importer 開放 Type 2。
- `GameType2Regression` 已擴充為：正常 Loader、兩種 payoutSource、payload → render model mapping、正常 Importer 安裝、Runtime Service reload 與 installed payload render 驗證。
- 未修改 Header / Stage / Footer 寬高、Grid 區域尺寸或整體版面邊界。

本批完成條件：

1. 正式工作 branch 只接受一個乾淨的 V0.5.4 Build 1 commit，不帶暫存 WIP 歷史。
2. Windows CI ScratchGame / PackEditor / Regression 全部編譯 PASS。
3. 原 GameType 1 / finite pool / Wallet / BuiltIn / PackEditor round-trip regression 不退化。
4. Type 2 loader / importer / validator / payload / renderer runtime regression PASS。
5. ScratchGame / PackEditor startup smoke PASS。
6. 完整 Portable 仍可成功產生。

## GameType 主線

基本 GameType 依序完成，每個 GameType 原則上走：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：已完成核心 runtime / regression。
- GameType 2：Build 1 Renderer / Runtime 待 final CI；通過後本 GameType 核心實作視為完成。
- GameType 3～6：規格已定，尚待逐一實作。

Type 2 Build 1 CI 通過後，下一個開發項目依 `GAMETYPE_SPEC.md` 進入 **GameType 3**；若 Type 2 實機驗收另發現問題，依版本規則續增 V0.5.4 Build N，不另升 Patch。

## PackEditor 正式收尾 — 延後

基本 GameType 全部完善後，再集中完成所有 GameType 的完整編輯 UI、preview、geometry 操作、錯誤導向、正式 UX 與最終 round-trip 使用者驗收。

目前正式流程仍為：

`① 基本資料與票面素材 → ② 遊戲區域 → ③ 獎金池 → ④ 驗證與輸出`

## 後續產品階段

- GameType 基本集合完成。
- PackEditor 正式收尾。
- Decoration Shop / cosmetic 收藏與裝備。
- 中獎 / 大獎 / 頭獎演出、音效 slot、角色與整體遊戲體驗。
- V0.9.x：migration / backup / 壞 Pack / 大量票池 / 多玩家 / Pending recovery / DPI / performance / memory regression。
- V1.0.0：Feature Freeze → 一整輪只修 bug → 使用者正式驗收 → 發布。

## V1.0.0 Release Gate

至少要求：ScratchPack V1 runtime、預定基本 GameType、PackEditor、完整 portable 重建、automated round-trip regression、database migration / backup、Feature Freeze 後實機驗收全部通過。

**最後主動提醒並由使用者確認是否移除開發用 `TestPacks/`；不得自行提前刪除。**
