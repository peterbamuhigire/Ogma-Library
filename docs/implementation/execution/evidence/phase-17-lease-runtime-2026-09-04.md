# Phase 17 Durable Lease Runtime Evidence

Date: 2026-09-04

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase17JobRuntimeTests" --verbosity minimal -m:1
```

Result: 7 passed, 0 failed.

The slice verifies exclusive claims, lease-owner enforcement, retry backoff
and terminal failure, expiry recovery, dead-letter quarantine, resource-group
capacity, redacted lifecycle events, and operational metrics snapshots.

## Scope boundary

This is local durable-runtime evidence. Search-extraction and embedding workers
are still stage-based, and no physical kill/restart load drill or cross-platform
worker evidence is claimed.
