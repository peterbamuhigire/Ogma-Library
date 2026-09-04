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
- Bounded exact-path navigation-graph materialization to 1,000 relevance-oriented
  candidates before client-side scoring, preventing common queries from loading
  an unbounded catalogue.
- Fuzzy fallback results now expose the local candidate value that matched as an
  explicit correction suggestion, while exact results leave the suggestion
  empty.
- Reduced exact-path graph materialization to the 50-result contract after
  verifying the named 50,000-book performance corpus; the metadata search p95
  gate now passes at <=150 ms.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `MetadataSearchServiceTests`: 8 passed, including the 50,000-book p95 <=150 ms
  benchmark.

## Remaining phase gate

Facets, paging, highlighting, and full-text fallback integration remain before
phase 22 closure. The named 50,000-book metadata-search performance sub-gate
is closed locally; reference-hardware confirmation remains a release concern.
