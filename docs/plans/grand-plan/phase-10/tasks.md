# Phase 10 — Tasks

Work packages and tasks for Search & Indexing.

---

## WP1 — Schema & Migrations

**Goal:** `ExtractedPages`, `SearchChunks`, `SearchFts5` virtual table with
external-content triggers; `Books.IndexStatus` enum; `Jobs` table entries for
extraction failures.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP1-T1 | Add `Books.IndexStatus` column (`NotIndexed`, `Extracting`, `Indexed`, `Failed`); EF Core migration | 1 h | Phase 04 | FR-SEARCH-002 |
| P10-WP1-T2 | `ExtractedPages` table: `(Id UUID, BookId, PageIndex, Text, ExtractionQuality, WordCount, ContentHash, ExtractedAtUtc)`; unique index `(BookId, PageIndex)`; migration | 2 h | Phase 04 | FR-SEARCH-002 |
| P10-WP1-T3 | `SearchChunks` table: `(Id UUID, BookId, PageIndex, ChunkIndex, ChunkText, Source, CreatedAtUtc)`; index `(BookId)`; migration | 2 h | P10-WP1-T2 | FR-SEARCH-002, ADR-0006 |
| P10-WP1-T4 | FTS5 virtual table: `CREATE VIRTUAL TABLE SearchFts5 USING fts5(chunk_text, content="SearchChunks", content_rowid="Id", tokenize="unicode61 remove_diacritics 1")`; in EF Core raw SQL migration | 2 h | P10-WP1-T3 | FR-SEARCH-002, ADR-0006 |
| P10-WP1-T5 | FTS5 consistency triggers: `AFTER INSERT/UPDATE/DELETE ON SearchChunks` → maintain `SearchFts5` shadow tables | 2 h | P10-WP1-T4 | ADR-0006 |
| P10-WP1-T6 | `IExtractedTextStore` and `ISearchChunkRepository` interfaces; EF Core implementations; unit tests against in-memory SQLite | 2 h | P10-WP1-T3 | ADR-0006 |

**WP1 exit:** migrations pass; FTS5 `integrity_check` passes on empty table.

---

## WP2 — Metadata Search

**Goal:** debounced as-you-type metadata search; relevance score; P95 ≤ 150 ms
on 2,000-book corpus.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP2-T1 | `MetadataSearchService.SearchAsync(query, ct)`: parameterized SQL joining `Books`, `Authors`, `Tags`, `Shelves`; relevance score formula; ORDER BY score DESC; LIMIT 50 | 3 h | Phase 04, P10-WP1-T1 | FR-SEARCH-001, NFR-OGMA-003 |
| P10-WP2-T2 | Covering index `IX_Books_TitleAuthorIsbn` on `(Title, Author, ISBN)` to keep query plan efficient; add to Phase 04 migration or here | 1 h | P10-WP2-T1 | NFR-OGMA-003 |
| P10-WP2-T3 | `SearchViewModel` debounce: Rx `Throttle(150ms)` or manual `CancellationTokenSource` pattern on every keystroke; dispatch on `ThreadPool`; marshal results to UI thread | 2 h | P10-WP2-T1 | NFR-PROD-005 |
| P10-WP2-T4 | Search result `SearchResultItem`: `BookId`, `Title`, `Author`, `Score`, `MatchedFields[]`; displayed in result list | 1 h | P10-WP2-T3 | FR-SEARCH-001 |
| P10-WP2-T5 | Performance benchmark: `PerfBenchmark_MetadataSearch_P95` — seed 2,000-book corpus; run 50 random queries; assert P95 ≤ 150 ms | 2 h | P10-WP2-T1, P10-WP2-T2 | NFR-OGMA-003 |
| P10-WP2-T6 | Unit tests: exact-title match scores highest; author-only match scores correctly; empty query returns empty; special characters escaped | 2 h | P10-WP2-T1 | FR-SEARCH-001 |

**WP2 exit:** benchmark P95 ≤ 150 ms on both CI runners; result ordering correct.

---

## WP3 — Extraction Pipeline

