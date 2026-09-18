# GameType Specification

Specification version: **1.0**  
Implementation status: **GameType 1～6 契約已定稿；主程式實作進度依 `TODO.md`。**

本文件是 ScratchGame **GameType 規則的唯一正式規格**。`SCRATCHPACK_SPEC.md` 只定義 ScratchPack 封裝與共通資料格式；所有 GameType 的核心勝負判定、公開參數、盤面生成、Prize Tier 對應、Renderer 限制與 GameType-specific 驗證只在本文件定義。

不得在 `PROJECT_RULES.md`、`TODO.md`、HANDOFF 或其他文件平行複製完整 GameType schema。其他文件只可引用本文件。

---

## 1. 共通原則

ScratchPack 只選玩法與填公開參數，不得自行寫規則。

舊 GameType 的底層行為一旦正式發布就不得改變；需要不同規則時新增變體 ID，例如 `1-2`，而不是偷偷改 `1`。

新增可選參數必須向下相容；舊包缺少新欄位時，使用不改變舊行為的預設值。

同類動態元件的尺寸與內部 Renderer 必須一致；PackEditor 以模板方式控制整組元件，不允許逐格做出不同字級、比例或內部排列。

---

## 2. GameType 1：星星連線

```text
gameType = "1"
```

### 基本規則

- 目前正式支援的 `gridSize`：3、4、5。
- `gridSize = N` 時，玩法盤面固定為標準 `N × N` 網格。
- 只計所有完整橫線、完整直線與兩條完整大斜線。
- 不計短斜線，不允許 ScratchPack 自訂 `winningLines`。
- 所有 Scratch Zone 都是盤面格，不允許額外裝飾刮區。
- Scratch Zone 數量必須精確等於 `N²`。
- 未來若增加 6×6 或更大尺寸，只擴充主程式支援的 `gridSize` 合法值；本 GameType 的 schema、網格語意與 Prize Tier 推導方式不因此改變。

目前刮區數量：

```text
3×3 → 9
4×4 → 16
5×5 → 25
```

### 標準網格與 Scratch Zone 順序

GameType 1 直接使用 `ticket.json.scratch.zones` 作為棋盤格，不另設 `cellZones` 或其他平行 mapping。

`scratch.zones` 對 GameType 1 的陣列順序具有玩法語意，固定為：

```text
由左到右，再由上到下（row-major）
```

因此 `gridSize = N` 時：

```text
row = index / N
column = index % N
```

PackEditor / Importer 必須同時驗證視覺 geometry 確實形成標準網格：

- 所有盤面格的 `width`、`height`、`shape` 必須一致。
- 若 shape 為 `roundedRectangle`，所有格的 `cornerRadius` 必須一致。
- 同一欄所有格的 `x` 必須相同；同一列所有格的 `y` 必須相同。
- 欄座標必須由左至右嚴格遞增，列座標必須由上至下嚴格遞增。
- 相鄰欄之間的水平間距必須一致；相鄰列之間的垂直間距必須一致。
- 盤面格不得互相重疊；間距可為 0，但不得為負值。
- `scratch.zones` 的實際陣列順序必須與上述左→右、上→下的 geometry 一致。

這些限制只屬於 GameType 1。其他 GameType 仍依各自規格決定 Scratch Zone 是否需要固定排列、分組或 mapping。

### `ticket.json.game`

GameType 1 的 `game` 物件只需要玩法參數：

```json
{
  "gridSize": 3,
  "allowNearMiss": false
}
```

GameType 1 **不使用也不接受 `cellZones`**。棋盤拓撲由 `gridSize` + 已驗證為標準網格的 `scratch.zones` 順序唯一決定。

### `allowNearMiss`

```text
allowNearMiss = false   預設
```

啟用時，生成器可在**不增加實際中獎線數**的前提下加入零散星星或「差一顆成線」的近似中獎盤面。它只影響盤面生成風格，不改變 Prize Tier 或勝負判定。

### Prize Tier 與線數推導

GameType 1 的 Prize Pool 仍使用共通 `ticket.json.prizes`，不得加入 `lineCount`、`lines-N` 或其他 GameType 1 專屬 outcome 欄位。

