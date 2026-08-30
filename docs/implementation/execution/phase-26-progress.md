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
- Added regression coverage for fusion metadata, dimension mismatch fallback,
  hybrid determinism, semantic relevance and performance.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- FTS/combined, hybrid-ranking and semantic retrieval slice: 18 passed.

## Remaining phase gate

Structured prefilters, evaluation-run/judgment persistence, Recall@K/MRR/nDCG
corpus evidence, true ANN or equivalent target-scale retrieval, diversity
controls, latency/memory acceptance at 50,000 books, and final search-contract
freeze remain before phase 26 closure.