**Goal:** idempotent, resumable per-book per-page PdfPig extraction; chunking
to `SearchChunks`; `ExtractionQuality` flag; `Jobs` table failures.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP3-T1 | `ExtractionPipelineService` as `IHostedService`: poll `Books WHERE IndexStatus = NotIndexed` on startup + rescan event | 2 h | Phase 05 `IBookScanEvent` | FR-SEARCH-002 |
| P10-WP3-T2 | Per-book extraction loop: open book with PdfPig; for each page: extract `TextWord[]`; compute `ExtractionQuality` (`Full/Partial/Empty/Scanned`); write `ExtractedPage`; skip if `ContentHash` matches existing row | 4 h | P10-WP1-T2, Phase 08 `TextLayerService` | FR-SEARCH-002, NFR-OGMA-009 |
| P10-WP3-T3 | Chunker: split `ExtractedPage.Text` into 512-token chunks with 64-token overlap; write `SearchChunk` rows; `Source = "page"`; `ChunkIndex` sequence | 3 h | P10-WP3-T2, P10-WP1-T3 | FR-SEARCH-002, ADR-0006 |
| P10-WP3-T4 | Annotation text indexing: after per-book page extraction, also chunk `AnnotationBodies WHERE BookId = @bookId AND Type = Note` into `SearchChunks` with `Source = "note"` | 2 h | P10-WP3-T3, Phase 09 | FR-SEARCH-002 |
| P10-WP3-T5 | Description/tag/TOC indexing: chunk `Books.Description`, joined tag names, and TOC entries (from PDFium outline) into `SearchChunks` with `Source = "description" / "tag" / "toc"` | 2 h | P10-WP3-T3 | FR-SEARCH-002 |
| P10-WP3-T6 | Failure handling: page extraction failure → `ExtractionQuality = Failed`; write `Jobs` row (`Type = ExtractionFailed, BookId, PageIndex, ErrorMessage`); continue with next page | 1 h | P10-WP3-T2 | FR-SEARCH-006 |
| P10-WP3-T7 | Mark `Books.IndexStatus = Indexed` only after all pages processed; no partial-indexed books visible as "Indexed" | 1 h | P10-WP3-T2 | FR-SEARCH-002 |
| P10-WP3-T8 | Resumability test: `ExtractionPipeline_Resume_NoDuplicates` — extract 10-book corpus; kill at book 5; restart; assert total `SearchChunk` count equals clean full run | 3 h | P10-WP3-T2, P10-WP3-T7 | NFR-OGMA-009 |

**WP3 exit:** pipeline is idempotent; resume test passes; all `Source` types populated.

---

## WP4 — FTS5 Index & Full-Text Search

**Goal:** FTS5 query returning ranked results within ≤ 500 ms P95 warm.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP4-T1 | `FtsIndexService.SearchAsync(query, ct)`: FTS5 `MATCH` query with `bm25()` ranking; JOIN back to `Books` for title/author; return `FtsSearchResult[]` (bookId, chunkId, snippet, score, source) | 3 h | P10-WP1-T4, P10-WP3-T3 | FR-SEARCH-002 |
| P10-WP4-T2 | Snippet extraction: `snippet(SearchFts5, 0, '<b>', '</b>', '…', 20)` for UI display | 1 h | P10-WP4-T1 | FR-SEARCH-002 |
| P10-WP4-T3 | Combined search result model: metadata results + FTS5 results; deduplicate by `BookId`; merge scores | 2 h | P10-WP2-T4, P10-WP4-T1 | FR-SEARCH-001, FR-SEARCH-002 |
| P10-WP4-T4 | FTS5 warm benchmark: `PerfBenchmark_FtsSearch_P95` — seed 2,000-book corpus with extracted text; run 50 phrase queries; assert P95 ≤ 500 ms | 2 h | P10-WP4-T1 | NFR-OGMA-004 |
| P10-WP4-T5 | FTS5 `integrity_check` utility method on `FtsIndexService`; called during G7 rebuild test | 1 h | P10-WP1-T4 | ADR-0006, G7 |
| P10-WP4-T6 | Integration tests: phrase in `simple-text` fixture found; match in `non-english` fixture with diacritics (unicode61 tokenizer); note text (source=note) found; tag found | 3 h | P10-WP4-T1, P10-WP3-T4 | FR-SEARCH-002 |

**WP4 exit:** FTS5 benchmark P95 ≤ 500 ms; `integrity_check` passes; multi-source search works.

---

## WP5 — Index Manager

