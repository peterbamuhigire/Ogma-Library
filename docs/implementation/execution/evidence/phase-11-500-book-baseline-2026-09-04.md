# Phase 11 500-Book Extraction Baseline

Date: 2026-09-04

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ExtractionPipelineServiceTests.ExtractionPipeline_MixedQualityBatch_RecordsThroughputBaseline" --verbosity normal -m:1
```

Result: 1 passed, 0 failed, 0 skipped.

Measured local result:

- 500 synthetic books;
- 1,500 full/scanned/partial pages;
- 9,190 milliseconds elapsed;
- 296,590,296 managed bytes allocated; and
- 0 failed books.

The benchmark exercises the real extraction pipeline and its artifact, page,
chunk, ISBN, and TOC paths. It is not a claim of real-PDF accuracy, 50,000-book
acceptance, cross-platform performance, or physical corpus evidence.
