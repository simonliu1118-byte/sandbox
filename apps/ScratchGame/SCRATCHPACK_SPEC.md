# ScratchPack Format Specification

Version: 1.0

本文件是 ScratchGame 外部彩券包 `.scratchpack` 的唯一正式格式規格。只要依本文件產出套件，即使沒有原始對話，也應能製作可由相容版本 ScratchGame 匯入的彩券。

## 1. 封裝

- 副檔名：`.scratchpack`
- 實體格式：ZIP archive。
- ZIP 根目錄不得再多包一層資料夾。
- JSON 一律 UTF-8（建議無 BOM）。
- 路徑一律使用 `/`，且必須為相對路徑。
- 禁止 `..`、絕對路徑、磁碟代號、UNC path、symbolic link、hard link 或任何可跳出解壓目錄的項目。
- 建議整包不超過 50 MB；單一檔案不超過 20 MB。

## 2. 必要檔案

```text
manifest.json
ticket.json
layout.json
prizes.json
assets/
```

`sounds/` 為可選。

## 3. 安全限制

ScratchPack 僅允許資料、美術與音效，不得包含可執行程式碼。

禁止副檔名至少包括：

```text
.exe .dll .com .scr .msi .ps1 .bat .cmd .js .vbs .py .pyw .jar .sh
```

亦禁止任何巨集、內嵌可執行 payload、可執行 script、外部 URL 載入程式碼、反射載入 assembly 或要求主程式執行包內二進位內容的設定。

V1.0 允許資源類型：

- 圖片：`.png`
- 音效：`.wav`
- 資料：`.json`

所有資源必須包在 ScratchPack 內；不得依賴本機絕對路徑或網路 URL。

## 4. `manifest.json`

必要欄位：

```json
{
  "formatVersion": "1.0",
  "packageId": "7d2b2f06-0f3d-46af-8afb-542225346345",
  "name": "三星連線 100",
  "author": "C.C.LIU",
  "minimumAppVersion": "0.1.0",
  "ticketFile": "ticket.json",
  "layoutFile": "layout.json",
  "prizesFile": "prizes.json"
}
```

規則：

- `formatVersion`：ScratchPack 規格版本；V1.0 必須為字串 `1.0`。
- `packageId`：UUID/GUID，作為套件唯一識別；不同彩券必須使用不同 ID。
- `name`：套件顯示名稱。
- `author`：作者顯示名稱，可空字串但欄位必須存在。
- `minimumAppVersion`：最低相容 ScratchGame 基礎版本 `X.Y.Z`。
- 三個 `*File` 路徑必須指向包內 JSON，且不得跳出根目錄。

## 5. `ticket.json`

範例：

```json
{
  "ticketId": "three-line-100-v1",
  "displayName": "三星連線",
  "price": 100,
  "ruleId": "ThreeLine",
  "issueSize": 100000,
  "art": {
    "background": "assets/background.png",
    "scratchMask": "assets/mask.png"
  },
  "scratch": {
    "brushRadius": 24,
    "completionRatio": 0.78
  }
}
```

規則：

- `ticketId`：套件內穩定識別字串；建議只用英數、`-`、`_`。
- `displayName`：使用者可見彩券名稱。
- `price`：正整數面額。
- `ruleId`：必須是主程式支援的 Game Rule ID。
- `issueSize`：正整數總發行張數，必須等於 `prizes.json` 全部 `count` 加總。
- `art.background`：必要 PNG。
- `art.scratchMask`：必要 PNG；若版型以程式生成遮罩，仍需在 V1.0 提供一張可用遮罩素材。
- `scratch.brushRadius`：建議 8～80；匯入器可拒絕不合理值。
- `scratch.completionRatio`：0.50～0.95；預設建議 0.78。

V0.1 支援的 `ruleId`：

```text
LuckyNumberMatch
ThreeLine
MatchThree
```

若套件使用未知 `ruleId`，匯入器必須拒絕並指出需要較新主程式；不得自行執行包內程式碼補足玩法。

## 6. `prizes.json`

範例：

```json
{
  "currency": "TWD",
  "tiers": [
    { "id": "jackpot", "amount": 1000000, "count": 1 },
    { "id": "p10000", "amount": 10000, "count": 30 },
    { "id": "p1000", "amount": 1000, "count": 800 },
    { "id": "p500", "amount": 500, "count": 3000 },
    { "id": "p200", "amount": 200, "count": 12000 },
    { "id": "p100", "amount": 100, "count": 25000 },
    { "id": "lose", "amount": 0, "count": 59169 }
  ]
}
```

規則：

- `currency` V1.0 預設 `TWD`。
- 每個 `tiers[].id` 在該彩券內唯一。
- `amount` 為大於等於 0 的整數。
- `count` 為大於 0 的整數。
- 必須至少有一個 `amount = 0` 的未中獎 tier。
- 所有 tier 的 `count` 加總必須精確等於 `ticket.issueSize`。
- 發行時中獎率由程式計算，不得以另一個可互相矛盾的 `winRate` 欄位作權威來源。

計算：

```text
WinningTickets = sum(count where amount > 0)
WinRate = WinningTickets / issueSize
```

匯入畫面顯示此計算結果。

## 7. `layout.json`

所有座標使用「設計座標系」，主程式依票券顯示大小等比例縮放。

範例：

