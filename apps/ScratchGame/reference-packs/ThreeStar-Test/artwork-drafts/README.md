# ThreeStar-Test artwork handoff

This folder archives the visual concepts produced while defining the first ScratchPack V1 reference ticket. These are **design references, not the authoritative built-in asset store**.

## Current decisions

- Dynamic data must not be baked into ticket artwork:
  - price is rendered by the app when `priceDisplay=1`;
  - serial is rendered by the app in `serialDisplayArea`;
  - scratch foil is a runtime layer;
  - dynamic game content remains renderer-owned.
- ScratchPack resource schema and Built-in ref registry are owned only by `SCRATCHPACK_SPEC.md`.
- Production runtime masters are stored in the repository under `RuntimeAssets/Live/`; this handoff folder only preserves historical visual references.

## GameType 1 built-in ticket masters

Canonical repository location:

```text
apps/ScratchGame/RuntimeAssets/Live/BuiltInAssets/Tickets/gameType1/
```

Current masters:

```text
01-red.png
01-blue.png
02.png
```

The authoritative public refs are defined only by `SCRATCHPACK_SPEC.md`. For ScratchPack resolution, `gameType="1"` selects the GameType 1 ticket namespace; the Pack therefore uses short refs such as `01-blue`, without repeating `gameType1` in the ref.

ThreeStar-Test currently selects:

```json
"art": {
  "ticket": {
    "source": "builtin",
    "ref": "01-blue"
  }
}
```

## Built-in foil masters

Canonical repository location:

```text
apps/ScratchGame/RuntimeAssets/Live/BuiltInAssets/Foils/
```

ThreeStar-Test currently selects the global built-in foil:

```json
"foil": {
  "source": "builtin",
  "ref": "brushed-silver-three-star"
}
```

No separate full-canvas mask / foil pipeline is currently part of the accepted V1 reference implementation. Any future foil presentation change must continue to respect `scratch.foil` + `scratch.zones` as the single ownership path unless the formal spec is explicitly revised.

## Historical artwork references

The source generations created in chat were 1388×1133 and are preserved only as design references. Git handoff copies include:

- `01-blue-original-concept.webp`
- `02-red-classic-concept.webp`
- `03-red-clean-base-draft.webp`
- `04-blue-clean-base-draft.webp`
- `05-silver-foil-style-a.webp`

Do not treat their old dimensions or geometry as production authority.

## Accepted ThreeStar-Test geometry

Canvas 1 is **1080×882**. Accepted geometry now recorded in `ticket.json`:

```text
zone size: 191 x 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

## Current ThreeStar-Test definition

- name: `三星連線（測試）`
- price: `500`
- `canvas=1`
- `gameType="1"`
- built-in ticket ref: `01-blue`
- built-in foil ref: `brushed-silver-three-star`
- `gridSize=3`
- `issueSize=8`
- `ticketsPerBook=8`
- `allowNearMiss=true`
- `priceDisplay=1`

Test pool:

```text
1 line  -> 100
2 lines -> 500
3 lines -> 1000
4 lines -> 2500
5 lines -> 5000
6 lines -> 10000
8 lines -> 100000
0 lines -> derived losing ticket
```

Because both ticket and foil are Built-in resources, the packaged ThreeStar-Test fixture does **not** need to duplicate those PNG files inside the `.scratchpack`; `manifest.json` + `ticket.json` are sufficient under the current V1 spec.

## GameType 1 decisions already finalized

- `gridSize=N` means a standard `N x N` grid.
- Scratch Zone count must be exactly `N^2`.
- Type 1 uses `scratch.zones` directly as the board and does not accept `cellZones`.
- Zone order is row-major.
- Equal dimensions/shape, aligned rows/columns, equal spacing and no overlap are GameType 1-specific validation rules.
- Prize Tier remains generic `{ amount, count }`.
- For `gridSize=N`, legal positive line counts are `1..2N` plus `2N+2`; `2N+1` is unreachable.

## Pack lifecycle

- ThreeStar-Test and the formal Built-in Base Pack are separate ScratchPacks with separate `packageId` values and separate finite pools.
- Built-in / Imported is runtime installation-source state, not ScratchPack schema.
- Do not mix artwork work with Loader / CI / runtime changes unless the artwork/Pack fixture step has been accepted.
