# Phase 13 Evidence

Date started: 2026-06-01

## Current Status

WP1 is implemented and verified locally. The slice adds the structural domain
contracts that later recommendation, reading-plan, answer-mode, UI, and
evaluation work will consume.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdvisorDomainTests` | Passed: 17 Phase 13 advisor domain and localization tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore` | Passed: 347 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 20 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore` | Passed: 104 UI tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| Confidence labels | `ConfidenceScore.Label` maps scores to `Low`, `Medium`, `High`, and `VeryHigh` bands without presenting them as LLM truth percentages |
| Recommendation provenance | `ProvenanceItem` captures local `BookId`, matched field, and evidence value |
| Recommendation explanation | `RecommendationExplanation` requires non-empty summary, provenance, and provider model |
| Recommendation card | `RecommendationCard` enforces local book id, one-based rank, confidence score, and explanation |
| Reading difficulty | `DifficultyLabel` defines `Introductory`, `Foundational`, `Intermediate`, `Advanced`, and `Expert` |
| Reading plan | `ReadingPlanStep`, `Checkpoint`, and `ReadingPlan` enforce non-empty rationale, positive optional estimates, valid checkpoint indexes, and non-empty steps |
| Answer citation | `AnswerCitation` defines the V2 local-evidence citation shape with page/chunk support and retrieval confidence |
| Localization | English and French resource keys exist for advisor confidence and reading difficulty labels |

## Remaining Phase 13 Work

- WP2-WP6: recommendation pipeline, parsing/validation, hybrid ranking, reading-plan service, answer-mode scaffold, and advisor composition.
- WP7-WP8: recommendation and reading-plan UI.
- WP9: offline structural evaluation harness and benchmark result.
- WP10-WP11: extension SDK entry points, integration tests, golden-corpus gates, code review, remote CI, and manual accessibility evidence.
