# Phase 13 — AI Reading Advisor & Plans

Single mission: deliver explainable, ranked recommendation cards from the user's
own collection, reading plans with structured rationale, and the foundations for
local-evidence answer mode — all routed through the Phase 12 AI gateway with
fully verifiable structural oracles.

---

## 1. Title & one-line mission

**Phase 13 — AI Reading Advisor & Plans**

Implement the `IAiAdvisorService` use cases — recommendation pipeline, reading
plans, and the V2 answer-mode stub — using the Phase 12 gateway, with
explainability, confidence scores, and an evaluation harness so every
recommendation can be inspected, not just accepted.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| Tier | MVP (recommendations + explanations) · V1 (reading plans, FR-AI-007) · V2 (answer mode with local evidence, FR-AI-008) |
| Estimate | 3 engineer-weeks |
| Owner | Peter Bamuhigire / Chwezi Core Systems |
| PRD build-phase mapping | Original Phase 6 (AI advisor) |
| Platforms | Windows + macOS |
| ADRs in scope | ADR-0007 (AI gateway); ADR-0006 (hybrid search — Phase 13 consumes Phase 11 ranking) |
| Skills cross-reference | `ai:ai-rag-patterns`, `ai:ai-evaluation`, `ai:ux-for-ai`, `ai:ai-output-design`, `claude-api` |

**Current implementation status (2026-06-01):** WP1-WP8 are implemented and
locally verified. This includes advisor confidence labels, recommendation
provenance/explanation/card models, reading-plan models, V2 answer-citation
model, en/fr resource keys for advisor confidence and difficulty labels, and the
metadata-only recommendation pipeline with local-catalogue provenance validation.
The Phase 11 hybrid-ranking adapter and weighted merger are wired behind a
default-off advisor option. Structured reading-plan generation now has an
embedded schema prompt, parser, retry-on-invalid-output, and gateway-backed
pipeline. `IAiAdvisorService` is typed, DI-wired, disabled when the active tier
is Offline, and answer mode is scaffolded as V2. Recommendation and reading-plan
Avalonia surfaces are implemented and headless-rendered. The WP9 offline
evaluation harness and benchmark are complete, and WP10 extension markers are
seeded as internal surfaces for Phase 23. WP11 closeout tests, review evidence,
accessibility evidence, and full local gates are complete.

---

## 3. Objectives

1. **Explainable recommendations.** `IAiAdvisorService.GetRecommendationsAsync`
   returns ranked `RecommendationCard` objects each with a human-readable
   explanation, a confidence score, and the provenance of the match (which fields
   matched, which model, which tier).
2. **Structural oracle gate.** Because recommendation relevance is a judgement
   call (VERIFIABILITY-FAIL, FR-AI-003), every automated test gates
   *structural completeness* (every card has an explanation, a confidence in
   [0,1], at least one provenance item, no null fields) rather than subjective
   quality.
3. **Local-only provenance.** The recommendation pipeline never invents books
   outside the user's own catalogue; the provenance list cites only `BookId`
   values that exist in the local DB.
4. **Reading plans (V1).** `GetReadingPlanAsync` produces a structured plan:
   ordered sequence of `BookId` references, per-book rationale, difficulty label,
   and checkpoints — all verifiable structurally (FR-AI-007).
5. **Answer mode stub (V2).** `GetAnswerAsync` is scaffolded with the
   local-evidence citation model (book/page/chunk/confidence); the full
   implementation is V2, but the interface and data types are final here so no
   breaking change is needed in V2.
6. **Recommendation evaluation harness.** An `ai-evaluation`-informed test
   harness runs offline, scores structural quality, and records results in
   `docs/benchmarks/phase-13/`; it is not a quality gate (VERIFIABILITY-FAIL)
   but informs prompt tuning.
7. **Extension SDK entry points seeded.** The `IRecommendationSource` and
   `IAiCatalogueReader` interfaces are defined as the stable extension points
   that Phase 23's Extension SDK will expose to community plugins.

---

## 4. Scope

### In scope

- `IAiAdvisorService` full implementation (`AdvisorService` in `Application`).
- `RecommendationCard`, `RecommendationExplanation`, `ConfidenceScore`,
  `ReadingPlan`, `ReadingPlanStep`, `AnswerCitation` domain types.
