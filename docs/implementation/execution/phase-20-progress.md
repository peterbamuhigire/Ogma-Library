# Phase 20 Progress - Book Detail, Organisation and Reading State

Date: 2026-09-04

## Delivered in this increment

- Added `IBookCurationService` and `BookCurationService` for validated personal
  reading status, rating and favourite mutations.
- Added durable `ReadingStateHistory` snapshots containing only state values,
  a short non-content reason and timestamps; note/history text is not copied.
- Added rating/favourite integrity constraints and a book/time history index.
- Extended summary/detail projections with `IsFavourite` while retaining the
  relative-path and LAN-safe projection boundary.
- Added focused tests for persistence, progress-row creation, history capture,
  projection visibility, invalid rating and unknown-book handling.
- Wired the durable curation service into the desktop book-detail panel with
  status, 1–5 rating, and favourite controls. Each action is validated by the
  application service, refreshes the projection after persistence, and reports
  a localized success/failure state without exposing history text.
- Implemented bounded bulk tag add/remove persistence and audit projection;
  tag input is normalized case-insensitively and stored as user-owned metadata.
- Added a bounded book-detail tag editor that normalizes comma/semicolon/pipe
  input, persists through `ICatalogueWriteService.UpdateMetadataFieldAsync`,
  refreshes the projection, and exposes localized success/failure feedback.
- Added localized sidebar collection create/rename/delete controls with
  trimmed-name validation, selected-shelf protection, reload-after-mutation,
  and failure feedback through the existing catalogue write boundary.
- Added closed-contract smart-shelf query parsing and validation, persisted
  smart-shelf evaluation in catalogue reads, post-evaluation paging, dynamic
  smart-shelf counts, and fail-closed handling for damaged queries.
- Translated the closed smart-shelf condition set to server-side predicates for
  bounded catalogue selection and count queries, retaining projection
  evaluation as a defense-in-depth check.
- Added a bounded, SQLite-compatible reading-history query and a lazy detail-
  panel history load with localized loading, empty, error, status, favourite,
  and reason presentation.
- Added explicit file-availability state to book detail and fail-closed reader
  opening for missing files; catalogue metadata remains visible when the PDF
  is unavailable.
- Wired the existing safe `IBookFileLocator` and bounded
  `ITocExtractionService` into the detail model. The TOC is loaded only after
  the user requests it, is capped at 500 rows, and never parses when the file
  locator reports the file missing.
- Added a lazy provenance tab that materializes provenance-tracked metadata
  rows only after the user requests them, with English and French labels.
- Connected the folder-picker recovery path to `ILibraryRootService`: an
  existing durable root is repointed with `RelinkAsync` when the selected
  folder replaces its legacy settings path; otherwise the new root is ensured
  before settings persistence and scanning continue.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase20BookCurationTests`: 2 passed.
- `BookDetailCurationTests`: 4 passed, including rendered tag-editor controls
  and the write-boundary/refresh path; the class also contains the Phase 14
  review-panel proof.
- Smart-shelf focused verification: 24 passed; write-path shelf verification:
  6 passed.
- Reading-history core verification: 3 passed; BookDetail UI verification: 5
  passed.
- Full isolated solution suite: 880 core + 41 architecture + 142 UI = 1,063
  passed, 0 failed, 0 skipped. See
  `evidence/phase-20-smart-shelf-2026-09-04.md`.

- Current-HEAD full solution verification: 884 core + 41 architecture + 144
  UI = 1,069 passed, 0 failed, 0 skipped.
- Current-HEAD full solution verification: 883 core + 41 architecture + 142
  UI = 1,066 passed, 0 failed, 0 skipped.

- Current-HEAD Phase 20 detail-file/provenance verification: 2 UI-model tests
  passed, 0 failed, 0 skipped. The application build passed with 0 warnings
  and 0 errors.
- Current-HEAD shell/root-recovery verification: 15 focused UI tests passed,
  0 failed, 0 skipped. This includes the shell navigation regression slice;
  the OS folder-picker interaction itself remains platform evidence.

## Remaining phase gate

Physical folder-picker/relink recovery, accessibility and E2E evidence remain
before phase 20 closure. The detail-view status/rating/
favourite, bounded tag, collection CRUD, and local status/history presentation
sub-gates, plus lazy TOC/provenance presentation, are closed by the curation/
organisation UI increments.

The Aug-39 Definition of Done is reconciled as complete for Phase 20's local
implementation scope. Physical folder-picker/relink, accessibility, and full
journey acceptance remain later release gates and keep the phase overall
`IN PROGRESS`.