`gridSize = N` 時：

```text
有效線總數 = 2N + 2
合法正獎線數 = 1..2N，以及 2N+2
2N+1 不可達
合法正獎 Tier 數 = 2N + 1
```

因此目前：

```text
3×3：1、2、3、4、5、6、8
4×4：1、2、3、4、5、6、7、8、10
5×5：1、2、3、4、5、6、7、8、9、10、12
```

未來若支援 6×6，依相同公式自然得到：

```text
6×6：1..12、14
```

映射規則：

- `prizes` 必須精確包含 `2N + 1` 個正獎 Tier。
- 將 `prizes` 依 `amount` 由小到大排序後，依序對應合法線數由少到多。
- 獎金必須隨線數嚴格增加，因此各 Tier 的 `amount` 必須唯一且嚴格遞增。
- 某個合法線數即使該 Pack 不發行，也必須保留對應 Prize Tier 並設定 `count = 0`；不得省略，否則線數與獎金的映射會改變。
- `prizes` 在 JSON 中的實體排列順序不承擔映射語意；Engine / PackEditor / Importer 一律依 `amount` 排序後建立線數映射。
- 0 線代表未中獎，不建立 `amount = 0` Prize Tier；未中獎張數仍由 `issueSize - Σ prizes.count` 推導。

3×3 範例：

```json
"prizes": [
  { "amount": 100,    "count": 1 },
  { "amount": 500,    "count": 1 },
  { "amount": 1000,   "count": 1 },
  { "amount": 2500,   "count": 1 },
  { "amount": 5000,   "count": 1 },
  { "amount": 10000,  "count": 1 },
  { "amount": 100000, "count": 1 }
]
```

依金額排序後，分別對應 **1、2、3、4、5、6、8 條線**。

---

## 3. GameType 2：中獎號碼

```text
gameType = "2"
```

### 核心規則

一組「中獎號碼」與一組「你的號碼」進行比對。每個你的號碼若命中任一中獎號碼，即形成一次命中；所有命中項目的獎金累加為本張最終獎金。

兩組號碼各自不得重複；兩組之間可以相同，這就是命中。未中獎票必須是 **0 個命中**，不得以「命中但金額為 0」表示未中獎。

中獎號碼與你的號碼全部都是刮區，玩家必須刮開全部玩法刮區才算手動完成。

### Scratch Zone mapping

GameType 2 直接使用 `ticket.json.scratch.zones`，不另設 `winningZones`、`playZones` 或其他平行 geometry / mapping 欄位。

`scratch.zones` 的陣列順序對 GameType 2 具有玩法語意：

```text
前 winningNumberCount 個 zone = 中獎號碼
後 playNumberCount 個 zone    = 你的號碼
```

因此 zone 總數必須精確等於：

```text
winningNumberCount + playNumberCount
```

所有 Type 2 zone 必須使用相同 `width`、`height`、`shape`；若為 `roundedRectangle`，`cornerRadius` 也必須一致，以符合固定 Renderer 模板。zone 的 x / y 由 PackEditor 依票面配置，但不得加入額外裝飾刮區。

### payoutSource

同一個 GameType 2 支援：

```text
play      獎金跟著「你的號碼」
winning   獎金跟著「中獎號碼」
```

不拆成額外 GameType 變體。因兩組內部各自唯一，每次命中都是一個中獎號碼與一個你的號碼的一對一交集；依 `payoutSource` 決定該次命中讀取哪一側格內獎金。

### 數量與數字範圍

開發者設定：

```text
winningNumberCount
playNumberCount
numberMin
numberMax
```

`winningNumberCount`、`playNumberCount` 必須大於 0，且 `numberMin < numberMax`。

PackEditor / Importer 必須依實際 Prize Tier、可用命中數、是否存在未中獎票等條件直接驗證數字範圍是否足以產生合法盤面，不以過度簡化的固定公式代替實際驗證。若存在未中獎票，範圍必須足以生成兩組完全不重疊且各自唯一的號碼。

### 格內金額

```text
displayPrizeAmounts
allowPrizeAmountRepeat = true | false
```

