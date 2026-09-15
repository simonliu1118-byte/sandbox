# ThreeStar-Test artwork handoff

This folder archives the visual concepts produced while defining the first ScratchPack V1 reference ticket. These are **design references, not final Pack assets**.

## Current decisions

- Keep both main visual directions for future ScratchPack Maker built-in ticket styles:
  1. blue / rainbow / starry style;
  2. red / gold classic festive style closer to the original ThreeStar ticket.
- Dynamic data must not be baked into final base art:
  - price is rendered by the app when `priceDisplay=1`;
  - serial is rendered by the app in `serialDisplayArea`.
- Scratch foil must be an independent runtime layer, not painted permanently into the ticket base image.
- ScratchPack V1 foil schema is now owned only by `SCRATCHPACK_SPEC.md` through `scratch.foil`; this handoff does not maintain a second foil schema.
- Current ThreeStar-Test direction uses the public built-in foil code `brushed-silver-three-star`.
- Reusable foil masters are stored in ChatGPT Library at `/ScratchGame/Asset-Library/Foils/`; this handoff folder should not become the long-term asset library.
- Current reusable masters:
  - `foil-brushed-silver-plain.png`
  - `foil-brushed-silver-three-star.png`
- A future optional full-canvas foil-overlay mode is tracked in `TODO.md`; it must extend the same foil system rather than introduce a second mask / foil pipeline.
- Image generation does not reliably output exact arbitrary pixel dimensions. Final production workflow must therefore generate/edit the art first, then post-process and validate the exact Canvas dimensions.

## Size status

The source generations created in chat were all **1388 x 1133**, so none of those source generations is directly valid as a formal `canvas=1` asset.

ScratchPack V1 currently requires:

```text
canvas = 1 -> 1080 x 882
```

The reference copies in this folder are intentionally downscaled lightweight snapshots for Git handoff only. Do **not** treat their dimensions or old Scratch Zone positions as authoritative production geometry.

## Artwork files to archive

Expected reference files:

- `01-blue-original-concept.webp`
  - first blue/starry concept;
  - contains baked-in silver foil, price, and serial in the original source generation;
  - keep only as a visual reference.
- `02-red-classic-concept.webp`
  - red/gold concept closer to the older ThreeStar visual direction;
  - contains baked-in silver scratch panels in the original source generation;
  - keep as a Maker style reference.
- `03-red-clean-base-draft.webp`
  - red/starry base draft with foil removed and dynamic price/serial text removed;
  - still not final production geometry.
- `04-blue-clean-base-draft.webp`
  - blue/starry version of the cleaned base draft;
  - this was the accepted visual direction before exact-size post-processing;
  - source generation size confirmed as 1388 x 1133.
- `05-silver-foil-style-a.webp`
  - earlier separated brushed-silver 3x3 foil reference;
  - preserve only as a historical visual reference;
  - reusable master foils now live in `/ScratchGame/Asset-Library/Foils/` instead.

## Current ThreeStar-Test Pack definition

Files already committed next to this folder:

```text
manifest.json
ticket.json
```

Current test Pack intent:

- name: `三星連線（測試）`
- price: `500`
- `canvas=1`
- `gameType="1"`
- `gridSize=3`
- `issueSize=8`
- `ticketsPerBook=8`
- `allowNearMiss=true`
- `priceDisplay=1`
- foil: public built-in `brushed-silver-three-star`

Test pool covers every legal 3x3 positive result plus one losing ticket:

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

The blue clean base has been normalized and visually accepted at **1080 x 882**. The accepted geometry is:

```text
zone size: 191 x 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

`ticket.json` now uses this accepted geometry.

## GameType 1 decisions already finalized

- `gridSize=N` means a standard `N x N` grid.
- Scratch Zone count must be exactly `N^2`.
- Type 1 uses `scratch.zones` directly as the board; it does not use or accept `cellZones`.
- Zone order for Type 1 is row-major: left-to-right, then top-to-bottom.
- Type 1 validates equal cell dimensions/shape, aligned rows/columns, equal horizontal/vertical spacing, no overlap, and matching array order.
- These geometry restrictions are Type 1-specific; the common Scratch Zone format remains flexible for other GameTypes.
- Prize Tier stays generic (`amount`, `count`) and does not store `lineCount` / `lines-N`.
- For `gridSize=N`, legal positive line counts are `1..2N` plus `2N+2`; `2N+1` is unreachable.
- Tier mapping is built by sorting prize amounts ascending and mapping them to legal line counts ascending. Missing-but-legal outcomes stay present with `count=0`.
- This naturally extends to future 6x6 without changing the schema (`1..12, 14`).

## Pack lifecycle decisions

- There will be two ThreeStar Packs:
  1. an independent development/test Pack;
  2. the formal Built-in Base Pack: price 500, issue size 10000.
- They use separate `packageId` values and separate pools.
- Built-in Base Pack may be disabled/re-enabled, but cannot be deleted/uninstalled.
- `BuiltIn` / `Imported` is runtime install-source state, not a manifest/ticket field.

## Next work after handoff

1. Keep the accepted blue clean base as the formal ThreeStar-Test direction.
2. Preserve the accepted **1080 x 882** production candidate without baking dynamic price, serial, or foil into it.
3. Use the accepted geometry now recorded in `ticket.json`.
4. Use `brushed-silver-three-star` through the common `scratch.foil` mechanism.
5. Place the accepted base as final `assets/three-star.png` when binary asset commit/upload is performed.
6. Only after the artwork asset is present, continue with V1 loader/validator work.

During ticket redraw/image rounds, do not mix in source-code, CI, or runtime changes; finish and accept the visual asset first.
