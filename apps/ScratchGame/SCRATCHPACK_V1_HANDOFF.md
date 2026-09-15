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

## ThreeStar-Test 交接入口

- Pack 位置：`apps/ScratchGame/reference-packs/ThreeStar-Test/`
- 視覺交接與下一步：`apps/ScratchGame/reference-packs/ThreeStar-Test/artwork-drafts/README.md`
- Git 內已封存 4 張彩券視覺 reference 與 1 張獨立銀膜 reference（輕量 WebP handoff copies）。
- ChatGPT Library 另保存完整來源生成 PNG：`/ScratchGame/ThreeStar-Handoff-2026-09-15/`。

目前所有來源生成圖尺寸都是 `1388×1133`，只可作設計 reference；正式 `canvas=1` 必須精確為 `1080×882`。最新工作方向是藍色乾淨底圖，正式化前仍需後製成正確尺寸、重新量測 3×3 刮區 / 面額區 / 票號區，並把銀膜維持為獨立資產。

下一步從 `artwork-drafts/README.md` 接續；不要先做 loader / CI。先完成並接受正式 1080×882 票面與獨立銀膜，再進入程式實作。
