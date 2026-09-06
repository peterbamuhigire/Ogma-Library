# Phase 26 — Semantic and Hybrid Retrieval

> [Roadmap index](./README.md) · [Previous](./phase-25-versioned-embeddings-and-vector-lifecycle.md) · [Next](./phase-27-ai-gateway-privacy-and-cost-runtime.md)

## Objective
Deliver scalable, calibrated semantic retrieval and principled hybrid fusion.

## Business/Product Rationale
Concept discovery must return relevant owned books, not merely “something.”

## SDLC Requirements
FR-SEARCH-004/005, semantic relevance/evaluation requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Search/SemanticSearchService.cs` loads vectors for local cosine, while `CombinedSearchService.cs` adds incompatible score families naively.

## Gap Analysis
No calibrated fusion, structured filters, diversity, benchmark corpus or quality gate.

## Architectural Impact
Retrieval returns candidates plus component scores/evidence; advisor consumes it later.

## Database Work
Evaluation judgments/runs, calibrated parameters and optional ANN index metadata.

## Backend Work
Query embedding, structured prefilter, lexical/semantic candidate union, reciprocal-rank or learned-calibrated fusion, dedup/diversity and explanations.

## Frontend Work
Natural-language search mode, evidence snippets, filters, unavailable/index-degraded states.

## PDF Processing Impact
None beyond versioned sources.

## Metadata Impact
Metadata scores remain explicit components.

## Search Impact
Primary deliverable and search-contract freeze.

## AI/RAG Impact
Provides advisor candidate retrieval without requiring completion model.

## 3D Bookshelf Impact
Result IDs can focus/filter shelves.

## External Integrations
Embedding provider via Phase 25 only.

## Privacy Requirements
Local query path default; remote embedding governed and disclosed.

## Security Requirements
Bound query/filter/result sizes; provider results validated.

## Performance Requirements
Approved p95 at 50k catalogue/representative chunks; bounded memory.

## Error & Recovery Behaviour
Missing semantic index falls back to structured+FTS with honest status.

## Logging/Observability
Component latency/candidate counts/fusion version without raw query by default.

## Testing
Unit fusion/dedup; DB evaluation runs; embedding pipeline; API filters/evidence; UI/E2E concept queries; fallback; Precision/Recall/MRR/nDCG and scale performance.

## Skills Engines Applied
`skills-web-dev` search/RAG/evaluation; `srs-skills` measurable acceptance; `design-system-skills` discovery UX.

## Dependencies
Phases 22–25.

## Parallelisation
Fusion experiments and UI can proceed after result contract; freeze follows benchmark.

## Migration Considerations
Run old/new retrieval side-by-side for offline comparison; do not preserve bad scores for compatibility.

## Definition of Done
- [x] Concept queries retrieve benchmark-relevant books.
- [x] Scores/fusion are versioned and explainable.
- [x] Duplicates/unavailable items are controlled.
- [x] Degraded lexical fallback works.
- [x] Search contract freeze is recorded.

## Kaizen Review
1. Complexity: multi-retriever fusion. 2. One candidate/evidence DTO. 3. Simplify advisor dependency. 4. Delete naive score addition. 5. Document evaluation. 6. Pattern: evidence-bearing candidate. 7. Debt decreases.
