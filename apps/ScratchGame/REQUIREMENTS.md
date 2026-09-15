# ScratchGame V0.1 Requirements — Historical Baseline

> 本文件只保留最初 V0.1 的歷史需求基線，**不是目前永久規則來源，也不代表 V0.5.0 現況**。目前產品規則以 `PROJECT_RULES.md` 為準；ScratchPack schema 以 `SCRATCHPACK_SPEC.md` 為準；GameType 契約以 `GAMETYPE_SPEC.md` 為準；目前 Roadmap 以 `TODO.md` 為準。
>
> 例如 V0.1 當時的「無 Wallet 限制、保存逐張歷史、Reservation」等內容已被後續正式設計取代，不得再依本文件恢復舊行為。

## Scope

V0.1 建立可擴充的 Windows 刮刮樂核心，不追求一次完成所有美術與玩法。

## V0.1 當時必做

### UI
- Windows WPF 主視窗，視窗標題「刮刮樂」。
- 主畫面中央為最大化票券區。
- 票券附近顯示目前彩券名稱＋面額。
- 「新的一張」、「全部刮開」、「設定／使用者」等必要操作。
- 新票選擇器依面額篩選彩券，顯示彩券名稱、面額、發行時中獎率。

### 使用者
- 新增、切換、重新命名使用者。
- 每個使用者獨立累計投入、兌獎、損益與歷史。
- V0.1 當時沒有資金上限／餘額不足限制。
- 每個使用者最多一張 Pending Ticket。

### 彩券與批次
- 建立／匯入彩券定義。
- 彩券一旦建立第一批即鎖定核心定義。
- 每張彩券同時間一個 Active 批次。
- 手動建立下一批；新批次重置完全相同獎池。
- 舊批次不可再抽。
- 已發行彩券可停用，不可真正刪除。

### Prize Pool
- 固定總發行量與各獎項張數。
- 公布中獎率由獎項表自動計算。
- 按剩餘 Available 張數抽取結果。
- V0.1 曾規劃 Reservation 防止唯一獎項被重複 Pending；此設計已被後續正式 finite-pool transaction 規則取代。
- 正常兌獎、換票與 crash recovery 目前應依最新 `PROJECT_RULES.md`，不是依本歷史文件。

### Scratch Engine
- 底層票券內容＋上層可刮遮罩。
- 滑鼠連續拖曳刮除，不可因快速移動產生明顯斷點。
- 必要刮獎區達約 78% 自動完成。
- 所有必要區完成即自動兌獎。
- 「全部刮開」立即揭曉並自動兌獎。

### 內建玩法
V0.1 最初規劃：
1. LuckyNumberMatch。
2. ThreeLine。
3. MatchThree。

目前正式 GameType 身分與規則只看 `GAMETYPE_SPEC.md`。

### ScratchPack
- 支援 `.scratchpack` 匯入。
- 嚴格驗證格式、安全性、資源路徑與獎項總數。
- 現行正式 V1 schema 只看 `SCRATCHPACK_SPEC.md`。

### Data
- SQLite。
- 每 3 天自動備份一次，最多 5 份。
- Schema migration 前強制備份。

## V0.1 當時列為後續的項目

以下只是歷史記錄；其中多項目前已實作或重新設計：

- 初始資金、補充資金、資金不足限制。
- 更多玩法。
- 更進階的刮屑、粒子、音效與中獎動畫。
- 彩券包製作器／視覺化版型編輯器（目前正式名稱為 PackEditor）。
- 雲端同步或多人連線。
