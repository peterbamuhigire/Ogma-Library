# Phase 8 — Filesystem Reconciliation and Recovery

> [Roadmap index](./README.md) · [Previous](./phase-07-discovery-and-incremental-scanning.md) · [Next](./phase-09-duplicate-and-bibliographic-resolution.md)

## Objective
Safely resolve rename, move, replacement, disappearance and root outage.

## Business/Product Rationale
Curated state must survive ordinary changes outside Ogma.

## SDLC Requirements
FR-LIB-003/004/006/007, library-integrity quality gates.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Ingestion/UnavailableFileFlagService.cs` marks every absent present-row after a scan, regardless of root health.

## Gap Analysis
No observation completeness, grace policy, root scoping or specific file update.

## Architectural Impact
Reconciliation consumes scan observations and identity decisions transactionally.

## Database Work
Presence observations, missing-since, root-session evidence, relocation candidates and reconciliation audit.

## Backend Work
Change/move/replace algorithms, grace windows, relink and user confirmation for ambiguity.

## Frontend Work
Missing/temporarily unavailable/moved/changed states and recovery actions.

## PDF Processing Impact
Replacement invalidates derived stages by asset hash.

## Metadata Impact
Curated metadata remains attached to edition/work.

## Search Impact
Unavailable assets are retained but filtered/action-labeled; changed assets reindex.

## AI/RAG Impact
Stale content is excluded until reprocessed.

## 3D Bookshelf Impact
No duplicate visual item for a confirmed move.

## External Integrations
None.

## Privacy Requirements
Relocation audit redacts paths in exported diagnostics.

## Security Requirements
Only observed in-root targets can be relinked automatically.

## Performance Requirements
Set-based reconciliation; no per-file N+1 queries.

## Error & Recovery Behaviour
Incomplete/failed root scan performs no absence transition.

## Logging/Observability
Counts and reason codes for unchanged/new/moved/changed/unavailable/ambiguous.

## Testing
Unit decision table; DB transaction tests; filesystem rename/move/delete/replace/disconnect/permission scenarios on both OSes; pipeline invalidation; UI recovery E2E; scale performance.

## Skills Engines Applied
`srs-skills` transitions; `skills-web-dev` transactional reconciliation; platform filesystem engines.

## Dependencies
Phase 7.

## Parallelisation
Algorithm and recovery UI can proceed from a frozen decision table.

## Migration Considerations
Existing unavailable flags require non-destructive revalidation.

## Definition of Done
- [x] Root outage never marks individual deletion.
- [x] Moves preserve curation.
- [x] Replacements trigger exact invalidation.
- [x] Ambiguity is reviewed, not guessed.
- [x] Audit trail explains every transition.

## Kaizen Review
1. Complexity: temporal presence. 2. Remove global missing sweep. 3. Simplify availability consumers. 4. Delete first-file update. 5. Document decision table. 6. Pattern: evidence-gated reconciliation. 7. Debt decreases sharply.
