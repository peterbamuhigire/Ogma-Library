# Phase 36 Backup and Restore Rehearsal

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

School administration now exposes an online SQLite backup service and a
non-destructive restore rehearsal. The rehearsal restores into an isolated
temporary database, runs `PRAGMA integrity_check`, and compares deterministic
schema and per-table row-count fingerprints with the source backup. It never
replaces the live catalogue.

Backup artifacts contain school data and therefore remain administrator-owned
sensitive files. Production procedures must place them in a protected,
access-controlled destination and prove recovery on the target platform.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SchoolBackupServiceTests"
Passed: 2, Failed: 0, Skipped: 0
```

The point-in-time test creates a live online backup, changes the live database,
rehearses the backup into a scratch database, proves the live change remains
untouched, and directly confirms the backup retained the pre-change audit row.
A corrupt backup is rejected and its temporary restore is cleaned.

## Gate disposition

The repository-level backup creation, integrity verification, point-in-time
content, and isolated restore-rehearsal sub-gates are closed. A physical
administrator-run backup/restore drill, protected-storage review, retention
policy, and recovery-time evidence remain `NOT ASSESSED`.
