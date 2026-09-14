# ScratchPack V1 設計交接索引

更新日期：2026-09-14

此檔只作為換對話／換接手者時的快速入口；完整內容以同分支的 `SCRATCHPACK_V1_PLAN.md` 為準。

## 1. 權威設計文件

- Branch：`docs/scratchpack-v1-plan`
- File：`apps/ScratchGame/SCRATCHPACK_V1_PLAN.md`
- 完整設計基準 commit：`33975fc85e998eb728cf99b1550f50e8230b7141`

該文件已完整記錄：

- ScratchPack V1 封裝定位與安全邊界。
- `manifest.json` / `ticket.json` 責任分工。
- `canvas=1 = 1080×882` 固定語意。
- `priceDisplay`、票號、Scratch Zone、銀膜、Prize Pool。
- `thumbnail.png` 完全取消。
- 通用 `contentBox` 取消。
- Maker 改為 GameType-aware 精靈，而不是通用空白刮區編輯器。
- Maker 自動統計 EV / RTP / 中獎率 / 未中獎率等 Derived Data。
- Maker / Importer 必要驗證與原子匯入要求。
- V1.0.0 六種基礎 GameType 的玩法、Renderer、盤面生成與 Prize Pool 規則。

## 2. V1.0.0 六種基礎玩法

1. `1` 星星連線
2. `2` 中獎號碼
3. `3` 三個相同
4. `4` 符號計數
5. `5` 賓果
6. `6` 比大小

原本討論過的「直接刮格子、每條線各自累加」不另占基本 GameType；未來若需要，優先作為 Type 1 變體。

## 3. 最近最後定案，後續不可遺失

### Type 5｜賓果

- 正式名稱就是「賓果」。
- 內容可由開發者選 `symbol` 或 `number`，不拆玩法。
- 棋盤與「你的符號／號碼」**兩邊都能刮**。
- 玩家可先刮棋盤、先刮自己的項目或交錯刮；順序不影響結果。
- 基本版只計完整橫線、直線、兩條大斜線。
- 每條線有固定獎金，多線累加。
- Maker 自動推導所有真正可生成的最終總獎金，開發者只填各金額張數。
- 3×3 / 4×4 / 5×5 都要支援；5×5 不以 `2^25` 暴力窮舉，改枚舉最多 12 條線的 line-set / bitmask。
- FREE 格、四角、X、十字、外框等不塞進基本 Type 5；未來用變體。

### Type 6｜比大小

- 一張票有多個相同模板比較區。
- 比較方向全票共用，只允許 `greater` / `less`；相等未中獎。
- 左右標題可自訂文字，例如「幸運數字／你的數字」「莊家／玩家」。
- 相對位置只允許左右或上下。
- 開發者可調模板大小，但所有區塊的兩側數字區大小必須一致。
- 獎金位置四選一：跟第一側、跟第二側、整區上方、整區下方。
- `useCustomBlockPrizes = false` 為預設：引擎從可用區塊金額組合出本張 Prize Tier。
- `useCustomBlockPrizes = true`：開發者指定每區固定獎金，Maker 自動推導所有可生成總獎金。

## 4. 目前不做／未實作

- ScratchPack V1 尚未開始正式功能實作。
- Maker 專案檔（例如 `.scratchproj`）目前只保留概念，**不要列入 TODO 或正式計畫**。
- V1 ScratchPack 不自訂中獎音效／動畫。
- 正式 `SCRATCHPACK_SPEC.md` 仍是目前已實作舊格式；在新 V1 真正完成前，不可把本計畫當成目前 App 已支援 schema。

## 5. 與目前 Build 4 工作的關係

Build 3 使用者實測後的 Build 4 correction 保存在開發分支：

- Branch：`scratchgame/build3-ui-rebuild`
- File：`apps/ScratchGame/WORK_HANDOFF.md`
- 最新 handoff commit：`ec0655d15eccd84f9f2d1817f077297264c883c0`
- Build 4 source / icon commit：`7d8d98f84c80b6aa18b91a47f7a6ab251424d2dc`
- Windows CI Run #43：success，但尚未完成使用者驗收。

目前仍有 `stage_bg.png`、ThreeStar 最終 `ticket.png` 與若干 UI 實測項目待完成；**Build 4 驗收前不要開始 ScratchPack V1 實作**。
