# ScratchPack V1 交接索引

更新日期：2026-09-15

本檔只做交接入口，不重複 schema、Prize Tier、Built-in registry 或 GameType 細節。

## 權威文件

- ScratchPack 封裝 / schema / ResourceRef / Built-in registry：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 1～6 規則：`apps/ScratchGame/GAMETYPE_SPEC.md`
- 永久專案原則：`apps/ScratchGame/PROJECT_RULES.md`
- 實作順序與未決事項：`apps/ScratchGame/TODO.md`

`SCRATCHPACK_V1_PLAN.md` 已完成設計任務並移除；歷史設計過程保留於 Git history，不再維護平行規格。

## 目前狀態

- ScratchPack V1 規格與 GameType 1～6 契約已拆分定稿。
- 主程式尚未完整實作 V1 loader / importer / Maker。
- 第一款正式「三星連線」定位為 Built-in Base Pack：500 元 / 10,000 張，與外部 Pack 共用相同 schema / GameType / runtime pipeline。
- 開發階段另有獨立 `ThreeStar-Test` 測試 Pack，不與正式 Built-in Base Pack 共用 `packageId` 或票池。
- `ThreeStar-Test/manifest.json` 與 `ticket.json` 已建立；測試票池 8 張，覆蓋 0 線與 3×3 所有合法正獎線數。
- GameType 1 使用 row-major `scratch.zones`，不接受 `cellZones`；詳細規則只看 `GAMETYPE_SPEC.md`。

## Built-in 共用資源

ScratchPack 共用 PNG 引用統一走 `ResourceRef = { source, ref }`，詳細規則與公開 registry 只看 `SCRATCHPACK_SPEC.md`。

### Ticket

Built-in ticket 資源依 GameType 分 namespace；完整 resolver key 是：

```text
(gameType, art.ticket.ref)
```

GameType 1 master assets 保存於 ChatGPT Library：

```text
/ScratchGame/Asset-Library/BuiltInTickets/gameType1/
```

目前已有 `01-red.png`、`01-blue.png`、`02.png`。這些 Library 檔名只對應 master store；公開 ref 仍以 SPEC 為唯一權威。

### Foil

Built-in foil 為跨 GameType 共用的全域 namespace。Master assets 保存於：

```text
/ScratchGame/Asset-Library/Foils/
```

ThreeStar-Test 目前使用 `brushed-silver-three-star`。

## ThreeStar-Test

Pack 位置：

```text
apps/ScratchGame/reference-packs/ThreeStar-Test/
```

目前關鍵設定：

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

因 ThreeStar-Test 的 ticket 與 foil 都引用 Built-in 共用資源，依目前 V1 SPEC，`.scratchpack` 本體不需要再重複攜帶這兩張 PNG；最小內容可只有 `manifest.json` 與 `ticket.json`。

## 下一步

先完成並驗收 ThreeStar-Test `.scratchpack` fixture 與基本 conformance 檢查，再進入 V1 loader / validator。不要另外新增平行資源欄位或為 ThreeStar 開專屬例外。
