# Phase 21 Progress - Reader Completion and Portability

Date: 2026-09-04

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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase21ReaderPortabilityTests`: 2 passed.
- Page-render cache, reader-session, and PDF-worker isolation slice: 27 passed.
- Portability bounds slice: 3 passed; full isolated solution suite: 881 core +
  41 architecture + 142 UI = 1,064 passed, 0 failed, 0 skipped. See
  `evidence/phase-21-portability-bounds-2026-09-04.md`.

## Remaining phase gate

Functional split view, complete import/export UI, coordinate-version fallback,
platform viewer actions, physical Narrator/VoiceOver journeys, and
cross-platform performance budgets remain before phase 21 closure. The local
automated cache/session/non-crash subgate is closed; physical crash recovery
evidence remains unassessed.
