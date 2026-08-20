# Phase 3 — Canonical Library Identity Model

> [Roadmap index](./README.md) · [Previous](./phase-02-composition-configuration-and-startup.md) · [Next](./phase-04-identity-schema-and-data-migration.md)

## Objective
Define immutable root, file occurrence, content asset, edition and work identities.

## Business/Product Rationale
Ogma cannot preserve a curated library while equating one path/PDF with one book.

## SDLC Requirements
FR-LIB-003/004, FR-CAT-007, metadata/duplicate requirements, ADR data model.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Catalogue/Entities/BookRow.cs` owns hashes; `BookFileRow.cs` lacks content facts; `Catalogue/Repositories/BookRepository.cs` injects path-derived placeholder hashes.

## Gap Analysis
Exact copies, duplicate editions, different editions and similar titles cannot be represented safely.

## Architectural Impact
Publish domain aggregates/invariants and identity decision records before schema changes.

## Database Work
Design keys, uniqueness, nullable transitions and migration mapping for Phase 4.

## Backend Work
Replace ambiguous `Book` semantics in contracts; require explicit unknown states.

## Frontend Work
Define user language for copy, edition, work, possible match and unavailable file.

## PDF Processing Impact
Fingerprint outputs belong to file/content assets.

## Metadata Impact
ISBN/external IDs attach to editions; titles/subjects may attach to work or edition explicitly.

## Search Impact
Results declare grouping level and availability.

## AI/RAG Impact
Recommendations select editions/assets but can diversify by work.

## 3D Bookshelf Impact
Shelf item IDs reference catalogue presentation IDs, not paths.

## External Integrations
Provider IDs need source+type+edition/work scope.

## Privacy Requirements
Paths remain infrastructure data, not bibliographic metadata.

## Security Requirements
No user-supplied path or external ID becomes an authority boundary.

## Performance Requirements
Lookup invariants must be indexable without loading full aggregates.

## Error & Recovery Behaviour
Ambiguity creates review candidates; it never silently merges.

## Logging/Observability
Record identity decision tier, inputs, version and confidence without full paths in general logs.

## Testing
Domain/property tests for exact copy, edition, work, similarity and ambiguity; schema contract tests; no UI/API implementation yet; filesystem scenario specifications; AI/3D ID-contract tests.

## Skills Engines Applied
`srs-skills` domain/state modeling; `skills-web-dev` DDD/database boundaries.

## Dependencies
Phases 1–2.

## Parallelisation
Domain modeling and migration rehearsal fixtures may proceed together; scanner changes wait.

## Migration Considerations
Every existing `BookRow` maps to provisional edition/work plus one or more file occurrences with explicit low-confidence provenance.

## Definition of Done
- [ ] Invariants and terminology approved.
- [ ] No fake hash is permitted.
- [ ] Exact file, duplicate edition and same-work/different-edition are distinct.
- [ ] Unknown/ambiguous identity is representable.
- [ ] Domain-model freeze candidate approved.

## Kaizen Review
1. Complexity: necessary identity layers. 2. Remove path/book conflation. 3. Simplify downstream IDs. 4. Delete placeholder hash. 5. Document invariants. 6. Pattern: explicit decision record. 7. Debt decreases sharply.
