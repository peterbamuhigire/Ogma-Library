# Phase 25 stale-vector status UI - 2026-09-04

## Scope

The existing embedding repository detected stale vectors per book, but the
Index Manager did not expose a library-wide count. This increment adds a
truthful status path for the rebuild decision.

## Implementation

- `IEmbeddingVectorRepository.GetStaleCountAsync` accepts a null book ID for a
  library-wide query.
- The repository projects only source-hash and chunk metadata and streams rows;
  vector BLOBs are not materialized for the status read.
- `IndexManagerStatus` carries `StaleEmbeddingCount`.
- `IndexManagerViewModel` exposes a localized `StaleEmbeddingSummary` and the
  panel gives it an accessible name.

## Verification

- `SearchViewModelTests`: stale count and localized summary assertions pass.
- `IndexManagerServiceTests` and the embedding schema/tombstone tests (including
  the library-wide streamed count) pass through the full suite.
- Full solution suite: 877 core + 41 architecture + 142 UI = 1,060 passed;
  0 failed and 0 skipped.

## Boundary

This closes the local stale-count/rebuild-status subgate. It does not prove
ANN or target-scale memory behavior, cost/cache telemetry, representative
Recall/MRR/nDCG quality, reference-machine performance, or physical
accessibility.
