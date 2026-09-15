# ThreeStar-Test artwork handoff

This folder archives the visual concepts produced while defining the first ScratchPack V1 reference ticket. These are **design references, not final Pack assets**.

## Current decisions

- Keep both main visual directions for future ScratchPack Maker built-in ticket styles:
  1. blue / rainbow / starry style;
  2. red / gold classic festive style closer to the original ThreeStar ticket.
- Dynamic data must not be baked into final base art:
  - price is rendered by the app when `priceDisplay=1`;
  - serial is rendered by the app in `serialDisplayArea`.
- Scratch foil must be an independent asset, not painted permanently into the ticket base image.
- The current V1 direction adopts the **reusable single-zone foil template** approach.
  - Do not treat the current 3x3 silver-grid reference as a final production asset.
  - Normalize foil as a reusable single-zone / template-style asset and let runtime place it per Scratch Zone.
  - The template should be scalable to different valid Scratch Zone sizes rather than requiring a new foil redraw for every ticket.
  - When implementing the reusable foil style, prefer an edge-preserving scaling approach (for example a 9-slice-style treatment) so rounded corners, border thickness, and metallic edge highlights remain visually stable.
- The newly separated brushed-silver foil is intended to become an additional Maker built-in foil style. Together with the existing legacy ThreeStar silver-star foil, Maker should eventually have at least two built-in foil styles.
- Reusable foil masters are stored in ChatGPT Library at `/ScratchGame/Asset-Library/Foils/`; this handoff folder should not become the long-term asset library.
- A future optional direction may evaluate a **full-canvas foil overlay / mask mode**:
  - for example a plain silver sheet or repeating foil pattern spanning the whole Canvas;
  - the foil texture itself would not depend on specific zone positions;
  - Scratch Zones would reveal only the relevant parts through masking / clipping;
  - this is **not** the current ThreeStar-Test V1 implementation target.
- Image generation does not reliably output exact arbitrary pixel dimensions. Final production workflow must therefore generate/edit the art first, then post-process and validate the exact Canvas dimensions.

## Size status

The source generations created in chat were all **1388 x 1133**, so none of those source generations is directly valid as a formal `canvas=1` asset.

ScratchPack V1 currently requires:

```text
canvas = 1 -> 1080 x 882
```

The reference copies in this folder are intentionally downscaled lightweight snapshots for Git handoff only. Do **not** treat their dimensions or Scratch Zone positions as authoritative production geometry.

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
  - this was the latest visual direction before handoff;
  - source generation size confirmed as 1388 x 1133.
- `05-silver-foil-style-a.webp`
  - newly separated brushed-silver 3x3 foil reference;
  - preserve as a candidate Maker built-in foil style;
  - use it as a visual / texture reference, not as the final production foil asset.

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
- default/runtime foil is still represented by `art.mask=null` until the final art asset is selected.

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

The blue clean base has now been normalized and visually accepted at **1080 x 882**. The accepted re-measurement is:

```text
zone size: 191 x 138
X: 219, 444, 669
Y: 256, 422, 588
horizontal gap: 34
vertical gap: 28
priceDisplayArea: x=852 y=43 width=201 height=82
serialDisplayArea: x=364 y=774 width=350 height=59
```

`ticket.json` still contains the older provisional geometry until the final Pack asset update step; do not treat the old 160x100 values as final.

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

1. Keep the accepted blue clean base as the current formal ThreeStar-Test direction.
2. Preserve the accepted **1080 x 882** production candidate without baking dynamic price, serial, or foil into it.
3. Use the accepted re-measured standard 3x3 grid / price / serial geometry listed above.
4. Use reusable single-zone foil templates rather than a fixed 3x3 foil overlay.
   - Accepted reusable masters are stored in `/ScratchGame/Asset-Library/Foils/`.
   - Current masters: `foil-brushed-silver-plain.png` and `foil-brushed-silver-three-star.png`.
5. Update `ticket.json` to the accepted 1080x882 geometry and place the accepted base as final `assets/three-star.png`.
6. Only after the artwork/geometry asset step is complete, continue with V1 loader/validator work.

During ticket redraw/image rounds, do not mix in source-code, CI, or runtime changes; finish and accept the visual asset first.
