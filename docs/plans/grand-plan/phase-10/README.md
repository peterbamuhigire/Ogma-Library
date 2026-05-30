# Phase 10 — Search & Indexing

Single-sentence mission: deliver metadata search within ≤ 150 ms P95 on
2,000 books, a FTS5 external-content index over all extracted text/notes/tags/
descriptions/TOC, a reliable extraction pipeline with per-page quality flags,
and an Index Manager UI surfacing progress/counts/errors/index size — with an
index rebuild gate (public-beta gate G7).

---

## 1. Title & one-line mission

**Phase 10 — Search & Indexing**
Realize the Search Index bounded context: instant metadata search
(FR-SEARCH-001, NFR-OGMA-003), FTS5 full-text index over all corpus text
(FR-SEARCH-002), the extraction pipeline (PdfPig per-page, ExtractionQuality
flag), and the Index Manager dashboard (FR-SEARCH-006), all with LAN-projection-
ready read-models for Phase 16.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP (FR-SEARCH-001) · V1 (FR-SEARCH-002, FR-SEARCH-006) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD original Phase 5 (Search) |
| **Platforms** | Windows 10+ + macOS 12+; CI on both |
| **Status** | Planned — not started |
| **Depends on** | Phase 04 (Catalogue schema; `Books`, `ExtractedPages`, `SearchChunks`), Phase 05 (book identity / content hash), Phase 08 (`TextLayerService`, `ExtractionQuality` flag), Phase 09 (annotation text available for indexing) |
| **Unblocks** | Phase 11 (Semantic Search — uses the same `SearchChunks` + `ExtractedPages` schema), Phase 13 (AI advisor reads index), Phase 16 (LAN host serves search read-model) |

---

## 3. Objectives

1. Metadata search (title, author, tags, ISBN, shelf) returns results within
   ≤ 150 ms P95 on a 2,000-book catalogue while the user types — no explicit
   submit required (FR-SEARCH-001, NFR-OGMA-003).
2. A FTS5 external-content index covers all extracted page text, annotation
   notes, tags, descriptions, and TOC entries; full-text search returns results
   within ≤ 500 ms P95 warm on 2,000 books (FR-SEARCH-002, NFR-OGMA-004).
3. The extraction pipeline processes each book's pages with PdfPig, stamps every
   page with an `ExtractionQuality` flag, and stores extracted text in `ExtractedPages`
   and chunked records in `SearchChunks` for both FTS5 and Phase 11 embeddings.
4. The Index Manager UI shows: indexed-book count, pending-OCR count, failed-
   extraction count, index size on disk, per-book extraction status, and a
   manually triggered rebuild action (FR-SEARCH-006, public-beta gate G7).
5. Index rebuild from source (G7) completes without duplicate chunks or corrupt
   FTS5 index; an interrupted rebuild recovers without manual intervention
   (NFR-OGMA-009).
6. The search read-model (`ISearchReadModel`) is designed as a LAN-projection-
   ready interface so Phase 16 can serve search over the LAN without a rewrite
   (LAN-CLASSROOM-ARCHITECTURE.md §3).
7. Metadata search, FTS5 search, and the extraction pipeline run on background
   workers without blocking the UI thread (NFR-PROD-005).

---

## 4. Scope

### In scope

- `SearchIndex` bounded context: `MetadataSearchService`, `FtsIndexService`,
  `ExtractionPipelineService`, `IndexManagerService`.
- SQLite FTS5 virtual table (`SearchFts5`) as an external-content table referencing
  `SearchChunks` (ADR-0006).
- `ExtractedPages` table: `(Id, BookId, PageIndex, Text, ExtractionQuality,
  ExtractedAtUtc, WordCount, ContentHash)`.
- `SearchChunks` table: `(Id, BookId, PageIndex, ChunkIndex, ChunkText,
  Source [page/note/tag/description/toc])`.
