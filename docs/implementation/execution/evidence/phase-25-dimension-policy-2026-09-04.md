# Phase 25 embedding dimension policy evidence

Date: 2026-09-04

`EmbeddingVectorRepository` now enforces one vector dimension for each
model/version/provider tuple. A rebuild that returns a different dimension fails
closed before persistence, preventing a partially mixed semantic index. The
existing per-vector finite-value and size validation remains in force.

Verification: `Phase11EmbeddingSchemaTests.EmbeddingVectorRepository_RejectsDimensionDriftWithinModelVersion` passes.

The remaining Phase 25 gates cover tombstones, side-by-side index swap/resume,
ANN and target-scale evidence, cost/cache telemetry, and UI stale-count/rebuild
controls.
