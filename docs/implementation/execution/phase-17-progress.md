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
- Added an explicit `DeadLetter` lifecycle for poison/unsupported jobs so they
  are quarantined without consuming retry attempts.
- Added structured local lifecycle audit events for claim, completion, failure,
  and expired-lease recovery. Event payloads contain job type, attempt, stable
  failure code, and retry state, but never job payloads or exception text.
- Added bounded resource-group capacity during atomic claims for document
  rendering, metadata indexing, and semantic indexing; unknown job types default
  to one active lease per type.
- Converted the OCR job processor from direct queue polling to the shared lease
  runtime while preserving resumable page progress and safe retry/dead-letter
  handling. Legacy running rows without lease metadata remain recoverable.
- Added focused coverage for exclusive claims, owner enforcement, retry versus
  terminal failure, and expiry recovery.
- Added a payload-free runtime metrics snapshot exposing status totals, attempt
  totals, and active leases grouped by job type for diagnostics and UI use.
- Added a bounded JSON diagnostics export containing only operational metrics
  and recent lifecycle fields; payloads, lease owners, and error text are
  excluded.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase17JobRuntimeTests`: 7 passed.

## Remaining phase gate

The search-extraction and embedding workers remain stage-based rather than
job-queue workers; kill/restart load evidence remains before phase 17 closure.
