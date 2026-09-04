# Phase 21 reader portability UI evidence

Date: 2026-09-04

## Scope closed locally

- The reader exposes Import reader state and Export reader state actions.
- Actions are disabled until a book is open and the portability service is
  available.
- Export uses the platform save picker and JSON file type.
- Import uses the platform open picker, accepts one JSON file, and invokes the
  same-book, versioned, 8 MiB-bounded application contract.
- Successful import refreshes bookmarks, annotations and reading memory in the
  open reader. Invalid, corrupt, foreign-book and inaccessible-file cases fail
  with localized status feedback rather than crashing the reader.

## Automated proof

- Isolated Release application build: passed, 0 warnings, 0 errors.
- Focused `ReaderView_ProvidesPageScrollViewerAndMagnificationControls`: passed;
  this proof now also asserts the portability action labels.
- Existing Phase 21 portability and cache/session tests remain green.

## Gate position

The local reader portability UI sub-gate is CLOSED. Split view, coordinate
fallback, platform viewer actions, physical accessibility/crash recovery, and
cross-platform performance-budget evidence remain open.
