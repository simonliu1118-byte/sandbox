# ScratchPack V1 / Maker 設計計畫

狀態：**已討論定案、尚未實作**  
實作時機：**等待 ScratchGame V0.3.0 Build 3 使用者驗收完成後再開始**。

本文件記錄下一階段 ScratchPack V1、ScratchPack Maker 與 V1.0.0 基礎玩法的已定案方向，避免依賴聊天記憶。現行 `SCRATCHPACK_SPEC.md` 仍代表目前已實作格式；在新格式真正完成前，不把本文件當成已支援能力。

---

## 1. V1.0.0 目標

ScratchGame V1.0.0 前至少完成以下六種基礎玩法：

| GameType | 名稱 | 核心判定 |
|---|---|---|
| `1` | 星星連線 | 完成線數對應固定獎項 |
| `2` | 中獎號碼 | 中獎號碼與你的號碼命中後累加獎金 |
| `3` | 三個相同 | 三個相同金額即得該金額 |
| `4` | 符號計數 | 單符號多數量／多符號固定數量 |
| `5` | 賓果 | 棋盤命中成線，每條線獎金累加 |
| `6` | 比大小 | 多個獨立比較區，中獎區塊獎金累加 |

原先討論的「直接刮格子、每條線各自累加獎金」不另占基本 GameType；若未來確有需求，優先作為 GameType 1 的變體，例如 `1-2`。

V1.0.0 不以玩法數量為目標；以上六型完成、Maker 可正確產包、Importer 可正確驗證／匯入、有限票池與兌獎均可穩定運作後，才進入 1.0.0。

---

## 2. ScratchPack V1 定位

ScratchPack 只提供資料、美術與玩法允許的公開參數，不包含可執行玩法程式碼。

主程式負責：

- GameType 規則引擎。
- 盤面生成與結果合法性。
- 刮膜互動。
- 票號。
- 面額程式顯示。
- 批次與有限票池。
- 兌獎。
- 中獎效果、音效與硬幣。
- 使用者與損益資料。

ScratchPack 負責：

- 彩券名稱、作者、面額。
- 固定 Canvas 代碼。
- 票面 PNG。
- GameType 與該玩法允許的參數。
- GameType 所需刮區的位置、尺寸與形狀。
- 總發行張數、每本張數。
- 最終獎池張數。
- 該 GameType 允許的額外符號／銀膜等資源。

V1 不允許：

- 包內程式碼或 Script。
- 自訂玩法引擎。
- ScratchPack 自己定義任意 winning lines / winning patterns。
- 額外裝飾刮區。

---

## 3. 最小封裝結構

`.scratchpack` 實體仍為 ZIP。

最小合法套件：

```text
manifest.json
ticket.json
assets/
  ticket.png
```

可選：

```text
assets/mask.png
```

各 GameType 若需要自訂符號素材，可在 `assets/` 下增加對應 PNG。

### Thumbnail 規則

ScratchPack V1 **不定義、也不接受 `thumbnail.png`**。

選票畫面的縮圖一律由主程式讀取 `ticket.png` 後等比例產生。Maker 不建立 thumbnail 欄位或縮圖資源。

V1 暫不開放 ScratchPack 自訂中獎音效／中獎動畫；這些由主程式與未來玩家商店系統管理。

---

## 4. manifest.json

目標結構：

```json
{
  "formatVersion": "1.0",
  "packageId": "GUID",
  "author": "C.C.LIU",
  "minimumAppVersion": "0.4.0",
  "ticketFile": "ticket.json"
}
```

規則：

- `packageId` 為套件唯一識別，Maker 自動產生 GUID。
- 不重複保存與 `ticket.json` 相同的彩券名稱。
- 不再拆分 `layout.json` / `prizes.json`，避免多份 JSON 互相矛盾。
- ScratchPack 作者不填本機 `styleNumber`；款式編號由主程式第一次匯入時自動分配並永久綁定本機安裝資料。

---

## 5. ticket.json 核心概念

基礎結構：

```json
{
  "name": "三星連線",
  "price": 500,
  "canvas": 1,
  "priceDisplay": 0,
  "gameType": "1",
  "issueSize": 10000,
  "ticketsPerBook": 100,
  "art": {
    "ticket": "assets/ticket.png",
    "mask": null
  },
  "serialDisplayArea": {
    "x": 64,
    "y": 742,
    "width": 205,
    "height": 36
  },
  "scratch": {
    "zones": []
  },
  "game": {},
  "prizes": []
}
```

