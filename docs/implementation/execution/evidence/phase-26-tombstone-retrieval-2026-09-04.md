# Phase 26 tombstone-retrieval evidence — 2026-09-04

## Change

Semantic corpus loading now excludes tombstoned vectors and rejects truncated
or malformed vector blobs whose decoded length does not equal the declared
dimension. Embedding-generation pending detection and progress accounting also
ignore tombstoned rows, allowing a stale/deleted vector to be regenerated rather
than suppressing the rebuild.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~SemanticSearchServiceTests" --logger "console;verbosity=minimal"
```

Result: **PASS — 6 passed, 0 failed, 0 skipped**. This includes the local 50,000
book semantic performance test and the tombstone fallback regression. The Debug
build completed without compiler errors.

## Remaining gates

- ANN/index selection, representative corpus quality metrics, and independently
  measured memory/latency budgets remain open.
- Citation/evidence UI, provider privacy consent, and release contract gates are
  not closed by this backend change.
