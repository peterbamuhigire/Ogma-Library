# Phase 17 Scan Retry State Safety

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

`ScanHealthService.RetryJobAsync` previously changed any selected job to
pending, including completed or actively leased work. It also retained stale
completion, lease, next-attempt, failure-code, and error fields. That could
misreport state and permit duplicate work.

Single retry now selects failed jobs only. Single and bulk retry share one
normalization path that clears stale runtime ownership/failure fields, advances
the retry count, and schedules the pending job immediately. Completed and
running jobs remain unchanged.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ScanHealthTests"
Passed: 5, Failed: 0, Skipped: 0
```

## Gate disposition

The scan-health retry state-integrity and active-lease preservation sub-gates
are closed. Cooperative active cancellation remains handler-specific, and
physical full-application recovery/soak evidence remains open.
