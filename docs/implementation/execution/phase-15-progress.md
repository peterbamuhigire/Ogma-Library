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

## Remaining phase gate

The locally reproducible Windows ACL and cancellation-interruption subgate is
closed. Process-kill interruption and cross-platform permission evidence remain
before phase 15 closure. The first-class durable writeback-plan and explicit
consent UI gates are closed by the restart-style, safety, and detail-panel
evidence above.
