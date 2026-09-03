# Phase 13 Evidence - Provider Health and Circuit State

Date: 2026-09-04

## Delivered

- `MetadataProviderHealth` accounts requests in a bounded one-minute window
  and counts quota/circuit rejections.
- Three consecutive provider failures open a short circuit window; successful
  results reset consecutive-failure state.
- The gateway reserves provider quota before network work and treats empty or
  zero-confidence responses as isolated failures.
- Health snapshots contain provider name, request/rejection counts, failure
  counts, window start, and circuit expiry; they contain no query or secret data.

## Verification

```text
dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj --configuration Release --no-restore -p:BuildProjectReferences=false
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~Phase13ProviderGatewayTests" --verbosity minimal -m:1
  Passed: 5, Failed: 0, Skipped: 0
```

## Open gates

Provider retry/backoff telemetry, conflict aggregation, and privacy disclosure
capture remain open. This is in-process deterministic evidence, not a live
provider quota or outage test.
