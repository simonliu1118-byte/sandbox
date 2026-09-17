# ScratchGame

Windows 單機刮刮樂娛樂程式。

- 顯示名稱：`刮刮樂`
- 技術：C# / .NET 8 / WPF
- 發行：Windows x64、self-contained、portable
- 版本來源：`VERSION` / `BUILD`
- 附屬工具：`PackEditor.exe`，與 ScratchGame 共用同一產品版本線

## 最新正式版本

**ScratchGame V0.5.5 Build 5** — 2026/09/18

- Tag：`ScratchGame-v0.5.5-build5`
- Tag target：`0926e2c69c500340d38f33f09f390e0e7ce24b63`
- Portable：`ScratchGame-V0.5.5-Build5-win-x64.zip`
- SHA-256：`eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459`
- 使用者實機驗收：PASS
- Windows CI：Run #200 PASS
- 正式 Release asset verification：Run #7 PASS
- 發布說明：`RELEASE_NOTES_V0.5.5_BUILD5.md`

Build 5 是目前正式基準；正式 GitHub Release 已發布，Portable ZIP 與 `SHA256SUMS.txt` 均完成 asset 驗證。下一個獨立開發項目依 `GAMETYPE_SPEC.md` 進入 GameType 3。

## 核心方向

ScratchGame 使用有限票池、批次、Pending Ticket、Wallet 與 ScratchPack V1。主畫面以彩券為核心；Header 負責程式功能，Footer 顯示 Player Card、遊戲操作與唯一 StatusText。

目前支援方向：

- 多使用者；每位使用者獨立 Wallet 與累積統計。
- 新使用者初始 Wallet `$100,000`；購票扣 Wallet、兌獎回 Wallet。
- 本機版不保存逐張玩家遊玩歷史。
- 多面額、多 ScratchPack、多批次與固定有限獎項池。
- 每張彩券同時間只有一個 Active 批次。
- 每位使用者最多一張 Pending Ticket。
- Built-in / Imported ScratchPack 共用相同 schema 與 runtime pipeline。
- Imported Pack 可隱藏／取消隱藏／解除安裝；有 Pending Ticket 時不得解除安裝。
- SQLite 本機資料與自動備份。
- 玩家可新增、改名與刪除；目前使用中、最後一位或仍有 Pending Ticket 的玩家不可刪除。

## PackEditor

`PackEditor.exe` 是 ScratchGame 的附屬 Pack 製作工具，source 位於：

```text
apps/ScratchGame/src/PackEditor/
```

PackEditor **只建立新的 `.scratchpack`**：

- 每次新增 Pack 自動產生新的 UUID v4 packageId。
- 不提供開啟、修改、覆寫或另存既有 Pack。
- 不建立 PackEditor 自己的 VERSION / BUILD / PROJECT_RULES。
- 輸出前使用 ScratchGame 正式 `ScratchPackV1Loader` 做 round-trip validation。

## 文件入口

- 永久專案規則：`PROJECT_RULES.md`
- ScratchPack V1 規格：`SCRATCHPACK_SPEC.md`
- GameType 規格：`GAMETYPE_SPEC.md`
- Roadmap / TODO：`TODO.md`
- 目前工作交接：`WORK_HANDOFF.md`
- ScratchPack 狀態索引：`SCRATCHPACK_V1_HANDOFF.md`
- Portable / Release 包裝：`RUNTIME_PACKAGE.md`
- V0.5.5 Build 5 發布說明：`RELEASE_NOTES_V0.5.5_BUILD5.md`
- `REQUIREMENTS.md` 是早期 V0.1 歷史需求基線，不是目前規則來源。