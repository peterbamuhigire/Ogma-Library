# Phase 15 Progress - Safe Writeback and Override Protection

Date: 2026-08-30

## Delivered in this increment

- In-root writeback validation now uses the shared canonical path authority,
  eliminating prefix-confusion acceptance for normal library files.
- A writeback token is bound to the source SHA-256 captured at backup/preview;
  writeback rejects the operation if the source changes before mutation.
- Added regression coverage for source-change rejection while preserving the
  existing backup/restore workflow.
- Preparation now writes a durable `WriteBackPrepared` audit event containing
  the source hash and backup locator before any write is attempted.
- The write path performs an exclusive-access check, resets derived search and
  embedding statuses after a successful content change, and records whether a
  failed operation restored the original bytes.
- Added an explicit `RestoreBackupAsync` undo command that validates the trusted
  backup location, verifies the restored PDF before replacement, resets derived
  indexes, retains the backup, and records success/failure audit events.
- Added an atomic, trusted `.ogma/writeback-plans` record that persists the
  prepared backup token across service recreation and records prepared, written,
  or restored lifecycle status. Plan loading validates book identity, status,
  original-path policy, and backup containment before resumption.

## Delivered in the current increment

- Added an explicit desktop writeback-consent flow to book detail: users first
  preview supported PDF metadata differences, then a backup is prepared, and
  only a separate confirmation action calls the reversible write boundary.
  Cancellation performs no PDF mutation; restore remains available for a
  prepared or failed operation.
- Added backup-byte integrity verification using the prepared token's original
  SHA-256 before plan resumption, writeback, and restore. A missing or tampered
  backup now fails closed without mutating the original PDF, with regression
  coverage for both write and restore paths.
- Replaced whole-file hash allocation and direct final-path backup copying with
  asynchronous streaming hash/copy operations, byte verification, temporary-
  file cleanup and atomic promotion. A pre-cancelled preparation leaves no
  backup or durable writeback plan.
- Removed delete-before-move replacement from writeback and undo. Verified PDFs
  now use same-directory overwrite promotion, while failure restoration stages,
  integrity-checks, and verifies a recovery copy before promotion. Evidence:
  `evidence/phase-15-atomic-writeback-promotion-2026-09-06.md`.

## Current verification

- Current-HEAD writeback-consent UI-model verification: 3 tests passed, 0
  failed, 0 skipped. The application build passed with 0 warnings and 0 errors.
- Added a real Windows temporary-directory ACL rehearsal and a pre-cancelled
  writeback regression. `PdfWriteBackTests` passed 8/8: permission denial and
  cancellation both failed closed without changing the original PDF, while
  the prepared backup and failure audit path remained available. Evidence:
  `evidence/phase-15-windows-writeback-failure-rehearsal-2026-09-05.md`.
- The complete serialized Release core suite passed 923/923 after the
  writeback failure-recovery increment; architecture and UI baselines remain
  green at 41/41 and 159/159.
- Focused backup-integrity verification passed 4/4 (`Phase15WriteBackSafetyTests`)
  on 2026-09-06. Evidence:
  `evidence/phase-15-backup-integrity-2026-09-06.md`.
- The combined writeback regression slice passed 13/13 after streaming backup
  preparation was added. Evidence:
  `evidence/phase-15-streaming-backup-2026-09-06.md`.
- The same 13-test slice passed after atomic source/undo/failure-recovery
  promotion replaced the destructive delete/copy windows; failure recovery
  also leaves no temporary recovery file.

## Remaining phase gate

The locally reproducible Windows ACL and cancellation-interruption subgate is
closed. The known delete-before-move and direct-copy interruption windows are
also closed in code. Physical process-kill interruption and cross-platform
permission evidence remain before phase 15 closure. The first-class durable
writeback-plan and explicit
consent UI gates are closed by the restart-style, safety, and detail-panel
evidence above. The streaming hash/copy and partial-backup cleanup subgate is
also closed by the focused preparation evidence.

The Aug-39 Definition of Done is reconciled as complete for the implemented
writeback contract. Phase 15 remains `IN PROGRESS` for physical process-kill
interruption and cross-platform permission evidence; those release gates are
not inferred from local fault injection.
