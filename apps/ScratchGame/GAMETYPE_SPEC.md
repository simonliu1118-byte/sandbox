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

同類動態元件的尺寸與內部 Renderer 必須一致；Maker 以模板方式控制整組元件，不允許逐格做出不同字級、比例或內部排列。

---

## 2. GameType 1：星星連線

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

## 3. GameType 2：中獎號碼

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

## 4. GameType 3：三個相同

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