**Goal:** dashboard showing real-time index status; rebuild action; G7 gate.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP5-T1 | `IndexManagerService.GetStatusAsync()`: return `IndexStatus` DTO with all counts; `IObservable<IndexStatusUpdate>` for real-time updates from extraction pipeline | 2 h | P10-WP3-T7 | FR-SEARCH-006 |
| P10-WP5-T2 | `IndexManagerService.RebuildAsync(ct)`: (1) begin transaction, delete `SearchChunks` rows and `ExtractedPages` rows, reset `Books.IndexStatus = NotIndexed`, commit; (2) trigger pipeline; (3) notify observers | 3 h | P10-WP3-T1, P10-WP4-T5 | FR-SEARCH-006, G7 |
| P10-WP5-T3 | `IndexManagerView`: dashboard with counts (indexed/total, pending OCR, failed, size); per-book extraction status list; "Rebuild index" button with confirmation dialog; progress bar during rebuild | 4 h | P10-WP5-T1 | FR-SEARCH-006 |
| P10-WP5-T4 | G7 reliability test: `IndexRebuild_CompletesWithoutDuplicatesOrCorruption` — (a) full index on 100-book corpus; (b) rebuild; (c) assert `SearchChunks` count identical; (d) `FtsIndexService.IntegrityCheck()` passes | 3 h | P10-WP5-T2, P10-WP4-T5 | G7, NFR-OGMA-009 |
| P10-WP5-T5 | Interrupted-rebuild recovery: kill DI scope during rebuild; restart; pipeline resumes from `NotIndexed` books; final state consistent | 2 h | P10-WP5-T2 | NFR-OGMA-009 |
| P10-WP5-T6 | Cancel rebuild: `CancellationToken` wired to "Cancel" button; pipeline stops at next page boundary; `Books.IndexStatus` left in consistent state | 1 h | P10-WP5-T2 | FR-SEARCH-006 |

**WP5 exit:** G7 test passes; interrupted rebuild recovers; cancel works.

---

## WP6 — UI, Icons, i18n, Accessibility

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP6-T1 | `search.en.resx` and `search.fr.resx`; externalize all search bar, result list, Index Manager labels, tooltips, and error messages | 3 h | Phase 03 i18n scaffold | I18N-STRATEGY.md |
| P10-WP6-T2 | Wire premium icons (or placeholders) for search bar, search-results panel, Index Manager, rebuild, filter-by-source chips | 2 h | icons.md, Phase 03 | ICON-SYSTEM.md |
| P10-WP6-T3 | Keyboard: `Ctrl+F` or `Ctrl+K` opens global search bar; result list navigable by arrow keys; Enter opens selected book; Escape closes | 2 h | WP2, WP4 | NFR-PROD-007 |
| P10-WP6-T4 | Automation peers: search result count announced; Index Manager status bar has `aria-label`; rebuild progress announced | 2 h | P10-WP5-T3 | NFR-PROD-008 |
| P10-WP6-T5 | Pseudolocale render: search bar, result list, Index Manager panel — no truncation | 1 h | P10-WP6-T1 | I18N-STRATEGY.md |

**WP6 exit:** pseudolocale clean; keyboard shortcuts work; SR announces result count.

---

## WP7 — Tests & Benchmarks

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P10-WP7-T1 | Architecture tests: `Architecture_Search_DoesNotDependOnReader`, `Architecture_Search_DoesNotDependOnAI` | 1 h | all WPs | Bounded-context discipline |
| P10-WP7-T2 | Full benchmark suite: metadata P95 + FTS5 P95 on 2,000-book corpus on both CI runners | 2 h | WP2, WP4 | NFR-OGMA-003, NFR-OGMA-004 |
| P10-WP7-T3 | Golden-corpus extraction: `simple-text`, `two-column`, `non-english`, `embedded-toc` all produce non-empty `SearchChunks`; `scanned-image-only` produces `ExtractionQuality = Scanned` | 2 h | WP3 | FR-SEARCH-002 |
| P10-WP7-T4 | G7 rebuild gate (P10-WP5-T4) confirmed in CI | 1 h | WP5 | G7 |
| P10-WP7-T5 | End-to-end smoke: type query in search bar → metadata results appear in < 150 ms; open Index Manager → counts accurate; trigger rebuild → completes; counts stable | 2 h | all WPs | FR-SEARCH-001, FR-SEARCH-006 |
| P10-WP7-T6 | CI matrix (Windows + macOS): `dotnet format`, `dotnet build`, `dotnet test` all pass | 1 h | all WPs | Global DoD §3 |
