# Phase 26 Evidence — Retrieval Evaluation Contract

Date: 2026-09-04
Scope: deterministic offline retrieval metrics

## Implemented control

`SearchOfflineEvaluator` evaluates captured ranked book IDs against explicit
relevance judgments under the versioned contract
`search-retrieval-evaluation-v1`. It reports Recall@K, mean reciprocal rank,
and binary nDCG at the case cutoff. Inputs are bounded to `K <= 100`, ranked
results are truncated deterministically, and empty judgment sets use explicit
conventions (`Recall@K = 1`, `MRR = 0`, `nDCG = 1`) so unjudged fixtures cannot
produce undefined values.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --filter "FullyQualifiedName~Phase26SearchEvaluationTests" --no-restore --verbosity minimal -m:1
```

Result: 3 passed, 0 failed.

## Still open

This closes the metric-contract gate only. Durable evaluation-run/judgment
storage, representative corpus results, structured semantic prefilters, ANN
or equivalent scale proof, diversity controls, and 50,000-book latency/memory
acceptance remain open.
