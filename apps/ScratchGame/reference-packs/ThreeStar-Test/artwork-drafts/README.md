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
- The newly separated brushed-silver foil is intended to become an additional Maker built-in foil style. Together with the existing legacy ThreeStar silver-star foil, Maker should eventually have at least two built-in foil styles.
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
  - final Maker foil should be normalized to a reusable single-zone/template asset rather than depending on one ticket's 3x3 placement.

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

Current provisional geometry in `ticket.json`:

```text
zone size: 160 x 100
X: 276, 460, 644
Y: 225, 353, 481
horizontal gap: 24
vertical gap: 28
priceDisplayArea: x=854 y=54 width=160 height=78
serialDisplayArea: x=390 y=780 width=300 height=44
```

These coordinates are structurally valid for the GameType 1 standard-grid rule, but must be remeasured against the finally accepted 1080x882 artwork before production use.

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

1. Choose which cleaned base (blue or red) becomes the first formal ThreeStar-Test artwork.
2. Normalize/post-process the selected art to exactly **1080 x 882**.
3. Remove any remaining baked dynamic content and ensure the base contains no foil.
4. Re-measure the standard 3x3 grid, price area, and serial area against the final image.
5. Produce the independent foil asset(s) in a reusable form.
6. Only after artwork is accepted, place the final `assets/three-star.png` into the test Pack and continue with V1 loader/validator work.

During ticket redraw/image rounds, do not mix in source-code, CI, or runtime changes; finish and accept the visual asset first.
