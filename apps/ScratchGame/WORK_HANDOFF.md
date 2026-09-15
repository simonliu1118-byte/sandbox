# ScratchGame 工作交接

更新日期：2026/09/16

## 目前基準

- Repository：`simonliu1118-byte/sandbox`
- 主開發分支：`scratchgame/feature-scratchpack-v1-runtime`
- PR：#7
- 目前版本：`V0.5.0`
- ScratchGame 與 PackEditor 共用同一條版本線；PackEditor 不是獨立產品。
- 最近 Windows CI：Run #161，ScratchGame / PackEditor build、publish、兩個 EXE startup smoke test 全部 PASS。

## 權威文件

- 永久專案規則：`apps/ScratchGame/PROJECT_RULES.md`
- ScratchPack V1 schema / ResourceRef / Built-in registry：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 契約：`apps/ScratchGame/GAMETYPE_SPEC.md`
- Roadmap / 未來工作：`apps/ScratchGame/TODO.md`
- ScratchPack 狀態索引：`apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`

不要依舊版 V0.3 / V0.4 handoff 內容直接修改；以最新 branch source、VERSION / BUILD 與上述權威文件為準。

## V0.4.x 主程式現況

已完成主要 ScratchPack runtime 與 UI 整理：

- ScratchPack V1 loader / validator / importer / runtime。
- Built-in / Imported Pack 共用同一 pipeline。
- GameType 1 由 Pack game / zones / prizes / ResourceRef 驅動。
- finite pool / Pending Ticket / batch / redemption pipeline。
- Wallet：新使用者初始 `$100,000`；購票扣款、兌獎回款。
- 本機只保存累積統計，不保存逐張玩家歷史。
- 挑券 thumbnail cache 可由 Pack 重建。
- Header / Footer / Player Card / StatusText / 使用者與遊玩紀錄 modal 已重整。
- 結果 modal 已移出票面 Viewbox，並有 CI 防止文字 UI 再被 Viewbox / parent Effect 模糊化。
- 未中獎 `lose.wav`、wallet grant 與 win audio 已納入 runtime asset 管理。

## V0.5.0 PackEditor 現況

程式位置：

```text
apps/ScratchGame/src/PackEditor/
```

執行檔：`PackEditor.exe`，與 `ScratchGame.exe` 共用版本，正式 portable 方向為放在同一資料夾並共用 `BuiltInAssets/`。

目前已完成：

- 新增 Pack，自動產生 UUID v4 packageId。
- 基本資料：名稱、作者、GameType、面額、Canvas、minimum app version。
- Built-in ticket / foil 與自訂 PNG 選擇。
- Canvas 1 自訂票面必須精確 1080×882。
- GameType 1：3×3 / 4×4 / 5×5；整組 grid 參數自動產生 row-major zones。
- Price Area / Serial Area / Scratch Zones 的中央 geometry 預覽。
- Prize Pool amount / count 編輯；合法線數由 GameType 1 衍生。
- 中獎率、未中獎張數、總銷售、總獎金、平均獎金（期望值）、獎金回饋率即時計算。
- 輸出時建立真實 `.scratchpack`，再以正式 `ScratchPackV1Loader` round-trip 驗證後才寫出。
- PackEditor 編譯直接共用 ScratchGame 的 ScratchPack model / loader source，不維護第二份 validator。

## PackEditor create-only 決策

這是目前正式方向：

- **不提供開啟既有 `.scratchpack`。**
- **不提供修改、覆寫或另存既有 Pack。**
- 每次新增都是新 Pack，產生新的 UUID v4 packageId。
- 相同 packageId 的 Pack 不作為版本更新入口；Importer 本身已拒絕相同 packageId 重複安裝。
- Built-in 若 packageId 相同但內容 hash 改變，也會拒絕，避免偷偷改寫已發布 Pack。

因此後續 PackEditor UI 中現有的 disabled「開啟／儲存」按鈕應移除，而不是實作。

## V0.5.0 下一步

1. 實機驗 PackEditor 第一版：新增 → 設資料 → 設 geometry → 設 Prize Pool → 驗證 → 輸出 → ScratchGame 匯入。
2. 移除「開啟／儲存既有 Pack」UI。
3. 讓中央預覽可直接拖曳整組 Scratch Grid、Price Area、Serial Area，與右側數字雙向同步。
4. 完善銀膜 clipping、動態符號／示意結果、面額／票號 renderer 預覽。
5. 完善驗證頁分項錯誤與導向。
6. 更新 `tools/package_portable.py`，正式驗證並封裝 `ScratchGame.exe + PackEditor.exe + shared assets`。
7. 增加 PackEditor → ScratchPack → Importer 的 round-trip regression。

## 開發注意

- 不為 PackEditor 建立另一套 PROJECT_RULES / TODO / VERSION / BUILD / CI workflow。
- 不複製 ScratchPack schema 或 GameType 規則到 Editor 專用文件。
- 文件只要是 status / roadmap 就放 TODO / HANDOFF；永久產品原則才進 PROJECT_RULES。
- Windows CI 仍採集中修改後一次驗證，不拿 Actions 當逐項編譯器。