```json
{
  "canvas": { "width": 1000, "height": 650 },
  "title": { "x": 60, "y": 35, "width": 500, "height": 80 },
  "scratchZones": [
    {
      "id": "main",
      "x": 80,
      "y": 160,
      "width": 840,
      "height": 390,
      "required": true
    }
  ],
  "rule": {
    "winningNumberArea": { "x": 120, "y": 170, "width": 760, "height": 90 },
    "playArea": { "x": 120, "y": 285, "width": 760, "height": 240 }
  }
}
```

規則：

- `canvas.width` / `height` 必須為正整數。
- 所有區域必須落在 canvas 範圍內。
- `scratchZones` 至少一個。
- `id` 不得重複。
- `required=true` 的區域全部完成後，視為整張票手動刮獎完成並自動兌獎。
- 每個 Game Rule 可以要求 `rule` 物件內具有額外欄位；匯入器必須依 `ruleId` 驗證。

## 8. Game Rule 資料契約 V1.0

### `LuckyNumberMatch`

`layout.rule` 至少需要：

```json
{
  "winningNumberArea": { "x": 0, "y": 0, "width": 100, "height": 100 },
  "playArea": { "x": 0, "y": 0, "width": 100, "height": 100 },
  "winningNumberCount": 3,
  "playNumberCount": 12,
  "numberMin": 1,
  "numberMax": 30
}
```

主程式必須先抽出最終 Prize Tier，再產生能精確對應該獎金的合法號碼內容；不得用隨機號碼碰運氣決定最後總獎金。

### `ThreeLine`

`layout.rule` 至少需要：

```json
{
  "grid": { "x": 0, "y": 0, "width": 100, "height": 100, "rows": 3, "columns": 3 },
  "winningLines": [
    [0,1,2], [3,4,5], [6,7,8],
    [0,3,6], [1,4,7], [2,5,8],
    [0,4,8], [2,4,6]
  ]
}
```

索引從左到右、由上到下，從 0 開始。主程式同樣必須依已抽出的 Prize Tier 生成可驗證的盤面。

### `MatchThree`

`layout.rule` 至少需要：

```json
{
  "grid": { "x": 0, "y": 0, "width": 100, "height": 100, "rows": 3, "columns": 3 },
  "matchCount": 3
}
```

主程式依 Prize Tier 產生至少 `matchCount` 個符合該獎金規則的相同結果；未中獎票不得意外形成可兌獎組合。

## 9. 美術

- PNG 建議使用 sRGB。
- 建議單張不超過 4096 × 4096。
- 背景可含彩券名稱與裝飾，但實際遊戲數值／符號仍應由引擎在指定 layout 上繪製，避免結果寫死在圖片。
- `scratchMask` 應為可平鋪或可縮放的刮膜素材；不得包含遊戲結果。
- 套件不得直接複製無授權的第三方彩券正式美術、商標或受保護素材。

## 10. 音效

可選：

```text
sounds/scratch.wav
sounds/win.wav
sounds/lose.wav
```

若提供，可在 `ticket.json` 加：

```json
{
  "sounds": {
    "scratch": "sounds/scratch.wav",
    "win": "sounds/win.wav",
    "lose": "sounds/lose.wav"
  }
}
```

主程式找不到可選音效時應靜默使用預設音效或無音效，不得使彩券無法載入；若路徑存在但檔案格式非法，匯入時拒絕。

## 11. 匯入驗證順序

匯入器至少依序檢查：

1. ZIP 結構與 path traversal / link 安全。
2. 檔案類型與大小。
3. `manifest.json` 格式版本與最低 App 版本。
4. 必要 JSON 是否存在且可解析。
5. `ticketId` / `packageId` 是否衝突。
6. `ruleId` 是否支援。
7. `issueSize` 與獎項 count 加總是否一致。
8. 是否存在至少一個未中獎 tier。
9. layout 是否在 canvas 範圍內且符合對應 Game Rule。
10. 所有資源路徑是否合法且檔案存在。
11. 圖片／音效是否可解碼。

任何必要檢查失敗都不得留下半套已匯入資料；整個匯入必須視為一次原子操作。

## 12. 彩券定義不可變規則

匯入 ScratchPack 只建立「彩券定義」，不直接修改既有已發行彩券。

- 尚未發行的匯入彩券可移除後重新匯入。
- 彩券一旦建立第一批，面額、發行量、獎項表與規則視為鎖定。
- 想改獎項表或機率時，必須使用新的 `packageId` / `ticketId`，作為全新彩券匯入。
- 新批次只能用該彩券原始固定獎項表重置票池。

## 13. 第三方／AI 製作檢查表

交付 `.scratchpack` 前確認：

- [ ] 根目錄直接有四個必要 JSON／assets。
- [ ] `formatVersion = 1.0`。
- [ ] `packageId` 是新的 GUID。
- [ ] `ruleId` 是相容主程式已支援的 ID。
- [ ] `issueSize = 所有 prize count 加總`。
- [ ] 至少一個 `amount = 0` tier。
- [ ] layout 所有座標在 canvas 內。
- [ ] 所有必要 scratch zone 已定義。
- [ ] 圖片只用 PNG、音效只用 WAV。
- [ ] 沒有 executable/script、外部 URL、絕對路徑或 `..`。
- [ ] 用 ZIP 封裝後將副檔名改成 `.scratchpack`。

只要本規格版本仍為 1.0，製作者不得依賴未寫在本文件中的私人約定。
