# Phase 13 — Tasks

---

## WP1 — Domain Types

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP1-T1 | Define `ConfidenceScore` value object: `double Value` constrained to [0.0, 1.0]; `Label` property returning `Low / Medium / High / VeryHigh` enum; XML doc | Phase 12 domain | 0.5 h | FR-AI-003 |
| P13-WP1-T2 | Define `ProvenanceItem`: `BookId`, `MatchField` (enum: Title/Author/Tags/Description/SemanticScore), `FieldValue` (string); list is always local-only | P13-WP1-T1 | 0.5 h | FR-AI-003, local provenance |
| P13-WP1-T3 | Define `RecommendationExplanation`: `Summary` (string), `ProvenanceItems` (list), `ModelUsed` (string), `Tier` (AiPrivacyTier) | P13-WP1-T2 | 0.5 h | FR-AI-003 |
| P13-WP1-T4 | Define `RecommendationCard`: `BookId`, `Rank` (int, 1-based), `Confidence` (ConfidenceScore), `Explanation` (RecommendationExplanation); enforce non-null invariants | P13-WP1-T3 | 0.5 h | FR-AI-003 |
| P13-WP1-T5 | Define `DifficultyLabel` enum: `Introductory, Foundational, Intermediate, Advanced, Expert`; localization resource keys for all five in en + fr | P13-WP1-T1 | 0.5 h | FR-AI-007 |
| P13-WP1-T6 | Define `ReadingPlanStep`: `BookId`, `Rationale` (string), `Difficulty` (DifficultyLabel), `EstimatedReadingDays` (int? nullable) | P13-WP1-T5 | 0.5 h | FR-AI-007 |
| P13-WP1-T7 | Define `Checkpoint`: `AfterStepIndex` (int), `Description` (string) | P13-WP1-T6 | 0.25 h | FR-AI-007 |
| P13-WP1-T8 | Define `ReadingPlan`: `Goal` (string), `Steps` (list of ReadingPlanStep), `Checkpoints` (list of Checkpoint); enforce non-empty Steps invariant | P13-WP1-T6..T7 | 0.5 h | FR-AI-007 |
| P13-WP1-T9 | Define `AnswerCitation`: `BookId`, `PageNumber` (int?), `ChunkId` (string?), `RelevantText` (string), `Confidence` (ConfidenceScore) — V2 type, used by scaffold | P13-WP1-T1 | 0.5 h | FR-AI-008 stub |
| P13-WP1-T10 | Unit tests: `ConfidenceScore_InvalidValue_Throws`, `RecommendationCard_Invariants`, `ReadingPlan_EmptySteps_Throws` | P13-WP1-T1..T8 | 1 h | FR-AI-003, FR-AI-007 |

---

## WP2 — Recommendation Pipeline (MVP Metadata-Only)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP2-T1 | `RecommendationQuery` DTO: `QueryText` (string), `MaxResults` (int, default 5), `ExcludeAlreadyRead` (bool, default false), `ShelfFilter` (ShelfId?) | P13-WP1 | 0.5 h | FR-AI-003 |
| P13-WP2-T2 | `ICatalogueReader` (application interface): `GetCandidatesAsync(RecommendationQuery)` returning `BookMetadataDto[]`; default implementation queries all books unless shelf filter | Phase 04 | 1 h | FR-AI-003 |
| P13-WP2-T3 | `MetadataEnricher`: builds a `MetadataPayload` from `BookMetadataDto[]` including only Tier-1 fields (title, author, tags, categories, descriptions, notes); trims to token budget (max 50 books or ~12,000 tokens estimate) | P13-WP2-T2 | 1.5 h | FR-AI-004 (gateway enforces; enricher respects) |
| P13-WP2-T4 | Recommendation prompt template: structured system prompt instructing the provider to output a JSON array of `{book_id, rank, confidence, explanation, provenance}`; template stored as embedded resource `prompts/recommendation.txt` | P13-WP2-T3 | 1 h | FR-AI-003 |
| P13-WP2-T5 | `RecommendationResponseParser`: parses provider JSON response; validates each item against `RecommendationCard` structural oracle; throws `AdvisorParseException` on structural failure | P13-WP1-T4 | 2 h | FR-AI-003 structural oracle |
| P13-WP2-T6 | `ProvenanceValidator`: for each parsed card, verifies every `BookId` in `Provenance` exists in local DB; strips non-local IDs; if > 50% non-local, logs warning and returns local-only fallback ranking | P13-WP2-T5 | 1 h | FR-AI-003 local provenance |
| P13-WP2-T7 | `StructuralValidator`: asserts `Explanation` non-null/non-empty, `Confidence` in [0,1], `Provenance` list non-empty, `Rank` sequential; returns validation result with error list | P13-WP2-T5..T6 | 1 h | FR-AI-003 structural oracle |
| P13-WP2-T8 | Integration test `RecommendationPipeline_MVP_StructuralOracle`: mock provider returns well-formed JSON; assert all cards pass `StructuralValidator`; assert all BookIds in Provenance are local | P13-WP2-T2..T7 | 1.5 h | FR-AI-003 |
| P13-WP2-T9 | Integration test `ProvenanceValidator_Strips_HallucinatedIds`: mock response contains 2 valid + 1 non-existent BookId; assert stripped card has 2 provenance items | P13-WP2-T6 | 1 h | local provenance |

