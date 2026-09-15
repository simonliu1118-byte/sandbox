# ScratchPack Format Specification

Specification version: **1.0**  
Implementation status: **規格已定稿；目前 ScratchGame 尚未完整實作 V1 loader / importer / Maker。**

本文件是 ScratchPack V1 **封裝與資料格式的唯一正式規格**。GameType 的勝負判定、公開參數、盤面生成、Prize Tier 對應、Renderer 與 GameType-specific 驗證只由 `GAMETYPE_SPEC.md` 定義。

`PROJECT_RULES.md` 只保存永久專案原則；`TODO.md` 只保存未完成工作與未決事項。不得在其他檔案平行維護本文件 schema。

## 1. 定位與資料責任

ScratchPack 只提供資料、美術與 GameType 允許的公開參數，不包含可執行玩法程式碼。

主程式負責驗證、載入與運行 Pack，並提供 GameType engine、Renderer、盤面生成、刮膜互動、票號、程式面額、批次、有限票池、Pending Ticket、兌獎、中獎效果、音效、硬幣、使用者與損益資料。

ScratchPack 負責彩券名稱、作者、面額、Canvas code、GameType 與公開參數、必要票面 PNG、GameType 額外資源、刮區 geometry、銀膜來源選擇、`issueSize`、`ticketsPerBook` 與最終 Prize Pool / Prize Tier。

PNG / WAV 對主程式是 opaque render asset。Importer 可檢查路徑、安全性、格式、尺寸與能否解碼，但不得靠 OCR、像素分析或檔名推導彩券名稱、面額、中獎率、最高獎金、Prize Tier、GameType、刮區、批次、日期或票號。

圖片可以印「中獎率 100%」「最高可中一百萬！」等宣傳字樣；這些只屬美術內容，不成為程式資料來源。第一款「三星連線」只是選擇不放這兩類宣傳字樣，並非 V1 全域限制。

## 2. 封裝與安全

- 副檔名：`.scratchpack`。
- 實體格式：ZIP；ZIP 根目錄不得再多包一層資料夾。
- JSON：UTF-8。
- 路徑：一律 `/`、相對路徑；禁止 `..`、絕對路徑、磁碟代號、UNC、symbolic link、hard link 或任何可跳出解壓目錄的項目。
- V1 只允許資料與 PNG 資源；不得包含 DLL、EXE、script、macro 或任何可執行 payload。
- V1 暫不開放 ScratchPack 自訂中獎音效或中獎動畫。

最小合法套件：

```text
manifest.json
ticket.json
assets/
  <票面檔名>.png
```

票面檔名不固定；主程式只依 `ticket.json.art.ticket` 載入。Maker 應使用可辨識名稱，例如 `assets/three-star.png`。

若 `scratch.foil.source="package"`，包內必須另外包含 `scratch.foil.ref` 指向的 PNG，例如 `assets/foil.png`。GameType 若允許額外符號素材，也可在 `assets/` 增加 PNG，並由該 GameType 的合法欄位引用。

V1 **不定義、也不接受 `thumbnail.png`**；挑券縮圖由主程式將 `art.ticket` 指向的正式票面 PNG 等比例產生。

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
- `minimumAppVersion`：最低相容 ScratchGame 版本。
- `ticketFile`：包內 `ticket.json` 相對路徑。
- `manifest.json` 只承擔封裝層資訊，不重複保存彩券名稱、面額、Prize Tier、Canvas、GameType 或美術路徑。
- V1 不另拆 `layout.json` / `prizes.json`。
- 不保存本機 `styleNumber`；款式編號由 runtime database 管理。

