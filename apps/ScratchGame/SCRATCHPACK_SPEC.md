# ScratchPack Format Specification

Specification version: **1.0**  
Implementation status: **規格已定稿；ScratchPack V1 runtime 已實作；PackEditor create-only 基礎流程已實作並納入 V0.5.5 Build 5 正式基準，ScratchPack formatVersion 仍為 1.0。**

本文件是 ScratchPack V1 **封裝、資料格式、共通 ResourceRef 與 built-in asset registry 的唯一正式規格**。GameType 的勝負判定、公開參數、盤面生成、Prize Tier 對應、Renderer 與 GameType-specific 驗證只由 `GAMETYPE_SPEC.md` 定義。

`PROJECT_RULES.md` 只保存永久專案原則；`TODO.md` 只保存未完成工作與未決事項。不得在其他檔案平行維護本文件 schema 或 built-in asset registry。

## 1. 定位與資料責任

ScratchPack 只提供資料、美術與 GameType 允許的公開參數，不包含可執行玩法程式碼。

主程式負責驗證、載入與運行 Pack，並提供 GameType engine、Renderer、盤面生成、刮膜互動、票號、程式面額、批次、有限票池、Pending Ticket、兌獎、中獎效果、音效、硬幣、使用者、錢包與累積遊玩統計。

ScratchPack 負責彩券名稱、作者、面額、Canvas code、GameType 與公開參數、票面資源引用、GameType 額外資源、刮區 geometry、銀膜資源引用、`issueSize`、`ticketsPerBook` 與最終 Prize Pool / Prize Tier。

PNG / WAV 對主程式是 opaque render asset。Importer 可檢查路徑、安全性、格式、尺寸與能否解碼，但不得靠 OCR、像素分析或檔名推導彩券名稱、面額、中獎率、最高獎金、Prize Tier、GameType、刮區、批次、日期或票號。

圖片可以印「中獎率 100%」「最高可中一百萬！」等宣傳字樣；這些只屬美術內容，不成為程式資料來源。第一款「三星連線」只是選擇不放這兩類宣傳字樣，並非 V1 全域限制。

## 2. 封裝與安全

- 副檔名：`.scratchpack`。
- 實體格式：ZIP；ZIP 根目錄不得再多包一層資料夾。
- JSON：UTF-8。
- 路徑：一律 `/`、相對路徑；禁止 `..`、絕對路徑、磁碟代號、UNC、symbolic link、hard link 或任何可跳出解壓目錄的項目。
- V1 只允許資料與 PNG 資源；不得包含 DLL、EXE、script、macro 或任何可執行 payload。
- V1 暫不開放 ScratchPack 自訂中獎音效或中獎動畫。

如果 Pack 全部使用 Built-in 資源，最小合法套件可以只有：

```text
manifest.json
ticket.json
```

只有當 Pack 使用 `source="package"` 的資源時才需要 `assets/`。例如：

```text
manifest.json
ticket.json
assets/
  my-ticket.png
  my-foil.png
```

V1 **不定義、也不接受 `thumbnail.png`**。挑券縮圖屬主程式可重建 runtime cache：Pack 安裝後由正式 Pack / runtime definition 產生；cache 遺失或損壞時重新產生，不成為 Pack 權威資料。

## 3. `manifest.json`

### 3.1 固定欄位

```json
{
  "formatVersion": "1.0",
  "packageId": "UUID",
  "name": "Pack 名稱",
  "author": "作者",
  "createdUtc": "ISO-8601 UTC"
}
```

- `formatVersion`：ScratchPack schema 版本，目前固定 `1.0`。
- `packageId`：UUID v4；永久唯一身分。
- `name`：Pack 名稱。
- `author`：作者。
- `createdUtc`：建立時間。

`packageId` 是 Pack 的唯一 package identity。Runtime 安裝時同一 packageId 再匯入視為衝突，不是更新；V0.x 不提供 replace / upgrade。

## 4. `ticket.json`

```json
{
  "ticketId": "three-star-test",
  "displayName": "三星連線（測試）",
  "price": 500,
  "canvas": 1,
  "gameType": "1",
  "gameParams": {},
  "issueSize": 8,
  "ticketsPerBook": 8,
  "priceDisplay": 1,
  "priceDisplayArea": { "x": 852, "y": 43, "width": 201, "height": 82 },
  "serialDisplayArea": { "x": 364, "y": 774, "width": 350, "height": 59 },
  "art": {
    "ticket": { "source": "builtin", "ref": "01-blue" }
  },
  "scratch": {
    "foil": { "source": "builtin", "ref": "brushed-silver-three-star" },
    "zones": []
  },
  "prizes": []
}
```

## 5. Canvas

ScratchPack 使用固定 canvas code，不使用任意圖片尺寸作 runtime 座標系。

目前：

```text
canvas=1 -> 1080 x 882
```

一旦發布的 canvas code 代表尺寸不可變更；需要新尺寸時新增新 code。

所有位置與尺寸都以 canvas design coordinates 表示，runtime 依實際顯示比例縮放。

## 6. ResourceRef

### 6.1 結構

```json
{ "source": "builtin", "ref": "01-blue" }
```

或：

```json
{ "source": "package", "ref": "assets/my-ticket.png" }
```

### 6.2 `source="builtin"`

由主程式 built-in registry 解析；Pack 內不帶檔案。

Ticket art 的 built-in namespace 由 `gameType` 決定。GameType 1 目前正式 refs：

```text
01-red
01-blue
02
```

Foil 使用共用 namespace，目前正式 refs：

```text
brushed-silver-plain
brushed-silver-three-star
```

對應 runtime canonical paths：

