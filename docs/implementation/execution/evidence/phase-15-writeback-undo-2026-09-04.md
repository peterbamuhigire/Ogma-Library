# Phase 15 write-back undo evidence

Date: 2026-09-04

`IMetadataWriteBackService.RestoreBackupAsync` now provides an explicit undo
operation over a prepared backup token. It rejects backups outside the trusted
`.ogma/backups` directory, verifies the backup as a readable PDF in a temporary
file, replaces the current PDF only after verification, retains the backup, and
resets search/embedding derived-state markers. Both successful and failed undo
attempts are recorded locally without embedding raw PDF content in the audit.

The remaining Phase 15 gates are a first-class durable plan record, explicit
consent UI, and physical interruption/permission evidence.
