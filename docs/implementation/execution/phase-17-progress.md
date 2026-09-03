# Phase 17 Progress - Worker Reliability and Observability

Date: 2026-08-30

## Delivered in this increment

- Added `IJobRuntimeService` and a durable job lease implementation over the
  existing `Jobs` queue.
- Added lease owner/expiry, retry due time and stable failure-code fields with
  migration/index support.
- Job claims are atomic and bounded; a second worker cannot claim an active
  lease, and completion/failure requires the owning worker.
- Retryable failures are returned to the queue with a bounded backoff; attempts
  become terminal failures at the configured maximum.
- Expired leases are recoverable without deleting job history.
- Converted `BookIngestionWorker` to claim, complete and fail through
  `IJobRuntimeService` instead of directly polling/mutating job rows.
- Added periodic lease renewal for long-running ingestion, with worker-owned
  completion and bounded typed failure codes.
- Added focused coverage for exclusive claims, owner enforcement, retry versus
  terminal failure, and expiry recovery.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase17JobRuntimeTests`: 3 passed.

## Remaining phase gate

The remaining polling workers still need conversion to this runtime, and
resource-group limits are not yet implemented. Poison/dead-letter handling,
structured redacted events/metrics, diagnostics export, and kill/restart load
evidence remain before phase 17 closure.
