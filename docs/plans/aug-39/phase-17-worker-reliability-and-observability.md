# Phase 17 — Worker Reliability and Observability

> [Roadmap index](./README.md) · [Previous](./phase-16-cover-thumbnail-and-spine-assets.md) · [Next](./phase-18-ogma-design-system-and-application-shell.md)

## Objective
Adopt leased stages across all heavy work and deliver privacy-safe operational visibility.

## Business/Product Rationale
Scanning, rendering, enrichment and indexing must survive crashes without duplicates or silence.

## SDLC Requirements
FR-LIB-005..007, NFR-PROD-008/009, observability and recovery requirements.

## Current Repository State
`src/OgmaLibrary.Workers/BookIngestionWorker.cs` and `EmbeddingGenerationWorker.cs` use polling, magic job types and limited retry; terminal-state save failure is swallowed in the ingestion worker.

## Gap Analysis
No unified worker host policy, poison queue, resource concurrency, event IDs or metrics.

## Architectural Impact
Worker runtime executes Phase 6 contracts; modules register handlers, not loops.

## Database Work
Indexes for leases/due work; dead-letter/diagnostic summaries and retention.

## Backend Work
Atomic claim/renew/complete, exponential backoff, cancellation, concurrency/resource groups, repair tools.

## Frontend Work
Activity centre with queues, failures, retry/cancel and redacted diagnostics export.

## PDF Processing Impact
Bounded PDF/OCR concurrency.

## Metadata Impact
Provider quotas and cache-aware retries.

## Search Impact
Targeted indexing and rebuild progress.

## AI/RAG Impact
Embedding/provider cost and failure isolation.

## 3D Bookshelf Impact
Asset generation readiness feed.

## External Integrations
Typed circuit status and retry schedules.

## Privacy Requirements
Structured event schema classifies/redacts prompts, paths, notes and text.

## Security Requirements
Administrative retries and diagnostic exports are audited; secrets excluded.

## Performance Requirements
Queue overhead bounded; no busy polling; throughput/reference-load budgets.

## Error & Recovery Behaviour
Crash, duplicate worker and DB transient scenarios recover deterministically; terminal-save failure cannot be swallowed.

## Logging/Observability
Event IDs, traces, counters, queue depth, latency, failures and cost with local bounded retention.

## Testing
Unit retry/lease; DB concurrency; stage pipeline fault injection; API/activity UI; filesystem/provider/AI failures; E2E kill/restart; load/performance; log-redaction tests.

## Skills Engines Applied
`skills-web-dev` reliability/observability; Linux operations principles; `srs-skills` recovery evidence.

## Dependencies
Phases 6–16.

## Parallelisation
Runtime, handlers and activity UI can proceed against event contracts.

## Migration Considerations
Convert generic jobs with an explicit mapping; dead-letter unknown jobs.

## Definition of Done
- [x] No duplicate execution under two workers.
- [ ] Kill/restart resumes safely.
- [x] Retry/dead-letter/cancel are visible.
- [x] Structured logs pass redaction tests.
- [ ] Queue throughput meets target.

## Kaizen Review
1. Complexity: robust runtime. 2. Remove per-worker loops. 3. Simplify handlers. 4. Delete magic strings/swallowed saves. 5. Document runbooks. 6. Pattern: leased handler. 7. Debt decreases.
