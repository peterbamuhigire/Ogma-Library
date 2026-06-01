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

## Rationale

The external-content design avoids storing text twice in the catalogue while
still giving fast keyword search. `SearchChunks` remains the source of truth for
Phase 11 embeddings and LAN projection, and `SearchFts5` is disposable derived
state guarded by trigger tests and an FTS integrity check.