- FTS5 `content=SearchChunks` table with `content_rowid`; triggers for insert/
  update/delete consistency (ADR-0006).
- Metadata search: parameterized SQL LIKE / FTS5 prefix query across `Books`
  fields; debounced to 150 ms; results sorted by relevance score.
- Extraction pipeline: background `IHostedService` worker; per-book, per-page
  PdfPig text extraction; `ExtractionQuality` flag (`Full`, `Partial`, `Empty`,
  `Scanned`); idempotent / resumable per book (NFR-OGMA-009); persists to
  `ExtractedPages`; chunks into `SearchChunks`.
- Index Manager UI: dashboard panel; progress bars, counts, error list;
  "Rebuild index" action with confirmation; rebuild is cancellable and
  recoverable.
- Beta gate G7: `IndexRebuild_CompletesWithout DuplicatesOrCorruption`
  reliability test.
- `ISearchReadModel` interface emitting `SearchIndexEvent` (book indexed, book
  failed, index rebuilt) — LAN-projection-ready; no LAN built here.
- Performance benchmarks: metadata search P95 ≤ 150 ms on synthetic 2,000-book
  corpus; FTS5 search P95 ≤ 500 ms warm (NFR-OGMA-003, NFR-OGMA-004).
- Icons, en/fr strings, accessibility for search UI and Index Manager.

### Explicitly out of scope

- Semantic/embedding search (Phase 11).
- OCR indexing of scanned pages (Phase 15 — `ExtractionQuality.Scanned` pages
  are flagged but OCR text is not generated here).
- Match-location explanation badges (Phase 11, FR-SEARCH-003).
- Hybrid ranking blending semantic score (Phase 11, FR-SEARCH-005).
- LAN search serving (Phase 16).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-SEARCH-001 | MVP | Metadata search while typing ≤ 150 ms P95 on 2,000 books | `PerfBenchmark_MetadataSearch_P95` benchmark |
| FR-SEARCH-002 | V1 | FTS5 index over extracted text/notes/tags/descriptions/TOC | `FtsIndex_SearchReturnsExpectedBooks` integration test |
| FR-SEARCH-006 | V1 | Index manager: progress, indexed count, pending OCR, failed, size | `IndexManager_ShowsCorrectCounts` UI test |
| NFR-OGMA-003 | MVP | Metadata search ≤ 150 ms P95 | `PerfBenchmark_MetadataSearch_P95_LessThan150ms` |
| NFR-OGMA-004 | V1 | Full-text search ≤ 500 ms P95 warm | `PerfBenchmark_FtsSearch_P95_LessThan500ms` |
| NFR-OGMA-009 | V1 | Background job recoverable without duplicate work | `ExtractionPipeline_Resume_NoDuplicates` test; G7 rebuild test |

---

## 6. Dependencies

### Depends on

- **Phase 04** — `Books`, `BookFiles`, `SearchChunks`, `ExtractedPages` table
  definitions; EF Core context; sidecar `extracted-text/` path.
- **Phase 05** — Stable book identity; content hash for cache-invalidation of
  extracted text.
- **Phase 08** — `TextLayerService` and `ExtractionQuality` flag; per-page
  bounding-box extraction already run; Phase 08 sidecar JSON reused.
- **Phase 09** — `AnnotationBody` text available for FTS5 indexing (notes).
- **ADR-0006** — Hybrid search: metadata + FTS5 + embeddings; FTS5 external-
  content table design.

### Unblocks

- **Phase 11** — `SearchChunks` + `ExtractedPages` schema; text is ready for
  embedding; `ISearchReadModel` contract.
- **Phase 13** — AI advisor reads FTS5 results as evidence.
- **Phase 16** — `ISearchReadModel` wired to Host's LAN projection.

---

## 7. Architecture & approach

### Bounded context: Search Index (HLD §4.6, §4.7)

The Search Index context reads book identity from Catalogue through
`IBookFileLocator` and text from the Reader's sidecar (via `IExtractedTextStore`
abstraction). It never owns book identity.

