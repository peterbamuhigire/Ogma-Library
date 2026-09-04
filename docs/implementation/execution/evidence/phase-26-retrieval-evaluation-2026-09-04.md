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

## Gate disposition

The metric-contract, durable evaluation-run/judgment storage, structured
semantic prefilter, diversity, bounded 50,000-book scan, and local latency
subgates are closed by the current implementation and focused regression
evidence. Representative-corpus Recall@K/MRR/nDCG, ANN or equivalent
relevance-quality proof, independent memory acceptance, and reference-machine
confirmation remain open.
