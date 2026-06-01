# Phase 13 Evidence

Date started: 2026-06-01

## Current Status

WP1-WP8 are implemented and verified locally. The slices add the structural
domain contracts plus the metadata-only recommendation pipeline that later
advisor composition, UI, and evaluation work will consume. Hybrid ranking is
integrated behind a default-off option. Reading-plan generation now has a
validated structured parser, embedded schema prompt, and retry-on-parse-failure
pipeline. The typed advisor service is wired through DI, disabled by the Offline
privacy tier, and answer mode is scaffolded for V2. Recommendation and
reading-plan Avalonia surfaces are implemented with localized view models and a
headless render test.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdvisorDomainTests` | Passed: 17 Phase 13 advisor domain and localization tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RecommendationPipelineTests\|FullyQualifiedName~AdvisorDomainTests"` | Passed: 21 advisor domain and metadata pipeline tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RecommendationPipelineTests` | Passed: 5 recommendation pipeline tests including hybrid merge |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReadingPlanPipelineTests\|FullyQualifiedName~RecommendationPipelineTests"` | Passed: 9 recommendation and reading-plan pipeline tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AdvisorServiceTests\|FullyQualifiedName~ReadingPlanPipelineTests\|FullyQualifiedName~RecommendationPipelineTests"` | Passed: 13 advisor service and pipeline tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdvisorViewModelTests` | Passed: 2 advisor view-model tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdvisorViewRenderTests` | Passed: 1 advisor render test |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore` | Passed: 362 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 21 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore` | Passed: 105 UI tests |
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
| Query contract | `RecommendationQuery` captures text, max results, read-state exclusion, and optional shelf filter |
| Catalogue candidates | `AdvisorCatalogueReader` uses the catalogue read model and metadata search service to gather local metadata candidates |
| Payload enrichment | `MetadataPayloadEnricher` emits Tier-1 metadata only and caps candidates at 50 with an estimated 12k-character budget |
| Prompt template | `AI/Advisor/prompts/recommendation.txt` is embedded in Infrastructure and loaded by `RecommendationPromptTemplate` |
| Response parser | `RecommendationResponseParser` parses strict provider JSON into `RecommendationCard` domain records |
| Provenance validator | `RecommendationProvenanceValidator` strips hallucinated provenance and falls back to deterministic local ranking when most ids are invalid |
| Structural oracle | `RecommendationStructuralValidator` checks sequential rank, confidence bounds, explanation, and provenance |
| Pipeline | `RecommendationPipeline` routes recommendation calls through `IAiGateway` and validates local-only output before returning cards |
| Advisor options | `AdvisorOptions` keeps hybrid ranking disabled by default and exposes AI/semantic merge weights |
| Hybrid adapter | `HybridRankerConsumer` consumes Phase 11 `ISemanticSearchService` scores without coupling advisor code to search internals |
| Hybrid merger | `HybridRecommendationMerger` combines provider recommendation order and Phase 11 ranking scores, then re-ranks cards structurally |
| Reading-plan request | `ReadingPlanRequest` captures goal, max books, difficulty preference, shelf filter, and seed book ids |
| Reading-plan prompt | `AI/Advisor/prompts/reading-plan.txt` is embedded and defines the strict JSON schema |
| Reading-plan parser | `ReadingPlanParser` validates local book ids, difficulty labels, non-empty steps, checkpoints, and estimate bounds |
| Reading-plan pipeline | `ReadingPlanPipeline` routes plan generation through `IAiGateway` and retries once after invalid provider JSON |
| Answer scaffold | `AnswerRequest` and `AnswerResponse` define the V2 answer-mode surface; `GetAnswerAsync` throws the planned V2 `NotImplementedException` |
| Advisor service | `AdvisorService` composes recommendation and reading-plan pipelines, exposes `IsEnabled`, and fails closed with `AiDisabledException` when tier is Offline |
| DI | `AddAiGatewayCore` registers `IAiAdvisorService`, recommendation pipeline services, hybrid services, and reading-plan services |
| Architecture | `Architecture_AdvisorService_UsesOnlyAiGateway` guards the advisor application boundary from provider adapters |
| Recommendation UI | `RecommendationPanelViewModel` and `RecommendationPanelView` show query, loading/error state, recommendation cards, text confidence bands, explanations, provenance chips, and open-book action |
| Reading-plan UI | `ReadingPlanViewModel` and `ReadingPlanView` show goal input, generated steps, localized difficulty labels, estimates, checkpoints, and open-book action |
| Advisor UI localization | English and French strings cover recommendation labels, plan labels, status text, accessible labels, and error state |
| Advisor render test | `AdvisorViewRenderTests` headless-renders loaded recommendation and reading-plan surfaces |

## Verification Notes

- One parallel architecture run collided with a core test build output lock on
  `OgmaLibrary.Application.dll`; the architecture suite passed when rerun alone.
- A later parallel core/architecture run hit the same build-output lock; both
  suites passed when run sequentially.
- Two full UI runs exposed different existing timing-sensitive tests; both failed
  cases passed when rerun in isolation, and the third full UI run passed all 104
  tests.

## Remaining Phase 13 Work

- WP9: offline structural evaluation harness and benchmark result.
- WP10-WP11: extension SDK entry points, integration tests, golden-corpus gates, code review, remote CI, and manual accessibility evidence.
