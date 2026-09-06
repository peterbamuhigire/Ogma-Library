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
- Added a non-sensitive processing projection to the shared catalogue summary,
  carrying full-text index state, semantic embedding state, OCR provenance, and
  bounded metadata quality score.
- Rendered localized processing, semantic, OCR, quality, and unavailable badges
  in both grid and list catalogue cards without exposing source paths.
- Added headless accessibility proof for the catalogue shell: all effectively
  visible interactive controls have automation names, and the enabled sidebar
  toggle accepts keyboard focus. Evidence:
  `evidence/phase-19-catalogue-accessibility-2026-09-05.md`.

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

Physical keyboard/screen-reader journeys and named reference-hardware
confirmation remain before phase 19 closure. The local headless naming/focus
subgate is closed. The cover-source fallback/precedence and API
asset-authorization sub-gates are now closed by the local evidence below. The
processing/quality projection and rendered badges, persisted-view-state,
UI-pagination, directory-view, visible filter/sort wiring, and local 50k
server-side page-performance sub-gates are also closed.

- Current-HEAD full solution verification passed 1,089 tests (895 core, 41
  architecture, 153 UI), with 0 failures and 0 skips. It does not close Phase
  19's remaining accessibility or reference-hardware gates.
- Processing/quality projection and badge proof: 1 core projection test and 1
  headless render test passed, 0 failed, 0 skipped. Evidence:
  `evidence/phase-19-processing-badges-2026-09-05.md`.
- Cover fallback proof: the catalogue read model now applies the same
  custom > embedded > provider > generated > placeholder source precedence as
  `IVisualAssetService`, and both summary and detail projections are covered by
  `CatalogueReadModel_UsesSourcePrecedenceForCoverFallback`.
- API asset authorization proof: the existing authenticated LAN endpoint suite
  verifies published-hash enforcement, bounded variants, unauthorized
  metadata/path exclusion, and asset serving; no raw local path is exposed.
- Complete post-accessibility Release solution regression: 916 core + 41
  architecture + 158 UI = 1,115 passed, 0 failed, 0 skipped.

The Aug-39 Definition of Done now records three-view action parity, real
cover/processing-state presentation, and persisted correct filter/sort state as
closed. Physical keyboard/screen-reader and named-reference 50,000-item gates
remain unchecked.
