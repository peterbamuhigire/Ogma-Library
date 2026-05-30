# Phase 13 — Test Plan

---

## 1. Test layers active

| Layer | Active | Notes |
| --- | --- | --- |
| Domain unit | Yes | `ConfidenceScore`, `RecommendationCard`, `ReadingPlan` invariant tests |
| Integration | Yes | Pipeline, parser, validator, advisor service end-to-end |
| UI / ViewModel | Yes | `RecommendationPanelViewModel`, `ReadingPlanViewModel` command tests |
| Architecture | Yes | `AdvisorService_UsesOnlyAiGateway`; extension-point visibility |
| Evaluation harness | Yes (offline) | 20 query fixtures; structural-only mode in CI |
| Performance | No | Phase 20 owns full benchmarks; Phase 13 has no new NFR budget |
| Accessibility | Yes | Confidence badges, difficulty badges, keyboard navigation |
| Golden corpus | Yes | `simple-text` and `bad-metadata` fixtures for pipeline tests |

---

## 2. Verifiability note (VERIFIABILITY-FAIL requirements)

FR-AI-003 and FR-AI-007 are `VERIFIABILITY-FAIL` — relevance and pedagogy are
judgements, not oracles. The automated tests gate **structural completeness only**:

| Structural sub-claim | Oracle |
| --- | --- |
| Every card has non-null/non-empty Explanation | `string.IsNullOrWhiteSpace(card.Explanation.Summary) == false` |
| Every card Confidence in [0.0, 1.0] | `card.Confidence.Value >= 0.0 && card.Confidence.Value <= 1.0` |
| Every card Provenance list non-empty | `card.Explanation.ProvenanceItems.Count >= 1` |
| Every Provenance BookId is local | `await repo.ExistsAsync(item.BookId) == true` for every item |
| ReadingPlan Steps list non-empty | `plan.Steps.Count >= 1` |
| Each Step.Difficulty is valid | `Enum.IsDefined(typeof(DifficultyLabel), step.Difficulty) == true` |
| Each Step.Rationale non-empty | `string.IsNullOrWhiteSpace(step.Rationale) == false` |

These are deterministic binary assertions and constitute the acceptance gate.

---

## 3. Test fixtures

| Fixture | Source | Used by |
| --- | --- | --- |
| `simple-text` golden corpus | Phase 05 | P13-WP11-T1 pipeline structural test |
| `bad-metadata` golden corpus | Phase 05 | P13-WP11-T1 pipeline robustness (missing fields) |
| 500-book synthetic metadata corpus | Phase 02 seed | P13-WP11-T2 reading plan test |
| `tests/evaluation/phase-13/queries.json` (20 queries) | Phase 13 WP9 | Eval harness |
| WireMock recommendation response fixture | Recorded from provider sandbox | P13-WP2-T8, WP4-T5 |
| WireMock malformed JSON response fixture | Handcrafted | P13-WP4-T6 retry test |

---

## 4. Key deterministic test assertions

| Test | Oracle |
| --- | --- |
| `RecommendationCard_HasExplanation_And_Confidence` | 100% structural pass rate on golden-corpus run |
| `ReadingPlan_StructuralOracle` | `plan.Steps.Count >= 1` and all Step fields valid |
| `ProvenanceValidator_Strips_HallucinatedIds` | Stripped card provenance count == 2 (from 3 items with 1 hallucinated) |
| `HybridPath_MergesSemanticAndAiScores` | Merged rank 1 corresponds to highest weighted score (deterministic with seeded mock scores) |
| `AdvisorDisabled_CatalogueBrowse_Unaffected` | Catalogue book count unchanged; no exception propagated to UI |
| `ExtensionPoints_AreInternal_In_Phase13` | Reflection asserts `IsPublic == false` on both interfaces |

---

## 5. Evaluation harness CI integration

- The evaluation harness (`run-eval.ps1`) runs in **mock-provider mode** on CI
  (no real API calls, no cost) after `dotnet test`.
- A structural-only CI run uses a deterministic mock provider returning
  pre-recorded well-formed responses.
- Real-provider runs are manual: documented in `tests/evaluation/phase-13/README.md`,
  results committed manually after a developer review session.
- The CI gate asserts `structural_pass_rate == 1.0` (100%) in the mock run.

---

## 6. Accessibility tests

| Surface | Test |
| --- | --- |
| `RecommendationPanelView` | Keyboard: Tab to first card, Enter to open book, Space/Enter to expand "Why?" explanation; SR: card rank (e.g., "Recommendation 1 of 5") and confidence label announced |
| Confidence badge | Text label present alongside color (`Low`, `Medium`, `High`, `Very High`); no color-only state encoding |
| `ReadingPlanView` | Keyboard: Tab through steps, Enter to open book; SR: step rank, title, difficulty label announced |
| Difficulty badge | Text label alongside color; `DifficultyLabel` enum localized in en + fr |
