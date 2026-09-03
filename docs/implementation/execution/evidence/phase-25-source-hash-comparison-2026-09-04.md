# Phase 25 Evidence - Vector Source-Hash Comparison

Date: 2026-09-04

## Delivered

`EmbeddingVectorRepository.GetStaleCountAsync` loads the current vectors and
selected chunk records for a book, recomputes the deterministic source fingerprint
used during generation, and counts mismatches. This gives UI and rebuild
orchestration a bounded stale-vector signal without deleting data implicitly.

## Verification

The embedding generation test now generates two vectors, mutates one source
chunk, and verifies that exactly one vector is reported stale.

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~EmbeddingGenerationServiceTests" --verbosity minimal -m:1
  Passed: 5, Failed: 0, Skipped: 0
```

## Open gates

Explicit tombstones, side-by-side vector index swap/resume, ANN/target-scale
evidence, cost/cache telemetry, dimension policy, and stale-count/rebuild UI
controls remain open.
