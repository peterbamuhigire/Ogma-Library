# Phase 9 — Duplicate and Bibliographic Resolution

> [Roadmap index](./README.md) · [Previous](./phase-08-filesystem-reconciliation-and-recovery.md) · [Next](./phase-10-pdf-validation-and-containment.md)

## Objective
Distinguish exact copies, duplicate editions, same work/different editions and similar titles.

## Business/Product Rationale
Correct identity is the foundation for metadata, search, recommendations and user trust.

## SDLC Requirements
FR-CAT-007, FR-LIB-003, metadata confidence and duplicate audit requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Catalogue/BookIdentityService.cs` claims five tiers but includes unsafe shortcuts and an unimplemented ISBN/DOI tier.

## Gap Analysis
No calibrated bibliographic resolution or merge/split/review UI.

## Architectural Impact
Introduce deterministic exact-file binding and scored edition/work match proposals.

## Database Work
External IDs, normalized identifiers, match candidates, decisions, aliases and merge/split audit.

## Backend Work
ISBN validation, title/author/year normalization, conflict rules, reversible merge/split.

## Frontend Work
Duplicate review showing evidence and consequences.

## PDF Processing Impact
Reuse one content asset for byte-identical copies.

## Metadata Impact
Edition/work fields merge only through provenance-aware decisions.

## Search Impact
Group/collapse options avoid duplicate-result explosion.

## AI/RAG Impact
One work does not dominate recommendations via duplicate assets.

## 3D Bookshelf Impact
User chooses copy/edition grouping policy.

## External Integrations
Provider IDs inform but do not override conflicts.

## Privacy Requirements
Resolution is local except approved metadata lookups.

## Security Requirements
Merge/split requires confirmation and is audited/reversible.

## Performance Requirements
Candidate blocking prevents all-to-all catalogue comparison.

## Error & Recovery Behaviour
Low confidence remains separate with a review suggestion.

## Logging/Observability
Decision reason, score components, version and actor.

## Testing
Property/unit identity cases; DB uniqueness; pipeline duplicate corpus; provider-contract mismatches; UI merge/split E2E; scale candidate benchmarks; AI diversity tests.

## Skills Engines Applied
`srs-skills` acceptance/ambiguity; `skills-web-dev` domain algorithms/database.

## Dependencies
Phases 3–8.

## Parallelisation
Exact-copy and bibliographic proposal work may proceed separately; merge UI waits for both.

## Migration Considerations
Legacy suspected duplicates are proposals only.

## Definition of Done
- [x] Four duplicate classes are executable and explained.
- [x] No low-confidence silent merge.
- [x] Merge/split is reversible.
- [x] Search/advisor grouping respects decisions.
- [x] Integrity-pipeline freeze is recorded.

## Kaizen Review
1. Complexity: confidence/reversibility. 2. Consolidate normalization. 3. Simplify duplicate consumers. 4. Remove unsafe tiers. 5. Document score calibration. 6. Pattern: reviewable match proposal. 7. Debt decreases.
