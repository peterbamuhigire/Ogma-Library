# Phase 22 Catalogue Search Contract Evidence

Date: 2026-09-04

## Scope

The additive `ICatalogueSearchService` provides a bounded page-oriented result
contract independent of FTS and semantic implementations. The metadata path
supports stable deterministic paging, field facets, and plain-text highlight
ranges. When metadata and its fuzzy path produce no result, indexed FTS hits are
returned with an explicit fallback notice and source labels.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase22CatalogueSearchQueryTests" --verbosity minimal -m:1
```

Result: 3 passed, 0 failed, 0 skipped.

Covered behaviours include stable page boundaries over 23 records, title facet
counts, safe highlight ranges, literal `%` handling, invalid page-size
rejection, and full-text fallback from an indexed chunk.

The named 50,000-book metadata latency gate was also rerun in the optimized
Release configuration:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MetadataSearchServiceTests.PerfBenchmark_MetadataSearch_P95_LessThan150ms" --verbosity minimal -m:1
```

Result: 1 passed, 0 failed, 0 skipped, with the existing p95 <=150 ms assertion.
The Debug configuration showed host-sensitive p95 misses during this session;
that does not replace the required Release result.

## Still open

UI facet chips, keyboard result navigation, full UI adoption of the new
contract, accessibility walkthroughs, and named reference-hardware evidence
remain open for full Phase 22 closure.
