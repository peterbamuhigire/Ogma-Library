# Phase 22 Progress - Structured and Fuzzy Catalogue Search

Date: 2026-08-30

## Delivered in this increment

- Preserved the existing exact, prefix and contains search path as the fast
  path for catalogue metadata queries.
- Added a bounded 128-character query guard so pathological input cannot
  trigger an unbounded fallback scan.
- Added an explainable fuzzy fallback that runs only when the exact path
  returns no matches. It reads scalar book/title/first-author candidates,
  compares the full value and punctuation-delimited tokens with Levenshtein
  distance, applies a bounded distance threshold, ranks deterministically and
  caps the response at 50 results.
- Kept wildcard escaping and existing result-field explanations intact for
  the exact path; fuzzy matches identify their source field with
  `title:fuzzy` or `author:fuzzy`.
- Added a regression fixture proving that the typo `tolkein` finds a book by
  `J.R.R. Tolkien`.
- Added bounded structured field parsing for `title:`, `author:`, `isbn:`,
  `shelf:`, `description:`, and `tag:` queries while retaining the broad exact
  and fuzzy fallback paths.
- Added a regression fixture proving an author-scoped query does not match a
  title-only occurrence.
- Verified the global search view's 150 ms type-ahead debounce, cancellation,
  stale-result suppression, and selected-result navigation in an Avalonia UI
  regression test.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `MetadataSearchServiceTests`: 7 passed.

## Remaining phase gate

Facets, paging, highlighting, richer
correction suggestions, full-text fallback integration and the 50,000-book
search benchmark remain before phase 22 closure.
