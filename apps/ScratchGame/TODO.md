# ScratchGame TODO / Roadmap

更新日期：2026/09/17

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前工作版本：**V0.5.4 / Build 0**。
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- V0.5.3 Build 2 的完整 Portable automation 已完成，Windows CI Run #189 PASS。
- 正式 runtime PNG / WAV 位於 `apps/ScratchGame/RuntimeAssets/Live/`；15 個 required assets 已核對為 byte-for-byte 正確，CI 直接由 repo 組完整 Portable Artifact。
- AITeam Common Rules 已升至 2.5.0；sandbox repo governance 已升至 1.2.1。本 V0.5.4 批次會把長期 ScratchGame branch 對齊該正式治理基準。
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

目前第一批核心實作：

- `GAMETYPE_SPEC.md` 已固定 Type 2 zone mapping：前 `winningNumberCount` 個 zone = 中獎號碼；後 `playNumberCount` 個 zone = 你的號碼。
- 原 `prizeAmountUsage = repeatable | uniquePerTicket` 已改為布林 `allowPrizeAmountRepeat = true | false`。
- `ScratchPackModels` 已加入 Type 2 欄位。
- `ScratchPackV1Loader` 已可解析並驗證 Type 2 schema。
- 新增 `GameType2Rules`：驗證號碼範圍、zone 數量 / 固定模板、顯示獎金、可生成 Prize Tier，並產生精確命中盤面。
- `GamePayloadFactory` 已支援 Type 2 payload。
- `payoutSource=play` 與 `payoutSource=winning` 共用同一 GameType。
- 未中獎票固定 0 命中；正獎票所有命中格獎金總和必須精確等於既定 Prize Tier。
- 新增獨立 `GameType2Regression`，測正常 / 未中獎、兩種 payoutSource、號碼唯一、禁止獎金重複、不可生成 Prize Tier 等核心契約。
- Renderer 尚未完成前，正常 `LoadAndValidate` 仍拒絕安裝 Type 2；只有 CI regression 可明確 opt-in 測核心，避免出現可購買但無法呈現的半成品。

本批完成條件：

1. V0.5.4 / Build 0 source + Common Rules 2.5.0 / Governance 1.2.1 同一次推進正式工作 branch。
2. Windows CI 編譯 ScratchGame / PackEditor / Regression 全部 PASS。
3. Type 1 既有 regression 不退化，Type 2 core regression PASS。
4. 完整 Portable 仍能成功產生。

### Type 2 下一批

本批 CI 通過後再做：

`Renderer → Runtime scratch interaction → 完整 Type 2 regression → 開放正常安裝`

不在核心批次提前修改 Header / Stage / Footer 尺寸或整體 UI 邊界。

## GameType 主線

基本 GameType 依序完成，每個 GameType 原則上走：

`Spec → Generator → Validator → Renderer → Runtime → regression`

目前：

- GameType 1：已完成核心 runtime / regression。
- GameType 2：V0.5.4 進行中。
- GameType 3～6：規格已定，尚待逐一實作。

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
