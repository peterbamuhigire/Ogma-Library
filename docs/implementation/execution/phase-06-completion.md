# Phase 06 Completion - Processing State Machine and Scan Sessions

Date: 2026-08-30

## Delivered

- Added durable `ScanSessions` and `StageExecutions` records.
- Added explicit session and stage lifecycle enums instead of relying on generic
  job status integers.
- Added a unique session/stage/subject idempotency constraint.
- Added worker lease owner, expiry, attempt, retry time, typed error code and
  redacted error message fields.
- Added `ProcessingStateService` with atomic transactional claims, owner-checked
  completion/failure, retry scheduling, expired-lease recovery, cancellation,
  and deterministic session finalization.
- Registered the service in the ingestion composition module.
- Added the `Phase06ProcessingStateTests` acceptance suite.

## Acceptance evidence

The phase suite verifies:

1. duplicate stage enqueue returns one durable stage;
2. only one worker can claim a stage and only its owner can complete it;
3. retryable failures are reclaimed after their retry time and become terminal
   failures at the attempt limit;
4. expired leases are returned to the queue with a stable recovery code;
5. cancellation marks pending stages without deleting history and finalizes the
   session as cancelled.

The solution build and architecture suite also pass after the schema addition.
