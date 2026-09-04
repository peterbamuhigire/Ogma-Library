# Phase 11 Mixed-Quality Extraction Benchmark Evidence

Date: 2026-09-04

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ExtractionPipeline_MixedQualityBatch_RecordsThroughputBaseline" --verbosity normal -m:1
```

Result: 1 passed, 0 failed.

Recorded baseline:

- 32 books;
- 96 pages;
- 2,994 milliseconds elapsed; and
- 84,820,632 allocated bytes.

The fixture includes full, scanned, and partial page layers and exercises the
artifact/chunk extraction path.

## Scope boundary

This is a repeatable developer-machine baseline. It does not claim the Phase 11
target-scale or 50,000-book acceptance gate, cross-platform performance, or
physical corpus evidence.
