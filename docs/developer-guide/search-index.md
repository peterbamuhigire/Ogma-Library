# Search Index Schema

Phase 10 uses SQLite FTS5 as a local, offline full-text index over
`SearchChunks`. The durable source rows stay in normal catalogue tables; FTS5 is
a derived index that can be rebuilt.

## Tables

- `Books.IndexStatus`: `0=NotIndexed`, `1=Extracting`, `2=Indexed`, `3=Failed`.
- `ExtractedPages`: one row per book/page with extracted text, quality,
  `WordCount`, `ContentHash`, and extraction timestamp.
- `SearchChunks`: token-bounded chunks derived from pages, notes, tags,
  descriptions, and TOC entries. `Source` uses `0=page`, `1=note`, `2=tag`,
  `3=description`, `4=toc`.
- `SearchFts5`: external-content FTS5 virtual table over `SearchChunks.ChunkText`
  with `content_rowid='ChunkId'`.

## Trigger Maintenance

`SearchChunks_Fts_Insert`, `SearchChunks_Fts_Update`, and
`SearchChunks_Fts_Delete` keep `SearchFts5` synchronized with chunk writes.
Repository code writes only `SearchChunks`; it never writes the FTS table
directly. Rebuild and repair paths may recreate the virtual table and triggers
because they are SQLite objects outside the EF Core entity model.

## Extraction Pipeline

`IExtractionPipelineService` is the Application contract for Phase 10
extraction. `ExtractionPipelineService` implements it in Infrastructure by using
the existing Reader PDF boundary (`IBookFileLocator` and `IPdfRendererFactory`),
then writing through `IExtractedTextStore` and `ISearchChunkRepository`.

The pipeline is idempotent by content hash. A page with an existing
`ExtractedPages` row for the current book hash is skipped, and source-scoped
chunk replacement keeps reruns from creating duplicate `SearchChunks`. Page
failures are stored as `ExtractionQuality = Failed`, logged as
`Jobs.JobType = ExtractionFailed`, and do not stop sibling pages from indexing.

Implemented chunk sources are:

- `page`: extracted PDF page text with `ExtractedPageId`.
- `note`: Phase 09 `AnnotationsV2.NoteText`, plus legacy annotation bodies.
- `tag`: metadata tags/categories and shelf names.
- `description`: metadata description/summary fields.
- `toc`: currently replaced with an empty source set until a real outline
  extractor is added.

`SearchExtractionWorker` polls the pipeline in the background so indexing does
not block the UI thread.

## Full-Text Search

`IFtsIndexService` exposes FTS5 search without leaking SQLite details into
Application code. The Infrastructure implementation builds a conservative
`MATCH` expression from plain user text, joins `SearchFts5.rowid` back to
`SearchChunks.ChunkId`, and returns `FtsSearchResult` records with book title,
first author, source, optional page index, snippet, and score.

Ranking uses `bm25(SearchFts5)` sorted ascending inside SQLite and exposed as a
higher-is-better score by negating the rank. Snippets use:

```sql
snippet(SearchFts5, 0, '<b>', '</b>', '...', 20)
```

`ICombinedSearchService` merges metadata and FTS hits into one book-level result
set, deduplicated by `BookId`. FTS integrity checks use SQLite's standard:

```sql
INSERT INTO SearchFts5(SearchFts5) VALUES ('integrity-check');
```

## Rationale

The external-content design avoids storing text twice in the catalogue while
still giving fast keyword search. `SearchChunks` remains the source of truth for
Phase 11 embeddings and LAN projection, and `SearchFts5` is disposable derived
state guarded by trigger tests and an FTS integrity check.
