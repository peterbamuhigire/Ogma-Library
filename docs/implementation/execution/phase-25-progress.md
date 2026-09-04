# Phase 25 Progress - Versioned Embeddings and Vector Lifecycle

Date: 2026-09-04

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
- Added a bounded five-minute in-memory query-embedding cache keyed by a
  SHA-256 digest, capped at 128 entries, with cache-hit telemetry in the
  semantic response and no raw-query persistence.
- Added a durable active/staging semantic-index pointer and a side-by-side
  rebuild coordinator. Vector uniqueness and repository lookup now include the
  index generation, staged embedding batches are resumable after provider or
  process interruption, promotion is an explicit atomic pointer transition,
  and semantic retrieval reads only the active generation.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- Embedding, schema, semantic and performance regression slice: 13 passed.
- Tombstone migration and lifecycle regression slice:
  `Phase11EmbeddingSchemaTests`: 7 passed.
- Full solution suite: 1,060 passed, 0 failed, 0 skipped; stale-count UI
  coverage is included in the 142-test Avalonia suite.
- Semantic retrieval now streams the bounded 50,000-vector target window and
  retains only top-K scored candidates; the vector corpus is not materialized
  as an in-memory list.
- Query-embedding cache slice: 7 passed; latest full isolated solution suite:
  882 core + 41 architecture + 142 UI = 1,065 passed, 0 failed, 0 skipped. See
  `evidence/phase-25-query-embedding-cache-2026-09-04.md`.
- Side-by-side lifecycle slice: 4 passed; schema/lifecycle regression slice:
  10 passed, 0 failed, 0 skipped. See
  `evidence/phase-25-side-by-side-vector-lifecycle-2026-09-04.md`.

## Remaining phase gate

The side-by-side vector index swap/resume gate is now closed locally. ANN/
target-scale relevance evidence, provider cost accounting, target-scale UI
performance, reference-corpus and reference-machine evidence remain open. The
stale-count/rebuild-status, bounded-memory exact-retrieval, and bounded
query-cache telemetry subgates remain closed locally.