ScratchPack 不保存中獎率、未中獎率、EV、RTP、總本數、未中獎張數等可推導資料。

資料責任分為三層：

1. **玩法參數**：哪些結果算中獎，由 GameType + ScratchPack 公開參數決定。
2. **盤面顯示資料**：例如可用格內金額、線獎金、符號素材；不是最終 Prize Pool。
3. **最終 Prize Pool**：每張票最後真正派出的總獎金；有限票池只認這一層。

---

## 6. Canvas、票面與動態層

V1 只允許主程式公告的固定 Canvas 代碼，不允許 ScratchPack 任意輸入寬高。

目前正式基準：

```text
canvas = 1  →  1080 × 882
```

新增 Canvas 只能增加新代碼；既有代碼的尺寸意義永遠不得改變。

Maker 選 Canvas。`ticket.png` 尺寸若不完全符合 Canvas，直接報錯；不得偷偷縮放、裁切或重新取樣。

### ticket.png 負責

- 彩券背景。
- 彩券名稱與固定說明。
- 裝飾圖案。
- 刮區底框、底色、金框等固定美術。
- `priceDisplay = 0` 時的完整面額美術。

### 主程式負責

- 每張票動態符號、數字、金額與遊戲結果。
- 銀膜建立與刮除。
- 彩券序號。
- `priceDisplay = 1` 時的完整標準面額徽章。
- 命中標記。
- 批次／票池狀態。
- 中獎效果、音效、硬幣。

疊圖順序：

```text
ticket.png
→ 動態遊戲內容
→ 刮膜
→ 票號 / 程式面額
→ 命中標記 / 硬幣
→ 中獎特效 / 結果 UI
```

---

## 7. 面額與票號

### priceDisplay

- `0`：面額完整畫在 `ticket.png`，主程式不再疊面額。
- `1`：底圖留空，ScratchPack 提供 `priceDisplayArea`，主程式在該區域畫完整標準面額徽章。

### serialDisplayArea

ScratchPack 只提供票號區域的位置與大小。票號格式、字型、圓角框、底色由主程式統一處理。

票號格式：

```text
款式編號-本號-本內序號
```

每段至少三位補零，超過三位時自然增加位數，不截斷、不換行。

---

## 8. Scratch Zone 共通規則

V1 支援：

```text
rectangle
roundedRectangle
circle
ellipse
```

- `roundedRectangle` 可有 `cornerRadius`。
- `circle` 必須 `width == height`，否則拒絕。
- 所有座標必須完整落在 Canvas 內。
- zone ID 不得重複。
- GameType 需要多少刮區，就必須剛好有多少刮區。
- 不允許額外裝飾刮區。

基本 zone：

```json
{
  "id": "cell01",
  "x": 281,
  "y": 225,
  "width": 163,
  "height": 101,
  "shape": "roundedRectangle",
  "cornerRadius": 12
}
```

### 不提供 contentBox

V1 取消通用 `contentBox`。Scratch zone 只負責位置、尺寸與形狀；數字、金額、符號等內部排版由各 GameType Renderer 固定。

這避免同一玩法因作者自行調整內部比例而造成不同票面行為。

### 銀膜

- `art.mask = null` 或缺省：使用主程式預設銀膜。
- 指定 `assets/mask.png`：使用包內銀膜材質。

---

## 9. 發行量、本數與 Prize Pool 共通規則

開發者輸入：

```text
issueSize
ticketsPerBook
```

必須：

```text
issueSize > 0
ticketsPerBook > 0
issueSize % ticketsPerBook == 0
```

自動計算：

```text
bookCount = issueSize / ticketsPerBook
```

`bookCount` 不寫入 ScratchPack。

### prizes

最終獎池每一個 `amount` 在同一張票中必須唯一，且：

```text
amount > 0
count >= 0
```

不保存 `amount = 0` 的未中獎 tier。

同一金額要增加發行張數，只增加該列 `count`，不得重複建立相同 `amount`。

計算：

```text
winningCount = Σ prizes.count
loseCount = issueSize - winningCount
```

規則：

- `winningCount < issueSize`：差額全部自動視為未中獎。
- `winningCount == issueSize`：100% 中獎，合法。
- `winningCount > issueSize`：錯誤，禁止封裝與匯入。

