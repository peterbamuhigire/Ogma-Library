# Phase 30 — Advisor UX and Quality Evaluation

> [Roadmap index](./README.md) · [Previous](./phase-29-grounded-explanations-and-answer-mode.md) · [Next](./phase-31-native-3d-host-and-catalogue-contract.md)

## Objective
Expose a polished advisor and reading-plan experience only after measurable quality gates pass.

## Business/Product Rationale
The signature experience must be trustworthy, understandable and delightful, not a chat façade.

## SDLC Requirements
FR-AI-003..011, advisor evaluation categories, accessibility/performance/privacy NFRs.

## Current Repository State
`src/OgmaLibrary.App/Views/Ai/` and `tests/OgmaLibrary.Tests.Ui/AdvisorViewRenderTests.cs` exist but are not shell-integrated and provide no real relevance benchmark.

## Gap Analysis
No complete journey, history controls, evidence interaction, evaluation dashboard or launch thresholds.

## Architectural Impact
Advisor presentation consumes frozen services; no provider logic in view models.

## Database Work
Evaluation sets/runs/judgments and user history retention settings.

## Backend Work
Evaluation runner, metrics, regression comparison, diversity/availability/grounding gates and reading-plan validation.

## Frontend Work
Advisor route, onboarding, examples, streaming/progress, recommendation cards, evidence, plan, history/delete, feedback and degraded states.

## PDF Processing Impact
None.

## Metadata Impact
Shows source confidence and gaps.

## Search Impact
Search-to-advisor/advisor-to-catalogue transitions.

## AI/RAG Impact
Quality and UX completion; AI retrieval freeze.

## 3D Bookshelf Impact
Optional focus-on-shelf action via shared IDs.

## External Integrations
Scheduled quarantined live-provider evaluation; deterministic offline suite on every change.

## Privacy Requirements
Visible tier/provider/payload/history controls and feedback consent.

## Security Requirements
Safe rendering, cancellation, rate limits and abuse bounds.

## Performance Requirements
Retrieval and total latency/token/cost budgets; responsive cancellation.

## Error & Recovery Behaviour
Clear local fallback, retry and saved request; no invented result during outage.

## Logging/Observability
Stage latency, quality run IDs, availability, tokens/cost and user feedback without unnecessary prompt retention.

## Testing
Unit view models/eval metrics; DB retention; retrieval/RAG/live-provider eval; API contracts; UI/accessibility/E2E all eight prompt categories; failure/cancel; Precision@K/Recall/MRR/nDCG/grounding/latency/cost.

## Skills Engines Applied
`design-system-skills` advisor UX; `skills-web-dev` evals; `srs-skills` acceptance; digital-research evidence discipline.

## Dependencies
Phases 27–29.

## Parallelisation
UX and evaluation harness proceed against frozen output contracts; launch waits on both.

## Migration Considerations
Old mock/generated history separated from validated production history.

## Definition of Done
- [x] Advisor/privacy/plan routes are reachable.
- [ ] Offline and live evaluation meet approved thresholds.
- [ ] Explanations/evidence/trade-offs render accessibly.
- [x] History/export/delete and cost controls pass.
- [x] AI retrieval freeze recorded.

## Kaizen Review
1. Complexity: quality operations. 2. Reuse cards/evidence. 3. Simplify view models. 4. Remove scaffold tests/claims. 5. Document thresholds/runbook. 6. Pattern: eval-gated feature flag. 7. Debt decreases.
