# ScratchGame TODO / Future Plan

本檔只記錄產品 Roadmap、目前狀態、未來規劃與待辦，不是永久規則來源。ScratchPack schema / ResourceRef / Built-in registry 以 `SCRATCHPACK_SPEC.md` 為唯一權威；GameType 契約以 `GAMETYPE_SPEC.md` 為唯一權威；永久專案原則以 `PROJECT_RULES.md` 為準。

## 目前 Roadmap

### V0.4.x — ScratchPack V1 Runtime / 主程式整理

已完成 ScratchPack V1 runtime 的主要骨架與 GameType 1 實機流程，包括：

- ScratchPack V1 loader / validator / importer。
- Built-in / Imported Pack 共用 loader、validator、engine、renderer、finite-pool、redemption pipeline。
- Built-in / Imported 只屬 runtime installation source，不寫入 `.scratchpack` schema。
- Built-in ticket ResourceRef 依 GameType namespace；foil 為跨 GameType 共用。
- GameType 1 由 Pack 的 game / zones / prizes / ResourceRef 驅動。
- 安裝後產生挑券 thumbnail cache；cache 可重建，不屬 Pack schema。
- Wallet 模型：新使用者初始 `$100,000`，購票扣 Wallet，兌獎回 Wallet。
- 本機版只保存累積遊玩統計，不保存逐張玩家歷史。
- Footer Player Card / StatusText、Header toolbar、結果 modal 等主 UI 已進入新架構。

### V0.5.1 — PackEditor / UI 重整

正式名稱：**PackEditor**。它是 ScratchGame 的附屬 Windows x64 EXE，source 位於 `apps/ScratchGame/src/PackEditor/`，與 ScratchGame 共用同一份 VERSION / BUILD，不建立第二個產品專案或平行版號。

目前已完成第一個可測 GameType 1 流程與 V0.5.1 UI 重整：

- 「新增 Pack」自動產生 UUID v4 `packageId`。
- 基本資料：名稱、作者、遊戲類型、面額、彩券尺寸；packageId / minimum app version 不作為一般輸入欄位。
- GameType 1 在 UI 顯示為 **「星星連線」**；Canvas 1 在 UI 只顯示 **`1080 × 882 px`**。
- Built-in ticket / foil 與 Pack 自帶 PNG 選擇。
- Canvas 1 自訂票面必須精確 1080×882，不做 silent resize / crop。
- GameType 1 的 3×3 / 4×4 / 5×5 Scratch Zone 由整組 grid 參數自動產生 row-major geometry。
- price display area / serial area 數字編輯與中央 geometry 預覽。
- Prize Pool 可編輯 amount / count；合法線數由 GameType 規則衍生，不寫入 schema。
- 即時計算中獎張數、未中獎張數、中獎率、總銷售、總獎金、平均獎金（期望值）、**獎金回饋率**。
- 輸出前建立真實 `.scratchpack`，再使用正式 `ScratchPackV1Loader` 做 round-trip validation。
- PackEditor Header 下方已有橫向五步驟流程：基本資料 → 票面素材 → 遊戲區域 → 獎金池 → 驗證與輸出。
- 主工作區已改成 **左側輸入、右側即時預覽**。
- 已移除沒有用途的「開啟／儲存既有 Pack」UI。
- ScratchGame 的「選擇玩家 / 設定選單 / 彩券小舖」三個主要 Dialog 已於 V0.5.1 重新整理標題、位置與操作按鈕。

PackEditor 的固定方向：

- **只建立新的 Pack，不提供開啟、修改、覆寫或另存既有 `.scratchpack`。**
- 每次新增 Pack 都建立新的 UUID v4 packageId；不提供手動重用 packageId 的一般流程。
- 不建立 PackEditor 專用 project file / `.scratchproject`。
- Built-in Pack 沒有特殊模式；PackEditor 永遠輸出一般 `.scratchpack`，正式 Built-in Pack 只是在發行時原樣放入 `BuiltInPacks/`。
- PackEditor 與 Importer / runtime 共用同一份 ScratchPack model / loader / validator，不建立第二套規則。

#### V0.5.1 目前最高優先 blocker：PackEditor ICON source 失真

