# Phase 22 — Structured and Fuzzy Catalogue Search

> [Roadmap index](./README.md) · [Previous](./phase-21-reader-completion-and-portability.md) · [Next](./phase-23-full-text-pipeline-and-search.md)

## Objective
Provide fast fielded, faceted and typo-tolerant catalogue search.

## Business/Product Rationale
Known-item lookup must work for misspellings such as “tolkein” without AI.

## SDLC Requirements
FR-SEARCH-001, catalogue filters, ≤150 ms metadata-search NFR.

## Current Repository State
Search implementations under `src/OgmaLibrary.Infrastructure/Search/` use SQL `%contains%` over selected fields; no fuzzy matching and limited scale evidence.

## Gap Analysis
Normalization, field weights, typo tolerance, highlighting and query explainability absent.

## Architectural Impact
Define `CatalogueSearchQuery/Result` independent of FTS/semantic services.

## Database Work
Normalized search columns/indexes or an approved local fuzzy index; version/status metadata.

## Backend Work
Parser, exact/prefix/fuzzy ranking, field filters, stable paging and score explanation.

## Frontend Work
Typeahead, chips/facets, corrections, match highlighting and keyboard result navigation.

## PDF Processing Impact
None.

## Metadata Impact
Canonical changes update only affected search records.

## Search Impact
Primary deliverable.

## AI/RAG Impact
Structured filters become advisor constraints.

## 3D Bookshelf Impact
Search result IDs can focus/filter the shelf later.

## External Integrations
None.

## Privacy Requirements
Queries local and not logged verbatim by default.

## Security Requirements
Parameterized queries; bounded parser and result size.

## Performance Requirements
p95 ≤150 ms on 50k catalogue for accepted query classes.

## Error & Recovery Behaviour
Index unavailable falls back to bounded exact database search with status notice.

## Logging/Observability
Latency, result count, query class and fallback, not raw private query.

## Testing
Unit parser/ranking/typos; DB index/SQL injection; API paging; UI typeahead/accessibility; E2E “tolkein”; 50k latency/relevance tests.

## Skills Engines Applied
`skills-web-dev` search/database/performance; `design-system-skills` search UX; `srs-skills` acceptance.

## Dependencies
Phases 12 and 19.

## Parallelisation
Index/ranker and search UI proceed from query/result contracts.

## Migration Considerations
Backfill normalized index in background; catalogue remains usable.

## Definition of Done
- [x] Exact/prefix/fuzzy behavior is deterministic.
- [x] “tolkein” fixture finds Tolkien.
- [x] Filters/paging/highlights are correct.
- [x] Fallback works during rebuild.
- [x] 50k p95 budget passes.

## Kaizen Review
1. Complexity: ranking/index. 2. Share normalization. 3. Simplify metadata search call sites. 4. Remove raw `%contains%` as sole path. 5. Document scoring. 6. Pattern: local explainable result. 7. Debt decreases.