```
SearchView / IndexManagerView (Avalonia)
  └─ SearchViewModel / IndexManagerViewModel
       ├─ MetadataSearchService         — parameterized SQL / debounced as-you-type
       ├─ FtsIndexService               — FTS5 query; rank; return SearchResult[]
       ├─ ExtractionPipelineService     — per-book, per-page PdfPig; idempotent; chunks
       ├─ IndexManagerService           — progress/counts; rebuild; cancel
       └─ ISearchReadModel              — IObservable<SearchIndexEvent> (LAN-ready)
```

#### Metadata search (FR-SEARCH-001)

A parameterized SQL query across `Books` (title, subtitle, authors joined,
tags joined, ISBN, shelf names) with `LIKE '%query%'` or FTS5 prefix match
on a lightweight metadata-only FTS5 table. Results sorted by a deterministic
relevance score:

```
score = (exact-title-match ? 100 : 0)
      + (title-prefix-match ? 80 : 0)
      + (author-match ? 60 : 0)
      + (tag-match ? 40 : 0)
      + (description-match ? 20 : 0)
```

The query is debounced to 150 ms after last keystroke via `ReactiveX` or a
manual debounce; executed on a `ThreadPool` thread; results marshalled to UI
thread. P95 ≤ 150 ms on 2,000 books (NFR-OGMA-003).

#### FTS5 external-content index (ADR-0006)

```sql
CREATE VIRTUAL TABLE SearchFts5 USING fts5(
    chunk_text,
    content="SearchChunks",
    content_rowid="Id",
    tokenize="unicode61 remove_diacritics 1"
);
```

Triggers maintain consistency between `SearchChunks` and `SearchFts5` on
insert/update/delete. The FTS5 table is rebuilt from `SearchChunks` during the
full rebuild action; no incremental FTS5 rebuild (SQLite FTS5 does not support
partial rebuild cleanly; the full rebuild gate G7 is the acceptance criterion).

`SearchChunks.Source` distinguishes: `page`, `note`, `tag`, `description`,
`toc`. Phase 11 uses this field to weight match explanations.

#### Extraction pipeline

`ExtractionPipelineService` runs as an `IHostedService`. Algorithm:

1. Query `Books` for any book with `IndexStatus != Indexed` or where
   `Books.ContentHash != ExtractedPages.ContentHash` (staleness detection).
2. For each book: open with PdfPig; for each page: extract text → `TextWord[]`;
   compute `ExtractionQuality`; write `ExtractedPage` row; chunk into
   `SearchChunk` rows (chunk size: 512 tokens, 64-token overlap); insert into
   `SearchChunks`; update `SearchFts5` via trigger.
3. Idempotent: if `ExtractedPage` row already exists with matching
   `ContentHash`, skip. Resumable: a crash at page N resumes from page N+1.
4. Mark `Books.IndexStatus = Indexed` only after all pages succeed.
5. Failed pages: `ExtractionQuality = Failed`; logged to `Jobs` table;
   visible in Index Manager.

#### Index Manager (FR-SEARCH-006)

`IndexManagerService` queries:
- `IndexedCount` = `COUNT(Books WHERE IndexStatus = Indexed)`.
- `PendingOcrCount` = `COUNT(ExtractedPages WHERE ExtractionQuality = Scanned)`.
- `FailedExtractionCount` = `COUNT(ExtractedPages WHERE ExtractionQuality = Failed)`.
- `IndexSizeBytes` = `SUM(LENGTH(chunk_text))` from `SearchChunks` or `DBSTAT`
  on `SearchFts5`.
- `TotalBooks` = `COUNT(Books WHERE IsAvailable = true)`.

"Rebuild index" action: deletes all `SearchChunks`, `ExtractedPages` rows and
the `SearchFts5` virtual table content, then re-queues all books. The rebuild
is atomic at the delete step (single transaction) and the pipeline resumes
idempotently if interrupted.

