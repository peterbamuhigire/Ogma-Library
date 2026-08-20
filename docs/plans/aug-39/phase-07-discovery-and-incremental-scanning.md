# Phase 7 — Discovery and Incremental Scanning

> [Roadmap index](./README.md) · [Previous](./phase-06-processing-state-machine-and-scan-sessions.md) · [Next](./phase-08-filesystem-reconciliation-and-recovery.md)

## Objective
Build an idempotent, recursive, resumable scanner over healthy roots.

## Business/Product Rationale
Untidy folders must become a library without blocking browsing or repeating expensive work.

## SDLC Requirements
FR-LIB-001/002/005/006 and performance/recovery NFRs.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Ingestion/PdfDiscoveryService.cs` recursively enumerates but suppresses some errors and lacks sessions/checkpoints.

## Gap Analysis
No durable cursor, watch/debounce policy, per-directory diagnostics or one-pass fingerprint reuse.

## Architectural Impact
Discovery produces immutable observations consumed by identity, not direct book mutation.

## Database Work
Directory checkpoints, observation batches and scan statistics.

## Backend Work
Bounded enumeration, exclusions, change hints, cancellation, optional watcher plus scheduled reconciliation.

## Frontend Work
Start/schedule/rescan controls and live but throttled progress.

## PDF Processing Impact
Only changed/unknown assets enqueue validation.

## Metadata Impact
Unchanged files never trigger enrichment.

## Search Impact
Unchanged files never reindex.

## AI/RAG Impact
Unchanged content never re-embeds.

## 3D Bookshelf Impact
Catalogue updates stream incrementally through one read model.

## External Integrations
None.

## Privacy Requirements
Discovery stays local.

## Security Requirements
Use root adapter; treat names and filesystem exceptions as untrusted.

## Performance Requirements
Bounded memory, no UI blocking, measured at 50k files.

## Error & Recovery Behaviour
One inaccessible directory is recorded and skipped; session completeness reflects it.

## Logging/Observability
Files/directories seen, skipped, changed, duration and error categories.

## Testing
Unit filters; DB checkpoint; filesystem large-tree/permission/cancel/watch tests; pipeline idempotency; API/UI progress; E2E restart; 50k performance.

## Skills Engines Applied
`skills-web-dev`, Windows filesystem guidance and Linux watcher/reliability principles.

## Dependencies
Phases 5–6.

## Parallelisation
Watcher is optional and may follow scanner core within the phase.

## Migration Considerations
First new scan is a non-destructive reconciliation, not a fresh import.

## Definition of Done
- [ ] Recursive scans are restartable and idempotent.
- [ ] Exclusions and errors are visible.
- [ ] One content observation feeds downstream stages once.
- [ ] Catalogue remains usable during scan.
- [ ] 50k benchmark meets budget.

## Kaizen Review
1. Complexity: checkpoints/watch events. 2. Share observation pipeline. 3. Simplify orchestration. 4. Remove duplicate hashing. 5. Document scan policy. 6. Pattern: immutable observation. 7. Debt decreases.
