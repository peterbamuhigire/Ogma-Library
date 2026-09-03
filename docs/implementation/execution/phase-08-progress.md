# Phase 08 Progress - Filesystem Reconciliation and Recovery

Date: 2026-09-04

## Delivered in this increment

- Added `IFilesystemReconciliationService` and an evidence-gated implementation.
- Healthy roots with a completed discovery checkpoint reconcile only occurrences
  belonging to that root and session.
- Absent occurrences become unavailable only after a complete healthy pass;
  observed unavailable occurrences are restored.
- Root outages and incomplete/failed scans are explicitly non-mutating.
- Each availability transition writes a local audit event with a stable reason
  code and no filesystem path or PDF content.
- Changed observations carry a verified SHA-256. A unique exact-hash match can
  move an occurrence to its new path without duplicating identity; a same-path
  hash mismatch clears the stale asset binding and records a reprocessing need.
- Added acceptance tests for restore, absence, outage and incomplete-scan paths.
- Added a configurable 24-hour missing-file grace window with durable
  `MissingSinceUtc` evidence; reappearance clears the pending absence.
- Added a durable, path-local relocation review queue for ambiguous exact-hash
  matches. Ambiguous candidates are never guessed or relinked automatically.
- Added path-free counted audit summaries to reconciliation results and queued a
  new versioned `FileProcessing` stage when a verified replacement invalidates
  the previous content asset.
- Added schema coverage and acceptance tests for grace, ambiguity review and
  downstream invalidation.

## Remaining phase gate

The Phase 8 implementation gates are closed by the acceptance evidence above.
Physical disconnected-volume/ACL behavior, operator review UI, and cross-OS
walkthroughs remain platform/release gates and are not silently treated as
assessed by this Windows unit/integration run.
