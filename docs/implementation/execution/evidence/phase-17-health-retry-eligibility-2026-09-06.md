# Phase 17 Health Retry Eligibility

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

Library Health retry and batch resume previously left stale lease, completion,
failure, or future `NextAttemptUtc` state on work presented as resumed. A user
could activate Resume yet leave the job ineligible for immediate worker claim.

Failed-job retry and batch resume now share a complete runtime-state
normalization path. It clears ownership, completion and failure state, sets the
next attempt to the current time, and increments attempts only for failed work.
Paused work resumes without an artificial failure increment.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~HealthDashboardTests"
Passed: 10, Failed: 0, Skipped: 0
```

The regression covers a failed job with stale lease/completion fields and a
future retry schedule, plus batch resume with a future schedule. Both become
immediately eligible and contain no stale failure metadata.

## Gate disposition

The Library Health manual retry/resume eligibility sub-gate is closed. Active
generic cancellation remains unsupported until handlers implement cooperative
checkpoints, and physical full-application/soak evidence remains open.
