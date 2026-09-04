# Phase 07 Progress - Discovery and Incremental Scanning

Date: 2026-09-04

## Delivered in this increment

- Added durable root-scoped `DiscoveryObservations` keyed by normalized relative
  path, including size, mtime and first/last-seen timestamps.
- Added root-relative `DirectoryCheckpoints` for completed discovery passes.
- Checkpoints now cover the root and every observed relative parent directory;
  observations retain the session that most recently saw them.
- Added `IncrementalDiscoveryService`, which starts a durable scan session,
  consumes bounded discovery output, persists observations, and queues changed
  files for downstream processing.
- Unchanged files are not re-queued, excluded folders remain outside the
  observation set, successful scans update root health history, and active
  sessions share content-versioned stage idempotency.
- Added acceptance tests for first scan, unchanged rescan, exclusions and root
  scoping.
- Added durable per-directory lifecycle state (`started`, `completed`,
  `failed`), resumable root cursors and a forward-compatible schema migration.
- Persisted directory diagnostics through independent catalogue contexts in the
  production path, so a process interruption retains the last completed cursor;
  the in-memory test constructor retains the same deterministic state model.
- Added stable SHA-256 downstream subject keys and cross-session completed-stage
  de-duplication, preventing a restart from re-queuing the same content version.
- Added diagnostics and restart acceptance tests, including unreadable-root
  reporting and resume-after-directory behavior.
- Added a bounded 50,000-file benchmark. The Windows run completed in 18.2
  seconds for the discovery stream and the acceptance test passed in 1 minute
  54 seconds including corpus creation and cleanup.
- Reduced large-scan filesystem overhead by reusing one `FileInfo` per ordinary
  file and checking reparse-point attributes before applying canonical boundary
  validation. The 50,000-file benchmark now completes without test-host crash.

## Remaining phase gate

Phase 7 scanner-core gates are closed by the implementation and acceptance
evidence above. Physical cross-platform filesystem permission behavior and UI
screen-reader walkthroughs remain platform gates for the later release review;
they are not silently treated as assessed by this Windows test run.
