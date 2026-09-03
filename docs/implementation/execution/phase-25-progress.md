# Phase 25 Progress - Versioned Embeddings and Vector Lifecycle

Date: 2026-08-30

## Delivered in this increment

- Added vector provenance for source fingerprint, extractor version, chunker
  version, search-index version and provider key.
- Added deterministic source fingerprints over book/chunk identity, selected
  index version, extraction artifact and chunk text.
- Re-embedding now treats legacy vectors without a source fingerprint as
  pending, while semantic retrieval remains compatible with those legacy rows
  during migration.
- Enforced local-only provider execution and rejected invalid provider model
  names, empty/oversized vectors and non-finite vector values.
- Added bounded vector persistence validation and preserved model/version
  idempotency semantics.
- Added schema migration and tests for provenance, unavailable/non-local
  providers, invalid vectors, semantic compatibility and existing performance
  behavior.
- Added a repository-level stale-count query that recomputes the current selected
  chunk fingerprint and detects vectors whose source changed after generation.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- Embedding, schema, semantic and performance regression slice: 13 passed.

## Remaining phase gate

Explicit tombstones,
tombstones, side-by-side vector index swap/resume, ANN/target-scale evidence,
cost/cache telemetry, dimension-consistency policy across a rebuild, and UI
stale-count/rebuild controls remain before phase 25 closure.
