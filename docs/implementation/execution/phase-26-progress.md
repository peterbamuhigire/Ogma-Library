# Phase 26 Progress - Semantic and Hybrid Retrieval

Date: 2026-08-30

## Delivered in this increment

- Replaced incompatible metadata/FTS score addition with deterministic,
  explainable reciprocal-rank fusion (`rrf-v1`) at the combined-search layer.
- Versioned hybrid ranking output (`hybrid-v1`) while retaining explicit
  component scores for downstream explanation and evaluation.
- Merged semantic candidates by book deterministically and added a bounded
  semantic corpus cap to avoid unbounded vector materialization.
- Filtered semantic candidates by query-vector dimension and provider/model
  compatibility, preventing mixed-dimension cosine failures.
- Preserved exact/FTS fallback when the local semantic provider is unavailable,
  the semantic corpus is empty, the query vector is invalid, or dimensions do
  not match.
- Added a versioned offline retrieval-evaluation contract for captured ranked
  results and relevance judgments, calculating bounded Recall@K, MRR and nDCG
  with deterministic empty-judgment conventions.
- Added regression coverage for fusion metadata, dimension mismatch fallback,
  hybrid determinism, semantic relevance and performance.
- Verified structured semantic prefilters for active/indexed catalogue rows,
  configured model/version/provider, and query-vector dimension before cosine
  ranking; incompatible dimensions use the exact-search fallback.
- Added an atomic app-data JSON store for versioned evaluation runs, including
  ranked results, relevance judgments, reports, load, replacement, and delete
  operations with path-safe run identifiers.
- Added a caller-controlled author-diversity policy to hybrid ranking, with a
  conservative default wired into semantic retrieval and a pure-score opt-out
  for callers that require it. Unknown authors remain independent so missing
  metadata does not collapse unrelated books into one group.
- Expanded the semantic retrieval latency benchmark to 50,000 books and
  verified the approved p95 <=1,500 ms local assertion.
- Reworked semantic candidate loading to stream the 50,000-vector target window
  and retain only a bounded top-K heap, removing all-vector corpus
  materialization while preserving deterministic cosine ordering and book-level
  deduplication.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- FTS/combined, hybrid-ranking, semantic retrieval and evaluation slice: 21
  passed.
- Hybrid diversity policy regression slice: 8 passed.

## Remaining phase gate

Recall@K/MRR/nDCG evidence from a representative corpus, true ANN or
equivalent relevance-quality retrieval, independent memory acceptance at
50,000 books, and final search-contract freeze remain before phase 26 closure.
The bounded-memory 50,000-vector scan and latency sub-gates are closed locally;
reference-machine confirmation remains a release gate.
