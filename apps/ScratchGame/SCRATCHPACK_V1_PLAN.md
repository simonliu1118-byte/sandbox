# ScratchPack V1 / Maker 設計計畫

狀態：**已討論定案、尚未實作**  
實作時機：**等待 ScratchGame V0.3.0 Build 3 使用者驗收完成後再開始**。

本文件記錄下一階段 ScratchPack V1 與 ScratchPack Maker 的已定案方向，避免依賴聊天記憶。現行 `SCRATCHPACK_SPEC.md` 仍代表目前已實作格式；在新格式真正完成前，不把本文件當成已支援能力。

---

## 1. ScratchPack V1 定位

ScratchPack 只提供資料與美術，不包含可執行玩法程式碼。主程式負責：

- GameType 規則引擎
- 盤面生成與合法性
- 刮膜互動
- 票號
- 面額程式顯示
- 批次與有限票池
- 兌獎
- 中獎效果與音效
- 使用者與損益資料

ScratchPack 負責：

- 彩券名稱、作者、面額
- 固定 Canvas 代碼
- 票面 PNG
- 刮區數量、位置、尺寸、形狀
- GameType 與該玩法允許的參數
- 總發行張數、每本張數
- 各有獎獎項的金額與張數

V1 不允許包內程式碼、不允許自訂玩法引擎、不讓 ScratchPack 自己定義 winning lines。

---

## 2. 最小封裝結構

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

`thumbnail.png` **不是必要檔案**。未提供時，主程式直接以 `ticket.png` 等比例產生縮圖。

V1 暫不開放 ScratchPack 自訂中獎音效／中獎動畫；這些由主程式與未來玩家商店系統管理。

---

## 3. manifest.json

建議結構：

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

- `packageId` 為套件唯一識別，建議由 Maker 自動產生 GUID。
- 不再另外保存與 `ticket.json` 重複的彩券名稱。
- 不再拆分 `layout.json` / `prizes.json`，避免多份 JSON 互相矛盾。
- ScratchPack 作者不填本機 `styleNumber`；彩券款式編號由主程式第一次匯入時自動分配並永久綁定該安裝資料。

---

## 4. ticket.json 核心欄位

目標結構：

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

不保存中獎率、期望值、RTP、總本數、未中獎張數等可推導資料。

---

## 5. Canvas 代碼

V1 只允許主程式已公告的固定 Canvas 代碼，不允許任意輸入寬高。

目前正式基準：

```text
canvas = 1  →  1080 × 882
```

之後新增 Canvas 只能新增新代碼；既有代碼的尺寸意義不得改變。

Maker 選 Canvas，不讓開發者手動輸入 width / height。匯入圖片尺寸若與 Canvas 不符，直接報錯；不得偷偷縮放、裁切或重新取樣。

---

## 6. 票面美術與動態層責任

### ticket.png 負責

- 彩券背景
- 彩券名稱與固定說明
- 裝飾圖案
- 刮區底框、底色、金框等固定美術
- `priceDisplay = 0` 時的完整面額美術

### 主程式負責

- 每張票的星星／符號／數字／獎金結果
- 銀膜建立與刮除
- 彩券序號
- `priceDisplay = 1` 時的完整面額徽章
- 批次／票池狀態
- 中獎效果、音效、硬幣

疊圖順序：

```text
ticket.png
→ 動態遊戲內容
→ 刮膜
→ 票號 / 程式面額
→ 硬幣
→ 中獎特效 / 結果 UI
```

---

## 7. 面額與票號

### priceDisplay

- `0`：面額完整畫在 `ticket.png`，主程式不再疊面額。
- `1`：底圖留空，ScratchPack 必須提供 `priceDisplayArea`，主程式在該區域畫完整標準面額徽章（外框、底色、金額）。

### serialDisplayArea

ScratchPack 只提供票號區域的位置與大小。票號格式、字型、圓角框、底色等由主程式統一處理。

票號仍採：

```text
款式編號-本號-本內序號
```