- 目前 `apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png` 雖可正常解碼、尺寸為 256×256，且 CI 能成功產生多尺寸 ICO / 嵌入 EXE，但**圖片像素內容本身已錯誤**。
- 使用者實機看到：ICON 上半部還有彩券 / 工具圖樣，下半部整片是咖啡色實心色塊。
- 這不是 ICO 尺寸、ApplicationIcon、Windows cache 或小尺寸 layer 問題；是 source binary / image upload integrity 問題。
- Run #164 曾因 binary 壞到無法解碼而 FAIL；Run #165 改為可解碼後 PASS，但實機證明 source 畫面內容仍錯。
- 下次必須先找回 / 重新產生正確 source，人工確認完整畫面，記錄 SHA-256，再用 binary-safe Git upload，上傳後回讀並比對 SHA-256，最後才允許 ICO generation / CI。
- 後續要把 **ICON source integrity + binary-safe upload + repo 回讀 SHA 比對 + Windows embedding** 整理成 repository 共通規則，不能只做 ScratchGame 特例。

V0.5.1 後續待做（依優先順序）：

1. **修正 PackEditor ICON source / 上傳流程，完成實機 ICON 驗收。**
2. 完成 V0.5.1 主程式三個 Dialog 與 PackEditor 新版 UI 的實機驗收。
3. 完善 GameType 1 預覽：正式銀膜 clipping、動態符號／示意結果、面額／票號 renderer 對齊。
4. 中央預覽支援直接拖曳整組 Scratch Grid、Price Area、Serial Area；左側精確數字同步更新。
5. 驗證頁列出 manifest / canvas / resource / GameType geometry / Prize Pool 等分項結果，錯誤可導向對應編輯區。
6. PackEditor 與 ScratchGame 共用 portable 資源；更新 `tools/package_portable.py`，正式把 `PackEditor.exe` 納入 portable 驗證與封裝。目前 CI 已可 publish / smoke-test 兩個 EXE。
7. 建立 PackEditor → `.scratchpack` → ScratchGame Importer 的自動 round-trip regression。

### V0.6.x ～ V0.9.x — 完整化階段

不預先把每一個 Minor 版號綁死。每完成一個足夠完整、使用者可明顯感受到的功能階段，再依共通版本規則決定是否升 Minor。

主要工作：

- 逐步完成 `GAMETYPE_SPEC.md` 已定義的 GameType 與對應 Renderer / Generator / validation / PackEditor UI。
- 完善刮獎手感、銀膜視覺、碎屑／刮痕、硬幣、音效與中獎效果。
- 建立 Decoration Shop / 收藏／裝備系統。
- 完善 UI、效能、穩定性與 regression tests。
- 每次涉及 runtime database schema 的變更，都由開發端負責 migration、migration backup 與既有資料驗證。

### V1.0.0 — 正式穩定版 Gate

至少完成：

- ScratchPack V1 runtime 穩定。
- PackEditor 可完整建立所有當時正式支援的 GameType Pack。
- 預定 GameType、Decoration Shop 與刮獎體驗完成。
- 新安裝流程驗證通過。
- runtime database migration / backup / 舊資料驗證通過。
- PackEditor → Pack → Import / Built-in → 發行 → 挑票 → 刮獎 → 兌獎完整 round-trip regression 通過。
- 功能 Freeze 後完成一輪只修 bug 的正式驗收，再由使用者決定發布 V1.0.0。

0.x 階段不建立 Pack 更新／替換框架。V1.0.0 發布後若確有需求，再另外討論長期 Pack 更新、跨大版本相容與線上配送。

## Pack lifecycle / packageId

- Built-in Pack 與 Imported Pack 的 `.scratchpack` 格式相同。
- `packageId` 是 Pack 永久唯一身分；Importer 已拒絕相同 packageId 重複安裝。
- Built-in 若 packageId 不變但內容 hash 改變，Importer 也會拒絕，避免偷偷改寫已發布 Pack。
- 需要不同內容時建立新 Pack / 新 packageId；PackEditor 不提供修改舊 Pack。
- Imported Pack 可隱藏／取消隱藏／解除安裝；有 Pending Ticket 時解除安裝必須拒絕。
- 解除安裝 Pack 不回滾使用者累積統計。

## 挑券 thumbnail cache

