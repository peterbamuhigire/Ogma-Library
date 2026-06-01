# ANN SQLite Vec Spike

**Status**: Proposed  
**Date**: 2026-06-01  
**Phase**: Grand Plan Phase 11, WP6  
**Owner**: Chwezi Core Systems

## Purpose

Ogma Library currently uses brute-force cosine scoring over local
`EmbeddingVectors` rows. This keeps Phase 11 simple, deterministic, and fully
offline. This spike defines when and how to evaluate `sqlite-vec` / Vec1 if
the brute-force path stops meeting the product target.

## Trigger

Run this spike when either condition is true:

- Semantic search P95 exceeds 1,000 ms at 5,000 or more books.
- Semantic search P95 exceeds 1,500 ms at 2,000 books after SIMD optimization.

The Phase 11 implementation remains brute-force until one trigger is met.

## Candidate Technology

`sqlite-vec` provides vector similarity search inside SQLite through a loadable
extension and virtual-table style APIs such as Vec1. It fits Ogma's local-first
model because vectors stay in the catalogue database instead of moving to a
network service.

## Questions

- Can the extension be packaged reliably on Windows and macOS school computers?
- Does extension loading work under the app's signed/packaged runtime model?
- Is exact or approximate nearest-neighbor behavior available for the chosen
  Vec1 table shape, and is the ranking stable enough for deterministic tests?
- How large is the index on disk for 5,000 books and realistic chunk counts?
- Can erasure delete both `EmbeddingVectors` rows and vector-index rows in one
  transaction?

## Integration Shape

Keep callers behind an Application contract so Phase 13 and the UI never depend
on a specific vector backend.

```csharp
public interface IVectorIndex
{
    Task UpsertAsync(
        long chunkId,
        string modelName,
        string modelVersion,
        ReadOnlyMemory<float> vector,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        string modelName,
        string modelVersion,
        int limit,
        CancellationToken cancellationToken);

    Task<int> DeleteAllAsync(CancellationToken cancellationToken);
}
```

Initial implementations:

- `BruteForceVectorIndex`: wraps the current `EmbeddingVectors` BLOB query and
  `CosineSimilarityService.TopK`.
- `SqliteVecVectorIndex`: spike-only implementation that mirrors vectors into a
  Vec1 table and searches through `sqlite-vec`.

## Spike Steps

1. Build a disposable branch with `IVectorIndex` and the two implementations.
2. Add packaging probes for Windows and macOS extension loading.
3. Generate a deterministic corpus at 5,000 books with realistic chunk counts.
4. Compare brute-force vs. sqlite-vec P50/P95, memory, and database size.
5. Verify deterministic tie-breaking: final sort must remain score desc,
   `BookId` asc or `ChunkId` asc depending on result level.
6. Verify erasure deletes vector-index rows and audit events remain correct.
7. Produce an ADR update if sqlite-vec wins on speed without unacceptable
   packaging or determinism risk.

## Acceptance Criteria

- P95 improves by at least 2x against the 5,000-book corpus.
- Extension packaging is documented for Windows and macOS.
- Search results remain reproducible for fixed inputs.
- Erasure remains transactional and audit-covered.
- Failure to load the extension falls back to brute-force without data loss.

## Exit Decision

- **Adopt** if performance and packaging are both acceptable.
- **Defer** if brute-force remains under threshold.
- **Reject** if extension loading, determinism, or erasure behavior is not
  reliable enough for school-computer deployments.

