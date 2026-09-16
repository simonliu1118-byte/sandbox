# ScratchGame TODO / Roadmap

更新日期：2026/09/17

本檔只記錄目前狀態、Roadmap 與待辦；永久規則以 `PROJECT_RULES.md` 為準，ScratchPack schema / ResourceRef 以 `SCRATCHPACK_SPEC.md` 為準，GameType 契約以 `GAMETYPE_SPEC.md` 為準。

## 目前基準

- Branch：`scratchgame/feature-scratchpack-v1-runtime`
- 目前版本：**V0.5.3 Build 2**
- ScratchGame 與 PackEditor 共用 VERSION / BUILD；PackEditor 是附屬 EXE。
- Build 2 新增正式 automated regression gate；繼續工作前以最新 branch HEAD 對應的 Windows CI 是否 PASS 為準。
- V0.5.2 Build 1 的 UI / 玩家 / 遊玩紀錄 / 刮獎效果修改仍留待使用者集中實機驗收，目前不回頭逐項修改。

## 已完成但待集中實機驗收 — V0.5.2

- Header「使用者」改為「選擇玩家」。
- 新增玩家：按新增 → Modal → 輸入 → 確認才建立。
- 遊玩紀錄入口位於 Footer 右側；卷軸式 Modal 由 Footer 飛入中央、關閉飛回。
- 遊玩紀錄標題、玩家名稱、目前錢包置中；移除餘額說明小字。
- 「顯示中獎結果」位於 Stage 底邊中央。
- 刮銀膜加入碎屑粒子與不規則刮痕邊緣。
- 選擇玩家內的玩家卡底框裁切已修正。
- Header / Stage / Footer 尺寸屬固定版面契約，未經使用者明確要求不得調整。

## V0.5.3 Build 0～1 — Portable Packager

已完成：

- `tools/package_portable.py` 要求 `ScratchGame.exe` + `PackEditor.exe`。
- 支援 `--assets <dir>` 與 `--assets-zip <approved package / asset bundle>`。
- `runtime-assets.json` 以 path / byte size / SHA-256 鎖定正式 runtime assets。
- required asset 缺檔、size mismatch、SHA mismatch 都會 FAIL。
- 輸出 ZIP 建立後重新開啟驗證檔案集合與 source bytes identity。
- 不繼承舊 package 的 EXE 或未宣告檔案。
- `TestPacks/ThreeStar-Test.scratchpack` 已列入 `testPacks` manifest。
- canonical TestPack 由 `tools/build_reference_testpack.py` deterministic 建立。
- canonical baseline：800 bytes / `2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a`。
- Build 1 packager unit tests 7/7 PASS；Windows CI Run #181 PASS。

**TestPacks 保留到 V1.0.0 正式驗收完成。到 V1.0.0 release gate 必須主動提醒使用者，再由使用者決定是否移除；不可提前移除。**

## V0.5.3 Build 2 — Automated Regression

Build 2 把自動驗證建立在穩定的產品規則上，不綁 UI 位置或畫面操作。UI 改版或內部重整若不改產品規則，不應要求重寫這套測試。

新增 `ScratchGame.Regression`，Windows CI 會自動驗證：

1. canonical `ThreeStar-Test.scratchpack` 能通過正式 `ScratchPackV1Loader`。
2. Imported Pack 正常安裝並建立有限票池。
3. 一位玩家同時只能有一張 Pending Ticket。
4. 購票扣 Wallet / `total_spent`，並立即從 Remaining 扣一張。
5. 尚未開始刮獎可以換票；換票不重複扣款、Remaining 總數不變。
6. 一旦開始刮獎就禁止換票。
7. GameType 1 每張 payload 的實際連線數必須等於該張既定獎項對應的目標連線數。
8. 把 ThreeStar-Test 全批 8 張全部完成，必須精確得到 7 張中獎 + 1 張未中獎，所有票池歸零。
9. 全批完成後固定核對：`completed=8`、`wins=7`、`spent=4,000`、`redeemed=119,100`、`maxPrize=100,000`、`wallet=215,100`。
10. BuiltIn installation source 與相同內容重裝的 idempotent 行為。
11. PackEditor 真實輸出 `.scratchpack` → 正式 Loader → ScratchGame Importer round-trip。
12. workflow 同時跑 packager Python static/unit tests，並重新建立 canonical TestPack。

Regression runner 只允許在 GitHub Actions 專用測試環境執行，且使用該次 runner 的暫存資料；不拿使用者正式玩家資料做測試。

### 尚未完成的 automation 邊界

完整 production portable 還差一項：**正式 PNG / WAV asset bundle 的長期 CI 可取得來源**。

- repo 目前刻意只保存 `runtime-assets.json` 的 exact hash，不把全部 runtime binary 當 Git source。
- 已從先前 `V0.5.2 Build 1 FULL Test Package` 核對目前 manifest 的 15 個 required production assets，path / size / SHA-256 **15/15 一致**。
- 但 ChatGPT Library 不是 GitHub Actions 可長期直接讀取的來源，因此不能把 Library 檔案假裝成 CI 的正式 asset repository。
- 在正式 asset bundle 有穩定 CI 來源前，workflow artifact 仍只能視為 executables；不得宣稱已完成 production portable artifact。

下一個 automation 收尾工作是建立／指定 CI 可讀的 approved asset bundle 來源，再讓 workflow 直接呼叫正式 packager 產生完整 portable ZIP。

## Automated Regression 後的治理工作

Automation 收尾後立即整理 ICON binary SOP，透過 `simonliu1118-byte/AITeam` governance branch 升為 repository 共通規則：

- intended PNG 人工目視。
- 記錄 byte size / SHA-256 / dimensions。
- binary-safe upload。
- repo read-back SHA identity。
- source gate 通過後才產 multi-size ICO。
- EXE associated icon / Windows Explorer / taskbar / window 實機驗收。

不得在 ScratchGame 另建平行永久治理文件。

## GameType 主線

完成 portable + automation + ICON governance 後，逐一完善基本 GameType。

每個 GameType 必須一起完成：

`Spec → Generator → Validator → Renderer → Runtime → regression`

PackEditor 在這段只做 GameType 開發必要的最低限度修改，不做正式 UI 收尾。

## PackEditor 正式收尾 — 延後

**等基本 GameType 全部完善後再回來一次完成。**

屆時集中做所有 GameType 的完整編輯 UI、preview、geometry 操作、錯誤導向、正式 UX 與最終 round-trip 使用者驗收。

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
