# Phase 26 Progress - Semantic and Hybrid Retrieval

Date: 2026-09-04

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
- Added a real-service local concept fixture with four judged queries, six
  varied vectors, distractors, and a versioned Recall@3/MRR/nDCG quality gate.
  The local fixture achieved 1.0 for all three metrics.
- Froze the v1 search boundary through shared application-layer version
  identifiers for semantic response, RRF fusion, hybrid ranking, and offline
  evaluation. Semantic responses now carry their version, and an executable
  shape guard requires a deliberate version change for breaking DTO changes.
  Evidence: `evidence/phase-26-search-contract-freeze-2026-09-06.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- FTS/combined, hybrid-ranking, semantic retrieval and evaluation slice: 21
  passed.
- Hybrid diversity policy regression slice: 8 passed.
- Local concept-quality slice: 1 passed with Recall@3, MRR and nDCG all at
  1.0. See `evidence/phase-26-local-retrieval-quality-2026-09-04.md`.
- Search contract, hybrid, evaluation, semantic, and combined-search slice: 19
  passed, 0 failed, 0 skipped.

## Remaining phase gate

The local Recall@K/MRR/nDCG sub-gate is closed for the synthetic concept
fixture. Approved representative/reference-corpus evidence, true ANN or
equivalent target-scale relevance-quality retrieval, independent memory
acceptance at 50,000 books, and reference-machine confirmation remain open.
The final v1 search-contract freeze is closed and version guarded.

The Aug-39 Definition of Done is reconciled as complete against the committed
versioned synthetic benchmark and executable v1 contract. Representative
external-corpus/ANN quality, independent memory, and reference-machine
acceptance remain open and keep the phase overall `IN PROGRESS`.
