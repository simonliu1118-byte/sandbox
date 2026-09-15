# ScratchPack Format Specification

Specification version: **1.0**  
Implementation status: **規格已定稿；ScratchGame V0.4.0 正在完成 V1 loader / importer / runtime 實機驗收；Maker 規劃於 V0.5.0。**

本文件是 ScratchPack V1 **封裝、資料格式、共通 ResourceRef 與 built-in asset registry 的唯一正式規格**。GameType 的勝負判定、公開參數、盤面生成、Prize Tier 對應、Renderer 與 GameType-specific 驗證只由 `GAMETYPE_SPEC.md` 定義。

`PROJECT_RULES.md` 只保存永久專案原則；`TODO.md` 只保存未完成工作與未決事項。不得在其他檔案平行維護本文件 schema 或 built-in asset registry。

## 1. 定位與資料責任

ScratchPack 只提供資料、美術與 GameType 允許的公開參數，不包含可執行玩法程式碼。

主程式負責驗證、載入與運行 Pack，並提供 GameType engine、Renderer、盤面生成、刮膜互動、票號、程式面額、批次、有限票池、Pending Ticket、兌獎、中獎效果、音效、硬幣、使用者與損益資料。

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

- `formatVersion`：V1 必須為字串 `"1.0"`。
- `packageId`：合法 GUID，套件唯一識別。
- `author`：作者顯示名稱，可空字串但欄位必須存在。
- `minimumAppVersion`：最低相容 ScratchGame 版本；若 Pack 引用較新版本才提供的 Built-in 資源，必須至少指向首次支援該資源的版本。
- `ticketFile`：包內 `ticket.json` 相對路徑。
- `manifest.json` 只承擔封裝層資訊，不重複保存彩券名稱、面額、Prize Tier、Canvas、GameType 或美術資源引用。
- V1 不另拆 `layout.json` / `prizes.json`。
- 不保存本機 `styleNumber`；款式編號由 runtime database 管理。

## 4. `ticket.json`

基礎結構：

```json
{
  "name": "三星連線",
  "price": 500,
  "canvas": 1,
  "priceDisplay": 1,
  "priceDisplayArea": { "x": 852, "y": 43, "width": 201, "height": 82 },
  "gameType": "1",
  "issueSize": 10000,
  "ticketsPerBook": 100,
  "art": {
    "ticket": {
      "source": "builtin",
      "ref": "01-blue"
    }
  },
  "serialDisplayArea": { "x": 364, "y": 774, "width": 350, "height": 59 },
  "scratch": {
    "foil": {
      "source": "builtin",
      "ref": "brushed-silver-three-star"
    },
    "zones": []
  },
  "game": {},
  "prizes": []
}
```

單一 owner：

- `manifest.json`：封裝層。
- `ticket.json`：彩券定義唯一權威資料。
- `art.ticket`：票面美術資源的唯一引用入口。
- `scratch.zones`：刮區 geometry / clipping / hit-test 的唯一來源；任何票面或銀膜資產都不得另帶第二套刮區座標或互動範圍。
- `scratch.foil`：銀膜材質資源的唯一引用入口；不得另建 `art.mask`、`foilFile`、逐 zone foil 或其他平行欄位。
- `prizes`：最終 Prize Pool / Prize Tier 唯一權威來源；不得另建 `prizes.json`、圖片 metadata、`artworkFile` 或其他平行欄位。
- runtime database：本機款式編號、Pack 安裝來源、批次、日期、Remaining、Pending、歷史等 runtime state。

ScratchPack 不保存中獎率、未中獎率、最高獎金、EV、RTP、總本數、未中獎張數等 Derived Data。

挑券卡資料來源：名稱=`name`；面額=`price`；縮圖由正式 Pack / runtime definition 產生的 cache；中獎率由 `prizes` + `issueSize` 算；最高獎由 `max(prizes.amount where count > 0)` 算；批次與發行日期來自 runtime database。

## 5. 共通 ResourceRef、Built-in Asset Registry 與 Canvas

### 5.1 共通 `ResourceRef`

V1 的共用 PNG 資源引用統一使用同一個結構：

```json
{
  "source": "builtin",
  "ref": "01-blue"
}
```