- `displayPrizeAmounts`：盤面可使用的正整數顯示金額；清單本身不得包含重複值。它不是最終 Prize Pool。
- `allowPrizeAmountRepeat = true`：同一張票的不同獎金格可以重複使用相同金額。
- `allowPrizeAmountRepeat = false`：同一張票所有有獎金的一側，每個顯示金額最多使用一次；因此可用金額種類數必須足以填滿該側全部獎金格。

不論該格是否真的命中，有獎金的一側每個格都會顯示一個 `displayPrizeAmounts` 中的金額；只有真正命中的格才計入本張獎金。

主程式先取得本張最終 Prize Tier，再尋找符合 `payoutSource`、可用命中數、數字範圍與 `allowPrizeAmountRepeat` 的合法命中金額組合。所有命中格金額總和必須**精確等於**本張最終 Prize Tier；不得四捨五入、截斷、補差額或建立隱藏獎金。

PackEditor / Importer 必須事前確認每個正式正獎 Prize Tier 都能精確生成；若 `issueSize - Σ prizes.count > 0`，也必須確認能生成 0 命中的未中獎盤面。

### `ticket.json.game` 範例

```json
{
  "winningNumberCount": 3,
  "playNumberCount": 12,
  "numberMin": 1,
  "numberMax": 30,
  "payoutSource": "play",
  "displayPrizeAmounts": [100, 200, 500, 1000],
  "allowPrizeAmountRepeat": true
}
```

### 固定 Renderer

有獎金的一側固定：

```text
大號數字在上
小號金額在下
```

無獎金的一側只顯示固定大小的大號數字。

同一張票所有 Type 2 格子的尺寸、數字字級、金額字級、比例、間距與位置全部一致，不允許個別格縮放。最長金額若無法放入固定模板，PackEditor 報錯。

---

## 4. GameType 3：三個相同

```text
gameType = "3"
```

### 核心規則

同一個金額恰好出現三次，即得該金額；不是三倍金額。

- `matchCount` 固定為 3，不作 ScratchPack 設定。
- `zoneCount` 合法範圍固定為 **3～25**。
- `scratch.zones` 數量必須精確等於 `zoneCount`；每個 zone 與產生的 `amounts[]` 依陣列索引一對一對應。
- Type 3 不要求標準網格；zone 的 x / y 可由 PackEditor 依票面配置。
- 所有 Type 3 zone 的 `width`、`height`、`shape` 必須一致；若為 `roundedRectangle`，`cornerRadius` 也必須一致。
- 本張若中 `$500`，引擎必須恰好生成三個 `$500`，最終獎金仍為 `$500`。
- 其他任何金額最多出現兩次。
- 未中獎票所有金額都最多出現兩次。
- 一張票只允許一組真正成立的「三個相同」，不做多組累加。

### `ticket.json.game`

```json
{
  "zoneCount": 9,
  "useCustomDecoyAmounts": true,
  "decoyAmounts": [50, 200, 750, 2000],
  "nearMissPairProbability": 75,
  "nearMissPairCount": 1
}
```

`zoneCount` 必填；其他 Type 3 欄位依下列規則處理。

### 非中獎金額

```text
useCustomDecoyAmounts = false   預設
```

預設 false 時，其他格只能由目前正式 `prizes` 中 **`count > 0`** 的其他正獎金額填充；`count = 0` 的 Prize Tier 不會被偷偷拿來當盤面干擾金額。

開啟 true 時，開發者可額外提供 `decoyAmounts`，作為非中獎位置的**補充**顯示金額，不會取代正式 Prize Tier 可用金額。

`decoyAmounts` 固定規則：

- 每個值必須為正整數。
- 清單內不得重複。
- 不得與任何 `prizes.amount` 重複，包括 `count = 0` 的 Prize Tier。
- 不建立新的 Prize Tier，不影響中獎率、Prize Pool、EV 或 RTP。
- `useCustomDecoyAmounts=false` 時不得提供 `decoyAmounts`。
- `useCustomDecoyAmounts=true` 時至少必須提供一個 `decoyAmounts`。

