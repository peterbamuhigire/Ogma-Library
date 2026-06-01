# ADR-0006: Hybrid Search And ANN Upgrade Path

## Status

Accepted

## Date

2026-06-01

## Context

Ogma Library needs fast, private, offline search for personal libraries and
school e-library installations. Phase 10 provides exact metadata and FTS5
search. Phase 11 adds local embeddings, cosine similarity, hybrid ranking,
match-location explanation, and embedding erasure.

The product target is semantic search P95 at or below 1,500 ms on a 2,000-book
corpus. The initial implementation should be easy to audit and should not add
packaging risk for school computers.

## Decision

Use brute-force SIMD cosine search over SQLite `EmbeddingVectors` as the Phase
11 vector-search implementation. Defer approximate nearest neighbor search to
the spike documented in `docs/spikes/ANN-SQLite-Vec-Spike.md`.

Add an ANN trigger: brute-force remains the default until semantic search P95
exceeds 1,000 ms at 5,000 or more books, or exceeds 1,500 ms at 2,000 books
after SIMD optimization.

## Decision Drivers

- Keep all embeddings local and erasable.
- Avoid extension-packaging risk before the need is proven.
- Preserve deterministic ranking and reproducible tests.
- Keep the Application layer behind vector-search contracts so future ANN
  backends do not leak into UI or AI-advisor code.

## Consequences

### Positive

- Phase 11 ships with a simple, inspectable local implementation.
- Privacy controls can delete all vectors without coordinating an external
  index service.
- Hybrid ranking remains deterministic because final ordering is controlled in
  managed code.

### Negative

- Very large libraries may eventually require ANN indexing.
- Brute-force search loads and scores more vector data than an ANN index.
- A future sqlite-vec adoption will require packaging and migration work.

## Follow-Up

- Maintain the `IVectorIndex` shape proposed in the ANN spike when introducing
  a second vector backend.
- Run the ANN spike only after the trigger threshold is met.
- Preserve the fallback path from sqlite-vec to brute-force if extension loading
  fails.

