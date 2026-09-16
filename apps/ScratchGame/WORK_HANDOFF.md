# ScratchGame 工作交接

更新日期：2026/09/16

## 目前基準

- Repository：`simonliu1118-byte/sandbox`
- 主開發分支：`scratchgame/feature-scratchpack-v1-runtime`
- PR：#7
- 目前版本：`V0.5.1`
- BUILD：`0`
- ScratchGame 與 PackEditor 共用同一條版本線；PackEditor 是 ScratchGame 附屬程式，不是獨立產品。
- V0.5.1 主要功能 / UI commit：`b552cd13ddb5eab49e85c3b6501f7e9a8ae08edc`
- 最近 Windows CI：Run #165（run id `35056946537`）PASS。
- Run #165 已通過 ScratchGame / PackEditor build、publish、多尺寸 ICO 結構、Shell icon 解析與兩個 EXE startup smoke test。
- **但 V0.5.1 PackEditor ICON 仍有已知視覺錯誤，詳見下方「目前最高優先 blocker」。**

## 權威文件

- 永久專案規則：`apps/ScratchGame/PROJECT_RULES.md`
- ScratchPack V1 schema / ResourceRef / Built-in registry：`apps/ScratchGame/SCRATCHPACK_SPEC.md`
- GameType 契約：`apps/ScratchGame/GAMETYPE_SPEC.md`
- Roadmap / 未來工作：`apps/ScratchGame/TODO.md`
- ScratchPack 狀態索引：`apps/ScratchGame/SCRATCHPACK_V1_HANDOFF.md`

不要依舊版 V0.3 / V0.4 handoff 直接修改。新對話應先重新讀最新 branch source、VERSION / BUILD、PROJECT_RULES、SCRATCHPACK_SPEC、GAMETYPE_SPEC、TODO，再接續。

---

# V0.5.1 已完成內容

## ScratchGame 主程式 UI

V0.5.1 這輪做的是較大的 UI 重整，不是 V0.5.0 Build 修補，因此版本升為 `0.5.1 / BUILD 0`。

已完成：

### 選擇玩家

- 標題由「使用者」改為 **「選擇玩家」**。
- 標題水平置中。
- 移除「切換玩家、修改名稱；詳細統計請開啟遊玩紀錄。」副標。
- 玩家卡片 padding / border / ListBox spacing 重整，目標是修正卡片下緣被切到。
- 「新增使用者」改為 **「新增玩家」**，放到下方正式操作區。
- 「切換使用者」改為 **「切換玩家」**。

### 設定

- 標題由「設定」改為 **「設定選單」**。
- 標題水平置中。
- 移除「彩券、批次與資料管理」副標。

### 彩券挑選

- 標題由「挑選彩券」改為 **「彩券小舖」**。
- 標題水平置中。
- 移除「選一張喜歡的彩券，開始今天的手氣。」副標。
- 「開始刮獎」移到底部正中央，維持主操作按鈕層級。
- 「取消」改為 **「關閉」**，放右側。

以上只調整 presentation / layout，不改 ScratchPack、Wallet、批次、票池、redemption 等遊戲邏輯。

---

# V0.5.1 PackEditor 現況

程式位置：

```text
apps/ScratchGame/src/PackEditor/
```

執行檔：`PackEditor.exe`。

PackEditor 與 `ScratchGame.exe` 共用：

- `apps/ScratchGame/VERSION`
- `apps/ScratchGame/BUILD`
- ScratchPack model / loader / validator authority
- 正式 portable 方向下的 `BuiltInAssets/`

## V0.5.1 UI 重整已完成

- Header 簡化，只保留 `PackEditor`、`新增 Pack`、`輸出 .scratchpack`。
- **不提供開啟 / 儲存既有 Pack UI**；create-only 決策維持不變。
- 原左側垂直「編輯項目」改成 Header 下方橫向流程列：

```text
① 基本資料 › ② 票面素材 › ③ 遊戲區域 › ④ 獎金池 › ⑤ 驗證與輸出
```

