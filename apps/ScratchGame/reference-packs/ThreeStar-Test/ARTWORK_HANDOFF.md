# ThreeStar-Test Artwork Handoff

更新日期：2026/09/18

本檔只記錄 ThreeStar-Test 的美術歷史與目前 reference Pack 狀態，不是 ScratchPack schema 或 GameType 規格來源。權威規格仍為 `SCRATCHPACK_SPEC.md` 與 `GAMETYPE_SPEC.md`。

## 目前 Reference Pack

`ThreeStar-Test` 目前正式 reference source：

- `manifest.json`
- `ticket.json`
- `packageId = 2e1e951e-0ca0-4b47-8146-82e0415d6155`
- `canvas = 1`，固定 `1080 × 882`
- `gameType = "1"`
- `gridSize = 3`
- `issueSize = 8`
- `ticketsPerBook = 8`
- `allowNearMiss = true`
- Built-in ticket ref：`01-blue`
- Built-in foil ref：`brushed-silver-three-star`

目前 `ticket.json` 已接受 geometry：

```text
zone size: 191 × 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

以上才是目前 geometry 權威；早期 160×100 / X 276,460,644 / Y 225,353,481 等數值已廢止，不得再依舊草稿恢復。

## 目前美術資產責任

- Dynamic price：`priceDisplay=1` 時由主程式依 `priceDisplayArea` 繪製。
- Dynamic serial：由主程式依 `serialDisplayArea` 繪製。
- 刮膜：由 `scratch.foil` ResourceRef + `scratch.zones` geometry 驅動。
- 動態遊戲內容：由 GameType Renderer 負責。
- Base ticket art 不燒死面額、票號、動態遊戲內容或刮膜。
- ScratchPack V1 不使用舊 `art.mask` 欄位；正式銀膜入口是 `scratch.foil`。

ThreeStar-Test 目前 ticket 與 foil 都使用 Built-in ResourceRef，因此 canonical `.scratchpack` 只需要 `manifest.json + ticket.json`，不必在 Pack 內重複 PNG。

## 歷史美術草稿

`artwork-drafts/` 只保存設計參考，不是 production authority。現有歷史檔包括：

- `01-blue-original-concept.webp`
- `02-red-classic-concept.webp`
- `03-red-clean-base-draft.webp`
- `04-blue-clean-base-draft.webp`
- `05-silver-foil-style-a.webp`

這些早期 generation 約為 1388×1133，只可作概念參考；不得拿其尺寸或舊 geometry 覆蓋目前 `ticket.json`。

正式可公開引用的 Built-in ticket / foil ref 清單只由 `SCRATCHPACK_SPEC.md` 定義。實際 portable runtime canonical assets 位於：

```text
RuntimeAssets/Live/BuiltInAssets/Tickets/gameType1/
RuntimeAssets/Live/BuiltInAssets/Foils/
```

## ThreeStar-Test 測試獎池

```text
1 line  -> 100
2 lines -> 500
3 lines -> 1000
4 lines -> 2500
5 lines -> 5000
6 lines -> 10000
8 lines -> 100000
0 lines -> derived losing ticket
```

## 不要做的事

- 不要把歷史草稿尺寸／geometry 當成目前 reference definition。
- 不要恢復已廢止的 `art.mask` / 第二套 mask pipeline。
- 不要把面額或票號燒進 Base art。
- 不要為 ThreeStar 再新增另一套硬編碼 ticket definition。
- 不要因 reference Pack 美術工作順便修改主畫面 Header / Stage / Footer 邊界。

若未來要新增票面或銀膜樣式，先依 `SCRATCHPACK_SPEC.md` 新增穩定 ResourceRef，再依 binary asset SOP 驗證 canonical source、尺寸、size / SHA-256 與 Windows portable。