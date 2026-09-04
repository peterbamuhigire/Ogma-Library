# Phase 25 Query-Embedding Cache Evidence

Date: 2026-09-04

## Delivered

- Repeated local semantic searches reuse query embeddings through a bounded
  in-memory cache.
- Cache keys are SHA-256 digests of trimmed query text; raw queries are not
  persisted and vectors are copied on insertion and retrieval.
- Entries expire after five minutes and the cache is capped at 128 entries,
  evicting the oldest entry when full.
- `SemanticSearchResponse.EmbeddingCacheHit` exposes the local cache outcome
  without exposing query content.
- Provider-unavailable and exact-fallback behavior remains unchanged.

## Verification

Focused semantic-search slice: 7 passed, 0 failed, 0 skipped, including
whitespace-normalized cache reuse with exactly one provider call.

Full isolated solution validation:

```text
dotnet test OgmaLibrary.sln --no-restore
  -p:BaseOutputPath=tmp/full-suite-build-2026-09-04-phase25-query-cache/
  --logger "console;verbosity=minimal"
  --results-directory tmp/full-suite-results-2026-09-04-phase25-query-cache/
```

Result: 883 core + 41 architecture + 142 UI = 1,066 passed, 0 failed,
0 skipped.

## Gate disposition

Closed locally: bounded query-embedding cache and cache-hit observability.

Still open: provider cost accounting, ANN/equivalent relevance quality,
representative corpus, independent reference-machine confirmation, and final
target-scale UI evidence.