- 主工作區改為：**左側輸入欄位、右側即時預覽**。
- 移除左上「編輯項目」與左下「尚未建立 Pack」等無必要小字。
- `GameType` UI 改成 **「遊戲類型」**。
- `GameType 1` 使用者顯示名稱改成 **「星星連線」**；內部 Tag / schema 仍維持 `1`。
- `Canvas` UI 改成 **「彩券尺寸」**。
- 使用者只看到 **`1080 × 882 px`**，不顯示 `Canvas 1`。
- Package ID 不再是一般編輯欄位；仍由 PackEditor 新增 Pack 時自動產生 UUID v4，完整驗證 / 輸出結果才顯示。
- 最低 ScratchGame 版本不再是一般輸入欄位；相容需求由程式 / 正式規格決定。
- shape 等部分工程術語開始改為一般使用者可理解的中文顯示名稱；內部 schema 值不改。
- 預覽下方 legend 改用「刮獎區 / 面額 / 票號」，不直接暴露內部變數名。

## PackEditor create-only 決策（已定案）

- **不提供開啟既有 `.scratchpack`。**
- **不提供修改、覆寫或另存既有 Pack。**
- 每次新增都是新 Pack，產生新的 UUID v4 `packageId`。
- 相同 packageId 不作為版本更新入口。
- Built-in 與 Imported Pack 格式相同；PackEditor 永遠輸出一般 `.scratchpack`。
- 0.x 階段不建立 Pack update / replace framework。

---

# 目前最高優先 blocker：PackEditor ICON source 已失真

## 使用者實機看到的現象

V0.5.1 測試包內的 `PackEditor.exe` Windows ICON 顯示錯誤：

- 上半部還看得到「彩券＋工具槌」的一部分。
- **下半部整片變成咖啡色實心色塊。**
- 這不是單純縮圖不好看，而是 ICON 原始圖片內容已經錯了。

## 已確認：不是尺寸問題

目前 repo 中：

```text
apps/ScratchGame/src/PackEditor/Assets/PackEditor-icon-source.png
```

已經是「可以被 PNG decoder 讀取」的 256×256 檔案，因此 Run #165 的：

- 256×256 master 檢查
- ICO 產生
- 16/20/24/28/32/40/48/64/72/80/96/128/256 entries
- EXE shell icon extraction

全部都能 PASS。

**但 PASS 只代表這是一張技術上有效的 PNG / ICO；不代表圖像像素內容正確。**

目前問題是：

> `PackEditor-icon-source.png` 本身的像素內容已經失真，下半部就是錯誤咖啡色色塊；GenerateWindowsIcon.ps1 只是把這張錯誤 source 正常縮放 / 包成 ICO，因此 EXE 最後也一定是錯的。

這不是：

- ICO 尺寸缺失
- WPF `ApplicationIcon` 沒設定
- Windows icon cache
- 只缺某一個 16 / 32 px layer

而是 **source binary / image upload integrity 問題**。

## Run #164 / #165 的歷史

- Run #164：第一次加入 PackEditor ICON 時，GitHub 上的 PNG binary 已壞到 `System.Drawing.Image.FromFile()` 無法解碼，因此 CI 在 icon master check 直接 FAIL。
- 後來改用 Git blob base64 方式重新寫 binary，Run #165 可以解碼並 PASS。
- **但使用者實機驗收證明：重新寫入的 source 雖然「可解碼」，圖像內容仍不是原本想要的完整 ICON；下半部是咖啡色色塊。**

因此下一個對話不要再把「CI 能讀 PNG」誤判為 ICON 已修好。

---

# ICON 後續正確處理方式（待整理進 Repository 共通規則）

這是使用者要求後續納入所有專案共通規則的重點。

## A. 正確 source 必須先在 Git 外確認

1. 先取得真正想用的完整 icon source PNG。
2. 在本地 / 工作容器直接開圖確認：
   - 圖片完整。
   - 透明區正確。
   - 沒有半張圖、色塊、解碼異常。
3. 記錄原始檔：
   - byte size
   - SHA-256
   - width / height
4. **這個「已人工看過」的 binary 才是 intended source。**

## B. PNG / ICO 是 binary，禁止用文字檔 API 當一般文字更新

建議只允許 binary-safe 路徑：

- 正常 `git add / commit / push`。
- 或 Git Data API `create_blob`，`encoding=base64`。
- 不要用 UTF-8 `update_file` / 文字 replacement 去碰 PNG / ICO bytes。

## C. 上傳後必須「回讀驗證」，不能只相信 API 成功

上傳 binary 後，在進入 build 前必須：

1. 從 repo **重新 fetch / checkout** 該 binary。
2. 比對：
   - byte size
   - SHA-256
   - PNG dimensions
3. Repo 回讀 SHA-256 必須與 intended local source 完全一致。
4. 最好再把 repo 回讀 PNG 實際 render / screenshot 一次確認。

