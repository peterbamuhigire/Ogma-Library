# Phase 21 Progress - Reader Completion and Portability

Date: 2026-09-05

## Delivered in this increment

- Added `IReaderPortabilityService` and a versioned JSON export/import format for
  progress, reading memory, bookmarks and extended annotations.
- Export validates the owning book and writes only local reader-state records;
  import rejects unsupported versions and exports belonging to another book.
- Import is bounded to 8 MiB, clamps numeric state to safe ranges, limits text
  fields to their persistence contracts, and upserts stable bookmark/annotation
  identifiers idempotently.
- Registered the portability service in the reader composition module.
- Added round-trip, idempotence, same-book and persistence-boundary tests.
- Verified the local reader cache/session safety slice: cache hit, eviction,
  prefetch, cancellation, memory-budget, session navigation/close, and
  malformed-PDF non-crash behavior all pass in the focused regression run.
- Added bounded reader-import cardinality checks and normalized malformed JSON
  failures to `InvalidDataException` while preserving omitted-array compatibility.
- Added localized reader actions for exporting and importing the current book's
  reader state through the platform file picker. Import refreshes bookmarks,
  annotations and reading memory, and invalid or inaccessible files fail safely.
- Added headless reader UI proof for the import/export action labels and existing
  scroll, navigation and magnification controls.
- Replaced the split-view placeholder with two independent `ReaderViewModel`
  sessions in the desktop shell. The left pane reuses the primary reader; the
  right pane opens a user-entered book ID through its own session, preserving
  independent page, annotation and reading-state context.
- Added the versioned `normalized-v1` annotation-coordinate contract and a
  migration for existing rows. Omitted legacy versions fall back to the
  normalized representation; unsupported versions retain their marker but
  expose no regions, preventing incorrect overlays. Portable reader-state
  exports carry the coordinate version and imports reject unsupported data.
- Removed stale user-facing “scaffold” terminology from the implemented
  two-session split-reader route while retaining a compatibility alias for
  callers of the former method name.

## Verification

- Isolated `dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj
  --configuration Release --no-restore` passed with 0 warnings and 0 errors;
  the normal solution output was locked by already-running application/worker
  processes and was not disturbed.
- `Phase21ReaderPortabilityTests`: 2 passed.
- Page-render cache, reader-session, and PDF-worker isolation slice: 27 passed.
- Portability bounds slice: 3 passed; full isolated solution suite: 881 core +
  41 architecture + 142 UI = 1,064 passed, 0 failed, 0 skipped. See
  `evidence/phase-21-portability-bounds-2026-09-04.md`.
- Current-HEAD full solution verification: 883 core + 41 architecture + 142
  UI = 1,066 passed, 0 failed, 0 skipped.
- Current full solution verification after the Phase 19 increment: 885 core +
  41 architecture + 145 UI = 1,071 passed, 0 failed, 0 skipped. The focused
  reader UI proof passes after this increment.
- Current-HEAD split-view route verification: 1 UI test passed, 0 failed, 0
  skipped. The application build passed with 0 warnings and 0 errors.
- Current-HEAD annotation coordinate/migration verification: 36 reader tests
  passed, 0 failed, 0 skipped, including the Phase 09 persistence regression
  and Phase 21 legacy/unsupported-version cases.
- Current-HEAD split-reader route regression: 14 UI tests passed, 0 failed, and
  0 skipped.

## Remaining phase gate

Platform viewer actions,
physical Narrator/VoiceOver journeys, and cross-platform performance budgets
remain before phase 21 closure. The local automated cache/session/non-crash and
reader import/export UI sub-gates are closed; physical crash recovery evidence
remains unassessed.

The Aug-39 Definition of Done now records the functional independent-session
split view as closed. The combined annotation/export/crash criterion remains
unchecked because physical crash recovery is still `NOT ASSESSED`, even though
the local annotation and portability round trips pass.
