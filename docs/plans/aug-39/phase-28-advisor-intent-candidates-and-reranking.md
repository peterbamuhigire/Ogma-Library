# Phase 28 — Advisor Intent, Candidates and Reranking

> [Roadmap index](./README.md) · [Previous](./phase-27-ai-gateway-privacy-and-cost-runtime.md) · [Next](./phase-29-grounded-explanations-and-answer-mode.md)

## Objective
Rewrite the advisor into an intent-aware, retrieval-first, catalogue-bounded ranking pipeline.

## Business/Product Rationale
Natural-language advice is Ogma's signature promise and must find conceptual matches in owned books.

## SDLC Requirements
FR-AI-003/004/007, reading-advisor and relevance requirements.

## Current Repository State
`src/OgmaLibrary.Application/Ai/AdvisorService.cs` and its infrastructure readers gate candidates through literal metadata search; semantic ranking occurs too late and no negative/difficulty/mood/length model exists.

## Gap Analysis
Pipeline cannot reliably handle the benchmark prompts.

## Architectural Impact
Intent parser produces structured constraints; Phase 26 returns candidates; reranker remains independently testable.

## Database Work
Versioned advisor requests, extracted intent, candidate/rank traces and cache keys.

## Backend Work
Deterministic/optional LLM intent extraction, filters, candidate union, constraint scoring, dedup/diversity and abstention.

## Frontend Work
Request composer, interpreted preferences editor and candidate-stage feedback.

## PDF Processing Impact
None.

## Metadata Impact
Page count, subjects, descriptions and availability become explicit features with known confidence.

## Search Impact
Consumes frozen structured/hybrid retrieval contracts.

## AI/RAG Impact
Primary retrieval/ranking; LLM unavailable fallback still returns ranked candidates.

## 3D Bookshelf Impact
Recommendations can focus books later through shared IDs.

## External Integrations
Optional completion model for intent only through gateway.

## Privacy Requirements
Only the request is needed for intent; no full catalogue is sent.

## Security Requirements
Bounded request/filters/candidates; model output validated against schema.

## Performance Requirements
Retrieval overhead meets advisor budget before explanation call.

## Error & Recovery Behaviour
Ambiguous intent is editable; no candidates yields reasons/suggestions, not invented books.

## Logging/Observability
Intent version, candidate counts, filters and latency; raw request subject to retention choice.

## Testing
Unit intent/negative filters/ranking; DB trace/cache; search pipeline; gateway/schema API; UI interpreted-intent E2E; unavailable/duplicate/failure; benchmark Recall@K/nDCG/latency.

## Skills Engines Applied
`skills-web-dev` AI retrieval/evaluation; `srs-skills` intent/use cases; design-system conversational UX.

## Dependencies
Phases 26–27.

## Parallelisation
Intent and reranking tracks can proceed against candidate contracts.

## Migration Considerations
Legacy advisor history remains viewable but is not reused as ranked evidence.

## Definition of Done
- [ ] All eight benchmark query categories produce correct intent structures.
- [ ] Concept queries are not keyword-gated.
- [ ] Only available catalogue IDs can rank.
- [ ] Negative constraints and diversity are tested.
- [ ] Provider-off fallback works.

## Kaizen Review
1. Complexity: intent/ranking. 2. Reuse search candidates. 3. Simplify advisor orchestration. 4. Delete literal-first reader. 5. Document features/abstention. 6. Pattern: editable interpreted intent. 7. Debt decreases critically.
