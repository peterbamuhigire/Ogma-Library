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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase19CatalogueAssetTests`: 2 passed.
- `CatalogueGridTests`: 2 passed.

## Remaining phase gate

Directory view parity, persisted filter/sort views, UI pagination wiring,
processing/quality badges, complete cover-source fallback, API asset
authorization, keyboard/screen-reader journeys, and 50k-record performance
evidence remain before phase 19 closure.
