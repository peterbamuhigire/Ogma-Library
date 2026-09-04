# Phase 23 Progress - Full-Text Pipeline and Search

Date: 2026-08-30

## Delivered in this increment

- Linked extracted pages to an explicit extractor version and search chunks to
  both the completed extraction artifact and index version.
- Persisted deterministic extraction manifests so a successful page/chunk
  projection can be identified and invalidated when source content changes.
- Added stale full-text cleanup for unavailable books, orphan page anchors,
  obsolete extraction hashes and unsupported index versions; FTS integrity is
  checked after cleanup.
- Added a durable rebuild checkpoint table. Index rebuilds now resume from a
  running checkpoint after interruption, update counters after each batch and
  close with a completed status. Terminally failed books are not retried
  indefinitely unless their source state changes.
- Repaired the SQLite migration edge in which adding the artifact foreign key
  rebuilds `SearchChunks` and drops external-content FTS triggers. A follow-up
  migration recreates the triggers and rebuilds FTS content.
- Full-text results now reject inactive books, stale artifact hashes and old
  index versions while preserving legacy unversioned rows.
- Added bounded source-scoped full-text grammar for `page:`, `note:`, `tag:`,
  `description:`, and `toc:` queries, applied as a SQL source filter.
- Added regression coverage proving source prefixes do not mix page and note
  matches.
- FTS snippets now expose plain text plus bounded highlight spans instead of
  passing markup through the result contract. Page-derived hits also expose an
  explicit validated reader page-jump target.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `ExtractionPipelineServiceTests`: 7 passed.
- `Phase11ExtractionArtifactTests`: 2 passed.
- `FtsIndexServiceTests`: 6 passed.
- `IndexManagerServiceTests`: 6 passed.
- `SearchSnippetParserTests`: 2 passed.
- `SearchViewModelTests`: 13 passed, including desktop full-text mode
  indication, result selection, reader navigation with page hint, and
  degraded exact-search behavior.
- Search state now distinguishes a ready result window, no matches, no local
  semantic index, and degraded exact fallback. The desktop panel displays the
  state through a polite status line rather than leaving an empty result list
  unexplained.
- The Avalonia search UI suite passed 14 tests, including the no-index status;
  the semantic-service suite passed 5 tests across ready and fallback paths.
- `IndexManagerServiceTests`: 6 passed, covering durable rebuild checkpoint
  counters, status publication, failure/recovery lifecycle events, and
  rebuild-duration/read-model observability.
- The 50,000-book FTS latency benchmark passed its p95 <=500 ms assertion on
  the local Windows test environment.
- Added an explicit staged extraction capability. Production rebuilds now keep
  the active `fts5-v1` rows readable, write a unique rebuild generation beside
  them, and promote that generation transactionally only after a healthy,
  failure-free rebuild. A failed staged run leaves the active generation in
  place.
- Added an integration regression proving the active index remains searchable
  during staging and that only the promoted generation is visible afterward.

## Remaining phase gate

The side-by-side rebuild/swap backend subgate is closed by the staged pipeline
and integration evidence. Reference-hardware confirmation and physical
assistive-technology walkthroughs remain `NOT ASSESSED`.