- Recommendation pipeline: MVP metadata-only path (Tier-1) + V1 hybrid path
  (Tier-1 metadata + Phase 11 cosine-similarity ranking signals).
- Reading plan generation (V1): structured prompt + structured response parsing;
  `ReadingPlan` validated against structural oracle.
- Answer mode interface and data types (V2) — implementation scaffolded, returns
  `NotImplementedException` in this phase; full in Phase 15 / post-V1.
- `RecommendationUI`: a recommendation panel in the catalogue/library view showing
  ranked cards with explanation and confidence badge.
- Reading plan view: sequence list with rationale expansions and difficulty badges.
- Recommendation evaluation harness: offline script using `ai:ai-evaluation`
  patterns; scores structural quality on the golden-corpus metadata set.
- `IRecommendationSource` and `IAiCatalogueReader` extension-point interfaces
  (sealed to internal for now; will be opened in Phase 23 Extension SDK).
- All strings en + fr; icon manifest for this phase.

### Explicitly out of scope

- Full local-evidence answer mode implementation (V2, Phase post-13 or Phase 23).
- Extension SDK publication and plugin harness (Phase 23).
- School-managed AI entitlements (Phase 18).
- Semantic embedding model (Phase 11 owns that; Phase 13 consumes its ranking
  output via `IHybridRanker`).
- OCR content indexing for recommendations (Phase 15 adds OCR; Phase 13 uses
  whatever text is already indexed).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-AI-001 | MVP | AI fully disableable; never blocks catalogue | `AdvisorDisabled_CatalogueBrowse_Unaffected` integration test |
| FR-AI-003 | MVP | Ranked recommendations with explanation + confidence | `RecommendationCard_HasExplanation_And_Confidence` structural test (VERIFIABILITY-FAIL gate: structural completeness only) |
| FR-AI-007 | V1 | Reading plan: sequence/rationale/difficulty/checkpoints | `ReadingPlan_StructuralOracle` test (all fields non-null, sequence non-empty, difficulty valid label) |
| FR-AI-008 | V2 | Answer mode citing local evidence | `GetAnswerAsync_ReturnsNotImplemented_Before_V2` scaffold test; interface and `AnswerCitation` types final |
| ADR-0007 | MVP | All calls route through `IAiProvider` gateway | Architecture test inherited from Phase 12; `AdvisorService_UsesOnlyAiGateway` |

---

## 6. Dependencies

### Depends on

| Dependency | Why |
| --- | --- |
| Phase 12 — AI Gateway | `AiGateway`, `IAiProvider`, `IAiPrivacyService`, tier enforcement, audit — all consumed here |
| Phase 10 — Search & Indexing | FTS5 index metadata; `IMetadataSearchService` used in metadata-only recommendation path |
| Phase 11 — Semantic Search | `IHybridRanker` consumed for V1 hybrid recommendation path; cosine similarity as ranking signal |
| Phase 04 — Catalogue & Data | `BookRepository.GetAllMetadataAsync()` — source of provenance records |

### Unblocks

| Unblocked | How |
| --- | --- |
| Phase 18 — School-Managed AI | Students consume `IAiAdvisorService` recommendations via managed gateway |
| Phase 23 — Extension SDK | `IRecommendationSource` and `IAiCatalogueReader` extension points defined here |

---

## 7. Architecture & approach

### 7.1 Bounded contexts touched

- **AI Advisor** (primary — extends Phase 12 foundation).
- **Catalogue** (read-only: book metadata, shelves, reading progress).
- **Search Index** (read-only: FTS5 scores, embedding scores via `IHybridRanker`).

### 7.2 Recommendation pipeline (two paths)

**MVP: Metadata-only (Tier-1)**

```
CatalogueReader.GetCandidatesAsync(query)
  -> MetadataEnricher.BuildMetadataPayload(candidates)
  -> AiGateway.SendAsync(payload)       // Tier-1: title/author/tags/desc
  -> ResponseParser.ParseRecommendations(response)
  -> Provenance validator: every BookId exists in catalogue
  -> StructuralValidator: all fields non-null, confidence in [0,1]
  -> RecommendationCard[] (ranked)
```

**V1: Hybrid path (Tier-1 metadata + Phase 11 semantic scores)**

