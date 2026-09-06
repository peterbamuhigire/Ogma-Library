# Phase 17 Generic Active-Pause Safety

Date: 2026-09-06

## Problem

The Library Health batch action previously changed both pending and actively
leased enrichment jobs to `Paused`. Unlike OCR, enrichment handlers do not yet
publish safe cooperative checkpoints. The database could therefore report a
running operation as paused while its external and persistence side effects
continued.

## Corrected invariant

Batch pause now transitions only pending enrichment jobs. An active leased job
retains `Running`, its lease ownership, and its current work unit. Resume
requeues paused jobs and explicitly failed jobs, but does not interfere with
the active lease.

This is fail-safe control behavior, not an assertion that active enrichment can
be paused. True active pause/cancel remains unavailable until each handler has
a safe checkpoint contract.

## Executable proof

`HealthDashboard_BatchEnrichmentPauseResumeAndFailedCsv_AreOperatorVisible`
starts with one pending, one running, and one failed batch job, then proves:

- pause changes only the pending job to `Paused`;
- the running job remains `Running`;
- resume requeues the paused and failed jobs;
- the running job remains untouched; and
- the failed job increments its retry count.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~HealthDashboardTests" --logger "console;verbosity=minimal" -m:1
Passed: 10, Failed: 0, Skipped: 0
```

## Residual gate

Active cooperative pause/cancel for enrichment, extraction, embedding, and
asset-generation handlers remains open. It must not be represented as complete
until those handlers define and test safe checkpoints.
