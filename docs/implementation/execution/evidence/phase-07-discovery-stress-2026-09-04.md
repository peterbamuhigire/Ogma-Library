# Phase 7 Evidence — Discovery Stress Reliability

Date: 2026-09-04
Scope: 50,000-file recursive discovery and bounded-channel delivery

## Implemented repair

`PdfDiscoveryService` now reuses the ordinary file's `FileInfo` metadata and
checks `FileAttributes.ReparsePoint` before invoking the canonical path guard.
This removes the previous redundant `LinkTarget` query and second `FileInfo`
construction for every ordinary file while retaining symlink/reparse-point
containment and disappearing-file handling.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --filter "FullyQualifiedName~DiscoveryServiceTests&TestCategory!=Benchmark" --no-restore --verbosity minimal -m:1
```

The filter was intended to exclude the benchmark but the xUnit adapter did not
apply that trait expression; the complete `DiscoveryServiceTests` class ran.

Result: 7 passed, 0 failed. The 50,000-file test's internal 30-second
discovery budget passed, and the test host completed normally. Observed command
wall time was 1m34s, including creation and cleanup of 50,000 temporary files.

## Still open

Physical cross-platform filesystem behavior, reparse-point scenarios on each
supported OS, and production-library throughput evidence remain separate
release gates.