或：

```json
{
  "source": "package",
  "ref": "assets/my-ticket.png"
}
```

規則：

- `source` 在 V1 只允許 `builtin`、`package`。
- `source="package"`：`ref` 必須是目前 ScratchPack 內安全的相對 PNG 路徑。
- `source="builtin"`：`ref` 是對應資源 namespace 內的穩定短代碼；namespace 由**使用欄位與必要上下文**決定，不把已知上下文重複塞進 `ref`。
- 同一套 `ResourceRef` 同時供 `art.ticket`、`scratch.foil` 與未來其他正式規格化的共用 PNG 資源引用；不得為每一種資源再發明 `ticketCode`、`foilCode`、`ticketFile`、`foilFile` 等平行入口。
- `art.ticket` 的 Built-in namespace 由 `ticket.json.gameType` 決定；完整識別鍵是 **(`gameType`, `ref`)**。
- `scratch.foil` 的 Built-in namespace 是全域 foil namespace，因銀膜為跨 GameType 共用材質。
- 因此票面 `ref` 不寫 `ticket/`、`gameType1/` 等前綴；銀膜 `ref` 也不寫 `foil/` 前綴。欄位本身已經提供資源類型上下文。
- `ResourceRef` 只負責「資源從哪裡來」，不承擔 geometry、GameType、Prize Tier、面額區、票號區或玩法 mapping。
- Built-in ref 找不到、版本不支援、Canvas 不相容或 namespace 不符時直接驗證失敗；不可靜默 fallback 成其他資源。

### 5.2 Built-in Asset Registry

**本節是 ScratchPack 可公開引用的 Built-in 資源唯一權威清單。** ChatGPT Library、README、Maker UI 或 runtime 檔名都不得建立另一份具有 schema 權威性的 registry。

#### Built-in ticket assets

票面資源**依 GameType 分 namespace**。不同 GameType 可以各自擁有相同的短 `ref`，互不衝突；不得跨 GameType 解析或借用票面。

GameType 1：

```text
ref      Canvas   說明
01-red   1        Style 01 紅色票面
01-blue  1        Style 01 藍色票面
02       1        Style 02 紅金票面
```

解析例：

```text
gameType="1" + art.ticket.source="builtin" + art.ticket.ref="01-blue"
→ GameType 1 Built-in ticket 01-blue
```

若未來 GameType 2 也有 `ref="01-blue"`，它是 GameType 2 自己 namespace 內的另一個資源，與 GameType 1 無關。

#### Built-in foil assets

銀膜為跨 GameType 共用，全域 namespace：

```text
brushed-silver-plain       一般拉絲銀膜
brushed-silver-three-star  三星圖樣拉絲銀膜
```

共同規則：

- Built-in asset 由 ScratchGame 發行內容提供，不需複製進每個 `.scratchpack`；Built-in Pack 與 Imported Pack 都可直接引用。
- 已發布的 Built-in ref 不得重新指向語意上完全不同的素材；新素材新增新 ref。
- App 內部可使用不同實體檔名、壓縮格式或最佳化版本，但公開 resolver key 與視覺身份必須保持相容。
- Built-in ticket asset 只是一張固定票面 PNG，不自帶 Scratch Zone、GameType 規則、`priceDisplayArea`、`serialDisplayArea`、Prize Tier 或其他 ticket 定義；這些仍由 `ticket.json` 各自唯一負責。
- Built-in foil asset 只提供銀膜材質，不自帶 Scratch Zone 或刮除判定。

### 5.3 Canvas 與票面

V1 只允許官方固定 Canvas code，不允許 Pack 任意輸入寬高。

```text
canvas = 1 → 1080 × 882
```

既有 Canvas code 的尺寸語意永遠不得改變；其他尺寸只能新增新 code。

`art.ticket.source="package"` 時，其 PNG 必須與 Canvas 尺寸完全一致；Maker / Importer 不得偷偷縮放、裁切或重新取樣後放行。

`art.ticket.source="builtin"` 時，必須先以 `gameType + ref` 在 ticket registry 解析；該資源還必須支援目前 `canvas`。任一條件不符直接驗證失敗。