每段至少三位補零，超過三位時自動增加位數。

---

## 8. Scratch Zone

V1 支援形狀：

```text
rectangle
roundedRectangle
circle
ellipse
```

`roundedRectangle` 可有 `cornerRadius`。

`circle` 必須 `width == height`；否則匯入拒絕。

每個 zone 至少包含：

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

可選 `contentBox`：若存在，動態遊戲內容置中於 `contentBox`；不存在時即使用整個 scratch zone 作內容區。

銀膜：

- `art.mask = null` 或缺省：使用主程式預設銀膜。
- 指定 `assets/mask.png`：使用包內銀膜材質。

V1 **不允許額外裝飾刮區**。玩法需要多少刮區，就必須剛好有多少刮區。

---

## 9. GameType 原則

ScratchPack 只選玩法與填該玩法公開參數，不得自己寫規則。

舊 GameType 的底層行為發布後不修改；未來有變體時新增新 ID，例如 `1-2`，而不是改 `1`。

新增參數必須向下相容：舊包沒有新欄位時，主程式使用「不改變舊行為」的預設值。

---

## 10. GameType 1：星星連線

使用：

```json
"gameType": "1"
```

公開參數：

```json
"game": {
  "gridSize": 3,
  "cellZones": [
    "cell01", "cell02", "cell03",
    "cell04", "cell05", "cell06",
    "cell07", "cell08", "cell09"
  ],
  "allowNearMiss": true
}
```

### gridSize

```text
3 → 3×3，三星一線
4 → 4×4，四星一線
5 → 5×5，五星一線
```

### 有效中獎線

只計：

- 所有完整橫線
- 所有完整直線
- 左上→右下大斜線
- 右上→左下大斜線

短斜線或自訂線路一律不屬於 GameType 1。

### 刮區數量

```text
3×3 → 必須恰好 9 個 scratch zones
4×4 → 必須恰好 16 個 scratch zones
5×5 → 必須恰好 25 個 scratch zones
```

`cellZones` 必須剛好引用所有 scratch zones 一次：不得缺少、不得重複、不得有額外 zone。

### allowNearMiss

控制是否允許生成「差一顆星就成線」之類的近似中獎盤面。

舊包缺少此欄位時，預設 `false`。

### GameType 1 Prize Tier 對應

ScratchPack 的 `prizes` 只保存金額與張數，不另外保存 `lineCount`。

Maker 依固定玩法建立不可編輯的 tier 順序：

```text
3×3：1、2、3、4、5、6、8 線 → 7 個正獎 tier
4×4：1～8、10 線          → 9 個正獎 tier
5×5：1～10、12 線         → 11 個正獎 tier
```

最後一個正獎 tier 永遠代表所有有效中獎線全部完成。

---

## 11. 發行量、本數與未中獎張數

開發者手動輸入：

```text
issueSize
ticketsPerBook
```

必須符合：

```text
issueSize > 0
ticketsPerBook > 0
issueSize % ticketsPerBook == 0
```

主程式／Maker 自動計算：

```text
bookCount = issueSize / ticketsPerBook
```

`bookCount` 不寫入 ScratchPack。

### prizes

`prizes` 只保存真正有獎金的獎項；每列只有：

```json
{ "amount": 100, "count": 6000 }
```

V1 不要求也不保存 `amount = 0` 的未中獎 tier。

計算：

```text
winningCount = Σ prizes.count
loseCount = issueSize - winningCount
```

規則：

- `winningCount < issueSize`：差額全部自動視為未中獎。
- `winningCount == issueSize`：100% 中獎，合法。
- `winningCount > issueSize`：錯誤，禁止 Maker 封裝，主程式匯入也必須拒絕。

---

## 12. Maker 自動統計

開發者不手動計算衍生資料。

Maker 依面額、issueSize、ticketsPerBook、prizes 即時計算：

