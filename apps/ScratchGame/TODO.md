# ScratchGame TODO / Future Plan

本檔只記錄未來規劃、實作工作與未決事項，不是永久規則或格式規格來源。

權威規格：

- ScratchPack schema：`SCRATCHPACK_SPEC.md`
- GameType 規則：`GAMETYPE_SPEC.md`
- 永久專案規則：`PROJECT_RULES.md`

不得在 TODO 重複 schema、Prize Tier 定義或 GameType 核心規則。

## ScratchPack V1 實作順序

1. 先完成獨立的「三星連線測試 Pack」：`manifest.json`、`ticket.json` 與必要資源；使用小型測試票池覆蓋 0 線與所有合法正獎線數。
2. 以三星連線測試 Pack 做第一個 V1 conformance / reference walkthrough，確認實際 Pack 能完整符合 `SCRATCHPACK_SPEC.md` + `GAMETYPE_SPEC.md`，不另加平行欄位。
3. 完成正式 Built-in Base Pack「三星連線」500 元／10,000 張版本；正式 Pack 與測試 Pack 使用獨立 `packageId` 與獨立票池。
4. 實作 V1 loader / validator，讓 Built-in / Imported / 開發測試 Pack 共用同一套載入與驗證 pipeline。
5. 將目前三星連線專屬硬編碼資料移除，改由 Built-in Base Pack 提供。
6. 完成 GameType 1 runtime / renderer / finite-pool end-to-end 驗證。
7. 依 `GAMETYPE_SPEC.md` 完成 GameType 2～6 engine / renderer / validation。
8. 實作外部 `.scratchpack` 原子匯入、packageId 管理、錯誤回復與安裝來源狀態。
9. 製作 ScratchPack Maker 第一版：GameType-aware 精靈、票面預覽、zone 位置配置、Prize Pool、derived statistics、預覽測試與一鍵封裝。
10. 每個 GameType 至少以一張實際 ScratchPack 做建立、驗證、匯入／註冊、遊玩、有限票池、兌獎與統計測試。
11. 完成六種基礎玩法交叉測試後，進入 ScratchGame V1.0.0 發行準備。

## ScratchPack Developer Guide

待建立對外 Developer Guide，引用正式 SPEC，不自行發明第二套規則。至少包含：

- `formatVersion` 與 `gameType` 對照。
- Canvas code 對照表。
- Scratch zone / Shape 對照。
- 內建／Pack 自帶銀膜的選擇方式與素材製作建議；schema 直接引用 `SCRATCHPACK_SPEC.md`。
- `priceDisplay` 與票號安全區說明。
- 發行量、每本張數、Prize Pool 驗證方式。
- 圖片格式與資源命名建議。
- 完整可重製範例。
- 評估提供 `scratchpack.schema.json` 供工具與 AI 做結構驗證。

## ScratchPack / Pack lifecycle 未完成工作

- 設計外部 Pack 解除安裝後，已發行批次、歷史紀錄與使用者既有遊玩資料的保留／封存規則。
- 擴增更多固定 Canvas code。
- 擴增更多官方刮膜幾何形狀。
- 未來再設計單張彩券多玩法／Bonus 區架構。
- 未來如需要新的核心玩法規則，新增 GameType / variant，不修改已發布 GameType 語意。
- ScratchPack 自訂中獎音效／中獎動畫不列入 V1；日後若需要另行規格化。
- 評估支援**全畫布銀膜 overlay / mask 模式**：
  - foil 可是一整張與 Canvas 同尺寸的單純銀色底或重複花紋；Canvas 1 即為 1080×882；
  - foil 紋理本身不綁定各 Scratch Zone 位置，因此 zone 放在哪裡都不需要重做銀膜；
  - runtime 最終只在 Scratch Zone 位置透過既有 `scratch.zones` geometry 做 clipping，就像一般市售無特定分格花樣的刮刮樂銀膜；
  - 若實作，必須**擴充既有 `scratch.foil` 模型與同一 foil renderer / ScratchSurface pipeline**，不得另建第二套 `mask`、`overlay`、逐 zone 座標或另一套刮除邏輯。

## ScratchPack Maker 後續

- Maker 專案檔（例如 `.scratchproj`）目前只保留概念，不列入近期正式開發；等 Maker 第一版實際使用後再決定是否需要。
- Maker 的銀膜 UI 只操作 `SCRATCHPACK_SPEC.md` 定義的同一個 `scratch.foil`：可選 ScratchGame 公用內建素材，或選擇 Pack 自帶 PNG；不建立另一套自訂銀膜功能。
- 後續以 runtime 視覺測試確認 reusable single-zone foil template 的安全縮放範圍與必要的 edge-preserving rendering；這屬 renderer / Maker 行為，不另外污染 Pack 的 zone geometry。

## 刮獎手感與硬幣

- 主程式使用程式繪製的硬幣游標，不依賴 Windows `.cur`；待機顯示硬幣正面，按住刮獎時切換成以邊緣接觸票面的直立／傾斜狀態。
- 未來支援多種硬幣、不同刮痕寬度、刮擦聲、銀膜碎屑與更自然的不規則刮除動畫。
- 規劃「硬幣商店」：玩家可使用遊戲內獲得的可用獎金／收藏金購買不同刮獎硬幣。
- 商店資金與「彩券投入／兌獎／損益」統計分離，購買硬幣不得回頭改寫彩券損益。

## 中獎效果與商店

- 主程式先提供預設的中獎效果分級；一般獎、二獎、頭獎的視覺強度不同，且以 Prize Tier 排名判定，不把特定金額寫死為頭獎。
- 頭獎效果可包含金色閃光、擴散光圈、星光／金幣粒子與延遲淡入結果框；二獎使用較克制的版本。
- 結果框維持半透明，讓玩家仍能看到背後已刮開的中獎盤面。
- 未來規劃「中獎效果商店」；效果只屬外觀收藏，不得改變中獎率、Prize Tier、獎金或票池。
- 中獎效果商店與硬幣商店共用未來的獨立收藏／消費資金模型，不回寫彩券損益統計。

## 外觀主題與商店

- 主畫面外觀分成 **介面框架（Frame Theme）** 與 **舞台主題（Stage Theme）**。
- Frame Theme 的 Header / Footer 成套，不跨不同框架混搭。
- Stage Theme 可獨立於 Frame Theme 購買、收藏與裝備。
- 預設 Frame Theme：**新春紅金**；預設 Stage Theme：**招財好運**。
- Theme 美術從 EXE 外部資源讀取。
- 未來可在外觀商店增加 Frame / Stage 類別，並與硬幣／中獎效果共用收藏、購買、裝備基礎架構。
- 外觀 Theme 只屬 cosmetic，不得改變彩券票池、中獎率、Prize Tier、獎金或損益統計。

## 音效

- 中獎音效分小獎與大獎；目前 `< $50,000` 使用小獎音效，`>= $50,000` 使用大獎音效。
- 手動把所有刮區刮完時播放「無提示逼聲」版本；全部刮開或系統自動揭曉時播放保留提示逼聲的版本。
- 音效只在完成兌獎、顯示結果時播放，避免提前暴雷。
- 未來可再評估頭獎專屬音效。

## 資料與測試工具

- 正式使用者資料維持在 `%LOCALAPPDATA%\ScratchGame`，更新／替換 EXE 不會刪除資料。
- 未來設定頁可增加「開啟資料資料夾」與具二次確認的「重置所有資料」，方便測試與維護。
