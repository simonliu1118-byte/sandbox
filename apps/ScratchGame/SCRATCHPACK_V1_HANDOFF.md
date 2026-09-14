# ScratchPack V1 設計交接索引

更新日期：2026-09-15

此檔只作為換對話／換接手者時的快速入口；**不重複 schema、Prize Tier、GameType 或資源欄位規則**。完整內容一律以同分支最新的 `SCRATCHPACK_V1_PLAN.md` 為準。

## 1. 權威設計文件

- Branch：`docs/scratchpack-v1-plan`
- File：`apps/ScratchGame/SCRATCHPACK_V1_PLAN.md`
- 讀取方式：直接讀 branch 最新版本，不再在本 handoff 固定寫設計基準 commit SHA，避免每次設計更新都需要同步改兩份狀態。

`SCRATCHPACK_SPEC.md` 仍只代表目前 App 已實作舊格式；ScratchPack V1 尚未正式實作前，不得把 V1 計畫誤寫成目前已支援 schema。

## 2. 文件責任

- `PROJECT_RULES.md`：永久不變原則。
- `SCRATCHPACK_V1_PLAN.md`：V1 尚未實作期間唯一詳細設計來源。
- `SCRATCHPACK_SPEC.md`：目前實際支援格式；V1 落地後才由正式規格接管。
- 本檔：入口與目前狀態，不複製詳細規格。

如果發現同一欄位、Prize Tier 規則或 GameType 規則在多檔重複描述，應保留在正確 owner 檔案，其餘改成引用，不再維護平行版本。

## 3. V1.0.0 六種基礎玩法

僅作快速索引：

1. `1` 星星連線
2. `2` 中獎號碼
3. `3` 三個相同
4. `4` 符號計數
5. `5` 賓果
6. `6` 比大小

各玩法完整規則只看 `SCRATCHPACK_V1_PLAN.md`。

## 4. 目前狀態

- ScratchPack V1 **尚未開始正式功能實作**。
- 2026-09-15 已重新整理 V1 的「結構化資料 vs 美術資源」責任：主程式不解析票面圖片取得遊戲資料，詳細規則只寫在 `SCRATCHPACK_V1_PLAN.md`。
- Prize Pool / Prize Tier 詳細規則仍由 V1 Plan 既有 `prizes` 章節負責，不新增 artwork metadata 或平行 Prize Tier 檔案。
- 第一款參考票仍為 GameType `1`「三星連線」；正式美術重畫會獨立一個工作回合處理，不與程式修改混在同一回合。
- ScratchPack Maker 專案檔（例如 `.scratchproj`）目前只保留概念，不列入 TODO 或正式計畫。
- V1 ScratchPack 暫不自訂中獎音效／動畫。

## 5. 與目前 ScratchGame 開發的關係

目前開發分支：`scratchgame/build3-ui-rebuild`。

Build 7 的主畫面 Header / Stage / Footer 已由使用者驗收通過；仍有挑券直式卡片、票面重畫與置中、中獎 UI／游標、ICON 等項目待修。

目前可以完善 V1 設計與準備第一款參考美術，但在上述現行彩券顯示／互動修正驗收前，不開始 ScratchPack V1 importer / Maker / GameType schema 的正式程式實作。