---

## WP3 — Hybrid Ranking Integration (V1)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP3-T1 | `IHybridRankerConsumer` adapter: wraps Phase 11 `IHybridRanker`; returns `RankedCandidate[]` with semantic score per book; isolated by anti-corruption adapter to absorb Phase 11 API changes | Phase 11 | 1.5 h | FR-AI-003 V1 hybrid path |
| P13-WP3-T2 | `HybridRecommendationMerger`: takes AI-ranked cards + semantic scores; weighted merge (configurable weights, default AI=0.6, semantic=0.4); re-ranks final list | P13-WP3-T1 | 1.5 h | FR-AI-003 V1 |
| P13-WP3-T3 | `AdvisorOptions.UseHybridRanking` feature flag (default `false` until confirmed performant); read from `ISettingsService`; if false, skip WP3 path | P13-WP3-T1..T2 | 0.5 h | V1 toggle |
| P13-WP3-T4 | Integration test `HybridPath_MergesSemanticAndAiScores`: mock both Phase 11 ranker and AI provider; assert merged list respects configured weights | P13-WP3-T1..T3 | 1.5 h | FR-AI-003 V1 |

---

## WP4 — Reading Plan Service (V1)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP4-T1 | `ReadingPlanRequest` DTO: `Goal` (string), `MaxBooks` (int, default 10), `DifficultyPreference` (DifficultyLabel?), `ShelfFilter` (ShelfId?), `SeedBookIds` (optional list) | P13-WP1 | 0.5 h | FR-AI-007 |
| P13-WP4-T2 | Reading-plan prompt template: system prompt instructing structured JSON output `{goal, steps: [{book_id, rationale, difficulty, estimated_reading_days}], checkpoints: [{after_step, description}]}`; stored as embedded resource `prompts/reading-plan.txt` | P13-WP4-T1 | 1.5 h | FR-AI-007 |
| P13-WP4-T3 | `ReadingPlanParser`: parses JSON response; validates against `ReadingPlan` structural oracle (Steps non-empty, each Step.BookId local, each Difficulty valid label, Rationale non-empty); retries once on parse failure | P13-WP1-T8, P13-WP4-T2 | 2 h | FR-AI-007 structural oracle |
| P13-WP4-T4 | `AnthropicProvider` reading-plan call: use prompt caching on the book-metadata context block (same pattern as Phase 12 WP4-T2); the large candidate-metadata block is marked `cache_control: ephemeral` | Phase 12 WP4-T2 | 1 h | FR-AI-007, prompt caching |
| P13-WP4-T5 | Integration test `ReadingPlan_StructuralOracle`: mock provider returns reading plan JSON; assert all `ReadingPlan` invariants hold; assert all `Step.BookId` values resolve locally | P13-WP4-T3 | 1.5 h | FR-AI-007 |
| P13-WP4-T6 | Integration test `ReadingPlanParser_Retry_OnParseFailure`: first response is malformed JSON; second is valid; assert `ReadingPlan` returned (not exception) | P13-WP4-T3 | 1 h | FR-AI-007 |

---