```
HybridRanker.RankAsync(query, candidates)  // cosine + recency + status signals
  -> MetadataEnricher.BuildMetadataPayload(top-K ranked candidates)
  -> AiGateway.SendAsync(payload)
  -> ResponseParser.ParseRecommendations(response)
  -> HybridRanker.Merge(aiRanking, semanticRanking)  // weighted merge
  -> StructuralValidator + ProvenanceValidator
  -> RecommendationCard[] (merged ranked)
```

The hybrid path is toggled by `AdvisorOptions.UseHybridRanking` (default false
until Phase 11 is confirmed performant).

### 7.3 Structural oracle rationale

FR-AI-003 and FR-AI-007 are `VERIFIABILITY-FAIL` because recommendation quality
and pedagogical value are subjective judgements. The automated gate therefore
tests *structural completeness*:

- Every `RecommendationCard`: `Explanation` non-null and non-empty; `Confidence`
  in `[0.0, 1.0]`; `Provenance` list has at least one item; every `BookId` in
  `Provenance` resolves to a real book in the local catalogue.
- Every `ReadingPlan`: `Steps` list non-empty; each `Step.BookId` resolves;
  `Step.Rationale` non-empty; `Step.Difficulty` is a valid `DifficultyLabel` enum
  value; `Plan.Checkpoints` list present (may be empty for short plans).

These structural tests are deterministic and serve as the phase acceptance gate.
The evaluation harness (§7.5) provides non-gating quality signals.

### 7.4 Reading plan generation

`GetReadingPlanAsync(ReadingPlanRequest)` sends a structured prompt to the AI
provider (via the gateway):

