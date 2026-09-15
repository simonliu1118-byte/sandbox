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

- ScratchPack V1 規格與 GameType 1～6 契約已拆分定稿。
- 主程式尚未完整實作 V1 loader / importer / Maker。
- 第一款正式「三星連線」定位為 Built-in Base Pack：500 元 / 10,000 張，使用與外部 Pack 相同的 V1 schema / GameType pipeline，隨程式提供、免手動匯入、不可刪除／解除安裝，但可停用／重新啟用。
- 開發階段另有獨立 `ThreeStar-Test` 測試 Pack，不與正式 Built-in Base Pack 共用 packageId 或票池。
- `ThreeStar-Test/manifest.json` 與 `ticket.json` 已建立；目前測試票池為 8 張，用於覆蓋 0 線與 3×3 所有合法正獎線數。
- GameType 1 已定案為標準 N×N 網格：直接使用 row-major `scratch.zones`，不使用 `cellZones`；詳細規則只看 `GAMETYPE_SPEC.md`。
- 銀膜選擇現已統一由 `SCRATCHPACK_SPEC.md` 的 `scratch.foil` 管理；內建公用銀膜與 Pack 自帶銀膜使用同一 schema / renderer pipeline，不再使用舊草案 `art.mask`。

## ThreeStar-Test 交接入口

- Pack 位置：`apps/ScratchGame/reference-packs/ThreeStar-Test/`
- 視覺交接與下一步：`apps/ScratchGame/reference-packs/ThreeStar-Test/artwork-drafts/README.md`
- Git 內已封存 4 張彩券視覺 reference 與 1 張早期銀膜 reference（輕量 WebP handoff copies）。
- ChatGPT Library 的歷史完整來源生成 PNG：`/ScratchGame/ThreeStar-Handoff-2026-09-15/`。
- ChatGPT Library 的可重用正式銀膜 master：`/ScratchGame/Asset-Library/Foils/`。

目前已接受藍色乾淨底圖正式化方向：Canvas 1 為 **1080×882**；3×3 zone、面額區與票號區已重新量測並寫入 `ThreeStar-Test/ticket.json`。ThreeStar-Test 目前選用公用內建銀膜 `brushed-silver-three-star`。

下一步從 `artwork-drafts/README.md` 接續；不要先做 loader / CI。先把已接受的 1080×882 底圖正式放入 `assets/three-star.png`，再進入 V1 loader / validator。
