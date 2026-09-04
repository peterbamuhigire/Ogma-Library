# Phase 22 Search Scale Evidence

Date: 2026-09-04

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~MetadataSearchServiceTests" --verbosity minimal -m:1
```

Result: 8 passed, including the 50,000-book metadata search benchmark with the
required p95 <=150 ms assertion.

## Implementation change

The metadata exact path now materializes no more than its 50-result response
contract before client-side scoring. This preserves the existing deterministic
ordering and prevents large entity graphs from dominating the interactive
search path.

## Scope boundary

This is a local Windows test-environment result, not named reference-machine
or cross-platform release evidence. The local facets, paging, highlighting,
and full-text-fallback subgates are covered by the current catalogue/search
implementation; reference-machine and physical accessibility evidence remain
open.
