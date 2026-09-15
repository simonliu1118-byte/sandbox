# ThreeStar-Test Artwork Handoff

更新日期：2026-09-15

本檔只記錄目前美術工作狀態與下一步，不是 ScratchPack schema 或 GameType 規格來源。權威規格仍為 `SCRATCHPACK_SPEC.md` 與 `GAMETYPE_SPEC.md`。

## 目前 Reference Pack

`ThreeStar-Test` 已建立：

- `manifest.json`
- `ticket.json`
- `packageId = 2e1e951e-0ca0-4b47-8146-82e0415d6155`
- `canvas = 1`，正式尺寸必須為 `1080 × 882`
- `gameType = "1"`
- `gridSize = 3`
- `issueSize = 8`，覆蓋 0 線未中獎與 1、2、3、4、5、6、8 線所有合法正獎 outcome

目前 `ticket.json` 的標準九宮格 geometry：

- 每格 `160 × 100`
- X：`276 / 460 / 644`
- Y：`225 / 353 / 481`
- 水平間距 `24`
- 垂直間距 `28`
- `roundedRectangle`，`cornerRadius = 12`

面額區：`x=854, y=54, width=160, height=78`。
票號區：`x=390, y=780, width=300, height=44`。

## 目前美術草稿

目前保留三張票面方向與一張獨立銀膜：

1. `red-original-silver`：紅色／金色版本，仍帶有銀膜，作為視覺方向參考。
2. `red-base-no-foil`：紅色／彩虹版本，已移除動態面額、票號與銀膜，九格保留底框／占位美術。
3. `blue-base-no-foil`：藍色星空／彩虹版本，已移除動態面額、票號與銀膜，九格保留底框／占位美術。
4. `foil-silver-style-a`：第一種獨立銀膜樣式，透明背景；未來可作 Maker 內建銀膜樣式候選。

原始生成草稿目前皆為 `1388 × 1133`，**不是**正式 `canvas=1` 尺寸。它們只作美術草稿／Maker 樣式候選，不得直接升格為正式 `assets/three-star.png`。

若 `artwork-drafts/` 中保存的是 `.archive-preview.avif`，它們是為了對話交接與 Git 備份而建立的壓縮預覽副本，不是 production master；正式資產仍需從原始 PNG／本地工作檔重新輸出。

## 已定案的資產責任

- Base ticket art 不應燒死動態面額。
- Base ticket art 不應燒死動態票號。
- 銀膜應可作獨立資產，不應與 Base art 強耦合。
- 未來 ScratchPack Maker 可保留目前兩種票面方向作內建樣式候選，並保留不同銀膜樣式作可選素材。
- 目前 `ticket.json` 的 `art.mask` 仍為 `null`；在銀膜尺寸與 geometry 正式驗證前不要改成正式 mask 路徑。

## 下一步

使用**影像處理工具**（PIL / ImageMagick 類），不要再用圖片生成器做精確尺寸工作：

1. 對選定的紅／藍 Base art 做精確 `1080 × 882` 輸出。
2. 使用一致的幾何轉換處理銀膜資產；不可只靠肉眼縮放。
3. 重新量測並校正九宮格，使畫面刮區精確對齊 `ticket.json` 的 9 個 `160 × 100` zones（X 276/460/644；Y 225/353/481）。
4. 確認面額區與票號區沒有被固定文字／裝飾侵入。
5. 逐張驗證尺寸、透明度、zone 對齊後，才挑一張升格為 `assets/three-star.png`。
6. 若選用自訂銀膜，再把對應 mask 正式接入 Pack；在此之前維持 `art.mask = null`。

## 不要做的事

- 不要把草稿 PNG／AVIF 直接當正式票面。
- 不要用圖片生成器重做精確 `1080 × 882` 尺寸；圖片生成器只負責視覺創作。
- 不要把面額或票號重新燒進 Base art。
- 不要為 ThreeStar 再新增另一套硬編碼 ticket definition。
- 主畫面 Header / Stage / Footer 已驗收，不要因本次彩券美術工作順便修改主畫面。
