# Phase 25 vector-integrity evidence — 2026-09-04

## Change

`EmbeddingVectorRepository.CreateAsync` now rejects inconsistent vector
metadata when `DimensionCount` does not equal the serialized payload length.
This prevents a durable vector from claiming a different dimensionality than
the bytes used by retrieval. Existing finite-value, maximum-dimension, source
hash, model-dimension, stale-detection, and tombstone checks remain active.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase11EmbeddingSchemaTests" --logger "console;verbosity=minimal"
```

Result: **PASS — 7 passed, 0 failed, 0 skipped**. The Debug build completed
without compiler errors.

## Remaining gates

- ANN/index selection and representative target-scale memory/latency evidence
  remain open.
- Incremental batch cost measurements and UI rebuild/cancel evidence remain
  `NOT ASSESSED`.
- Provider/privacy evidence remains bounded by the local-only policy and the
  separate AI/provider gates.