### count = 0

對具有固定 tier 順序的玩法，例如 GameType 1、GameType 4 `singleSymbolProgressive`，某個固定結果可以設定 `count = 0`；該列仍保留，用來維持 tier 與玩法結果的固定對應。

對 Maker 自動推導最終獎金的玩法，UI 可顯示所有可生成金額，但輸出套件時可只保存 `count > 0` 的最終 Prize Tier，因為這些金額沒有位置順序語意。

---

## 10. Maker 自動統計

Maker 依面額、issueSize、ticketsPerBook、prizes 即時計算：

- 總本數。
- 有獎張數。
- 未中獎張數。
- 中獎率／未中獎率。
- 總派彩金額。
- 每張期望值 EV。
- RTP／回收率。
- 每張期望損益。
- 打平以上機率（`amount >= price`）。
- 真正賺錢機率（`amount > price`）。

這些皆為 Derived Data，不作 ScratchPack 權威欄位。

---

## 11. GameType 共通原則

ScratchPack 只選玩法與填公開參數，不得自行寫規則。

舊 GameType 的底層行為一旦正式發布就不得改變；需要不同規則時新增變體 ID，例如 `1-2`，而不是偷偷改 `1`。

新增可選參數必須向下相容；舊包缺少新欄位時，使用不改變舊行為的預設值。

同類動態元件的尺寸與內部 Renderer 必須一致；Maker 以模板方式控制整組元件，不允許逐格做出不同字級、比例或內部排列。

---

## 12. GameType 1：星星連線

```text
gameType = "1"
```

### 基本規則

- `gridSize`：3、4、5。
- 只計所有完整橫線、完整直線與兩條大斜線。
- 不計短斜線，不允許 ScratchPack 自訂 winningLines。
- 所有刮區都是盤面格。

刮區數量：

```text
3×3 → 9
4×4 → 16
5×5 → 25
```

`cellZones` 必須剛好引用所有 zones 一次，不得缺少、重複或額外存在。

### allowNearMiss

```text
allowNearMiss = false   預設
```

控制是否允許生成「差一顆星即可成線」等近似中獎盤面。

### Prize Tier

固定可達線數：

```text
3×3：1、2、3、4、5、6、8
4×4：1、2、3、4、5、6、7、8、10
5×5：1、2、3、4、5、6、7、8、9、10、12
```

Maker 建立固定 tier 列，開發者填各 tier 獎金與張數。最後一列永遠代表全部有效線完成。

獎金必須隨線數嚴格增加；不得出現線數更多但獎金相同或更低的設定。

---

## 13. GameType 2：中獎號碼

```text
gameType = "2"
```

### 核心規則

一組「中獎號碼」與一組「你的號碼」進行比對。每個你的號碼若命中任一中獎號碼，即形成一次命中；所有命中項目的獎金累加為本張最終獎金。

兩組號碼各自不得重複；兩組之間可以相同，這就是命中。

中獎號碼與你的號碼全部都是刮區，玩家必須刮開全部玩法刮區才算手動完成。

### payoutSource

同一個 GameType 2 支援：

```text
play      獎金跟著「你的號碼」
winning   獎金跟著「中獎號碼」
```

不拆成額外 GameType 變體。

### 數量與數字範圍

開發者設定：

```text
winningNumberCount
playNumberCount
numberMin
numberMax
```

Maker 必須依實際 Prize Tier、可用命中數、是否存在未中獎票等條件直接驗證數字範圍是否足以產生合法盤面，不以過度簡化的固定公式代替實際驗證。

### 格內金額

```text
displayPrizeAmounts
prizeAmountUsage = repeatable | uniquePerTicket
```

- `repeatable`：同一金額可在同一張票的獎金格重複出現。
- `uniquePerTicket`：同一金額在同一張票的獎金格最多使用一次。

`displayPrizeAmounts` 是盤面可用金額，不是最終 Prize Pool。

主程式先取得本張最終 Prize Tier，再尋找符合 `payoutSource`、命中數上限及 `prizeAmountUsage` 的合法組合；若無法精確組成，Maker 必須在封裝前報錯。

### 固定 Renderer

有獎金的一側固定：

```text
大號數字在上
小號金額在下
```

無獎金的一側只顯示固定大小的大號數字。

