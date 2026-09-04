# Phase 19 Progress - Production 2D Catalogue

Date: 2026-08-30

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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase19CatalogueAssetTests`: 2 passed.
- `CatalogueGridTests`: 2 passed.
- `CatalogueDirectoryViewRenderTests`: 1 passed.

## Remaining phase gate

Persisted filter/sort views, UI pagination wiring, processing/quality badges,
complete cover-source fallback, API asset authorization, and keyboard/screen-
reader journeys remain before phase 19 closure. The directory-view delivery
sub-gate and local 50k server-side page performance sub-gate are closed; named
reference-hardware confirmation remains a release gate.
