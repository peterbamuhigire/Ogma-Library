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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- `ExtractionPipelineServiceTests`: 7 passed.
- `Phase11ExtractionArtifactTests`: 2 passed.
- `FtsIndexServiceTests`: 6 passed.
- `IndexManagerServiceTests`: 6 passed.

## Remaining phase gate

The complete phase still requires
highlight-safe snippets with explicit page-jump contracts, UI full-text mode
and reader navigation, progress/no-index states, observability metrics,
side-by-side rebuild swap, and the 50,000-book latency benchmark.
