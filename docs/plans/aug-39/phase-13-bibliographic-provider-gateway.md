# Phase 13 — Bibliographic Provider Gateway

> [Roadmap index](./README.md) · [Previous](./phase-12-canonical-metadata-and-provenance.md) · [Next](./phase-14-metadata-review-and-manual-curation.md)

## Objective
Make Google Books/Open Library enrichment cached, resilient, observable and replaceable.

## Business/Product Rationale
External APIs improve messy catalogues but must not corrupt, leak or block them.

## SDLC Requirements
FR-META-002/006/008; privacy/data-flow and external resilience requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Metadata/Providers/GoogleBooksProvider.cs`, `OpenLibraryProvider.cs` and `RateLimitedHttpClientHandler.cs` exist; durable cache, quotas and contract evidence are incomplete.

## Gap Analysis
Repeated queries, malformed/conflicting results and provider outages lack a coherent policy.

## Architectural Impact
Provider gateway emits normalized proposals and typed outcomes.

## Database Work
Request cache keyed by normalized query/provider/version, TTL, ETag, negative result, quota and raw-response retention metadata.

## Backend Work
ISBN-first lookup, fallback order, timeout/backoff/jitter/circuit breaker and response validation.

## Frontend Work
Provider health, cached/stale result label and retry controls.

## PDF Processing Impact
None.

## Metadata Impact
Provider outputs remain proposals with source IDs/confidence.

## Search Impact
Accepted metadata triggers targeted reindex.

## AI/RAG Impact
No LLM fallback for deterministic bibliographic lookup by default.

## 3D Bookshelf Impact
External cover references pass to Phase 16 resolver.

## External Integrations
Google Books and Open Library first; Crossref only if SDLC-approved applicability is demonstrated.

## Privacy Requirements
Disclose query fields/providers; minimize; cache to reduce repeated disclosure.

## Security Requirements
HTTPS, response size limits, SSRF-safe fixed endpoints, no secrets in logs.

## Performance Requirements
Cache hits local and fast; provider calls never block catalogue UI.

## Error & Recovery Behaviour
Outage yields local/cached workflow and scheduled retry, not failed ingestion.

## Logging/Observability
Provider, cache hit, latency, status/quota and proposal count.

## Testing
Unit mapping; DB cache TTL; recorded/malformed/provider-failure integration; API status; UI degraded state; privacy payload capture; E2E fallback; rate/concurrency performance.

## Skills Engines Applied
`skills-web-dev` integration resilience; `digital-research-skills` provider evidence; `srs-skills` controls.

## Dependencies
Phase 12.

## Parallelisation
Provider adapters run in parallel behind gateway conformance tests.

## Migration Considerations
Legacy raw responses gain retention/version metadata or are purged by policy.

## Definition of Done
- [ ] Identical lookups use cache.
- [ ] Provider outage does not block library readiness.
- [ ] Conflicts produce reviewable proposals.
- [ ] Quota/timeout/backoff are observable.
- [ ] Privacy disclosure is accurate.

## Kaizen Review
1. Complexity: cache/resilience. 2. Share HTTP policy. 3. Simplify providers. 4. Remove ad hoc retries. 5. Document provider contracts. 6. Pattern: normalized gateway outcome. 7. Debt decreases.
