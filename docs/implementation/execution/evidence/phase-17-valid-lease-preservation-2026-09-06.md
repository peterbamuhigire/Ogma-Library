# Phase 17 Valid-Lease Preservation

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

Startup recovery previously reset every running job to pending. If a second
Ogma process opened the same catalogue while another worker still held a valid
lease, startup could steal that work and permit duplicate execution.

Recovery now reclaims only:

- legacy running rows with no lease expiry; or
- running rows whose lease has expired.

It clears recovered lease ownership, records a stable `startup_recovery`
failure code, schedules the job immediately, and preserves a still-valid lease
without changing its owner, status, expiry, or retry count.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~JobManagementTests|FullyQualifiedName~Phase17JobRuntimeTests"
Passed: 12, Failed: 0, Skipped: 0
```

## Gate disposition

The duplicate-process valid-lease preservation sub-gate is closed. Physical
full-application kill/restart, long-duration soak, and complete Activity Centre
UI evidence remain open.
