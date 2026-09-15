# ScratchPack V1 交接索引

更新日期：2026-09-15

本檔只做目前狀態與交接入口，不重複 schema、Prize Tier、Built-in registry 或 GameType 核心規則。

## 權威文件

- ScratchPack 封裝 / schema / ResourceRef / Built-in registry：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 1～6 契約：`apps/ScratchGame/GAMETYPE_SPEC.md`
- 永久專案原則：`apps/ScratchGame/PROJECT_RULES.md`
- Roadmap / TODO / 未來工作：`apps/ScratchGame/TODO.md`

## 目前版本線

目前主程式開發線：

```text
V0.4.0 / BUILD 0
branch: scratchgame/feature-scratchpack-v1-runtime
PR: #7
```

V0.4.0 的目標是 ScratchPack V1 runtime，不另設 importer / ScratchPack 平行產品版號。

## V0.4.0 已完成方向

- ScratchPack V1 使用 `manifest.json + ticket.json`。
- `ScratchPackV1Loader` 是 ticket schema 解析／驗證單一 owner；Importer 與 runtime 共用。
- Built-in / Imported Pack 使用相同 schema、ResourceRef、GameType 契約與 runtime pipeline。
- `BuiltIn` / `Imported` 只是 runtime installation source，不是 Pack 欄位。
- Built-in Pack 隨 portable 發行內容提供，啟動時自動註冊；Imported Pack 由使用者手動匯入。
- `SeedDataService` 不再硬編碼正式彩券定義。
- GameType 1 runtime 改由 Pack 的 game / zones / prizes / ResourceRef 驅動。
- `scratch.zones` 是 symbol / foil / ScratchSurface geometry 的共同來源。
- 程式面額與票號使用 Pack 的 `priceDisplayArea` / `serialDisplayArea`。
- 挑券縮圖與正式票面將使用同一份 Pack/runtime definition；縮圖本身是可重建 cache，不屬於 ScratchPack schema。

## Built-in Pack 原則

這項已由 `SCRATCHPACK_SPEC.md` 正式定義：Built-in Pack 與一般 Imported Pack 的 `.scratchpack` 內容格式沒有差別。

正式 Built-in Pack 的製作流程定案：

```text
V0.5.0 ScratchPack Maker
→ 使用者用 Maker 產出正式 .scratchpack
→ 使用者確認／測試
→ 提供最終 Pack
→ ScratchGame 發行時原樣放入 BuiltInPacks/
→ 啟動時由 V1 loader / validator / installer 自動註冊
```

主程式不另外手寫 Built-in 專用 Pack 格式或第二套 ticket definition。

## Built-in 共用資源

ScratchPack 共用 PNG 引用統一走 `ResourceRef = { source, ref }`，公開 registry 只看 `SCRATCHPACK_SPEC.md`。

### Ticket

Built-in ticket 依 GameType 分 namespace；完整 resolver key：

```text
(gameType, art.ticket.ref)
```

GameType 1 master assets：

```text
/ScratchGame/Asset-Library/BuiltInTickets/gameType1/
01-red.png
01-blue.png
02.png
```

### Foil

Built-in foil 為跨 GameType 共用全域 namespace。Master assets：

```text
/ScratchGame/Asset-Library/Foils/
```

目前 refs：

```text
brushed-silver-plain
brushed-silver-three-star
```

## ThreeStar-Test

Pack source：

```text
apps/ScratchGame/reference-packs/ThreeStar-Test/
```

關鍵設定：

```text
gameType = "1"
art.ticket = builtin / 01-blue
scratch.foil = builtin / brushed-silver-three-star
canvas = 1 (1080×882)
gridSize = 3
issueSize = 8
ticketsPerBook = 8
```

Accepted geometry：

```text
zone size: 191 x 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

ThreeStar-Test 已封裝並通過基本 conformance；目前用來做 V0.4.0 手動 Imported Pack 的 Windows 實機驗收，不是正式 Built-in Base Pack。

## 挑券縮圖定案

ScratchPack V1 不保存 `thumbnail.png`。

主程式：

```text
Pack 安裝成功
→ 依正式 Pack/runtime definition 產生 360×294 PNG thumbnail cache
→ 挑券頁正常直接讀 cache
→ cache 遺失／損壞時重建
```

縮圖是款式預覽，不包含實際票號、某張票的 outcome 或 Pending Ticket 狀態。

V0.5.0 Maker 的編輯預覽不使用這套 thumbnail cache；Maker 直接依編輯中的設定即時疊圖，避免把一次性編輯預覽和 runtime cache 混成同一功能。

## Roadmap 摘要

```text
V0.4.0  ScratchPack V1 Runtime
V0.5.0  ScratchPack Maker
V0.6.x～V0.9.x  GameType / Decoration Shop / 刮獎體驗 / UI / 穩定性逐步完整化
V1.0.0  功能 Freeze + migration / regression / round-trip 驗證完成後正式發布
```

Roadmap 細節只維護於 `TODO.md`。

目前所稱「商店」是 Decoration Shop：Frame Theme、Stage Theme、硬幣、刮刮效果、中獎效果等 cosmetic / 使用者體驗內容。彩券商店 / Pack Marketplace 對單機版不是近期需求，只列為 V1.0.0 之後、若未來進入 Steam / Workshop / 線上內容配送時再研究的長期 TODO。

## 升版安全方向

- 已發布 GameType 欄位不得刪除、改名或改變既有語意。
- 後續擴充以新增 optional 欄位為主。
- 新 optional 欄位必須有明確 default，舊 Pack 缺少時維持原有行為。
- Runtime database migration / migration backup / 舊資料驗證由開發端負責。
- V1.0.0 發布後再另外討論 Pack 長期更新／跨大版本升級政策；0.x 不提前建立不必要的 upgrade framework。

## 下一步

繼續 V0.4.0，不提前進入 V0.5.0 Maker：

1. 完成 runtime thumbnail cache。
2. 使用 ThreeStar-Test 做 Windows 手動匯入 → 發行 Batch 1 → 挑票 → 01-blue → 三星銀膜 → 191×138 zones → 面額／票號 → 刮獎／兌獎實機驗收。
3. 修正實機驗收問題。
4. V0.4.0 通過後再進入 V0.5.0 ScratchPack Maker。
