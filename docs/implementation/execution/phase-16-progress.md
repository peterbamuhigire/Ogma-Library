# Phase 16 Progress - Cover, Thumbnail and Spine Assets

Date: 2026-08-30

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
- Catalogue summary and detail projections now expose the preferred ready cover
  relative path instead of returning `null` unconditionally.
- Added focused tests for manifest durability, custom-cover precedence,
  invalidation, path safety and 2D read-model exposure.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase16VisualAssetTests`: 3 passed.

## Remaining phase gate

Embedded/provider source acquisition, image decode/size validation, explicit
garbage collection, lazy high/low variants, API authorization, UI journeys,
and large-library asset budget testing remain before phase 16 closure.