## WP5 — Answer Mode Scaffold (V2)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP5-T1 | `AnswerRequest` DTO: `Question` (string), `MaxCitations` (int, default 5), `AllowContentAwareTier` (bool) | P13-WP1-T9 | 0.5 h | FR-AI-008 stub |
| P13-WP5-T2 | `AnswerResponse` DTO: `Answer` (string), `Citations` (list of AnswerCitation), `IsV2` (bool, always false in this phase) | P13-WP5-T1 | 0.5 h | FR-AI-008 stub |
| P13-WP5-T3 | `IAiAdvisorService.GetAnswerAsync` implementation: throws `NotImplementedException("Answer mode is V2; coming in a future release")` with a user-visible fallback message; scaffolded so V2 can replace the throw without interface changes | P13-WP5-T1..T2 | 0.5 h | FR-AI-008 V2 scaffold |
| P13-WP5-T4 | Test `GetAnswerAsync_ReturnsNotImplemented_Before_V2`: assert correct exception and user-visible message; assert `AnswerRequest` and `AnswerCitation` types are final (no breaking changes expected in V2) | P13-WP5-T3 | 0.5 h | FR-AI-008 |
| P13-WP5-T5 | Add TODO tracking item in phase change log and `docs/plans/grand-plan/phase-13/README.md §15` pointing to V2 implementation scope | P13-WP5-T3 | 0.25 h | FR-AI-008 |

---

## WP6 — AdvisorService Composition

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP6-T1 | `AdvisorService` implements `IAiAdvisorService`: composes `ICatalogueReader`, `MetadataEnricher`, prompt templates, `AiGateway`, response parsers, validators; `IsEnabled` property checks `AiPrivacyTier != Offline` | P13-WP2..WP5 | 2 h | FR-AI-001, FR-AI-003, FR-AI-007 |
| P13-WP6-T2 | DI registration in `App` composition root: bind `AdvisorService` to `IAiAdvisorService`; bind `IRecommendationSource` to default `CatalogueReader` implementation; bind `IAiCatalogueReader` | P13-WP6-T1, P13-WP10 | 1 h | architecture |
| P13-WP6-T3 | Integration test `AdvisorDisabled_CatalogueBrowse_Unaffected`: set `AiPrivacyTier.Offline`; call `GetRecommendationsAsync`; assert `AiDisabledException` is caught by the UI layer and catalogue browsing continues without AI error propagation | P13-WP6-T1 | 1 h | FR-AI-001 |
| P13-WP6-T4 | Architecture test `AdvisorService_UsesOnlyAiGateway`: no type in `Application.Ai.Advisor` namespace has a direct dependency on any provider implementation class | P13-WP6-T1 | 0.5 h | ADR-0007 |

---

## WP7 — Recommendation UI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP7-T1 | `RecommendationPanelViewModel`: `LoadAsync(query)` triggers `AdvisorService.GetRecommendationsAsync`; exposes `ObservableCollection<RecommendationCardViewModel>`; loading/error/empty states | P13-WP6-T1 | 2 h | FR-AI-003 |
| P13-WP7-T2 | `RecommendationCardViewModel`: exposes BookId, Rank, ConfidenceLabel, ConfidenceValue, ExplanationSummary, ProvenanceItems; `OpenBookCommand` navigates to book detail (same route as grid/list/3D) | P13-WP7-T1 | 1.5 h | FR-AI-003 |
| P13-WP7-T3 | `RecommendationPanelView` Avalonia: cards in an `ItemsControl`; each card shows cover thumbnail, title, author, confidence badge (color = `accent/plum` graduated), explanation summary, "Why?" expand button, provenance chips | P13-WP7-T2 | 2.5 h | FR-AI-003 |
| P13-WP7-T4 | Confidence badge: color not the sole carrier — also text label (`Low / Medium / High / Very High`); WCAG 2.2 1.4.1 compliance; confidence `0..1` mapped to four color grades of `accent/plum` | P13-WP7-T3 | 1 h | a11y |
| P13-WP7-T5 | Externalize all strings en + fr; icons from `icons.md`; keyboard: Tab to card, Enter to open, space/Enter to expand "Why?"; SR: card rank and confidence label announced | P13-WP7-T3..T4 | 1 h | i18n, a11y |

---