票面 PNG 可負責：背景、名稱、固定說明、裝飾、刮區固定美術、`priceDisplay=0` 時的完整面額美術與宣傳性固定文字。

主程式負責：每張票動態符號／數字／金額／結果、銀膜、票號、`priceDisplay=1` 的面額徽章、命中標記、批次／票池狀態、中獎效果、音效與硬幣。

疊圖順序：

```text
票面 PNG
→ 動態遊戲內容
→ 刮膜
→ 票號 / 程式面額
→ 命中標記 / 硬幣
→ 中獎特效 / 結果 UI
```

## 6. 面額與票號

`priceDisplay`：

- `0`：面額完整畫在票面 PNG，主程式不再疊面額；實際面額仍以 `ticket.json.price` 為權威。
- `1`：票面留空並提供 `priceDisplayArea`，主程式在該區域畫完整標準面額徽章；票面不得殘留舊面額文字或底塊。

`serialDisplayArea` 只提供票號區域位置與大小；票號格式、字型、圓角框與底色由主程式統一處理，票面不得燒入舊票號或舊票號底塊。

票號格式：`款式編號-本號-本內序號`。各段至少三位補零，超過三位自然增加，不截斷、不循環、不換行。

## 7. Scratch Zone 與銀膜

V1 shape：

```text
rectangle
roundedRectangle
circle
ellipse
```

- `roundedRectangle` 可有 `cornerRadius`。
- `circle` 必須 `width == height`。
- zone 必須完整落在 Canvas 內，ID 不得重複。
- GameType 需要多少刮區，就必須剛好有多少；不允許額外裝飾刮區。
- `scratch.zones` 在共通層只定義 geometry；其陣列順序預設不具有全域玩法語意。
- 特定 GameType 可以在 `GAMETYPE_SPEC.md` 明確收緊 zone 的數量、尺寸、排列、陣列順序或 mapping；這些限制只對該 GameType 生效。

基本 zone：

```json
{
  "id": "cell01",
  "x": 219,
  "y": 256,
  "width": 191,
  "height": 138,
  "shape": "roundedRectangle",
  "cornerRadius": 12
}
```

V1 不提供通用 `contentBox`。Scratch zone 只負責位置、尺寸與形狀；同類元件內部排版由各 GameType Renderer 固定。

### 7.1 `scratch.foil`

V1 每張票必須明確指定一個 `scratch.foil` ResourceRef。Maker 介面可以預選預設樣式，但輸出的 ScratchPack **不得靠缺省值或 `null` 暗示預設銀膜**。

使用 ScratchGame 公用內建銀膜：

```json
"foil": {
  "source": "builtin",
  "ref": "brushed-silver-three-star"
}
```

使用 Pack 自帶銀膜：

```json
"foil": {
  "source": "package",
  "ref": "assets/my-foil.png"
}
```

銀膜規則：

- `source="package"` 的 foil PNG 不要求等於 Canvas 尺寸；V1 將其視為可重用的 **single-zone foil template**。
- 不論 `builtin` 或 `package`，都進入同一套 foil renderer / ScratchSurface / 刮除 pipeline；不得因來源不同建立第二套互動或判定邏輯。
- 同一張票 V1 只指定一種 foil；所有 Scratch Zone 共用同一 `scratch.foil`。不提供逐 zone foil override。
- `scratch.zones` 是唯一 geometry / clipping / hit-test 來源。foil ref 或 foil PNG 只提供材質，不得定義 zone 的 x/y/width/height、shape、順序或玩法 mapping。
- runtime 依每個 `scratch.zones` geometry 將 foil template 套入並以 zone shape clipping；自訂 PNG 的 alpha 可參與視覺，但不得被視為新的互動 geometry。
- V1 不接受舊草案的 `art.mask`，也不以固定檔名 `mask.png` 建立第二套語意。
- 未來若實作「全 Canvas 銀膜再由 Scratch Zone 遮罩切出」模式，必須擴充同一個 `scratch.foil` ResourceRef / renderer pipeline，不得另建平行的 mask / overlay schema。該模式目前只列 TODO，不屬 V1。

## 8. 發行量、本數與 Prize Pool

必須：

```text
issueSize > 0
ticketsPerBook > 0
issueSize % ticketsPerBook == 0
```

