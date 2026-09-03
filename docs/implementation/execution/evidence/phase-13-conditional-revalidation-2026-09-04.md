# Phase 13 Evidence - Conditional Provider Revalidation

Date: 2026-09-04

## Delivered

- `ProviderCacheEntryRow.ETag` is persisted with a bounded length.
- Expired positive cache entries supply their validator to normalized provider
  requests.
- Google Books and Open Library send `If-None-Match` when a validator exists.
- `304 Not Modified` responses preserve the cached payload, clear its stale
  presentation state, update retrieval/expiry timestamps, and retain the
  validator.
- A migration and model snapshot update are included.

## Verification

```text
dotnet build src/OgmaLibrary.Application/OgmaLibrary.Application.csproj --configuration Release --no-restore
  Passed: 0 warnings, 0 errors

dotnet build src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj --configuration Release --no-restore -p:BuildProjectReferences=false
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~Phase13ProviderGatewayTests|FullyQualifiedName~ProviderClientTests" --verbosity minimal -m:1
  Passed: 14, Failed: 0, Skipped: 0
```

## Open gates

Quota accounting, circuit-breaker/backoff telemetry, provider conflict
aggregation, and privacy disclosure capture remain open. Physical provider
network evidence is not implied by these deterministic tests.
