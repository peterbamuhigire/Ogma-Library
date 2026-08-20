# Phase 4 — Identity Schema and Data Migration

> [Roadmap index](./README.md) · [Previous](./phase-03-canonical-library-identity-model.md) · [Next](./phase-05-library-roots-and-path-security.md)

## Objective
Implement and migrate the canonical identity model without losing curated data.

## Business/Product Rationale
Delaying this migration multiplies corruption and rework across every feature.

## SDLC Requirements
FR-LIB-003/004, FR-CAT-007, NFR-PROD-007/012, backup/reversibility controls.

## Current Repository State
Eleven migrations under `src/OgmaLibrary.Infrastructure/Catalogue/Migrations/` and work/edition tables in `CatalogueDbContext.cs` exist, but workflows and file-level facts do not.

## Gap Analysis
Constraints, indexes and historical mapping do not protect new invariants.

## Architectural Impact
Repositories return explicit identity projections; compatibility adapters are temporary.

## Database Work
Add roots, file occurrences, content assets, checksums/fingerprint versions, edition/work links and identity decisions; constraints and covering indexes; reversible data migration.

## Backend Work
Replace repository placeholder mapping and update-by-first-file behavior.

## Frontend Work
Migration progress, backup and failure-recovery screen.

## PDF Processing Impact
Existing hashes are marked legacy/unverified until recomputed.

## Metadata Impact
Migrate metadata/provenance to correct scope without overwriting user values.

## Search Impact
Retain old search IDs through an alias table until reindex.

## AI/RAG Impact
Mark legacy embeddings stale, do not discard until rebuild succeeds.

## 3D Bookshelf Impact
Provide ID aliases for saved shelf state.

## External Integrations
Preserve provider raw responses and IDs with source scope.

## Privacy Requirements
Migration backup stays local and is user-deletable.

## Security Requirements
Transactional migration, verified backup, canonical backup location.

## Performance Requirements
Rehearse on 2k/50k synthetic catalogues; startup migration must show progress.

## Error & Recovery Behaviour
On failure restore pre-migration database and leave source PDFs untouched.

## Logging/Observability
Counts, orphan/conflict reports, checksums and duration; no sensitive text.

## Testing
Unit mappings; DB up/down/invariant tests; integration legacy fixtures; pipeline reindex flags; filesystem aliases; API DTO compatibility; E2E failed migration; performance migration benchmarks.

## Skills Engines Applied
`skills-web-dev` database evolution; `srs-skills` traceability; Windows/macOS filesystem guidance for backups.

## Dependencies
Phase 3.

## Parallelisation
Migration code and read-model adapter can proceed; no identity-dependent feature merges.

## Migration Considerations
Mandatory preflight backup, dry-run report, restartable batches and rollback rehearsal.

## Definition of Done
- [ ] Legacy fixtures migrate with zero curated-field loss.
- [ ] Constraints reject invalid identity combinations.
- [ ] Up/down or forward-recovery path is proven.
- [ ] Alias/reindex plan works.
- [ ] Domain model freeze is recorded.

## Kaizen Review
1. Complexity: migration/aliases. 2. Remove obsolete columns after validation. 3. Simplify repositories. 4. Delete fake mappings. 5. Document recovery. 6. Pattern: dry-run migration. 7. Debt decreases.
