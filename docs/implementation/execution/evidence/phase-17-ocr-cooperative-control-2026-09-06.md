# Phase 17 Evidence - Cooperative OCR Pause and Cancellation

Date: 2026-09-06

## Problem and invariant

The OCR controls previously changed a running job directly to numeric status 4
or 5 while the worker continued processing. Status 5 also meant `DeadLetter`
to the shared runtime, so a paused OCR or enrichment job was operationally
indistinguishable from quarantined poison work.

The required invariant is: a control must never report that active work has
stopped while the handler continues across further safe work units.

## Critical flow

| Actor/trigger | Happy path | Failure protection | Operator-visible evidence |
| --- | --- | --- | --- |
| User pauses queued/running OCR | Atomic transition to `Paused`; lease cleared | Conditional update prevents overwrite of unrelated/terminal state | Paused status, stable `paused_by_user` code, redacted audit event |
| User cancels queued/running OCR | Atomic transition to `Cancelled`; retry and lease cleared | Worker checks durable state between pages and before final indexing | Cancelled status, completion time, stable code, redacted audit event |
| User resumes paused OCR | Atomic transition back to `Pending` | Existing page checkpoints are retained; completed OCR pages are skipped | Pending status and retry audit event |
| Legacy paused row is upgraded | OCR/Enrich status 5 becomes status 6 | Other status-5 rows remain `DeadLetter` | Migration regression distinguishes all three rows |

## Implementation evidence

- `JobRuntimeStatus.Paused = 6` now has a distinct durable value.
- Runtime metrics and bounded diagnostic JSON expose `pausedCount` separately
  from `deadLetterCount`.
- Migration `20260906060000_Phase17PausedJobStatus` updates only legacy
  `OcrJob` and `Enrich` rows from 5 to 6; unrelated dead letters stay at 5.
- OCR pause/cancel/retry commands use conditional database updates and clear
  stale lease/retry fields in the same transition.
- Every successful control writes one local payload-free audit record.
- `OcrJobProcessor` checks the durable control state before each page and
  before final chunk/index projection. A page already inside OCR may complete;
  no subsequent page starts after the checkpoint observes pause/cancel.

## Verification

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj \
  --configuration Release --no-restore \
  --filter "FullyQualifiedName~Phase17JobRuntimeTests|FullyQualifiedName~OcrJobProcessorTests|FullyQualifiedName~IndexManagerServiceTests|FullyQualifiedName~HealthDashboardTests" \
  --logger "console;verbosity=minimal" -m:1

Passed: 32
Failed: 0
Skipped: 0
Duration: 21 seconds
```

The active-pause regression blocks the first OCR page, requests pause, releases
that page, verifies the worker stops at one page, resumes, and verifies only two
remaining pages are processed. The active-cancel regression performs the same
interleaving and proves the cancelled job cannot be claimed again.

## Gate disposition

The active OCR cooperative pause/cancel/resume subgate and paused/dead-letter
status-separation migration subgate are closed locally. Generic non-OCR
handlers still require handler-specific safe checkpoints before active
cancellation can be claimed. Full-application crash, physical reference,
activity-centre and soak gates remain open.

Rollback of the data-only migration maps paused OCR/Enrich rows back to legacy
status 5. No user PDF, OCR text, or page checkpoint is deleted by either path.
