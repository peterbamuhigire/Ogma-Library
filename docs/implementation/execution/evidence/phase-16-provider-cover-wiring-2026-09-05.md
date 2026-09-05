# Phase 16 Provider Cover Wiring Evidence

Date: 2026-09-05

## Delivered

Deterministic metadata enrichment now invokes the existing validated
`ProviderCoverAssetService` when external providers are enabled and a positive,
non-stale provider result supplies a cover URL. The selected result is the
highest-confidence eligible provider result. The service validates the fixed
provider image endpoint, bounds and decodes the image, re-encodes it locally,
atomically persists it under the sidecar, and registers the `provider` cover
manifest entry.

Provider-art persistence is optional and failure-isolated: rejected or failed
provider artwork records a redacted local event and does not discard valid
metadata proposals or block catalogue readiness. Stale metadata is not used to
refresh provider artwork.

## Verification

- `Phase16VisualAssetTests`: 9 passed, 0 failed, 0 skipped. This covers exact
  variant resolution, bounded variant policy, provider image validation and
  atomic provider persistence/manifest provenance.
- `dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj
  --configuration Release --no-restore`: 0 warnings, 0 errors.
- The feature remains fail-closed when external providers are disabled because
  the provider cover service is not registered in that default mode.

## Gate disposition

Closed locally: provider cover acquisition is now connected to enrichment and
uses the approved asset validation/persistence boundary.

Still open: real provider image/network evidence, large-library asset budgets,
physical accessibility, and cross-platform acceptance.
