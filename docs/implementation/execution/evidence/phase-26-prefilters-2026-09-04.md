# Phase 26 semantic prefilter evidence

Date: 2026-09-04

Semantic retrieval applies structured prefilters before materializing vectors:
active books, indexed chunks, the configured local model/version/provider, and
the query-vector dimension. The focused mismatch test confirms incompatible
vectors do not enter cosine ranking and trigger an exact-search fallback.

Verification: the two `Phase26SearchEvaluationTests` plus
`SemanticSearch_DimensionMismatch_UsesExactFallback` passed 4/4.

Remaining Phase 26 gates cover durable evaluation-run/judgment persistence,
representative-corpus metrics, ANN or equivalent target-scale retrieval,
diversity controls, 50,000-book latency/memory acceptance, and final contract
freeze.
