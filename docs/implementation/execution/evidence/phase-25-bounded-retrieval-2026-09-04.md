# Phase 25 bounded retrieval memory - 2026-09-04

## Scope

Semantic retrieval previously materialized its bounded corpus query into a
list, including every vector BLOB in the selected window. This increment keeps
the 50,000-row scan bounded in memory while retaining exact deterministic
cosine ranking.

## Implementation

- The query remains limited to the approved model/version/provider, active
  books, current dimension, and a maximum 50,000-vector scan window.
- Rows are consumed asynchronously and scored as they arrive.
- A min-heap retains only `maxResults * 4` candidates, with score/chunk-ID tie
  ordering matching the prior `CosineSimilarityService.TopK` contract.
- Only the selected top-K rows retain vector arrays and snippets for hybrid
  ranking; the full corpus is never stored as a list.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter FullyQualifiedName~SemanticSearchServiceTests --logger "console;verbosity=minimal" --results-directory tmp/phase25-stream-semantic-results-3/
```

Result: 6 passed, 0 failed, 0 skipped, including the 50,000-book semantic
latency benchmark at the existing p95 <=1,500 ms threshold.

The same change set was then checked by the full solution command:

```text
dotnet test OgmaLibrary.sln --no-restore -p:BaseOutputPath=tmp/full-suite-build-2026-09-04-phase25-stream/ --logger "console;verbosity=minimal" --results-directory tmp/full-suite-results-2026-09-04-phase25-stream/
```

That run passed 877 core, 41 architecture, and 142 UI tests, with 0 failures
and 0 skips.

## Boundary

This closes the local bounded-memory retrieval subgate. It does not establish
a true ANN index, representative Recall@K/MRR/nDCG quality, independent peak
memory acceptance, reference-machine performance, or physical accessibility.