**若 SHA 不一致，不得進行 ICO generation / Windows CI。**

## D. ICO generation 是下一層，不是 source 驗證工具

只有 source binary identity 驗證通過後，才跑：

```text
GenerateWindowsIcon.ps1
```

再驗證：

- multi-size ICO entries
- EXE `ApplicationIcon`
- `ExtractAssociatedIcon`

這些只能證明「正確地包進去了」，不能證明原始圖畫對了。

## E. 共通規則建議新增兩層 Gate

### Gate 1 — Source Integrity

- PNG 可解碼。
- 尺寸正確。
- **Repo binary SHA-256 == intended source SHA-256。**
- 必要時人工看 repo 回讀的 PNG。

### Gate 2 — Windows Embedding

- 多尺寸 ICO 正確產生。
- EXE 有 associated icon。
- Shell / 工作列 / 檔案總管實機驗收。

這次的教訓就是：Run #165 只有 Gate 2 類型驗證，缺少「intended source binary identity」這個 Gate 1。

---

# 下一個對話的建議處理順序

## Priority 1 — 先修 PackEditor ICON，不要先繼續 PackEditor 功能

1. 找回 / 重新產生一張**完整且人工目視正確**的 PackEditor icon source。
2. 不要沿用目前 repo 裡已失真的 `PackEditor-icon-source.png`。
3. 記錄 intended source SHA-256。
4. 使用 binary-safe base64 Git blob / 正常 git 上傳。
5. 從 GitHub 回讀並比對 SHA-256。
6. 確認 repo 回讀圖片完整，特別檢查下半部不可再是咖啡色色塊。
7. 再讓 CI 產生 ICO / EXE。
8. 使用者實機驗收檔案總管 / 工作列 / 視窗 ICON。
9. 驗收後把「ICON source integrity + binary upload」正式整理進 repository 共通規則，而不是只寫 ScratchGame 特例。

## Priority 2 — V0.5.1 UI 實機驗收

檢查：

- 選擇玩家：標題、玩家卡片下緣、新增玩家位置、切換玩家。
- 設定選單：置中標題與去副標。
- 彩券小舖：置中標題、開始刮獎正中央、關閉靠右。
- PackEditor：橫向五步驟、左輸入右預覽、工程術語移除。

若只是 V0.5.1 同一驗收項目的小修，依 repository version rules 決定 Build；不要自行亂升版。

## Priority 3 — PackEditor 後續功能

待 V0.5.1 UI / ICON 穩定後再做：

1. 中央預覽直接拖曳整組 Scratch Grid。
2. Price Area / Serial Area 拖曳 / resize，與數字雙向同步。
3. 正式銀膜 clipping 預覽。
4. 動態符號 / 示意結果 / 面額 / 票號 renderer 預覽。
5. 驗證頁分項結果與錯誤導向。
6. 更新 `tools/package_portable.py`，正式把 `PackEditor.exe` 納入 portable 驗證 / 封裝。
7. PackEditor → `.scratchpack` → ScratchGame Importer round-trip regression。

---

# 目前 ScratchGame / PackEditor 已定的重要規則

- ScratchPack V1 schema 仍是 `formatVersion 1.0`。
- Canvas 1 固定 1080×882。
- PackEditor UI 不讓一般使用者看到 `GameType 1 / Canvas 1 / packageId / minimumAppVersion` 這類工程代號；內部 schema 不改。
- UI 顯示名稱：GameType 1 = **星星連線**。
- Prize Pool UI 使用台灣用詞 **獎金回饋率**，並顯示平均獎金（期望值）。
- PackEditor create-only；不修改既有 Pack。
- packageId 由新增 Pack 時自動產生 UUID v4。
- Built-in Pack / Imported Pack 使用同一 `.scratchpack` schema。
- PackEditor 不建立第二套 validator / GameType rules / VERSION / BUILD / CI workflow。
- Windows CI 應集中修改後再跑，不能當逐項 debugger。

---

# 目前測試包

已產出：

```text
ScratchGame-V0.5.1-PackEditor-test-package.zip
```

其中 ScratchGame / PackEditor EXE 來自 Run #165。

**注意：這個測試包的 PackEditor.exe ICON 已知錯誤，不可視為 ICON 驗收通過版本。**

除了 ICON blocker 外，Run #165 對編譯 / publish / startup 的結果為 PASS。
