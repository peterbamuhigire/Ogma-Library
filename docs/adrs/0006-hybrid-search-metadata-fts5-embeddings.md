# ADR-0006: Build Search as Hybrid Metadata, FTS5, and Semantic Embeddings

## Status

Accepted

> Ratified in Phase 00 by the project owner, 2026-05-30.

## Date

2026-05-30

## Context

Ogma Library promises to make a collection usable through search and intelligent through an AI advisor that recommends books from the user's own corpus with explanations and confidence. Three retrieval needs coexist: exact structured lookup over metadata (title, author, tag, year), full-text keyword search over extracted document text, and semantic similarity for recommendation and concept search where the query words do not match the document words. No single index serves all three well. The catalogue is SQLite with a sidecar folder (ADR-0005), and full-text indexing is scheduled for Phase 4 before AI content-aware mode, per design-report Section 17. Semantic similarity needs a vector store; at small-to-medium personal-library scale a brute-force cosine scan over stored embeddings is correct and simple, while a dedicated vector index is a later optimisation to be proven by a spike rather than adopted on faith.

## Decision Drivers

- **Cover exact metadata lookup, full-text keyword search, and semantic similarity** from one design.
- **Keep the index out of the catalogue file** using the external-content pattern (ADR-0005).
- **Meet the P95 search-latency budget** at personal-library scale.
- **Start with the simplest correct semantic approach** and defer index complexity until measured.
- **Keep retrieval fully local** so offline search and local AI remain useful.

## Considered Options

### Option A — Hybrid: SQLite metadata queries plus FTS5 plus semantic embeddings

- **Pros:** each retrieval need is served by the index suited to it; FTS5 external-content keeps the keyword index lean against the catalogue (ADR-0005); embeddings enable concept search and explainable recommendation; all three run locally and offline.
- **Cons:** three retrieval paths must be orchestrated and their results merged and ranked; embedding generation and storage add a pipeline.

### Option B — FTS5 keyword search only

- **Pros:** simplest; one index.
- **Cons:** no semantic similarity, so the AI advisor cannot recommend by concept and queries that do not share words with the text fail.

### Option C — Embedding vector search only

- **Pros:** strong semantic recall.
- **Cons:** poor at exact metadata and exact-phrase lookup; over-engineered and slower for simple keyword needs; loses precise structured filtering.

## Decision Outcome

Adopt a hybrid search architecture combining three local retrieval paths: structured SQLite metadata queries for exact and filtered lookup, an FTS5 external-content full-text index for keyword search over extracted text, and semantic embeddings for similarity and explainable recommendation. For the semantic path, start with a brute-force cosine-similarity scan over stored embeddings, which is correct and adequate at personal-library scale and avoids premature index machinery. A dedicated vector index — for example sqlite-vec or a comparable SQLite vector extension — is spiked later and adopted only if measured corpus growth pushes brute-force cosine past the P95 search-latency budget; the spike outcome is recorded as an amendment to this ADR. Full-text indexing lands in Phase 4 before AI content-aware mode, matching the design-report sequencing.

## Consequences

### Positive

- Exact, keyword, and semantic retrieval are each served well, and the AI advisor can recommend by concept with supporting evidence.
- Starting with brute-force cosine keeps Phase 4 simple and defers vector-index complexity until data justifies it.

### Negative

- Result merging and ranking across three paths must be designed and tuned.
- Embedding generation, storage, and erasure (CTRL-OGMA-023) add a pipeline that the sidecar folder must hold (ADR-0005).

### Affects

- ADR-0005 (FTS5 external-content and embedding storage in the sidecar); ADR-0007 (content-aware AI consumes retrieved context); ADR-0008 (annotation and metadata records are searchable); CTRL-OGMA-023 (embedding erasure).