`bookCount = issueSize / ticketsPerBook`，但不寫入 ScratchPack。

`ticket.json.prizes` 是最終 Prize Pool / Prize Tier 唯一權威欄位：

```text
amount > 0
count >= 0
```

同一 Pack 每個 `amount` 必須唯一；不保存 `amount=0` 的未中獎 tier。

```text
winningCount = Σ prizes.count
loseCount = issueSize - winningCount
winRate = winningCount / issueSize
maxPrize = max(prizes.amount where count > 0)
```

- `winningCount < issueSize`：差額視為未中獎。
- `winningCount == issueSize`：100% 中獎，合法。
- `winningCount > issueSize`：非法。
- `winningCount=0` 時 `maxPrize=0`。
- `loseCount`、`winRate`、`maxPrize` 與 EV / RTP 等都只做 Derived Data，不另存權威欄位。

具有固定必備 Tier 集合的 GameType 可以要求即使 `count=0` 也保留該 Tier；其 outcome mapping 與排序規則由 `GAMETYPE_SPEC.md` 定義。

## 9. GameType 契約

`ticket.json.gameType` 必須是主程式已支援的 ID，`ticket.json.game` 只能使用該 GameType 公開合法欄位。

GameType 1～6 的核心勝負判定、公開參數、盤面生成、Prize Tier 對應、Renderer 與 GameType-specific validation 唯一權威來源：

```text
GAMETYPE_SPEC.md
```

## 10. Maker / Importer 共通驗證

至少驗證：

- ZIP / 路徑安全與不可執行內容。
- `manifest.json` / `ticket.json` 可解析且版本相容。
- `packageId` 唯一合法。
- 不存在未定義資源，例如 `thumbnail.png`。
- `manifest.json` 不重複保存 ticket 業務資料。
- Prize Tier 只由 `ticket.json.prizes` 定義。
- 所有 `ResourceRef` 必須存在、格式合法、`source` 合法，且 ref namespace 與使用欄位相符。
- `art.ticket.source="builtin"` 時，以 `gameType + ref` 解析；該 ref 必須存在於該 GameType ticket registry，且目前 App 版本與 Canvas 相容。不得跨 GameType fallback。
- `scratch.foil.source="builtin"` 時，`ref` 必須存在於全域 foil registry，且目前 App 版本支援。
- Built-in ref 不支援時直接驗證失敗，不可靜默替代。
- `source="package"` 時，`ref` 必須是合法包內相對 PNG 路徑，檔案存在且可解碼。
- `art.ticket.source="package"` 時票面尺寸必須完全符合 Canvas。
- `scratch.foil.source="package"` 的 PNG 不要求與 Canvas 同尺寸。
- 不得接受 `art.mask`、`ticketCode`、`foilCode`、`ticketFile`、`foilFile` 或其他平行資源選擇欄位。
- 所有座標合法；`priceDisplay=1` 時 `priceDisplayArea` 合法；`serialDisplayArea` 合法。
- zone ID、shape，以及 GameType-specific 的 zone 數量、geometry、順序／mapping 合法。
- `issueSize % ticketsPerBook == 0`、`Σ prizes.count <= issueSize`。
- GameType、玩法參數與 Renderer 限制符合 `GAMETYPE_SPEC.md`。
- 所有被引用的 package PNG 可正常解碼。

Importer 對 PNG 的驗證到「路徑、檔案、格式、必要尺寸、可解碼」為止；不 OCR、不解析圖片文字、不從圖片推導任何遊戲資料。票面或 foil PNG 都不得反向成為 Scratch Zone geometry、Prize Tier 或玩法資料來源。

任何驗證失敗都不得留下半套已安裝資料；外部 Pack 匯入必須為原子操作。

## 11. Pack 安裝來源、初始批次與 lifecycle

正式彩券一律以 ScratchPack 定義；主程式不得為某張正式彩券另外硬編碼專屬 Prize Tier、layout、玩法結果或第二套 ticket definition。

Built-in Pack 與 Imported Pack：