## WP8 — Reading Plan UI (V1)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP8-T1 | `ReadingPlanViewModel`: `GenerateAsync(request)` triggers `AdvisorService.GetReadingPlanAsync`; exposes `Goal`, `ObservableCollection<PlanStepViewModel>`, `CheckpointViewModels` | P13-WP6-T1 | 2 h | FR-AI-007 |
| P13-WP8-T2 | `PlanStepViewModel`: exposes `Rank`, `BookId`, `BookTitle` (resolved from catalogue), `Rationale`, `DifficultyLabel` (localized), `EstimatedReadingDays` | P13-WP8-T1 | 1 h | FR-AI-007 |
| P13-WP8-T3 | `ReadingPlanView` Avalonia: goal header; ordered step list with difficulty badge and rationale; checkpoint markers between steps; "Open book" action per step; "Regenerate" button | P13-WP8-T2 | 2.5 h | FR-AI-007 |
| P13-WP8-T4 | Difficulty badge: color (`accent/clay` = advanced; `accent/sage` = introductory) + text label; WCAG 2.2 compliance | P13-WP8-T3 | 0.5 h | a11y |
| P13-WP8-T5 | Externalize strings en + fr; icons from `icons.md`; keyboard navigation through steps; SR: step rank, title, difficulty announced | P13-WP8-T3..T4 | 1 h | i18n, a11y |

---

## WP9 — Evaluation Harness

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP9-T1 | Define 20 synthetic query fixtures in `tests/evaluation/phase-13/queries.json`: varied goals (deep-dive fiction, research survey, beginner guide, etc.) against the 500-book metadata corpus | P13-WP2 | 2 h | evaluation harness |
| P13-WP9-T2 | Offline eval script `run-eval.ps1` / `run-eval.sh`: iterates queries; fires against mock provider (structural-only mode) and/or real provider (manual); records `structural_pass_rate`, `avg_explanation_length`, `confidence_distribution` | P13-WP9-T1 | 2 h | evaluation harness |
| P13-WP9-T3 | Mock-provider eval: run eval harness against deterministic mock provider; assert structural pass rate == 100%; commit result to `docs/benchmarks/phase-13/eval-mock-<date>.json` | P13-WP9-T2 | 1 h | FR-AI-003 structural gate |
| P13-WP9-T4 | Document eval process in `tests/evaluation/phase-13/README.md`: how to run against real provider; interpretation of metrics; what "structural pass" means vs quality | P13-WP9-T2 | 0.5 h | documentation |

---

## WP10 — Extension SDK Entry Points

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP10-T1 | `[ExtensionPoint]` custom attribute in `Application/` (`AttributeUsage.Class | Interface`); XML doc: "Marks an interface/class as a stable extension surface for Phase 23 Extension SDK" | Phase 02 | 0.5 h | SOURCE-SUMMARY delta #8 |
| P13-WP10-T2 | `IRecommendationSource` interface with `[ExtensionPoint]` attribute and XML doc (internal visibility; Phase 23 will publish); bind default `CatalogueReaderSource` impl in DI | P13-WP10-T1, P13-WP2-T2 | 1 h | delta #8 |
| P13-WP10-T3 | `IAiCatalogueReader` interface with `[ExtensionPoint]`; `GetByIdAsync`, `GetByShelfAsync`; default impl wraps `IBookRepository`; internal visibility | P13-WP10-T1 | 1 h | delta #8 |
| P13-WP10-T4 | Architecture test `ExtensionPoints_AreInternal_In_Phase13`: assert `IRecommendationSource` and `IAiCatalogueReader` have `InternalsVisibleTo` access only to test projects; no public visibility until Phase 23 | P13-WP10-T2..T3 | 0.5 h | delta #8 control |

---

## WP11 — Integration & Golden-Corpus

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P13-WP11-T1 | `RecommendationCard_HasExplanation_And_Confidence` test: run pipeline against `simple-text` + `bad-metadata` golden-corpus fixtures; assert every card passes structural validator | All WPs | 1.5 h | FR-AI-003 |
| P13-WP11-T2 | `ReadingPlan_StructuralOracle` test: run reading-plan service with goal "understand machine learning fundamentals" against 500-book corpus; mock provider; assert plan structural invariants | P13-WP4, P13-WP6 | 1.5 h | FR-AI-007 |
| P13-WP11-T3 | `AdvisorDisabled_CatalogueBrowse_Unaffected` end-to-end test with UI layer | P13-WP6-T3 | 1 h | FR-AI-001 |
| P13-WP11-T4 | Run `/code-review` on all WPs; record and resolve findings | All WPs | — | Global DoD |