同一張票所有 Type 2 格子的尺寸、數字字級、金額字級、比例、間距與位置全部一致，不允許個別格縮放。最長金額若無法放入固定模板，Maker 報錯。

---

## 14. GameType 3：三個相同

```text
gameType = "3"
```

### 核心規則

同一個金額恰好出現三次，即得該金額；不是三倍金額。

- `matchCount` 固定為 3，不作 ScratchPack 設定。
- `zoneCount` 由開發者在 Maker 支援範圍內自由設定。
- 本張若中 `$500`，引擎必須恰好生成三個 `$500`。
- 其他任何金額最多出現兩次。
- 未中獎票所有金額都最多出現兩次。
- 一張票只允許一組真正成立的「三個相同」，不做多組累加。

### 非中獎金額

```text
useCustomDecoyAmounts = false   預設
```

預設 false 時，其他格由正式獎池中可用的其他正獎金額填充。

開啟 true 時，開發者可額外提供 `decoyAmounts`，作為非中獎位置的補充顯示金額。`decoyAmounts` 不建立新的 Prize Tier，也不能改變本張真正中獎金額恰好出現三次的規則。

Maker 必須確認目前可用金額種類足以填滿 `zoneCount` 而不意外形成第二組三個相同。

### Renderer

每格只顯示金額；同一張票所有格尺寸與金額字級固定，不因金額長短個別縮放。Maker 事前檢查最長金額是否可完整顯示。

---

## 15. GameType 4：符號計數

```text
gameType = "4"
```

Type 4 有兩種固定模式，但共用同一個 GameType，因底層都是「統計符號出現次數」。

### Mode A：singleSymbolProgressive

一種中獎符號，不同出現數量對應不同獎項。

例如：

```text
3 個 ★ → $100
4 個 ★ → $500
5 個 ★ → $1,000
```

開發者設定最低中獎數；Maker 建立連續的數量 tier。數量越多，獎金必須嚴格增加。

對抽中的 tier，引擎必須生成恰好對應數量的 target symbol；不能多一個，否則會落入下一個 tier。

### Mode B：multiSymbolFixedCount

多種有獎符號共用一個固定 `matchCount`。

例如：

```text
★★★ → $100
○○○ → $500
◆◆◆ → $1,000
```

每個被判定為本張中獎的符號，必須恰好出現 `matchCount` 個；不能多一個。未成立的有獎符號最多出現 `matchCount - 1` 個。

開發者設定：

```text
allowMultipleWins = false | true
```

- false：一張票最多只有一種有獎符號成立。
- true：可同時成立多種有獎符號，獎金全部累加。

Mode B 的最終 Prize Pool **由 Maker 自動推導**：

- false：依可成立的單一符號獎金推導。
- true：依所有合法符號組合、zoneCount 與 `matchCount` 推導可生成總獎金。

Maker 顯示可生成總獎金，開發者只填各金額發行張數；不自行新增任意最終獎金。

### 干擾符號

兩種 mode 都支援：

```text
useCustomDecoySymbols = false   預設
```

預設 false 時，由主程式提供通用無獎干擾符號，且其他未成立的有獎符號也可以在合法次數內作為盤面干擾。

開啟 true 時，ScratchPack 可提供 `decoySymbols`。這些符號永遠沒有獎金，不建立 Prize Tier。

### Renderer

同一張票所有 Type 4 刮區尺寸一致；所有符號使用一致的顯示框與縮放規則，不允許單一符號特別放大或縮小。

---

## 16. GameType 5：賓果

```text
gameType = "5"
```

### 基本規則

- `gridSize`：3、4、5。
- `contentType`：`symbol` 或 `number`。
- 棋盤每格內容在單張票內唯一。
- 「你的符號／你的號碼」在單張票內也唯一。
- 棋盤與「你的符號／號碼」兩邊全部都是刮區。
- 玩家可以先刮棋盤、先刮自己的項目或交錯刮，順序不影響結果。
- 若「你的項目」已先刮出，之後棋盤對應格揭露時立即顯示命中標記；反之亦然。
- 手動模式下，所有棋盤與你的項目刮區都完成後才自動兌獎；不因中途已可推導最終結果而提前跳結果。

### 中獎線

基本 Type 5 只計：

- 所有完整橫線。
- 所有完整直線。
- 兩條完整大斜線。

