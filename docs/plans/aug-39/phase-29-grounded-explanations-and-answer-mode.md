# Phase 29 — Grounded Explanations and Answer Mode

> [Roadmap index](./README.md) · [Previous](./phase-28-advisor-intent-candidates-and-reranking.md) · [Next](./phase-30-advisor-ux-and-quality-evaluation.md)

## Objective
Generate recommendation explanations and answers from source-labeled metadata/passages with citations and uncertainty.

## Business/Product Rationale
Users need to know why a recommendation fits and what evidence supports it.

## SDLC Requirements
FR-AI-003/005/008, explainability, hallucination prevention and answer-mode requirements.

## Current Repository State
`src/OgmaLibrary.Application/Ai/AdvisorService.cs` builds metadata explanations without claim evidence and its answer-mode path throws `NotImplementedException`.

## Gap Analysis
No evidence DTO, passage attribution, citation validator, mismatch explanation or grounded fallback.

## Architectural Impact
Evidence assembly is deterministic; generation is optional; validation gates all claims/results.

## Database Work
Evidence references, source/version, prompt/template/model, generated claim/citation and validation outcome.

## Backend Work
Evidence selection, context budgets, deterministic match/mismatch summaries, provider prompt, citation/ID validation and abstention.

## Frontend Work
Recommendation cards with “matches,” “trade-offs,” evidence source/page, confidence and open-at-page; cited answer view.

## PDF Processing Impact
Uses versioned page/TOC evidence only.

## Metadata Impact
Distinguish description/subject facts from title inference.

## Search Impact
Evidence comes from Phase 26 candidates/Phase 23 anchors.

## AI/RAG Impact
Primary deliverable; every artifact versioned.

## 3D Bookshelf Impact
Result action may focus recommended volumes.

## External Integrations
Completion provider only through Phase 27 tier/preview.

## Privacy Requirements
Passages require explicit content tier; metadata-only explanation remains available.

## Security Requirements
Prompt-injection-resistant document handling; treat passages as data; output schema/citation validation.

## Performance Requirements
Bound evidence/token size; cache by query/candidate/source/model/prompt versions.

## Error & Recovery Behaviour
Invalid/unsupported claims are removed or downgraded; deterministic evidence cards survive provider failure.

## Logging/Observability
Evidence count, citation coverage, unsupported claims, tokens/cost/latency; no passage text logs.

## Testing
Unit evidence/citation/schema/prompt-injection; DB version/invalidation; RAG pipeline; provider API recordings; UI citation E2E; privacy tier tests; unsupported-claim eval; latency/cost performance.

## Skills Engines Applied
`skills-web-dev` RAG/security/evaluation; `srs-skills` explainable acceptance; design-system evidence UI.

## Dependencies
Phase 28.

## Parallelisation
Evidence assembler/validator and UI cards can proceed; prompts wait for evidence contract.

## Migration Considerations
Legacy explanations are labeled ungrounded or excluded from current history.

## Definition of Done
- [x] Every content claim has a resolvable source or uncertainty label.
- [x] Metadata-only and content-aware tiers are distinct.
- [x] Answer mode no longer throws/not-scaffolded.
- [x] Prompt-injection and citation validation pass.
- [x] Provider failure retains useful grounded results.

## Kaizen Review
1. Complexity: claims/evidence/versioning. 2. One evidence model for advisor/answers. 3. Simplify prompts. 4. Delete generic ungrounded explanation path. 5. Document evidence rules. 6. Pattern: validate-after-generate. 7. Debt decreases.
