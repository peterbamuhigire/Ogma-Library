# Phase 16 Spine-Job Scheduling Evidence

Date: 2026-09-05

## Result

`BookRegistrationService` now schedules an idempotent `SpineGeneration` job
when a newly discovered PDF is registered and when a matched book receives a
new file version. `BookIngestionWorker` already claims and executes this job
through `ISpineService`, so both ingestion paths are now connected to the
existing spine-generation implementation.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DirectPdfOpenServiceTests|FullyQualifiedName~IngestionPipelineTests|FullyQualifiedName~IncrementalRescanTests"
  Passed: 19, Failed: 0, Skipped: 0
```

The regression slice asserts spine scheduling for new registration and
re-matched file versions, including the content-hash-derived idempotency key.

## Gate disposition

Closed: local ingest/update spine-job scheduling.

Still open: embedded/provider source acquisition, physical UI and
cross-platform evidence, and controlled large-library asset-budget evidence.
