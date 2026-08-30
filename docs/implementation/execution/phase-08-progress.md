# Phase 08 Progress - Filesystem Reconciliation and Recovery

Date: 2026-08-30

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

## Remaining phase gate

Grace windows, ambiguity review, full reconciliation audit summaries, and
downstream asset-stage invalidation remain to be delivered before phase 8 can be
marked complete.