- ScratchPack 不保存 thumbnail；票面 / Pack definition 才是 Source of Truth。
- 主程式在 Pack 安裝成功後產生挑券 PNG cache，基準尺寸 360×294。
- 可包含固定票面、未刮銀膜與 `priceDisplay=1` 的程式面額示意。
- 不包含實際票號、某張票 outcome 或 Pending Ticket。
- cache 可刪除並重建，不影響 Pack、票池、批次或使用者資料。
- PackEditor 編輯預覽直接依 draft 即時疊圖，不使用 runtime thumbnail cache。

## Wallet 與遊玩統計

本機版保存：

- `wallet_balance`
- `completed_ticket_count`
- `win_count`
- `total_spent`
- `total_redeemed`
- `max_prize`
- `grant_count`
- `grant_total_amount`

Derived Data 不另存：

- 勝率 = `win_count / completed_ticket_count`
- 總損益 = `total_redeemed - total_spent`

資金規則：

- 新使用者初始 Wallet `$100,000`。
- 購票立即扣 Wallet 並累加 `total_spent`；餘額不足不得建立 Pending Ticket。
- 兌獎把獎金加入 Wallet / `total_redeemed`，更新完成張數、中獎張數與最大獎。
- 尚未開始刮獎時換一張不重複扣款／增加投入。
- Wallet grant 不算中獎或遊玩損益。
- 遊玩統計不提供任意重置單一使用者統計的功能。

## 乾爹乾媽 Wallet Grant

目前 UI 文案已使用 **「乾爹乾媽給我錢」**；底層仍維持 generic wallet grant 模型。

後續方向：

- 每次觸發從已擁有／已啟用角色池隨機抽一位。
- 每個角色可有自己的圖片、對話框與可選專屬音效；沒有專屬音效時使用 fallback。
- 角色／圖片／音效都只屬 cosmetic presentation，不改 grant 金額或遊戲結果。
- 可納入 Decoration Shop 收藏／裝備。

## Decoration Shop / 使用者體驗商店

Decoration Shop 只管理 cosmetic / 使用者體驗資源，例如：

- Frame Theme（Header + Footer 成套）。
- Stage Theme。
- Player Card Skin：Footer 玩家資訊卡外觀；資料仍由同一 Player Card component 提供。
- 硬幣。
- 刮痕／銀膜碎屑／刮刮視覺效果。
- 一般獎／大獎／頭獎等慶祝效果。
- 中獎／大獎／未中獎結果音效組。
- 乾爹乾媽 wallet grant 角色／圖片／音效。

Decoration 不得改變 ScratchPack、票池、中獎率、Prize Tier、獎金或遊玩損益。

## 音效

- 結果音效目前至少分小獎、大獎、未中獎；Wallet grant 有獨立音效。
- 未中獎 `lose.wav`、wallet grant、結果音效缺檔或播放錯誤應留下 runtime log。
- 後續把中獎／大獎／未中獎做成 sound slots；預設音效永遠保留 fallback。
- 未來若增加頭獎或其他級別，延伸同一 sound-slot 模型，不建立平行播放流程。

## 詳細玩家歷史 / 線上發行追蹤

目前單機版**不保存逐張彩券玩家歷史**，不記錄逐張 packageId、批次、票號與獎金明細，也不做 Pack history snapshot。

只有未來若發展成連線版、主控端需要發行／批次／稽核／同步／客訴追查時，再重新設計 server-side / online history。

## Pack Marketplace — 最長期計畫

單機版目前不需要彩券商店。只有未來若進入 Steam、Workshop、線上內容配送等情境，再研究 Pack 下載、版本配送、線上索引與安全驗證。此項不阻擋單機版 V1.0.0。

## 測試與 Conformance

- 每個正式 GameType 保留最小 reference / test Pack，驗證合法 Pack、非法 geometry、Prize mapping、Renderer 與 outcome generation。
- Test Pack 與正式 Pack 使用同一 ScratchPack schema / GameType pipeline，不建立測試專屬 loader。
- 正式使用者資料維持 `%LOCALAPPDATA%\ScratchGame`；替換 portable EXE 不刪除資料。
- database schema 變更先做 migration backup。
- 0.x 測試資料不要求保留舊的逐張歷史；必要時使用者可自行清除 `%LOCALAPPDATA%\ScratchGame` 重新測試。
