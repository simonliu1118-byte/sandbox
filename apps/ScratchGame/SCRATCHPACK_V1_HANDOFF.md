# ScratchPack V1 交接索引

更新日期：2026-09-15

本檔只做交接入口，不重複 schema、Prize Tier 或 GameType 細節。

## 權威文件

- ScratchPack 封裝 / schema：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 1～6 規則：`apps/ScratchGame/GAMETYPE_SPEC.md`
- 永久專案原則：`apps/ScratchGame/PROJECT_RULES.md`
- 實作順序與未決事項：`apps/ScratchGame/TODO.md`

`SCRATCHPACK_V1_PLAN.md` 已完成設計任務並移除；歷史設計過程保留於 Git history，不再維護第三份平行規格。

## 目前狀態

- ScratchPack V1 規格與 GameType 1～6 契約已正式拆分定稿。
- 主程式尚未完整實作 V1 loader / importer / Maker。
- 第一款「三星連線」定位為 Built-in Base Pack：使用與外部 Pack 相同的 V1 schema / GameType pipeline，隨程式提供、免手動匯入、不可刪除／解除安裝。
- 三星連線同時是第一個 V1 reference Pack；不再另外維護一套專屬硬編碼 ticket definition。

下一步工作只看 `TODO.md`，不得在本檔新增 schema 或玩法規則。
