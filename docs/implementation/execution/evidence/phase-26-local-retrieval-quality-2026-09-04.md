# Phase 26 — Local Retrieval Quality Evidence

Date: 2026-09-04

## Scope

This closes the local deterministic retrieval-quality sub-gate. The fixture is
synthetic and deliberately small; it is not evidence for the approved external
reference corpus, ANN selection, or reference hardware.

## Fixture and acceptance

The test runs four concept queries over six locally persisted books with varied
semantic directions, including distractors and an oppositely directed vector.
It executes the real `SemanticSearchService`, then evaluates the captured
rankings with the versioned `search-retrieval-evaluation-v1` contract at K=3.

Acceptance requires every judged book to be retrieved in the top three, with
Recall@3, MRR, and nDCG all equal to 1.0. The result was:

| Cases | Recall@3 | MRR | nDCG@3 |
| ---: | ---: | ---: | ---: |
| 4 | 1.0 | 1.0 | 1.0 |

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~Phase26RepresentativeRetrievalTests --logger "console;verbosity=minimal"
```

Result: **1 passed, 0 failed, 0 skipped**.

The preceding full solution regression also passed at the current head:
**1,075 passed, 0 failed, 0 skipped** (41 architecture, 889 core, 145 UI).

## Remaining gates

- Approved representative/reference corpus: **NOT ASSESSED**.
- ANN or equivalent target-scale relevance-quality comparison: **NOT ASSESSED**.
- Independent 50,000-book memory acceptance and reference-machine confirmation:
  **NOT ASSESSED**.
- Final search-contract freeze pending those external/reference gates:
  **NOT ASSESSED**.
