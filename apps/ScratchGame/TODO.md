# ScratchGame TODO / Future Plan

本檔只記錄產品 Roadmap、未來規劃與待辦，不是永久規則來源。ScratchPack schema / ResourceRef / Built-in registry 以 `SCRATCHPACK_SPEC.md` 為唯一權威；GameType 契約以 `GAMETYPE_SPEC.md` 為唯一權威；永久專案原則以 `PROJECT_RULES.md` 為準。

## 目前 Roadmap

### V0.4.0 — ScratchPack V1 Runtime

目前開發階段。目標是完成並實機驗收：

- ScratchPack V1 loader / validator / importer。
- Built-in / Imported Pack 共用同一套 loader、validator、engine、renderer、finite-pool、redemption pipeline。
- Built-in Pack 隨 portable 發行內容提供，程式啟動時自動註冊；不要求使用者手動匯入。
- Built-in / Imported 身分只屬 runtime installation source，不寫入 `.scratchpack` schema。
- Built-in ticket ResourceRef 依 GameType namespace 解析；foil 為跨 GameType 共用資源。
- GameType 1 runtime 完全由 Pack 的 game / zones / prizes / ResourceRef 驅動，不保留三星專屬平行定義。
- Pack 安裝完成時產生挑券用縮圖 PNG cache；cache 遺失或損壞時可由正式 Pack / runtime definition 重建。
- ScratchPack V1 本身不接受 `thumbnail.png`；縮圖不是 Pack 權威資料。
- 完成 ThreeStar-Test 的 Windows 實機匯入、發行、挑票、刮獎、兌獎驗收。

### V0.5.0 — ScratchPack Maker

製作 Windows x64 ScratchPack Maker，讓使用者以 GUI 製作正式 `.scratchpack`。

- Maker 直接依編輯中的資料即時計算並疊出預覽；這是一次性編輯預覽，不使用主程式 thumbnail cache，也不另建 Thumbnail Renderer。
- 選擇官方 Canvas、GameType 與合法玩法參數。
- 選擇 Built-in ticket / foil 或 Pack 自帶 PNG。
- 視覺化設定 Scratch Zone geometry。
- 設定面額、price display、serial area、issueSize、ticketsPerBook、Prize Pool。
- Maker 只允許產生主程式已正式支援的規格。
- Maker 與 Importer 對相同 schema / GameType 契約必須得到一致驗證結果，不建立第二套規則。
- 正式 Built-in Pack 也由 Maker 產生；完成後由使用者提供最終 `.scratchpack`，主程式發行時原樣納入 `BuiltInPacks/`，不另外手寫一套 Built-in Pack 格式。

### V0.6.x ～ V0.9.x — 完整化階段

不預先把每一個 Minor 版號綁死。每完成一個足夠完整、使用者可明顯感受到的功能階段，再依共通版本規則決定是否升 Minor。

主要工作：

- 逐步完成 `GAMETYPE_SPEC.md` 已定義的 GameType 與對應 Renderer / Generator / validation。
- 完善刮獎手感、銀膜視覺、碎屑／刮痕、硬幣、音效與中獎效果。
- 建立 Decoration Shop / 收藏／裝備系統。
- 完善 UI、效能、穩定性與 regression tests。
- 每次涉及 runtime database schema 的變更，都必須由開發端負責 migration、migration backup 與既有資料驗證。

### V1.0.0 — 正式穩定版 Gate

V1.0.0 不只以「功能都有」判定，至少須完成：

- ScratchPack V1 runtime 穩定。
- ScratchPack Maker 可完整製作所有當時正式支援的 GameType Pack。
- 預定 GameType、Decoration Shop 與刮獎體驗完成。
- 新安裝流程驗證通過。
- 既有 runtime database 升版 / migration 驗證通過。
- Maker → Pack → Import / Built-in → 發行 → 挑票 → 刮獎 → 兌獎完整 round-trip regression 通過。
- 功能 Freeze 後完成一輪只修 bug 的正式驗收，再由使用者決定發布 V1.0.0。

V1.0.0 發布後，再另外討論長期 Pack 更新／跨大版本升級／相容政策；目前 0.x 階段不提前建立不必要的複雜升級框架。

## GameType 相容與升版安全

GameType 的正式欄位與核心判定以 `GAMETYPE_SPEC.md` 為準。升版安全方向已定：

- 已發布欄位不得刪除、改名或偷偷改變原有語意。
- 後續需要擴充時以新增欄位為主。
- 新增 optional 欄位必須定義明確 default；舊 Pack 缺少新欄位時必須維持原有行為。
- 若需求會改變核心勝負判定，而不是單純增加相容 optional 行為，應新增 GameType / variant，不修改既有 GameType 的既定語意。
- Runtime database schema migration、migration backup 與舊資料驗證由開發端負責，不要求使用者自行處理資料升版。

## Built-in Pack 與 Pack lifecycle

正式規則仍以 `SCRATCHPACK_SPEC.md` 為準；此處只記錄後續工作：

