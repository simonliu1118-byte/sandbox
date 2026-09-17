# ScratchGame V0.5.5 Build 5

Release date: **2026/09/18**

Tag: `ScratchGame-v0.5.5-build5`

Formal Windows package:

```text
ScratchGame-V0.5.5-Build5-win-x64.zip
SHA-256: eaef6282150652ab6a726919ba5d89105c8c626559b9c3c549e23b42ef650459
Size: 142755863 bytes
```

## Release status

- User real-machine acceptance: **PASS**
- ScratchGame Build Run #200: **PASS**
- ScratchGame build: PASS
- PackEditor build: PASS
- Regression runner build: PASS
- GameType 1 / 2 domain regression: PASS
- Canonical ThreeStar-Test build: PASS
- Windows icon verification: PASS
- ScratchGame / PackEditor startup smoke: PASS
- Complete Portable build + post-package verification: PASS

## Included baseline

### ScratchGame runtime

- Finite prize pool and batch model.
- Per-player Wallet and cumulative statistics.
- One Pending Ticket per player.
- Purchase, swap-before-scratch, scratch, redeem and settlement flow.
- Built-in / Imported ScratchPack installation paths.
- Imported Pack hide / unhide / uninstall protection.
- SQLite local data and backup flow.

### GameType

- GameType 1: complete.
- GameType 2「中獎號碼」: complete.
- GameType 3～6: specifications exist but implementation is not part of this release.

### ScratchPack / PackEditor

- ScratchPack V1 runtime loader / validator.
- `manifest.json + ticket.json` canonical V1 package model.
- PackEditor create-only workflow.
- Export round-trip through the authoritative ScratchGame loader.
- ScratchGame and PackEditor share one VERSION / BUILD line.

### V0.5.5 UI acceptance line

- Ticket shop card layout cleanup.
- Initial player dialog layout correction.
- Result resume transition alignment.
- Settings ticket list converted from card/accordion behavior to a compact data-row layout.
- Unified settings action column: 資訊 / 發行下一批 / 隱藏.
- Centered ticket-information modal with full prize-pool display.
- Imported Pack uninstall moved to a small red action separated from the close button.
- Settings header / divider / rounded-container alignment fixes.
- Player card Wallet label readability improvement.
- Simple player deletion action with confirmation and safety guards.

Player deletion guards in this release:

- the current active player cannot be deleted directly;
- at least one player must remain;
- a player with a Pending Ticket cannot be deleted;
- deletion permanently removes that player's Wallet and cumulative statistics.

## Portable contents

The formal package contains both executables in the same portable root and shares external runtime assets:

```text
ScratchGame/ScratchGame.exe
ScratchGame/PackEditor.exe
ScratchGame/Themes/
ScratchGame/BuiltInAssets/
ScratchGame/UI/
ScratchGame/Audio/
ScratchGame/TestPacks/ThreeStar-Test.scratchpack
ScratchGame/PACKAGE_CONTENTS.txt
```

`TestPacks/ThreeStar-Test.scratchpack` remains intentionally included. Project policy requires TestPacks to stay in the portable package until V1.0.0 formal acceptance; removal is deferred to the V1.0.0 release gate and requires an explicit user decision.

## Next development item

The next independent development item after this release is **GameType 3**, following `GAMETYPE_SPEC.md`.

PackEditor final UI / preview / validation UX cleanup remains deferred until the basic GameType set is complete.