每條線由開發者設定固定獎金，且必須 `> 0`；多條成立時全部累加。

不支援 FREE 格、四角、X 形、十字、外框或自訂 Pattern。這些若未來需要，使用新的 Type 5 變體，不修改基本 Type 5。

### drawCount

開發者設定「你的符號／號碼」刮區數量 `drawCount`。Maker 依可用唯一內容與 decoy 設定驗證是否足以生成合法盤面。

### 干擾項

```text
allowDecoys = false   預設
```

false：你的項目全部必須存在於棋盤上。

true：允許混入棋盤不存在的項目。

- `contentType = symbol`：可提供棋盤外 decoy symbol 素材。
- `contentType = number`：由設定的數字範圍中挑選未出現在棋盤的號碼作干擾。

### 最終 Prize Pool

Type 5 的最終獎金不由開發者任意輸入。

Maker 依：

- gridSize。
- drawCount。
- decoy 規則。
- 每條線固定獎金。

自動計算所有真正可生成的最終總獎金，再讓開發者只填每個金額的發行張數。

### 快速精確計算

不得用 5×5 全部 `2^25` 盤面暴力窮舉作為主要演算法。

5×5 只有 12 條有效線，因此可先枚舉最多：

```text
2^12 = 4096
```

種「成立線集合」。對每組線集合：

1. 求成立這些線必須命中的棋盤格。
2. 重新驗證是否會被迫同時完成其他線。
3. 使用小型回溯／bitmask 計算在不多完成其他線的前提下，可額外命中的格數範圍。
4. 結合 `drawCount` 與 decoy 規則判斷此線集合是否真正可生成。
5. 計算成立線獎金總和並去重。

因此 3×3、4×4、5×5 都必須支援，不因計算量而砍掉 4×4 / 5×5。

---

## 17. GameType 6：比大小

```text
gameType = "6"
```

### 基本規則

一張票有多個相同模板的比較區；每區有兩個可刮數字。依整張票共用的比較方向判斷該區是否中獎，所有中獎區獎金累加為本張最終獎金。

比較方向：

```text
greater   左側／幸運值 > 右側／你的值
less      左側／幸運值 < 右側／你的值
```

相等固定為未中獎，不支援 `>=` / `<=`。

開發者可自訂兩側顯示標題，例如：

```text
幸運數字 / 你的數字
莊家 / 玩家
對手 / 你
```

標題只影響顯示，不改變比較邏輯。

### 相對排列

整張票只能選：

```text
horizontal   左右
vertical     上下
```

固定順序：第一側在左／上，第二側在右／下。不開放逐區反轉或斜排。

### 共用區塊模板

開發者可以在 Maker 調整一次比較區模板尺寸；所有區塊必須共用相同尺寸、相同內部比例與相同 Renderer。

新增區塊時只複製模板並移動整個區塊位置，不能讓不同區塊的 lucky/your 數字格大小不同。

### 獎金顯示位置

```text
prizePlacement = lucky | your | top | bottom
```

- `lucky`：獎金跟著第一側數字，大數字在上、小金額在下。
- `your`：獎金跟著第二側數字，大數字在上、小金額在下。
- `top`：獎金獨立顯示在整個比較區上方。
- `bottom`：獎金獨立顯示在整個比較區下方。

整張票所有比較區使用同一個 `prizePlacement`。不提供逐區自由拖曳獎金位置。

### 數字範圍

開發者設定 `numberMin` / `numberMax`。不同區塊之間允許重複數字；同一區可出現相等數字，相等即未中獎。

### 一般獎金模式

```text
useCustomBlockPrizes = false   預設
```

開發者設定：

```text
displayPrizeAmounts
prizeAmountUsage = repeatable | uniquePerTicket
```

主程式先取得本張最終 Prize Tier，再決定哪些區塊中獎及各區顯示多少金額，使中獎區金額總和精確等於本張 Prize Tier。

Maker 必須事前驗證所有正式 Prize Tier 都能在 `blockCount`、可用金額及重複規則下精確生成。

### 自訂每區固定獎金

```text
useCustomBlockPrizes = true
```

開發者直接設定每一區固定獎金。該區的金額不會因不同票而改變。

此模式不再使用 `displayPrizeAmounts` / `prizeAmountUsage`；Maker 以 subset-sum / 動態規劃快速計算所有可生成總獎金，開發者只填各金額發行張數，不自行新增任意最終獎金。

