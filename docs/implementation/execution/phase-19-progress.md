# Phase 19 Progress - Production 2D Catalogue

Date: 2026-09-05

## Delivered in this increment

- Added a shared asynchronous `CoverImageView` used by both grid and list
  catalogue views.
- Cover loading resolves only manifest-relative paths beneath the configured
  library root, rejects traversal/absolute paths, and falls back to the title
  placeholder for missing or corrupt assets.
- Image decoding runs off the UI thread and stale loads are cancelled when the
  bound asset changes or the control detaches.
- Catalogue view-model loading now exposes the configured root only to the
  local presentation layer; catalogue/LAN projections remain relative-path
  based.
- Added ordered server-side `SkipCount` plus `MaxResults` paging to the shared
  catalogue read-model projection, with validation for negative paging values.
- Added focused tests for successful local decoding and traversal rejection;
  existing grid/list render tests continue to pass.
- Added a 50,000-record SQLite read-model page benchmark; the server-side
  100-result catalogue page passed the <=2-second local assertion.
- Added a functional directory catalogue view backed by the shared filtered
  projection. It renders library-root-relative paths in a virtualized list and
  opens the selected book in the reader on double-click; LAN projections remain
  explicit and do not inherit the desktop-only path field.
- Bound the visible filter panel's title and author fields, validated sort-field
  choices, and ascending/descending direction to the shared filter model. The
  existing single clear action remains available and filtering stays
  conjunctive.
- Added a crash-tolerant, atomic JSON store for non-sensitive catalogue view
  state; view mode, filter/sort choices and the current page restore on startup.
- Added debounced state persistence so typing in the filter panel does not write
  on every keystroke.
- Added bounded 100-item UI paging with localized page summaries and accessible
  previous/next controls shared by grid, list and directory surfaces.

## Verification

- Isolated `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj
  --configuration Release --no-restore` passed with 0 warnings and 0 errors;
  the normal solution output was locked by already-running application/worker
  processes and was not disturbed.
- `Phase19CatalogueAssetTests`: 2 passed.
- `CatalogueGridTests`: 2 passed.
- `CatalogueDirectoryViewRenderTests`: 2 passed, including filter/sort state
  binding coverage.
- `Phase19CataloguePagingTests`: 1 passed, covering restore, 205-row paging,
  boundary navigation and debounced persistence.
- `Phase19CatalogueViewStateStoreTests`: 1 passed, covering atomic round-trip
  and corrupt-preference recovery.

## Remaining phase gate

Processing/quality badges, complete cover-source fallback, API asset
authorization, and keyboard/screen-reader journeys remain before phase 19
closure. The persisted-view-state, UI-pagination, directory-view, visible
filter/sort wiring, and local 50k server-side page-performance sub-gates are
closed. Named reference-hardware confirmation remains a release gate.

- Current-HEAD full solution verification is green at 1,085 tests (893 core,
  41 architecture, 151 UI), with 0 failures and 0 skips. The refreshed run
  includes the current Phase 18 appearance/palette changes; it does not close
  Phase 19's remaining badges, fallback, authorization, accessibility, or
  reference-hardware gates.
