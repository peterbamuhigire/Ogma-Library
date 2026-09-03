# Phase 7 acceptance evidence

Date: 2026-09-04  
Environment: Windows, .NET 10.0.1 SDK, Release configuration

## Gates closed

| Gate | Evidence | Result |
| --- | --- | --- |
| Recursive, bounded discovery | `DiscoveryServiceTests` plus `scripts/Phase07DiscoveryBenchmark` | Pass |
| Directory lifecycle diagnostics | `DiscoveryService_EmitsPerDirectoryLifecycleDiagnostics` | Pass |
| Unreadable-directory reporting | `DiscoveryService_ReportsUnreadableDirectoryWithoutAbortingTheScan` | Pass for deterministic I/O failure; OS ACL permission behavior remains a platform gate |
| Durable cursor resume | `Phase07IncrementalDiscoveryTests.Scan_ResumesAfterDurableDirectoryCursor` | Pass |
| Per-directory checkpoint persistence | `Phase07IncrementalDiscoveryTests.Scan_PersistsDirectoryLifecycleAndCompletionCursor` | Pass |
| Cross-session downstream idempotency | `Phase06ProcessingStateTests.ClaimCompleteAndFinalize_ProducesDurableSuccess` | Pass |
| 50,000-file bounded benchmark | `scripts/Phase07DiscoveryBenchmark` | 50,000 files, bounded channel capacity 500, 18,200 ms discovery stream |

## Verification commands

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase07IncrementalDiscoveryTests|FullyQualifiedName~Phase06ProcessingStateTests" --verbosity minimal -m:1
Passed: 7, Failed: 0

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DiscoveryService_DiscoversPdfs|FullyQualifiedName~DiscoveryService_HonorsExcludedFolders|FullyQualifiedName~DiscoveryService_PathsNormalized|FullyQualifiedName~DiscoveryService_EmitsPerDirectory|FullyQualifiedName~DiscoveryService_ResumesAfter|FullyQualifiedName~DiscoveryService_ReportsUnreadable" --verbosity minimal -m:1
Passed: 6, Failed: 0

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DiscoveryService_EnumeratesFiftyThousand" --verbosity minimal -m:1
Passed: 1, Failed: 0, Duration: 1 minute 54 seconds including corpus creation

dotnet run --project scripts/Phase07DiscoveryBenchmark/Phase07DiscoveryBenchmark.csproj --configuration Release --no-build
{"files":50000,"elapsedMilliseconds":18200,"channelCapacity":500}
```

The benchmark measures scanner stream time after corpus creation. It does not
claim macOS behavior, screen-reader behavior, or signed-release behavior; those
remain later physical release gates.
