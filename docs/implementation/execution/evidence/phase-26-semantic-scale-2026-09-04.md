# Phase 26 Semantic Retrieval Scale Evidence

Date: 2026-09-04

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~PerfBenchmark_SemanticSearch_P95_LessThan1500ms" --verbosity minimal -m:1
```

Result: 1 passed. The benchmark seeded 50,000 books with four-dimensional
embedding vectors and passed the approved p95 <=1,500 ms assertion over the
configured query set.

## Scope boundary

This is local Windows evidence for the current bounded in-memory semantic
retrieval path. It does not claim a true ANN index, representative labeled
relevance quality, memory-budget acceptance, or named reference-machine
performance.