#### LAN-projection-ready design

`ISearchReadModel` emits `SearchIndexEvent` values:
`BookIndexed(bookId, chunkCount)`, `BookIndexFailed(bookId, reason)`,
`IndexRebuilt(totalChunks, durationMs)`. In Phase 10 only the local
`IndexManagerViewModel` subscribes. Phase 16 wires it to the Host LAN
projection.

---

## 8. Work breakdown (summary)

| Work package | Key tasks | Detail |
| --- | --- | --- |
| WP1 — Schema & migrations | `ExtractedPages`, `SearchChunks`, `SearchFts5` VT, triggers | `tasks.md` WP1 |
| WP2 — Metadata search | Debounced as-you-type; relevance scoring; P95 benchmark | `tasks.md` WP2 |
| WP3 — Extraction pipeline | PdfPig per-page; chunking; idempotent; resumable | `tasks.md` WP3 |
| WP4 — FTS5 index | Query; ranking; result model; warm P95 benchmark | `tasks.md` WP4 |
| WP5 — Index Manager | Dashboard UI; counts; rebuild; cancel; G7 reliability test | `tasks.md` WP5 |
| WP6 — UI, icons, i18n, a11y | Search bar, Index Manager; en/fr strings; keyboard + SR | `tasks.md` WP6 |
| WP7 — Tests & benchmarks | All layers; golden-corpus; G7 rebuild; perf benchmarks | `tasks.md` WP7 |

---

## 9. Cross-cutting checklist

- [ ] **Colorful icons + manifest**: `icons.md` complete; search, index-manager,
      rebuild, filter-by-match icons listed; owner procurement request issued.
- [ ] **i18n (en/fr)**: all search UI strings externalized in en + fr;
      pseudolocale check passes.
- [ ] **Accessibility**: search input keyboard-operable; result list navigable
      by arrow keys; Index Manager progress bars have `aria-label`s;
      WCAG 2.2 AA.
- [ ] **Privacy/egress**: search is entirely local; no network calls; no AI
      gateway — N/A.
- [ ] **Reversibility**: rebuild is a destructive-then-rebuild action; the
      delete step is transactional; interrupted rebuild recovers; G7 test proves
      no data-loss path.
- [ ] **Performance**: NFR-OGMA-003 (≤ 150 ms P95 metadata) and NFR-OGMA-004
      (≤ 500 ms P95 FTS5 warm) both CI-gated; NFR-PROD-005 (no UI stall) enforced
      by background dispatch.
- [ ] **Bounded-context tests**: Search does not depend on Reader, AI, or 3D
      bounded contexts.
- [ ] **Documentation**: `ISearchReadModel`, `ExtractionPipelineService`
      algorithm, FTS5 schema, chunking parameters carry XML doc comments.

---

## 10. Definition of Done

**Global DoD (README §6) fully applied, plus:**

- [ ] FR-SEARCH-001: `PerfBenchmark_MetadataSearch_P95_LessThan150ms` passes
      on both CI runners against the synthetic 2,000-book corpus.
- [ ] FR-SEARCH-002: `FtsIndex_SearchReturnsExpectedBooks` integration test
      passes: insert known text; search for a phrase in that text; result
      contains the expected book.
- [ ] FR-SEARCH-006: Index Manager shows accurate counts (indexed, pending OCR,
      failed, index size) on a known corpus.
- [ ] NFR-OGMA-004: `PerfBenchmark_FtsSearch_P95_LessThan500ms` passes on
      2,000-book corpus (warm index).
- [ ] NFR-OGMA-009 / G7: `IndexRebuild_CompletesWithoutDuplicatesOrCorruption`
      — rebuild on 100-book corpus; count `SearchChunks` before and after; counts
      match and FTS5 `integrity_check` passes.
- [ ] Extraction pipeline: `ExtractionPipeline_Resume_NoDuplicates` — kill
      mid-extraction; restart; chunk counts are the same as a clean full run.
