# Phase

Phase 4 - Identity Schema and Data Migration

# Status

COMPLETE - 2026-08-20

# Requirements Implemented

- Added canonical SQLite persistence for roots, file occurrences, content assets,
  works, editions, catalogue items, scoped identifiers, identity decisions and
  legacy aliases.
- Added constraints and indexes for identity shape, hash/version uniqueness,
  root-relative locator uniqueness, owner scope and decision validity.
- Added an idempotent, 500-row transactional legacy backfill with preflight,
  conflict counts, alias checkpoints and canonical identity projections.
- Preserved legacy Books/search/reader/classroom foreign keys while giving new
  consumers an explicit canonical repository contract.

# Major Code Changes

- Added canonical persistence rows in
  `src/OgmaLibrary.Infrastructure/Catalogue/Entities/CanonicalIdentityRows.cs`.
- Added all EF configuration and checks in
  `src/OgmaLibrary.Infrastructure/Catalogue/Configurations/CanonicalIdentityConfiguration.cs`.
- Added generated migration
  `20260820101651_Phase04CanonicalIdentity`.
- Added `CanonicalIdentityMigrationService`, redacted preflight/progress/result
  records and shared Crockford ID generation.
- Added `ICanonicalIdentityRepository` and the path-free EF projection adapter;
  kept the legacy repository explicitly temporary.
- Added SQLite API backup/integrity verification and restore-sidecar cleanup.
- Added startup migration progress text and determinate progress bar while
  retaining the recoverable blocked shell.

# Database Changes

The generated migration adds eleven canonical/compatibility relations and their
foreign keys, checks and indexes. Legacy schema tables remain untouched for safe
compatibility. A later cleanup phase may retire them only after every consumer has
migrated and curated-field preservation is proven.

# Pipeline Changes

Migration now runs schema application -> integrity-safe canonical preflight ->
transactional backfill -> startup readiness. Each batch reports counts and can
be safely re-entered via `LegacyIdentityAliases`.

# Search Changes

Legacy BookIds remain resolvable through aliases. Existing embeddings are not
deleted; their book status is marked stale so later index/embedding phases can
rebuild with canonical IDs.

# AI/RAG Changes

No provider or model calls occur during migration. Canonical projections expose
semantic-reindex-required state without transmitting source content.

# UI Changes

Startup displays redacted canonical migration progress and keeps catalogue hidden
until required migration succeeds. Failed migration remains retryable and
exportable through the existing recovery shell.

# 3D Changes

No renderer state is duplicated. Saved legacy IDs remain resolvable through the
alias table until the 3D contract adopts catalogue item IDs.

# Security/Privacy Changes

- SQLite backup uses a local verified snapshot and never copies source PDFs.
- Migration progress and reports contain counts/stages/conflict totals only.
- Provider IDs are source-scoped; invalid identifiers are not promoted.
- Backup restore closes connections and removes only the exact database sidecars.

# Tests Added

- Canonical migration fixture preserving rating, unavailable state, aliases,
  occurrence links, unknown hash, stale embedding state and progress.
- Restart/idempotency fixture proving no duplicate graph on re-entry.
- SQLite check-constraint fixture rejecting malformed SHA-256 and invalid root
  state.
- Existing migration backup, missing-table repair, downgrade/remigration and
  startup render suites rerun after schema expansion.

# Evaluations Performed

- Re-read Phase 4 and Phase 5 dependencies plus SRS, database roadmap, backup,
  testing and design/error-state requirements before schema work.
- Rehearsed clean migration, legacy fixture backfill, second-run re-entry,
  invalid-identity rejection and projection lookup.
- Verified generated migration Up/Down exists and source PDFs are outside all
  migration paths.

# Performance Results

- Release solution build: 0 warnings, 0 errors (3.21 seconds after incremental
  rebuild).
- Phase-specific migration and backup suite: 9/9 core tests passed.
- Startup progress UI suite: 3/3 passed.
- Final sequential Release regression: 835/835 passed - 41 architecture, 665
  core/service/database/performance and 129 headless UI tests. The canonical
  completion short-circuit reduced the core suite from 4m10s to 2m27s.
- 2,000/50,000 synthetic migration benchmarks remain scheduled for the roadmap's
  later release/performance gate; Phase 4 uses 500-row checkpointing to bound
  transaction and memory cost.

# Deviations From Plan

- Existing numeric Work/Edition tables are retained as legacy compatibility
  tables because earlier migrations and curated foreign keys still reference
  them. Canonical tables are explicitly named `CanonicalWorks` and
  `CanonicalEditions`; removal is deferred until all consumers switch.
- The roadmap's "up/down" requirement is satisfied by generated EF Down plus
  verified backup restore for data-preserving forward recovery. Newly captured
  canonical data is never destroyed by an automatic downgrade.

# Deferred Findings

- Phase 5 owns real root paths, volume identity, path authorization and symlink
  policy; the compatibility root is intentionally pathless.
- Phases 7-9 own discovery/reconciliation and reversible bibliographic merge or
  split decisions.
- Later search/embedding phases own physical reindex and vector migration.
- Legacy table retirement requires a later consumer inventory and preservation
  proof.

# Kaizen Cleanup

- Centralized ULID-style ID generation previously duplicated in ingestion.
- Removed path-derived identity assumptions from migration and retained explicit
  unknown state.
- Added a single canonical repository projection rather than exposing EF rows to
  Application/UI.
- Added backup integrity verification and redacted progress observability.

# Definition of Done Verification

- [x] Legacy fixture migrates with curated fields and availability preserved.
- [x] Constraints reject invalid identity combinations.
- [x] Generated Down and verified backup restore provide rollback/forward recovery.
- [x] Alias and stale-reindex handoff works without deleting old IDs/vectors.
- [x] Phase 3 domain freeze is recorded and persistence freeze is documented.
