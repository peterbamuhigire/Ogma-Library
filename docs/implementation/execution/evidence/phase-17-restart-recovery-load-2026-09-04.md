# Phase 17 Evidence — Restart Recovery Load

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The local restart/recovery subgate is closed. A worker context was disposed
with an active lease, a newly created context expired and recovered that lease,
and recreated worker contexts drained the bounded queue without duplicate
completion. The test is deliberately a restart-style database exercise; it is
not physical OS process-kill, crash, cross-machine, or long-duration soak
evidence.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --nologo -m:1 --filter "FullyQualifiedName~Phase17RuntimeRecoveryLoadTests|FullyQualifiedName~Phase17JobRuntimeTests"
```

Result: 8 passed, 0 failed, 0 skipped. The load case drained 64 metadata jobs
after one orphaned lease and asserted completion within 10 seconds.
