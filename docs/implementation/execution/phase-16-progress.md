# Phase 16 Progress - Cover, Thumbnail and Spine Assets

Date: 2026-09-05

## Delivered in this increment

- Added a durable `VisualAssetManifests` table keyed by book, asset kind and
  variant, including source, source-content hash, dimensions, format,
  generation version and lifecycle status.
- Added `IVisualAssetService` with safe `.ogma`-relative path validation,
  deterministic generated registration, custom-cover registration and source
  hash invalidation.
- Custom covers use a protected `custom` variant and always win preferred
  resolution; generated registration cannot replace them.
- Cover and spine generators now register successful outputs in the manifest.
- Registration and file re-match now enqueue idempotent `SpineGeneration` jobs;
  the existing worker path therefore generates spines on both ingest and file
  version changes, as required by the Phase 16 design.
- Catalogue summary and detail projections now expose the preferred ready cover
  relative path instead of returning `null` unconditionally.
- Added focused tests for manifest durability, custom-cover precedence,
  invalidation, path safety and 2D read-model exposure.
- Added output-side image decode and exact-dimension verification for worker
  generated cover and spine assets before they leave the sandbox.
- Added explicit stale-asset garbage collection that removes stale manifest rows,
  deletes only unreferenced `.ogma` files under the configured root, and retains
  files still referenced by another manifest entry.
- Replaced the book-detail title placeholder with the shared safe cover control,
  bound to the manifest-relative asset path and configured local sidecar root;
  a headless render regression confirms the boundary.
- Narrowed LAN visual-asset variants to the published contract (`provider` and
  `detail` for covers, `retina` for spines, and the default thumbnail route),
  preventing arbitrary safe-looking suffixes from addressing sidecar files.
- Added an authenticated endpoint regression proving unsupported variants fail
  closed before file resolution.
- Connected the existing validated provider-cover persistence boundary to
  deterministic metadata enrichment. Only positive, non-stale, highest-
  confidence provider artwork is considered; failure is isolated from metadata
  proposals and the default external-provider-disabled mode remains fail-closed.
  Evidence: `evidence/phase-16-provider-cover-wiring-2026-09-05.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase16VisualAssetTests`: 4 passed.
- Detail cover UI regression: 2 passed.
- Direct-open and ingestion regression slice: 19 passed, 0 failed, 0 skipped;
  this includes new-book and re-matched-file spine-job assertions.

## Remaining phase gate

Embedded source acquisition, remaining UI journeys, and large-library asset
budget testing remain before phase 16 closure. Provider cover acquisition is
now wired through the validated provider-image boundary; lazy high/low variants
and exact variant rejection are covered by the local variant tests.
The ingest/update spine scheduling sub-gate is closed locally.
The local detail cover-control and LAN asset-authorization sub-gates are closed;
physical accessibility and cross-platform evidence remain open.
