# Phase 17 Evidence - Worker Lease Routing and Heartbeats

Date: 2026-09-04

## Delivered

- `BookIngestionWorker` now claims the supported ingestion job types through
  `IJobRuntimeService`.
- Completion and failure are owner-checked runtime commands; failure uses a
  typed bounded diagnostic and retry policy.
- A periodic heartbeat renews the five-minute lease while a job is executing.
- Direct pending-row polling and direct running/completed status mutation were
  removed from the ingestion worker.
- Poison/unsupported failures can now be explicitly quarantined in the durable
  `DeadLetter` state without retry.

## Verification

```text
dotnet build src/OgmaLibrary.Workers/OgmaLibrary.Workers.csproj --configuration Release --no-restore
  Passed: 0 warnings, 0 errors

dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~Phase17JobRuntimeTests|FullyQualifiedName~JobManagementTests|FullyQualifiedName~Phase15WriteBackSafetyTests" --verbosity minimal -m:1
  Passed: 6, Failed: 0, Skipped: 0
```

## Open gates

OCR, embedding, search-extraction and remaining polling workers still require
the runtime conversion. Resource-group limits, poison/dead-letter handling,
structured metrics/diagnostics export, and kill/restart load evidence remain
open; only the poison quarantine state is implemented in this increment.
