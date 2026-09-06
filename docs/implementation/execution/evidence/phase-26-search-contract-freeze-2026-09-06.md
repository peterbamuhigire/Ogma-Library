# Phase 26 Search Contract Freeze

Date: 2026-09-06

## Frozen version identifiers

The application layer now owns one authoritative set of Phase 26 search
contract versions:

| Boundary | Version |
| --- | --- |
| Semantic response/degradation DTO | `semantic-search-v1` |
| Metadata/full-text fusion | `rrf-v1` |
| Exact/semantic/reader-signal ranking | `hybrid-v1` |
| Offline relevance evaluation | `search-retrieval-evaluation-v1` |

The semantic response now carries its contract version explicitly. Combined
and hybrid result defaults, the production combined-search implementation, and
the evaluator all consume the shared constants instead of repeating literals.

## Compatibility rule

The v1 semantic response shape is frozen as:

```text
ProviderUnavailable
UsedExactFallback
Results
Availability
EmbeddingCacheHit
ContractVersion
```

A breaking rename, removal, type/meaning change, or default-semantic change
requires a new contract version and migration of every consumer. Additive
fields must be optional/defaultable and still require an explicit review of
desktop, LAN, advisor, 3D-focus, and evaluation consumers.

Ranking/fusion version changes require side-by-side evaluation evidence; an
implementation must not publish old version identifiers for changed scoring
semantics.

## Executable proof

`Phase26SearchContractFreezeTests` guards exact v1 identifiers, semantic DTO
property shape, and default result versions. The broader focused slice covers
hybrid determinism, evaluation, semantic fallback, and combined fusion.

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase26SearchContractFreezeTests|FullyQualifiedName~HybridRankingServiceTests|FullyQualifiedName~Phase26SearchEvaluationTests|FullyQualifiedName~SemanticSearchServiceTests|FullyQualifiedName~CombinedSearchServiceTests" --logger "console;verbosity=minimal" -m:1
Passed: 19, Failed: 0, Skipped: 0
```

## Residual gates

The contract freeze is closed. Representative/reference-corpus quality,
true-ANN or equivalent target-scale quality, independent 50,000-book memory
acceptance, and reference-machine confirmation remain open.