- 總本數
- 有獎張數
- 未中獎張數
- 中獎率 / 未中獎率
- 總派彩金額
- 每張期望值（EV）
- RTP / 回收率
- 每張期望損益
- 打平以上機率（`amount >= price`）
- 真正賺錢機率（`amount > price`）

這些只作 Maker / 主程式顯示，不作 ScratchPack 權威欄位。

若獎項張數超過 `issueSize`，立即顯示錯誤並鎖住封裝功能。

---

## 13. ScratchPack Maker 第一版流程

第一版 Maker 採精靈式流程：

1. **基本資料**：名稱、作者、面額、Canvas、GameType、issueSize、ticketsPerBook。
2. **票面美術**：匯入 `ticket.png`；選擇票面是否已含面額；在預覽上指定程式面額區與票號區。
3. **刮獎區配置**：在票面預覽直接新增、拖曳與微調 scratch zones；選形狀與銀膜來源。
4. **玩法設定**：依 GameType 顯示公開參數。GameType 1 可選 3×3 / 4×4 / 5×5、調整 cellZones 順序、設定 allowNearMiss。
5. **獎項與票池**：只輸入各正獎金額與張數；未中獎張數與所有統計自動計算。
6. **預覽測試**：能選指定獎項產生測試票，檢查符號、銀膜、票號、程式面額與實際刮獎位置。
7. **檢查與封裝**：全部驗證通過後才可輸出 `.scratchpack`。

Maker 專案檔（例如 `.scratchproj`）目前**只保留為概念，不列入 TODO，也不列入正式未來開發計畫**。

---

## 14. Maker / 匯入器必要驗證

至少同時在 Maker 與主程式匯入器驗證：

- ZIP / 路徑安全與禁止可執行內容。
- manifest / ticket JSON 可解析且版本相容。
- `packageId` 唯一且格式合法。
- `canvas` 為主程式已支援固定代碼。
- `ticket.png` 尺寸完全符合 Canvas。
- 所有座標區域都落在 Canvas 內。
- `priceDisplay=1` 時存在合法 `priceDisplayArea`。
- `serialDisplayArea` 合法。
- scratch zone 數量精確符合 GameType。
- zone ID 唯一。
- `cellZones` 不缺漏、不重複，並剛好引用所有 zones。
- shape 參數合法。
- GameType 與公開參數受主程式支援。
- `issueSize % ticketsPerBook == 0`。
- 所有 prize `amount > 0`、`count > 0`。
- `Σ prize.count <= issueSize`。
- GameType 1 正獎 tier 數量符合 gridSize。
- 所有 PNG 可正常解碼。

任何驗證失敗都不得留下半套已安裝資料；匯入必須為原子操作。

---

## 15. 未來正式彩券 ScratchPack 化

不是 Build 3 工作內容。

未來方向：

- 除開發測試票外，正式彩券（例如三星連線）逐步從主程式硬編碼移除。
- 正式彩券改由 `.scratchpack` 安裝。
- 主程式只保留 GameType 引擎與共通框架。
- 移除已安裝 ScratchPack 時，該彩券的專屬定義與資源應一起移除，不應因主程式仍有該款專屬硬編碼而重新出現。
- 已存在歷史紀錄、已結束／已發行批次在移除套件後如何保存，待真正實作刪除功能時另行設計；目前不先定死。

---

## 16. 下一階段順序

在 Build 3 尚未經使用者實測通過前，**不開始 ScratchPack V1 實作**。

Build 3 通過後，建議依序：

1. 將本計畫整理成新的正式 `SCRATCHPACK_SPEC.md`。
2. 調整主程式 ScratchPack importer / schema。
3. 以「三星連線 $500」製作第一個正式參考 ScratchPack。
4. 驗證主程式能完全由 ScratchPack 提供票面、版型、票池與 GameType 參數，不依賴該款彩券專屬硬編碼。
5. 完成匯入／刪除／重新發行等資料生命週期設計。
6. 再開始 ScratchPack Maker 第一版。

在此之前只保存規劃，不修改 Build 3 功能。