- **Prompt input:** ordered `BookMetadataDto[]` (Tier-1 fields only); goal string
  (user's stated learning objective); difficulty preference.
- **Expected response:** a JSON object matching `ReadingPlanDto` schema, validated
  by `ReadingPlanParser.Parse()` before converting to `ReadingPlan` domain type.
- **Structural validator:** asserts schema validity; rejects and retries once on
  parse failure; surfaces `AdvisorParseException` if second attempt also fails.

### 7.5 Recommendation evaluation harness

Informed by `ai:ai-evaluation` skill. An offline, non-CI script:

- Uses the golden-corpus 500-book metadata seed as the candidate set.
- Fires 20 synthetic queries (stored as JSON fixtures in
  `tests/golden-corpus/ai-evaluation-queries/`).
- Records: structural pass rate (must be 100%); explanation length distribution;
  confidence distribution; provider latency.
- Results written to `docs/benchmarks/phase-13/eval-<date>.json`.
- Not a CI gate (subjective quality) but reviewed in the phase code review.

### 7.6 Extension SDK entry points

Two interfaces are defined in `Application/Ai/Extensions/` and marked
`[ExtensionPoint]` (a custom attribute that Phase 23 will use to discover
extension surfaces):

```csharp
/// <summary>
/// [ExtensionPoint] Source of candidate books for the recommendation pipeline.
/// Phase 23 Extension SDK will open this interface to community plugins.
/// </summary>
public interface IRecommendationSource
{
    Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
        RecommendationQuery query, CancellationToken ct);
}

/// <summary>
/// [ExtensionPoint] Read-only view of the local catalogue for AI use cases.
/// Exposed to extensions via a sandboxed proxy in Phase 23.
/// </summary>
public interface IAiCatalogueReader
{
    Task<BookMetadataDto?> GetByIdAsync(BookId id, CancellationToken ct);
    Task<IReadOnlyList<BookMetadataDto>> GetByShelfAsync(
        ShelfId shelfId, CancellationToken ct);
}
```

Both are `internal` until Phase 23 opens them. Their existence here ensures no
breaking interface change is needed when the SDK is published.

### 7.7 Cross-platform notes

- No platform-specific code in this phase; purely managed C# consuming the Phase
  12 gateway and Phase 11 ranking.
- UI (recommendation panel, reading plan view): pure Avalonia XAML + ViewModels;
  no WebView.
- Provider calls are async and cancellation-token-aware; no UI thread blocking.

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| WP | Work Package | Key tasks |
| --- | --- | --- |
| WP1 | Domain types | `RecommendationCard`, `ReadingPlan`, `AnswerCitation`, `ConfidenceScore`, `DifficultyLabel`, `ReadingPlanStep` |
| WP2 | Recommendation pipeline | `CatalogueReader`, `MetadataEnricher`, `ResponseParser`, `ProvenanceValidator`, `StructuralValidator`, MVP path |
| WP3 | Hybrid ranking integration | `HybridRanker` consumer; V1 toggle; merged ranking |
| WP4 | Reading plan service | `GetReadingPlanAsync`; prompt template; `ReadingPlanParser`; structural oracle |
| WP5 | Answer mode scaffold | `GetAnswerAsync` stub; `AnswerCitation` types; V2 tracking item |
| WP6 | `AdvisorService` composition | Wire WP2-5 into `AdvisorService`; DI registration; `IsEnabled` guard |
| WP7 | Recommendation UI | `RecommendationPanelView` + `RecommendationPanelViewModel`; cards with explain + confidence |
| WP8 | Reading plan UI | `ReadingPlanView` + `ReadingPlanViewModel`; sequence, rationale, difficulty, checkpoints |
| WP9 | Evaluation harness | Offline eval script; 20 query fixtures; benchmark results |
| WP10 | Extension SDK entry points | `IRecommendationSource`, `IAiCatalogueReader`; `[ExtensionPoint]` attribute |
| WP11 | Integration & golden-corpus | Structural tests; provenance tests; disabled-AI tests |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest** — `icons.md` lists recommendation, explanation,
      confidence, reading-plan, answer/cite icons; owner procurement request appended.
- [x] **i18n (en/fr)** — All recommendation panel labels, reading plan UI, difficulty
      labels, confidence wording, and error messages are resource-keyed; `fr` present
      in same PR; pseudolocale check passes.
- [x] **Accessibility** — Recommendation cards keyboard-navigable (Tab to card, Enter
      to expand explanation); confidence badges have text equivalents; reading-plan
      sequence list navigable by keyboard; screen-reader announces card rank and
      confidence.
- [x] **Privacy/egress** — All recommendation and reading-plan calls route through
      `AiGateway`; no direct provider calls; architecture test inherited from Phase 12.
- [x] **Reversibility** — Recommendations and reading plans are generated on demand,
      not persisted as authoritative data; no destructive write-back.
- [x] **Performance budgets** — Recommendation panel renders within NFR-PROD-003
      (first screen ≤ 1 s; page ≤ 200 ms) in offline/disabled mode; AI response
      time is outside our control but the panel shows a progress indicator.
- [x] **Bounded-context tests** — `AdvisorContext_HasNo_DirectDependency_On_Reader`;
      `AdvisorService_UsesOnlyAiGateway`.
- [x] **Documentation** — XML doc comments on all public interfaces; `[ExtensionPoint]`
      attribute documented; HLD §7 updated with recommendation pipeline.

---

## 10. Definition of Done

- [ ] Every FR/NFR ID in section 5 has a passing deterministic test or tagged gap.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format`, `dotnet build` (warnings = errors), `dotnet test`, architecture
      tests pass on Windows and macOS CI.
- [ ] New strings in `en` + `fr`; pseudolocale check passes.
- [ ] Recommendation cards, plan steps, and explain/confidence badges have icons and
      accessible labels; keyboard + SR walkthrough passes; `icons.md` complete.
- [ ] ADR-0007 updated with advisor service implementation notes; HLD §7 updated.
- [x] `RecommendationCard_HasExplanation_And_Confidence` structural test passes
      (FR-AI-003 structural gate).
- [x] `ReadingPlan_StructuralOracle` test passes (FR-AI-007 structural gate).
- [x] `AdvisorDisabled_CatalogueBrowse_Unaffected` integration test passes (FR-AI-001).
- [x] Evaluation harness run produces ≥ 95% structural pass rate on the 20 synthetic
      queries; results committed to `docs/benchmarks/phase-13/`.
- [x] `IRecommendationSource` and `IAiCatalogueReader` defined with `[ExtensionPoint]`
      attribute and XML doc comments (Phase 23 readiness, SOURCE-SUMMARY delta #8).
- [x] `/code-review` completed; findings resolved.

---

## 11. Skills to use

Full detail in `skills.md`.

| Skill | Task |
| --- | --- |
| `ai:ai-rag-patterns` | WP2: recommendation pipeline design (RAG-style retrieval over local catalogue) |
| `ai:ai-evaluation` | WP9: evaluation harness design; structural quality scoring |
| `ai:ai-output-design` | WP7/WP8: recommendation card and reading plan display design |
| `ai:ux-for-ai` | WP7/WP8: explainability UX; confidence badge design |
| `ai:ai-feature-spec` | WP4: structured reading-plan prompt template |
| `claude-api` | WP4: Anthropic reading-plan call with prompt caching on book-metadata context |
| `frontend-design:frontend-design` | WP7/WP8: recommendation panel and reading-plan view |
| `superpowers:test-driven-development` | All WPs |

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| Domain types (`RecommendationCard`, `ReadingPlan`, etc.) | `src/OgmaLibrary.Domain/Ai/` |
| `IAiAdvisorService` implementation `AdvisorService` | `src/OgmaLibrary.Application/Ai/` |
| `CatalogueReader`, `MetadataEnricher`, `ResponseParser`, validators | `src/OgmaLibrary.Infrastructure/Ai/Advisor/` |
| `IRecommendationSource`, `IAiCatalogueReader`, `[ExtensionPoint]` | `src/OgmaLibrary.Application/Ai/Extensions/` |
| `RecommendationPanelView/ViewModel`, `ReadingPlanView/ViewModel` | `src/OgmaLibrary.App/Views/Ai/` |
| Evaluation harness (offline script + query fixtures) | `tests/evaluation/phase-13/` |
| Benchmark results | `docs/benchmarks/phase-13/` |
| `icons.md` (Phase 13 manifest) | `docs/plans/grand-plan/phase-13/icons.md` |
| HLD §7 update | `docs/references/HLD.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Provider returns structurally invalid JSON for reading plan | R5 | `ReadingPlanParser` retries once; on second failure surfaces `AdvisorParseException` with user-visible fallback message |
| Provenance validator finds a `BookId` not in local DB (hallucination) | R2 | `ProvenanceValidator` strips any non-local BookId; if > 50% are invalid, surfaces a warning and falls back to local-only ranking |
| Hybrid ranking integration fragile across Phase 11 interface changes | R5 | Consume `IHybridRanker` through an anti-corruption adapter; pin Phase 11 interface version |
| Evaluation harness slow due to provider latency | R5 | Harness uses a mock provider for structural tests; real-provider run is manual and off CI critical path |
| Extension SDK entry points freeze an immature API | R5 | Interfaces are `internal`; `[ExtensionPoint]` attribute is a marker only; no public API commitment until Phase 23 |

---

## 14. Owner asks

1. **Icon procurement.** Procure the icon set listed in `icons.md` (recommend,
   explain, confidence, reading-plan, answer/cite icons) in the Phase 03 style.

2. **Evaluation quality bar.** Confirm whether there is an acceptance threshold
   for recommendation quality that goes beyond structural completeness — for
   example, a manual spot-check pass by the owner before phase DoD. The structural
   test is the automated gate; owner spot-check is optional but recommended.

3. **Reading plan difficulty labels.** Confirm the set of `DifficultyLabel` values
   to use in `fr` (draft: `Débutant`, `Intermédiaire`, `Avancé`,
   `Expert`). Final wording needs native review before UI copy is locked.

4. **Answer-mode priority.** FR-AI-008 (answer mode) is V2. Confirm whether to
   keep it as V2 or accelerate it into V1 scope. This affects Phase 15 planning.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Chwezi Core Systems | v1.0 baseline authored |
| 2026-06-01 | Codex | WP1 domain contracts implemented and locally verified; see `evidence.md`. |
| 2026-06-01 | Codex | WP2 metadata-only recommendation pipeline implemented and locally verified. |
| 2026-06-01 | Codex | WP3 hybrid ranking adapter and weighted merger implemented behind default-off option. |
| 2026-06-01 | Codex | WP4 structured reading-plan parser and gateway-backed pipeline implemented. |
| 2026-06-01 | Codex | WP5-WP6 answer-mode scaffold and typed advisor service composition implemented. |
| 2026-06-01 | Codex | WP7-WP8 recommendation and reading-plan Avalonia surfaces implemented and render-tested. |
