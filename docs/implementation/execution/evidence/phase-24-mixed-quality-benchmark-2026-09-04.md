# Phase 24 Mixed-Quality Benchmark Evidence

Date: 2026-09-04

## Scope

The deterministic local extraction benchmark exercises 500 books with three
pages each: complete selectable text, scanned/image-only content, and partial
text. It records elapsed time and total allocations while asserting that all
1,500 pages and 500 books complete without a failed book.

This is a reproducible engineering baseline, not a substitute for real mixed
PDF accuracy, native OCR CPU/memory profiling, or cross-platform packaged-asset
evidence.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ExtractionPipelineServiceTests.ExtractionPipeline_MixedQualityBatch_RecordsThroughputBaseline" --verbosity normal -m:1
```

Initial result: passed with 500 books, 1,500 pages, and 0 failed books, but
reported 37,005 elapsed milliseconds and 15,010,299,152 allocated bytes. That
finding identified shared test-context retention as an allocation amplifier.
After clearing completed shared-context tracking between books, the same run
reported 9,190 elapsed milliseconds and 296,590,296 allocated bytes, with all
500 books and 1,500 pages still successful.

The improved result is a local baseline, not an accepted real-PDF/native OCR
resource budget; it requires repeat measurement on the supported platforms.

## Still open

Real mixed-PDF accuracy and CPU/memory acceptance, packaged assets on the
supported platforms, and physical assistive-technology evidence remain open.