- 使用**完全相同**的 `.scratchpack` V1 schema、ResourceRef、GameType 契約與 runtime pipeline。
- 共用 loader / validator / engine / renderer / finite-pool / redemption pipeline；不得因安裝來源建立第二套玩法或資料格式。
- 安裝成功後皆由同一個 runtime lifecycle 建立**第 1 批**；使用者不需要再手動做第一次發行。後續批次仍使用一般「發行下一批」流程。
- `BuiltIn` / `Imported` 是本機 runtime 安裝來源狀態，不是 Pack 可自行宣稱的 manifest / ticket 欄位。
- `Hidden / Visible` 也是本機 runtime UI 狀態，不寫入 `.scratchpack`。

Built-in Pack：

- 隨 ScratchGame 發行內容提供，不要求使用者手動匯入；主程式初始化時自動確認並註冊。
- 不提供解除安裝；缺少或損壞視為程式發行內容不完整。
- 可**隱藏 / 解除隱藏**。隱藏後不出現在正常設定主列表或挑券列表，但不刪除 Pack、批次、票池或歷史資料。

Imported Pack：

- 由使用者手動匯入；匯入完成後自動建立第 1 批並可直接遊玩。
- 可**隱藏 / 解除隱藏**；語意與 Built-in 相同，只影響正常 UI 可見性與新票選擇，不刪除資料。
- 可要求**解除安裝**；解除安裝的語意是移除該 Imported Pack 的安裝資料、Pack 檔案副本與可重建 cache。
- 為避免破壞既有 Pending / 歷史資料引用，runtime 可以在仍有未完成票或既有遊玩紀錄時拒絕解除安裝並要求改用「隱藏」。這是資料安全限制，不是新的 Pack schema。

外部 Pack 匯入後與 Built-in Pack 使用同一套 runtime model。外部 Pack 可以直接引用目前 GameType 可用的 Built-in ticket ref 與全域 Built-in foil ref，也可以透過 `source="package"` 攜帶自己的 PNG；兩者不建立第二套 loader 或 renderer。

第一款正式 Built-in Base Pack「三星連線」的內容將由 V0.5.0 ScratchPack Maker 產生後提供給主程式發行流程，不由主程式另外手寫一套 Pack。其既定方向：

- `gameType="1"`。
- `canvas=1`。
- 正式版面額為 500、`issueSize=10000`。
- 可直接引用 GameType 1 的 `01-blue` 等 Built-in ticket asset，不需把共用票面 PNG 複製進 Pack。
- 可直接引用 `brushed-silver-three-star` 等 Built-in foil asset，不需把共用銀膜 PNG 複製進 Pack。
- 基本款票面不放中獎率與「最高可中 N 元」宣傳字樣。
- 正式 Built-in Base Pack 不另維護主程式硬編碼的平行 ticket definition。

開發階段可以另外維護一個獨立的「三星連線測試 Pack」作為 V1 conformance / loader / renderer / Prize Tier 測試 fixture：

- 測試 Pack 也必須完整符合相同的 `SCRATCHPACK_SPEC.md` + `GAMETYPE_SPEC.md`。
- 測試 Pack 使用獨立 `packageId`、獨立票池與測試用 `issueSize` / Prize Tier count，不得冒充正式 Built-in Base Pack。
- 測試 Pack 是獨立 ScratchPack，不是主程式內另一套硬編碼 ticket definition。
- 測試 Pack 是否隨正式 portable 發行屬發行／測試流程決策，不寫入 ScratchPack schema。

## 12. 相容性與文件責任

- `formatVersion` 只代表 ScratchPack schema / 封裝版本，不代表 GameType 版本。
- 已發布欄位的語意不得在相同 formatVersion 下偷偷改變；不相容 schema 變更必須升級 `formatVersion`。
- 已發布的 Built-in ticket resolver key **(`gameType`, `ref`)** 與全域 foil `ref` 都視為公開相容性契約；新增視覺使用新 ref，不把既有 key 重新指向語意上不同的素材。
- Built-in Asset Registry 只在本文件維護；素材庫 README、handoff、Maker UI 可引用 ref，但不得形成第二份權威清單。
- GameType 相容性只由 `GAMETYPE_SPEC.md` 管理。
- `PROJECT_RULES.md` 不複製 schema；`TODO.md` 不複製 schema、Built-in registry 或 GameType 核心規則。
