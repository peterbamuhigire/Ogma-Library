# Phase 20 Progress - Book Detail, Organisation and Reading State

Date: 2026-08-30

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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `Phase20BookCurationTests`: 2 passed.

## Remaining phase gate

Detail-view write controls, collections/tags/favourites UI, smart-shelf saved
queries, file/relink actions, complete status/history presentation, lazy TOC and
provenance tabs, and accessibility/E2E evidence remain before phase 20 closure.
