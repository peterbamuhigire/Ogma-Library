# Phase 4 recovery runbook

1. Keep the generated `.bak` beside the local catalogue file until the migration
   is accepted. The backup is user-deletable after verification.
2. If startup reports `catalogue.migration` failed, do not rescan or modify the
   source PDF folders. Retry after checking free disk space and file access.
3. `CatalogueMigrator` verifies the backup and restores it after closing SQLite
   connections. The original exception is reported as a safe startup failure.
4. If restore itself fails, preserve the `.bak` and export redacted diagnostics;
   do not delete either database file. Copy the verified backup to a new working
   path only after SQLite integrity check succeeds.
5. Re-run startup. Canonical aliases are checkpoints, so completed batches are
   not duplicated. A partially created batch is transactional and rolls back.
6. After successful startup, inspect preflight counts and rebuild stale search or
   embedding indexes in their scheduled phases. Do not manually merge provisional
   works or editions during migration.

No command in this runbook opens or sends PDF contents, and no path is written to
general telemetry.