## 4. `ticket.json`

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
    "ticket": "assets/three-star.png"
  },
  "serialDisplayArea": { "x": 64, "y": 742, "width": 205, "height": 36 },
  "scratch": {
    "foil": {
      "source": "builtin",
      "ref": "brushed-silver-plain"
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
- `art.ticket`：靜態票面 PNG 的唯一來源。
- `scratch.zones`：刮區 geometry / clipping 的唯一來源；銀膜資產不得另帶第二套刮區座標或互動範圍。
- `scratch.foil`：銀膜材質來源的唯一設定；不得另建 `art.mask`、`foilFile`、逐 zone foil 或其他平行欄位。
- `prizes`：最終 Prize Pool / Prize Tier 唯一權威來源；不得另建 `prizes.json`、圖片 metadata、`artworkFile` 或其他平行欄位。
- runtime database：本機款式編號、Pack 安裝來源、批次、日期、Remaining、Pending、歷史等 runtime state。

ScratchPack 不保存中獎率、未中獎率、最高獎金、EV、RTP、總本數、未中獎張數等 Derived Data。

挑券卡資料來源：名稱=`name`；面額=`price`；縮圖=`art.ticket`；中獎率由 `prizes` + `issueSize` 算；最高獎由 `max(prizes.amount where count > 0)` 算；批次與發行日期來自 runtime database。

## 5. Canvas、票面與動態層

V1 只允許官方固定 Canvas code，不允許 Pack 任意輸入寬高。

```text
canvas = 1 → 1080 × 882
```

既有 Canvas code 的尺寸語意永遠不得改變；其他尺寸只能新增新 code。

`art.ticket` 指向的 PNG 必須與 Canvas 尺寸完全一致；Maker / Importer 不得偷偷縮放、裁切或重新取樣後放行。

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

- `0`：面額完整畫在 PNG，主程式不再疊面額；實際面額仍以 `ticket.json.price` 為權威。
- `1`：底圖留空並提供 `priceDisplayArea`，主程式在該區域畫完整標準面額徽章；底圖不得殘留舊面額文字或底塊。

`serialDisplayArea` 只提供票號區域位置與大小；票號格式、字型、圓角框與底色由主程式統一處理，PNG 不得燒入舊票號或舊票號底塊。

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
  "x": 281,
  "y": 225,
  "width": 163,
  "height": 101,
  "shape": "roundedRectangle",
  "cornerRadius": 12
}
```

V1 不提供通用 `contentBox`。Scratch zone 只負責位置、尺寸與形狀；同類元件內部排版由各 GameType Renderer 固定。

### 7.1 `scratch.foil`

V1 每張票必須明確指定一個 `scratch.foil`。Maker 介面可以預選預設樣式，但輸出的 ScratchPack **不得靠缺省值或 `null` 暗示預設銀膜**。

使用 ScratchGame 公用內建銀膜：

```json
"foil": {
  "source": "builtin",
  "ref": "brushed-silver-plain"
}
```

使用 Pack 自帶銀膜：

```json
"foil": {
  "source": "package",
  "ref": "assets/my-foil.png"
}
```

規則：

- `source` 在 V1 只允許：`builtin`、`package`。
- `ref` 永遠只有一個欄位：
  - `source="builtin"` 時，`ref` 是 ScratchGame 公開且穩定的內建銀膜代碼。
  - `source="package"` 時，`ref` 是目前 ScratchPack 內的安全相對 PNG 路徑。
- V1 第一批公用內建銀膜代碼：

```text
brushed-silver-plain       一般拉絲銀膜
brushed-silver-three-star  三星圖樣拉絲銀膜
```

- 公用內建銀膜屬 ScratchGame 共用資源，不需複製進每個 `.scratchpack`；Imported Pack 與 Built-in Pack 都可直接使用相同代碼。
- 內建代碼一旦正式發布，不得改作另一種完全不同的銀膜；要提供新視覺時新增新代碼，不覆寫既有代碼語意。
- 若 Pack 使用某個較新版本才提供的內建代碼，`minimumAppVersion` 必須至少指向首次支援該代碼的 ScratchGame 版本。
- `source="package"` 讓 Pack 可以攜帶自己的銀膜 PNG；此 PNG 不要求等於 Canvas 尺寸，因 V1 將其視為可重用的 **single-zone foil template**。
- 不論 `builtin` 或 `package`，都進入同一套 foil renderer / ScratchSurface / 刮除 pipeline；不得因來源不同建立第二套互動或判定邏輯。
- 同一張票 V1 只指定一種 foil；所有 Scratch Zone 共用同一 `scratch.foil`。不提供逐 zone foil override。
- `scratch.zones` 是唯一的 geometry / clipping / hit-test 來源。foil code 或 foil PNG 只提供材質，不得定義 zone 的 x/y/width/height、shape、順序或玩法 mapping。
- runtime 依每個 `scratch.zones` geometry 將 foil template 套入並以 zone shape clipping；自訂 PNG 的 alpha 可參與視覺，但不得被視為新的互動 geometry。
- V1 不接受舊草案的 `art.mask`。也不以固定檔名 `mask.png` 建立第二套語意；自訂銀膜只由 `scratch.foil.source="package"` + `ref` 引用。
- 未來若實作「全 Canvas 銀膜再由 Scratch Zone 遮罩切出」模式，必須擴充同一個 `scratch.foil` 模型與同一 renderer pipeline，不得另建平行的 mask / overlay schema。該模式目前只列 TODO，不屬 V1。

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
- `art.ticket` 是合法包內相對路徑；Canvas code 支援，且 `art.ticket` 存在、可解碼、尺寸完全符合 Canvas。
- `scratch.foil` 必須存在且格式合法；不得接受 `art.mask` 或其他平行 foil / mask 欄位。
- `scratch.foil.source="builtin"` 時，`ref` 必須是目前 App 支援的公開 foil code；不支援時直接驗證失敗，不可靜默改用其他銀膜。
- `scratch.foil.source="package"` 時，`ref` 必須是合法包內相對 PNG 路徑，檔案必須存在且可解碼；不要求與 Canvas 同尺寸。
- 所有座標合法；`priceDisplay=1` 時 `priceDisplayArea` 合法；`serialDisplayArea` 合法。
- zone ID、shape，以及 GameType-specific 的 zone 數量、geometry、順序／mapping 合法。
- `issueSize % ticketsPerBook == 0`、`Σ prizes.count <= issueSize`。
- GameType、玩法參數與 Renderer 限制符合 `GAMETYPE_SPEC.md`。
- 所有被引用 PNG 可正常解碼。

Importer 對 PNG 的驗證到「路徑、檔案、格式、尺寸、可解碼」為止；不 OCR、不解析圖片文字、不從圖片推導任何遊戲資料。foil PNG 也不得反向成為 Scratch Zone geometry 或玩法資料來源。

任何驗證失敗都不得留下半套已安裝資料；外部 Pack 匯入必須為原子操作。

## 11. 內建基礎 Pack 與外部 Pack

正式彩券一律以 ScratchPack 定義；主程式不得為某張正式彩券另外硬編碼專屬 Prize Tier、layout、玩法結果或第二套 ticket definition。

第一款「三星連線」是 **Built-in Base Pack**：

- 使用與外部 `.scratchpack` 完全相同的 V1 schema、資源結構與 GameType 契約。
- 隨 ScratchGame 發行內容提供，不要求使用者手動匯入。
- 主程式初始化時自動確認並註冊。
- Built-in 與 Imported Pack 共用 loader / validator / engine / renderer / finite-pool / redemption pipeline。
- Built-in Base Pack 不提供刪除／解除安裝；缺少或損壞視為程式發行內容不完整。
- Built-in Base Pack 可停用／重新啟用；停用只影響新票選擇，狀態由 runtime database 管理，不寫入 ScratchPack schema。
- `BuiltIn` / `Imported` 是本機 runtime 安裝來源狀態，不是 Pack 可自行宣稱的 manifest / ticket 欄位。

外部 Pack 由使用者匯入；匯入後與 Built-in Pack 使用同一套 runtime model。解除安裝與歷史資料保存屬 runtime lifecycle，不寫入 Pack schema。

第一款正式 Built-in Base Pack「三星連線」：

- `gameType="1"`。
- `canvas=1`，票面 PNG 必須 1080×882。
- 正式版面額為 500、`issueSize=10000`。
- 正式美術使用可辨識檔名，例如 `assets/three-star.png`。
- 基本款票面不放中獎率與「最高可中 N 元」宣傳字樣。
- 可直接以 `scratch.foil.source="builtin"` 使用 ScratchGame 公用銀膜代碼，不需把共用銀膜複製進 Pack。
- 正式 Built-in Base Pack 不另維護主程式硬編碼的平行 ticket definition。

開發階段可以另外維護一個獨立的「三星連線測試 Pack」作為 V1 conformance / loader / renderer / Prize Tier 測試 fixture：

- 測試 Pack 也必須完整符合相同的 `SCRATCHPACK_SPEC.md` + `GAMETYPE_SPEC.md`。
- 測試 Pack 使用獨立 `packageId`、獨立票池與測試用 `issueSize` / Prize Tier count，不得冒充正式 Built-in Base Pack。
- 測試 Pack 是獨立 ScratchPack，不是主程式內另一套硬編碼 ticket definition。
- 測試 Pack 是否隨正式 portable 發行屬發行／測試流程決策，不寫入 ScratchPack schema。

## 12. 相容性與文件責任

- `formatVersion` 只代表 ScratchPack schema / 封裝版本，不代表 GameType 版本。
- 已發布欄位的語意不得在相同 formatVersion 下偷偷改變；不相容 schema 變更必須升級 `formatVersion`。
- 已發布的 built-in foil code 也視為公開相容性契約；新增視覺使用新 code，不把既有 code 重新指向不同語意的銀膜。
- GameType 相容性只由 `GAMETYPE_SPEC.md` 管理。
- `PROJECT_RULES.md` 不複製 schema；`TODO.md` 不複製 schema 或 GameType 核心規則。
