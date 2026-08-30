# Phase 23 — Full-Text Pipeline and Search

> [Roadmap index](./README.md) · [Previous](./phase-22-structured-and-fuzzy-catalogue-search.md) · [Next](./phase-24-selective-ocr-and-extraction-quality.md)

## Objective
Deliver page-aware full-text extraction, FTS indexing, snippets and reader navigation.

## Business/Product Rationale
Users need to find ideas inside books without cloud or AI dependencies.

## SDLC Requirements
FR-SEARCH-002/003/006, FR-READ-004, content processing requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Search/IndexManagerService.cs` plus catalogue page/chunk/FTS entities exist; lifecycle, extraction quality and complete result jump are partial.

## Gap Analysis
No fully versioned extractor-to-FTS invalidation, robust stale cleanup or scale/reference acceptance.

## Architectural Impact
Full-text index is a derived projection keyed by asset/extraction version.

## Database Work
FTS content/version/status, page anchors, rebuild checkpoints and orphan cleanup.

## Backend Work
Transactional batch indexing, snippets/highlights, query grammar, targeted rebuild and integrity checker.

## Frontend Work
Full-text mode, source/page snippets, progress, no-index and jump-to-reader states.

## PDF Processing Impact
Consumes Phase 11 text and Phase 24 OCR selectively.

## Metadata Impact
Display metadata only; index separation retained.

## Search Impact
Primary deliverable; score remains separately calibrated.

## AI/RAG Impact
FTS provides lexical candidates and evidence anchors.

## 3D Bookshelf Impact
Result set can filter/focus shelf via shared IDs.

## External Integrations
None.

## Privacy Requirements
All text/index local; snippets not logged.

## Security Requirements
Bounded FTS grammar and safe snippets.

## Performance Requirements
p95 ≤500 ms at reference corpus; rebuild is resumable and background.

## Error & Recovery Behaviour
Corrupt index rebuilds without touching catalogue/source files.

## Logging/Observability
Indexed pages/books, lag, version, query latency and integrity failures.

## Testing
Unit grammar/snippets; DB FTS/invalidation; PDF extraction/index pipeline; API page anchors; UI reader-jump E2E; crash/rebuild; Unicode/large corpus; 50k/query performance.

## Skills Engines Applied
`skills-web-dev` search/index lifecycle; `srs-skills` traceability; design-system search states.

## Dependencies
Phases 11, 17 and 21.

## Parallelisation
Indexer/integrity and UI result experience can proceed against page-anchor contract.

## Migration Considerations
Rebuild legacy FTS side-by-side and swap after validation.

## Definition of Done
- [ ] Index version follows extraction version.
- [ ] Deleted/changed assets leave no stale rows.
- [ ] Snippets jump to the correct reader page.
- [ ] Rebuild resumes after crash.
- [ ] Latency budget passes.

## Kaizen Review
1. Complexity: derived index lifecycle. 2. Reuse version manifests. 3. Simplify search modes. 4. Remove stale legacy rows. 5. Document grammar/rebuild. 6. Pattern: side-by-side projection rebuild. 7. Debt decreases.
