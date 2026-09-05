# Phase 5 — Page geometry and rendering contract

**Depends on:** Phase 4; canonical phases 11, 16, 21.
**Outcome:** document geometry, raster output and overlays share one transform.

## Work

- Resolve effective MediaBox/CropBox and relevant box defaults/inheritance;
  retain trim/bleed/art boxes when available for future features.
- Normalize rotation, user-unit, origin and device transform in one model.
- Extend `RenderRequest` with page box, rotation, pixel dimensions, quality,
  color/background, annotation/form policy, optional-content state and tile.
- Include every visual parameter and renderer version in the page-cache key.
- Replace the UI’s fixed geometry fallback wherever actual geometry exists;
  keep fallback only as an explicit degraded state.
- Test selection, annotation overlays, page labels and thumbnails against the
  same transform and physical dimensions.

## Experiment and exit

Use portrait, landscape, rotated, crop-offset and unusual-size fixtures. Measure
fit-width/fit-page error in pixels and overlay alignment. Exit when geometry
tests have no unexplained drift, render status distinguishes preview/full/error,
and a renderer failure cannot silently produce a misleading page.