- [ ] Index Manager rebuild action: complete + cancel paths work; cancellation
      leaves a partial but consistent state; re-trigger resumes.
- [ ] `ISearchReadModel` interface published with LAN-ready XML doc comment.
- [ ] All search UI controls keyboard-operable; screen-reader announces result
      count; Index Manager status announced on update.
- [ ] Architecture tests pass; no R1/R2 defects open.
- [ ] `/code-review` completed; findings resolved.

---

## 11. Skills to use

See `skills.md` for full guidance. Summary:

- `backend-databases:database-internals` — FTS5 external-content table design;
  trigger maintenance; `integrity_check`; query plan analysis.
- `backend-databases:database-performance` patterns — metadata search latency;
  index cardinality; covering indexes.
- `frontend-ux:frontend-performance` — debounced as-you-type; background
  dispatch; no UI stall.
- `devops-cloud:reliability-engineering` — idempotent/resumable extraction
  pipeline; G7 rebuild gate.
- `sdlc-meta:advanced-testing-strategy` — pipeline fault injection; rebuild
  reliability test.
- `avalonia-desktop-development` — search bar, result list virtualization,
  Index Manager progress UI; Automation peers.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `MetadataSearchService`, `FtsIndexService`, `ExtractionPipelineService`, `IndexManagerService` | `src/OgmaLibrary.Application/Search/` |
| `ISearchReadModel` interface | `src/OgmaLibrary.Application/Search/` |
| FTS5 migration + triggers | `src/OgmaLibrary.Infrastructure/Migrations/` |
| `SearchView`, `SearchViewModel`, `IndexManagerView`, `IndexManagerViewModel` | `src/OgmaLibrary.App/Views/Search/` |
| Search en/fr resource files | `src/OgmaLibrary.App/Assets/Strings/search.en.resx`, `search.fr.resx` |
| Performance benchmarks | `src/OgmaLibrary.Benchmarks/SearchBenchmarks.cs` |
| G7 rebuild reliability test | `tests/OgmaLibrary.Tests/Reliability/IndexRebuildTest.cs` |
| `icons.md` | `docs/plans/grand-plan/phase-10/icons.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| FTS5 external-content triggers cause write amplification at scale | R3 | Measure chunk-insert throughput on 2,000-book corpus; batch insert with trigger disabled; re-enable; benchmark |
| Metadata search P95 exceeds 150 ms on slow macOS CI runner | R3 | Ensure covering index `(title, author, isbn)` exists; test on reference hardware |
| PdfPig extraction fails on non-standard encodings (CJK, Symbol) | R5 | `ExtractionQuality = Partial` flagged; test with `non-english` golden fixture |
| Rebuild delete step blocks DB for > 100 ms during active search | R3 | Rebuild on a separate WAL connection; measure read latency during delete |
| FTS5 `integrity_check` reveals shadow-table corruption after interrupted rebuild | R4 | G7 test explicitly runs `integrity_check` after a simulated interrupted rebuild |

---

## 14. Owner asks

1. **Premium icon procurement (Search set):** Please review `icons.md` and
   purchase the named premium PNG icons for search, index-manager, rebuild, and
   filter surfaces. Full style/size spec in `icons.md`.

2. **Chunk size decision:** The extraction pipeline uses 512 tokens per chunk
   with 64-token overlap (following common RAG practice). This affects both FTS5
   result granularity and Phase 11 embedding quality. Please confirm or adjust
   before WP3 implementation is finalized.

3. **Index rebuild scope confirmation:** The current design rebuilds all
   `SearchChunks` and `ExtractedPages` on a full rebuild. Please confirm this
   is acceptable (an alternative would be incremental rebuild per content-hash
   change only). The incremental alternative is more complex but faster; it
   can be delivered as V1 enhancement if desired.

---

## 15. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-05-30 | Initial v1.0 baseline authored | Grand-plan agent |
