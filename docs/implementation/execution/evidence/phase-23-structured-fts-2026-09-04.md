# Phase 23 Evidence - Structured Full-Text Source Queries

Date: 2026-09-04

## Delivered

The FTS5 service parses bounded source prefixes and applies the selected
`SearchChunkSource` in the SQL predicate. Unknown prefixes remain literal
queries. Existing snippets, artifact validity checks, source metadata, and page
indices remain unchanged.

## Verification

```text
dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj --configuration Release --no-restore -p:BuildProjectReferences=false
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~FtsIndexServiceTests" --verbosity minimal -m:1
  Passed: 7, Failed: 0, Skipped: 0
```

## Open gates

Highlight-safe rendering/page-jump UI, progress/no-index states, observability,
side-by-side rebuild swap, and 50,000-book latency evidence remain open.