- Built-in Pack 與 Imported Pack 的 `.scratchpack` 內容格式沒有差別。
- 正式 Built-in Pack 不由主程式 source 手寫產生；V0.5.0 Maker 完成後，由使用者用 Maker 製作並確認，再提供給發行流程放入 `BuiltInPacks/`。
- Built-in Pack 啟動自動註冊；Imported Pack 由使用者手動匯入。
- 1.0.0 前只處理目前實際需要的安裝與 runtime lifecycle；Pack 更新／替換／跨版本 upgrade policy 留到 V1.0.0 發布後再討論。

## 挑券縮圖 cache

- ScratchPack 不保存 thumbnail；票面 / Pack definition 才是 Source of Truth。
- 主程式在 Pack 安裝成功後產生一張挑券用 PNG cache，建議基準尺寸 `360×294`（Canvas 1 的 1080×882 等比例 1/3）。
- 縮圖用來表達「這款彩券長什麼樣」，不是某張已發行 Pending Ticket 的 screenshot。
- 可包含固定票面、程式面額（若 `priceDisplay=1`）與未刮銀膜示意。
- 不包含實際票號、某張票的遊戲結果、Prize outcome 或 Pending Ticket 資料。
- 挑券頁正常只讀 cache；cache 被刪除、損壞或失效時重新產生並寫回。
- Cache 可全部清除而不影響 Pack、票池、批次或使用者資料。
- Maker 編輯預覽不使用此 cache；Maker 直接即時疊圖。

## Decoration Shop / 使用者體驗商店

目前 Roadmap 中的「商店」是 **Decoration Shop**，不是彩券商店。

Decoration Shop 管理 cosmetic / 使用者體驗資源，例如：

- Frame Theme：Header + Footer 成套。
- Stage Theme：中央舞台背景。
- 硬幣。
- 刮痕／銀膜碎屑／刮刮視覺效果。
- 一般獎／二獎／頭獎等慶祝效果。
- 後續可擴充其他純視覺／音效收藏。

共同方向：

- Decoration 不得改變 ScratchPack、票池、中獎率、Prize Tier、獎金或彩券損益。
- Header + Footer 屬同一 Frame Theme，不跨框架拆開混搭。
- Stage Theme 可獨立收藏與裝備。
- Theme / Decoration 美術維持 EXE 外部可替換資源，不把可替換 cosmetic 強制嵌入主程式。
- 商店收藏／消費資金模型與彩券投入／兌獎／損益統計分離。

目前預設：

- Frame Theme：`新春紅金`。
- Stage Theme：`招財好運`。

## 彩券商店 / Pack Marketplace — 最長期計畫

單機版目前不需要另外製作彩券商店。已安裝 Pack 直接透過現有挑券流程使用即可。

只有未來若真的發展到 Steam、線上內容配送、Workshop / Marketplace 等情境，再研究：

- Ticket Shop / Pack Marketplace。
- Pack 下載、版本配送與更新。
- 線上內容索引與安全驗證。
- Steam Workshop 或其他平台整合。

此項為 V1.0.0 之後的長期 TODO，不阻擋單機版 V1.0.0。

## 刮獎手感、硬幣與中獎效果

- 主程式使用程式繪製／外部 cosmetic 資源的硬幣游標，不依賴 Windows `.cur`。
- 待機顯示硬幣正面；按住刮獎時切換為以邊緣接觸票面的直立／傾斜狀態。
- 後續支援多種硬幣、不同刮痕寬度、刮擦聲、銀膜碎屑與自然不規則刮除動畫。
- 中獎效果依 Prize Tier 排名分級，不把固定金額寫死成頭獎／二獎。
- 結果框維持可看到背後中獎盤面的呈現方式。
- 硬幣、刮刮效果、中獎效果都可成為 Decoration Shop 的收藏／裝備項目，但不得影響遊戲結果。

## 音效

- 目前中獎音效分小獎與大獎；`< $50,000` 使用小獎音效，`>= $50,000` 使用大獎音效。
- 手動完整刮完與「全部刮開」／系統自動揭曉可以使用不同提示版本，避免手動刮獎時提前暴雷。
- 音效只在完成兌獎、顯示結果時播放。
- 後續可再評估頭獎專屬音效與 Decoration Shop 音效收藏。

## 測試與 Conformance

- 每個正式完成的 GameType 應保留最小 reference / test Pack，驗證合法 Pack、非法 geometry、Prize mapping、Renderer 與 outcome generation。
- Test Pack 必須走與正式 Pack 相同的 ScratchPack schema / GameType pipeline，不建立測試專屬 loader。
- 正式使用者資料維持在 `%LOCALAPPDATA%\ScratchGame`；更新／替換 EXE 不得刪除資料。
- 涉及 database schema 變更時先建立 migration backup，再執行 migration。
- 後續設定頁可增加「開啟資料資料夾」與具二次確認的「重置所有資料」，供測試與維護。

## 其他後續

- 擴增更多固定 Canvas code；既有 code 語意不得改變。
- 擴增更多官方 Scratch Zone shape。
- 評估全 Canvas foil texture + Scratch Zone clipping 模式；若實作必須沿用同一 `scratch.foil` / ScratchSurface pipeline，不建立第二套 mask 系統。
- 未來再正式設計單張彩券多玩法／Bonus 區架構。
- 後續建立 ScratchPack Developer Guide 與可機器驗證的 schema / fixture 工具；不得形成與正式 SPEC 平行的規則來源。