PackEditor / Importer 必須對未中獎票及每個 `count > 0` 的正式 Prize Tier 分別驗證可生成性。扣掉真正中獎金額的三格後，所有剩餘金額每種最多只能使用兩次；若目前可用金額種類不足以填滿 `zoneCount`，直接驗證失敗，不得等到玩家購票時才降級或報錯。

### Near Miss／差一個成獎

Type 3 支援進階盤面刺激選項：

```text
nearMissPairProbability = 75   預設，合法範圍 0～100
nearMissPairCount = 1          預設，必須 > 0
```

Near Miss Pair 指**非本張真正中獎金額**的某個金額刻意出現兩次，例如 `$100、$100`，形成「差一個就三個相同」的視覺效果。

規則：

- `nearMissPairProbability` 是產生器**刻意安排** Near Miss 的機率百分比。
- 觸發時，引擎必須至少安排 `nearMissPairCount` 組不同的非中獎金額各出現兩次。
- 真正中獎票可同時存在 Near Miss Pair；真正中獎的三個相同金額不計入 Near Miss Pair。
- 未中獎票也可存在 Near Miss Pair。
- Near Miss 金額永遠最多兩個，絕對不能因 Near Miss 形成第二組三個相同。
- Near Miss 只改變盤面呈現，不改變本張已抽中的 Prize Tier、中獎率、Prize Pool 或兌獎結果。
- 未觸發 Near Miss 時，其他合法填充金額仍受「每種最多兩個」限制；若可用金額種類本身不足，盤面可能自然出現成對金額，因此 `nearMissPairProbability` 定義的是**刻意保證 Pair 的機率**，不是「盤面出現任何 Pair 的絕對機率」。
- `nearMissPairProbability > 0` 時，PackEditor / Importer 必須確認未中獎票以及每個 `count > 0` Prize Tier 都實際容得下指定 `nearMissPairCount`，且有足夠不同金額建立這些 Pair；不允許 runtime 靜默降低 Pair 數量。
- 若 `zoneCount` 太小而無法在某個中獎盤面放入設定的 Near Miss Pair，該 ScratchPack 驗證失敗。開發者可降低 Pair 數、將機率設為 0，或增加 `zoneCount`。

### Renderer

每格只顯示金額，標準格式為 `$1,000`。同一張票所有格尺寸與金額字級固定，不因金額長短個別縮放。PackEditor 必須事前以所有可能顯示的正式獎金與 `decoyAmounts` 檢查最長金額是否可完整顯示。

第一版 Type 3 不另外加入三個中獎格的專屬高亮、閃爍或框線；中獎結果沿用 ScratchGame 共通結果流程。專屬視覺效果留待後續 Decoration / 整體遊戲體驗階段處理。

---

## 5. GameType 4：符號計數

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

開發者設定最低中獎數；PackEditor 建立連續的數量 tier。數量越多，獎金必須嚴格增加。

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

Mode B 的最終 Prize Pool **由 PackEditor 自動推導**：

- false：依可成立的單一符號獎金推導。
- true：依所有合法符號組合、zoneCount 與 `matchCount` 推導可生成總獎金。

PackEditor 顯示可生成總獎金，開發者只填各金額發行張數；不自行新增任意最終獎金。

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

## 6. GameType 5：賓果

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

開發者設定「你的符號／號碼」刮區數量 `drawCount`。PackEditor 依可用唯一內容與 decoy 設定驗證是否足以生成合法盤面。

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

PackEditor 依：

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

## 7. GameType 6：比大小

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

開發者可以在 PackEditor 調整一次比較區模板尺寸；所有區塊必須共用相同尺寸、相同內部比例與相同 Renderer。

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

PackEditor 必須事前驗證所有正式 Prize Tier 都能在 `blockCount`、可用金額及重複規則下精確生成。

### 自訂每區固定獎金

```text
useCustomBlockPrizes = true
```

開發者直接設定每一區固定獎金。該區的金額不會因不同票而改變。

此模式不再使用 `displayPrizeAmounts` / `prizeAmountUsage`；PackEditor 以 subset-sum / 動態規劃快速計算所有可生成總獎金，開發者只填各金額發行張數，不自行新增任意最終獎金。

---