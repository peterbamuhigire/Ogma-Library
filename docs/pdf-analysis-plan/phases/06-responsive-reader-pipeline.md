# Phase 6 — Responsive preview, cache, scroll and zoom pipeline

**Depends on:** Phase 5; canonical phases 17–21 and 38.
**Outcome:** opening and reading feel immediate without promising impossible
instant rendering for expensive pages.

## Work

- Warm one document session; do not reopen the parser for each page.
- Render a bounded low-resolution preview first, then upgrade to the requested
  raster; keep the UI responsive and cancel stale work.
- Add cache keys for page, geometry, zoom, tile, display policy and engine
  version; deduplicate concurrent foreground/prefetch requests.
- Keep native `ScrollViewer` offset handling for mouse wheel, trackpad,
  scrollbar and keyboard input. Boundary page-turn fallback must never fire
  when the offset actually changed.
- Implement virtualised continuous multi-page scroll or explicitly retain the
  page-only profile with a visible product decision.
- Preserve focal point on zoom and use measured viewport width for fit width.

## Budgets

Measure open-to-shell, open-to-first-preview, first full page, cached next page,
uncached next page, wheel frame continuity, zoom upgrade, peak RSS and worker
CPU on reference Windows/macOS hardware. The existing 100 ms cached page-turn
goal is a target to verify, not evidence supplied by the old synthetic spike.

## Exit criteria

No visible blocking dialog, stuck request or stale page replacement; thumbnails
run below reader priority; reader tests cover middle-page wheel, boundaries,
sidebar isolation, resize, double-click open and failure recovery.
