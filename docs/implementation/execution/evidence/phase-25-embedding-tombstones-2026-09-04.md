# Phase 25 embedding tombstones evidence

Date: 2026-09-04

`EmbeddingVectorRow` now records `IsTombstoned` and `TombstonedUtc`, with a
migration-backed index on tombstone state and model identity. The repository
can explicitly tombstone vectors whose source fingerprint no longer matches
the current search chunk. Tombstoned vectors are excluded from chunk lookup,
book lookup, and stale counts. Re-saving a regenerated vector clears the
tombstone state so the vector becomes retrievable again.

Verification: `Phase11EmbeddingSchemaTests` passed 6/6, including migration
columns/index, stale detection, durable tombstone metadata, retrieval exclusion,
and regeneration reactivation.

The remaining Phase 25 gates cover side-by-side index swap/resume, ANN and
target-scale evidence, cost/cache telemetry, and UI stale-count/rebuild
controls.