---

## 18. Maker：GameType-aware 精靈流程

Maker 第一版不做成通用空白刮區編輯器；先選 GameType，再由 Maker 產生該玩法的模板與必要欄位。

建議流程：

1. **基本資料**：名稱、作者、面額、Canvas、GameType、issueSize、ticketsPerBook。
2. **票面美術**：匯入 `ticket.png`；選擇 `priceDisplay`；指定程式面額區與票號區。
3. **玩法模板**：依 GameType 建立對應布局，例如 N×N 格、號碼兩組、比較區模板、Bingo 棋盤＋你的項目。
4. **位置配置**：在票面預覽拖動整組／整格位置；同類元件尺寸由共用模板控制，不逐格自由改內部比例。
5. **玩法參數**：依 GameType 顯示有限且明確的公開參數。
6. **獎池設定**：固定 tier 玩法填獎金／張數；可推導玩法先由 Maker 產生合法總獎金，再只填張數。
7. **即時統計**：顯示未中獎張數、EV、RTP、中獎率等。
8. **預覽測試**：可指定某個最終獎項產生測試票，檢查動態內容、銀膜、票號、面額與刮獎命中。
9. **完整驗證與封裝**：全部通過後才輸出 `.scratchpack`。

Maker 專案檔（例如 `.scratchproj`）目前**只保留為概念考慮，不列入 TODO，也不列入正式未來開發計畫**。

---

## 19. Maker / Importer 必要驗證

Maker 與主程式匯入器都必須驗證：

- ZIP / 路徑安全，禁止可執行內容。
- manifest / ticket JSON 可解析且版本相容。
- `packageId` 唯一且合法。
- 不存在 V1 未定義資源，例如 `thumbnail.png`。
- `canvas` 為主程式已支援固定代碼。
- `ticket.png` 尺寸完全符合 Canvas。
- 所有座標完整落在 Canvas 內。
- `priceDisplay = 1` 時存在合法 `priceDisplayArea`。
- `serialDisplayArea` 合法。
- scratch zone 數量精確符合 GameType。
- zone ID 唯一、shape 參數合法。
- 各 GameType zone mapping 不缺漏、不重複、不引用不存在的 zone。
- 同類模板要求的尺寸一致。
- 固定 Renderer 的最長數字／金額可完整顯示。
- GameType 與公開參數受目前 App 版本支援。
- `issueSize % ticketsPerBook == 0`。
- final prize `amount > 0` 且同一張票中唯一。
- prize `count >= 0`。
- `Σ prize.count <= issueSize`。
- Maker 自動推導型玩法的 final prize 必須確實可生成。
- 所有 PNG 可正常解碼。

任何驗證失敗都不得留下半套已安裝資料；整個匯入必須為原子操作。

---

## 20. 未來正式彩券 ScratchPack 化

不是 Build 3 工作內容。

方向：

- 除開發測試票外，正式彩券逐步從主程式硬編碼移除。
- 正式彩券改由 `.scratchpack` 安裝。
- 主程式保留 GameType 引擎與共通框架。
- 移除已安裝 ScratchPack 時，該彩券專屬定義與資源一起移除，不應因主程式硬編碼而重新出現。
- 已存在歷史紀錄、已結束／已發行批次在移除套件後如何保存，等真正實作刪除功能時再設計，不提前定死。

---

## 21. 下一階段順序

Build 3 尚未經使用者實測通過前，**不開始 ScratchPack V1 功能實作**。

Build 3 通過後建議依序：

1. 將本計畫整理為新的正式 `SCRATCHPACK_SPEC.md`。
2. 調整主程式 ScratchPack importer / schema。
3. 完成 GameType 1 正式 ScratchPack 化，並以「三星連線 $500」作第一個參考包。
4. 依序完成 GameType 2～6 引擎與 schema 驗證。
5. 完成 ScratchPack Maker GameType-aware 第一版。
6. 每個 GameType 至少以一張實際 ScratchPack 做建立、匯入、遊玩、有限票池、兌獎與統計測試。
7. 完成匯入／刪除／重新發行等資料生命週期設計。
8. 完成六種基礎玩法交叉測試後，才進入 V1.0.0 發行準備。

在此之前只保存規劃，不修改 Build 3 功能。