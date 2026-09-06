# Phase 17 Progress - Worker Reliability and Observability

Date: 2026-09-04

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
- Refreshed the durable lease/runtime regression slice on 2026-09-04: all
  exclusive-claim, owner-enforcement, retry/dead-letter, expiry-recovery,
  resource-capacity, lifecycle-redaction, and metrics cases pass.
- Converted search extraction and embedding workers to claim durable
  `FtsReindexJob`/`SearchExtraction` and `EmbeddingJob`/`EmbeddingGeneration`
  triggers with five-minute leases, renewal, typed redacted failure outcomes,
  and a compatibility poll for legacy rows. New registrations enqueue search
  work and successful extraction enqueues an idempotent embedding trigger.
- Added restart-style recovery/load evidence: an orphaned lease is recovered
  after context disposal and 64 queued jobs drain through recreated workers.
- Added atomic, idempotent cancellation for pending shared-runtime jobs. A
  conditional state transition prevents a concurrent claim from being
  overwritten; actively leased work is rejected until handlers provide safe
  cooperative cancellation. Cancelled work is now counted in metrics and
  bounded diagnostics, and a payload-free audit event records the transition.
  Evidence: `evidence/phase-17-queued-cancellation-2026-09-06.md`.
- Added a Windows process-kill rehearsal for the production persistent PDF
  worker. The worker was terminated through the OS process API, the dead
  session surfaced an operation failure, and a new session rendered
  successfully. `PdfWorkerIsolationTests` passed 10/10; evidence:
  `evidence/phase-17-process-recovery-2026-09-05.md`.
- Protected-`main` CI run 34012939882 reproduced the same process-kill/restart,
  queued-cancellation, and complete core regression on Windows and macOS hosted
  runners.
- Separated paused work from dead letters with `JobRuntimeStatus.Paused = 6`
  and a targeted legacy-row migration. OCR pause/cancel/retry transitions are
  now atomic, clear stale leases, emit payload-free audit events, and are
  observed by the worker at page boundaries. Pause/resume retains completed
  pages without duplicate OCR; active cancellation prevents future pickup.
  Evidence: `evidence/phase-17-ocr-cooperative-control-2026-09-06.md`.
- Corrected generic batch pause to transition only queued enrichment work.
  Actively leased enrichment now remains running instead of being falsely
  labelled paused while its handler continues; resume also leaves that lease
  untouched. Evidence:
  `evidence/phase-17-generic-active-pause-safety-2026-09-06.md`.
- The complete serialized Release core suite passed 924/924 after the
  process-recovery increment; architecture and UI baselines remain green at
  41/41 and 159/159.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase17JobRuntimeTests`: 7 passed.
- Refreshed local lease/runtime evidence: 7 passed, 0 failed.
- Stabilized the restart/load benchmark so the post-restart worker reuses one
  database context while draining the bounded queue; the gate no longer
  measures 64 repeated context startups. The focused load test passed 1/1 on
  2026-09-06. Evidence:
  `evidence/phase-17-restart-recovery-load-2026-09-06.md`.
- Shared-runtime cancellation and stage-worker regression slice: 10 passed, 0
  failed, 0 skipped.
- Cooperative OCR control, migration, runtime, Index Manager, and Health
  Dashboard regression slice: 32 passed, 0 failed, 0 skipped.
- Library Health batch-control regression: 10 passed, proving unsupported
  active pause is rejected by state rather than misreported.
- Startup recovery now leaves another process's unexpired lease untouched while
  reclaiming legacy unleased and expired running rows. The recovery/runtime
  regression passed 12/12. Evidence:
  `evidence/phase-17-valid-lease-preservation-2026-09-06.md`.
- Scan-health retry now accepts failed jobs only, preserves running/completed
  work, and clears stale completion, lease, schedule, and failure state through
  one normalization path. The focused scan-health slice passed 5/5. Evidence:
  `evidence/phase-17-scan-retry-state-safety-2026-09-06.md`.
- Library Health retry and batch resume now clear stale lease/completion/failure
  state and override delayed schedules so user-resumed work is immediately
  claimable. The 10-test Health Dashboard slice passed. Evidence:
  `evidence/phase-17-health-retry-eligibility-2026-09-06.md`.
- Delivered the Activity Centre inside the existing Index Manager: bounded queue
  and failure summaries, recent redacted job states, refresh, safe pending-job
  cancellation, audited failed-job retry, and user-selected redacted JSON
  export. Dead-letter and active jobs remain visible without unsafe controls.
  The runtime slice passed 11/11, the acceptance contract passed 1/1, and
  Activity Centre view-model/render coverage passed 3/3. Evidence:
  `evidence/phase-17-activity-centre-2026-09-06.md`.
- Enforced the redacted diagnostic boundary at write time: runtime failure codes
  now accept only bounded lowercase machine-code characters. Paths, query
  strings, whitespace, and token-like free text cannot enter audit events or
  diagnostic exports through the failure-code field.

## Remaining phase gate

The local durable lease/runtime, queue-backed stage-worker, restart-style
recovery/load, Windows process-kill/restart, and pending-job cancellation
subgates are closed. Valid leases are no longer stolen during startup recovery.
Active OCR pause/cancel/resume is now cooperative at page
boundaries; unsupported active generic pause is fail-safe and leaves the lease
running, but those handlers do not yet expose safe cooperative cancellation.
The Activity Centre software surface is complete locally. Crash recovery under
the full application queue, physical reference-machine process behavior, and
long-duration soak evidence remain before phase 17 closure; the compatibility
poll is retained for pre-queue catalogue rows and is not a substitute for
production evidence.

The Aug-39 Definition of Done now records duplicate-execution prevention,
visible retry/dead-letter/cancel state, and structured-log redaction as closed.
Full-application kill/restart remains unchecked pending physical process and
complete-handler evidence.

The bounded local queue-throughput criterion is now closed by the 64-job
restart-style drain assertion. Long-duration soak and physical reference-machine
throughput remain separate release evidence and are not implied by this check.
