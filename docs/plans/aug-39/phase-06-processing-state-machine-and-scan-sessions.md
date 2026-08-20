# Phase 6 — Processing State Machine and Scan Sessions

> [Roadmap index](./README.md) · [Previous](./phase-05-library-roots-and-path-security.md) · [Next](./phase-07-discovery-and-incremental-scanning.md)

## Objective
Create durable scan sessions and explicit per-stage processing states.

## Business/Product Rationale
Users need trustworthy progress, failure, retry and recovery rather than a vague processed flag.

## SDLC Requirements
FR-LIB-005..007, NFR-PROD-008/009, processing-state requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Catalogue/CatalogueDbContext.cs` persists generic Jobs status 0–4, while `src/OgmaLibrary.Workers/BookIngestionWorker.cs` polls them; discovery is not durably staged.

## Gap Analysis
No stage graph, lease, idempotency key, retry time or failure taxonomy.

## Architectural Impact
Define orchestration state separate from domain identity; stages emit versioned commands/events.

## Database Work
ScanSession, StageExecution, lease owner/expiry, attempt, next-attempt, error code, timestamps and idempotency keys.

## Backend Work
Stage transition service, atomic claims, cancellation and dependency graph.

## Frontend Work
Per-root/session/book progress, actionable failure and retry/cancel controls.

## PDF Processing Impact
Validation/extraction are explicit isolated stages.

## Metadata Impact
Identification/enrichment/review have separate outcomes.

## Search Impact
Index readiness derives from stages and versions.

## AI/RAG Impact
Embedding failure never blocks catalogue-ready state.

## 3D Bookshelf Impact
Asset readiness is explicit.

## External Integrations
Provider failures use typed retry policies.

## Privacy Requirements
Errors do not persist PDF text/provider secrets.

## Security Requirements
Only valid transitions; administrative retries are audited.

## Performance Requirements
Batch state writes; avoid per-item UI churn.

## Error & Recovery Behaviour
Crash leases expire; poison work is isolated; retryable and terminal failures differ.

## Logging/Observability
Stage latency, attempt, queue depth and terminal codes.

## Testing
State-machine unit/property tests; DB atomic-claim tests; crash/restart pipeline tests; API/UI progress; filesystem cancellation; AI/provider retry; E2E resume; load/concurrency tests.

## Skills Engines Applied
`srs-skills` state modeling; `skills-web-dev` queues/reliability; Linux operations principles for leases/logs.

## Dependencies
Phases 4–5.

## Parallelisation
Schema/state library and UI projections can proceed; workers adopt it in Phase 17.

## Migration Considerations
Map pending generic jobs; archive unknown job types for manual retry.

## Definition of Done
- [ ] State graph and failure taxonomy are executable.
- [ ] Atomic leases prevent double processing.
- [ ] Crash/cancel/retry behavior is deterministic.
- [ ] Optional stages do not block core readiness.
- [ ] Processing-pipeline freeze candidate recorded.

## Kaizen Review
1. Complexity: visible workflow. 2. Remove magic statuses. 3. Simplify workers. 4. Delete ambiguous processed flags. 5. Document transitions. 6. Pattern: leased stage. 7. Debt decreases.
