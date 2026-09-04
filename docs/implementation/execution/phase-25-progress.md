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
- Enforced one vector dimension per model/version/provider tuple so rebuild drift
  fails closed before a mixed semantic index can be persisted.
- Added migration-backed tombstone state for stale vectors, including the
  invalidation timestamp and a model-scoped retrieval index.
- Added an explicit stale-tombstone operation; tombstoned vectors are excluded
  from stale counts and repository retrieval, while successful regeneration
  clears the tombstone state on the existing model-scoped row.
- Exposed a streamed, metadata-only global stale-vector count through the Index
  Manager contract and rendered it with a localized, named status control so
  users can see why a rebuild may be needed without loading vector blobs.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- Embedding, schema, semantic and performance regression slice: 13 passed.
- Tombstone migration and lifecycle regression slice:
  `Phase11EmbeddingSchemaTests`: 7 passed.
- Full solution suite: 1,060 passed, 0 failed, 0 skipped; stale-count UI
  coverage is included in the 142-test Avalonia suite.

## Remaining phase gate

Side-by-side vector index swap/resume, ANN/target-scale evidence, cost/cache
telemetry, and target-scale UI performance remain before phase 25 closure. The
stale-count/rebuild-status subgate is now closed locally; ANN/memory, cost,
reference-corpus, and reference-machine evidence remain open.