```text
BuiltInAssets/Tickets/gameType1/01-red.png
BuiltInAssets/Tickets/gameType1/01-blue.png
BuiltInAssets/Tickets/gameType1/02.png
BuiltInAssets/Foils/brushed-silver-plain.png
BuiltInAssets/Foils/brushed-silver-three-star.png
```

Built-in registry 的新增與變更只能在本文件更新，不得在 README / handoff 平行另列第二份權威清單。

### 6.3 `source="package"`

`ref` 必須指向 ZIP 內相對路徑，且通過安全路徑檢查。V1 package resource 目前只允許 PNG。

## 7. Ticket art 與 dynamic ownership

Base ticket art 是靜態美術底圖。下列資料由程式動態擁有，不應燒死在 Base ticket art：

- 程式面額（當 `priceDisplay=1`）
- 票號 / serial
- 刮膜
- Scratch Zone 內動態符號 / 數字 / prize content
- 中獎結果與程式 UI

彩券美術可以包含固定說明字、玩法文字、裝飾、印刷風格資訊與不作為程式資料來源的宣傳文字。

## 8. `priceDisplay` / display areas

- `priceDisplay=0`：主程式不額外繪製面額。
- `priceDisplay=1`：主程式依 `price` 在 `priceDisplayArea` 繪製面額。

`priceDisplayArea` / `serialDisplayArea` 為 canvas design coordinates。

這兩個 area 只能定義程式資料的顯示位置，不改變資料本身。

## 9. Scratch geometry

`scratch.zones` 是所有 ScratchSurface 的權威 geometry；Renderer 與刮膜不得另外維護近似座標。

每個 zone 使用：

```json
{
  "x": 219,
  "y": 256,
  "width": 191,
  "height": 138,
  "shape": "roundedRectangle",
  "cornerRadius": 12
}
```

V1 目前 shape：

```text
rectangle
roundedRectangle
ellipse
```

GameType 對 zones 的數量、順序、對齊、語意與額外限制只由 `GAMETYPE_SPEC.md` 定義。

## 10. Prize Pool

共通 schema：

```json
{
  "amount": 100,
  "count": 1
}
```

`prizes` 是整批有限票池的 Prize Tier 清單。GameType 如何把某個 Prize Tier 映射到盤面 outcome，由 `GAMETYPE_SPEC.md` 定義，不得在共通 Prize schema 增加 GameType-specific outcome 欄位。

總發行量：

```text
issueSize
```

正獎票數：

```text
SUM(prizes[].count)
```

未中獎票數：

```text
issueSize - SUM(prizes[].count)
```

必須 >= 0。

## 11. `ticketsPerBook`

```text
issueSize > 0
ticketsPerBook > 0
issueSize % ticketsPerBook == 0
```

Runtime 可由此推導總本數，不另外保存重複欄位。

## 12. GameType

`gameType` 是字串 ID；目前基本 GameType 契約與合法公開參數由 `GAMETYPE_SPEC.md` 定義。

ScratchPack 不允許攜帶玩法 DLL、script、expression engine 或自訂程式碼。

已發布 GameType 的勝負核心語意不可被同 ID 靜默改寫；若玩法核心不同，新增 GameType / variant。

## 13. PackEditor contract

PackEditor 不是第二套 schema owner。

- 與 ScratchGame Importer / runtime 共用 model、loader、validator。
- 只輸出目前正式支援的 canvas / GameType / ResourceRef / 參數。
- create-only；不讀回既有 Pack 作修改。
- 每個新 draft 建立新的 UUID v4 packageId。
- Export 前建立真實 `.scratchpack` 後再用正式 `ScratchPackV1Loader` round-trip。
- win rate、losing count、top prize、average prize、payout rate 等是 editor derived display，不寫入 schema。

## 14. Built-in / Imported

Built-in 與 Imported `.scratchpack` 格式完全相同，只差 installation source。

正式 Built-in Pack 也由 PackEditor 產生。使用者定稿 `.scratchpack` 後，正式 release 將該檔原樣放入 `BuiltInPacks/`；不得再維護另一套 built-in-only schema。

Imported Pack：

- 可 hide / unhide。
- 可 uninstall，但有 Pending Ticket 時拒絕。

Built-in Pack：

- 可 hide / unhide。
- 不可 uninstall。

## 15. Validation minimum

Loader 至少驗證：

- ZIP 安全路徑。
- `manifest.json` / `ticket.json` 存在且唯一。
- `formatVersion` 支援。
- packageId 合法。
- canvas code 支援。
- GameType 支援。
- GameType params 由對應 GameType validator 驗證。
- ResourceRef source/ref 合法。
- package PNG 存在、可解碼、限制尺寸 / byte size。
- `issueSize` / `ticketsPerBook` 合法。
- Prize Pool 不超過 issueSize。
- display areas / zones 在 canvas 內。
- zones 不得為零尺寸。
- GameType-specific geometry / semantics 交由 `GAMETYPE_SPEC.md` 契約驗證。

## 16. Canonical TestPack

開發與回歸測試使用：

```text
TestPacks/ThreeStar-Test.scratchpack
```

Repository 不提交 `.scratchpack` binary；保存：

```text
reference-packs/ThreeStar-Test/manifest.json
reference-packs/ThreeStar-Test/ticket.json
```

並由：

```text
tools/build_reference_testpack.py
```

deterministic 產生 canonical fixture。

目前 fixture：

```text
size: 800 bytes
SHA-256: 2e1b00c03588af9380fb48f25b7475cdd74affdcb1f4d7f6ed23ddd00efcd42a
```

TestPacks 的 portable 保留策略以 `PROJECT_RULES.md` / `RUNTIME_PACKAGE.md` 為準。
