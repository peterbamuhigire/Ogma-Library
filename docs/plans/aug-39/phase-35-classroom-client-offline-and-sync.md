# Phase 35 — Classroom Client, Offline and Sync

> [Roadmap index](./README.md) · [Previous](./phase-34-classroom-host-security-and-read-model.md) · [Next](./phase-36-school-administration-and-managed-ai.md)

## Objective
Complete the paired C# desktop client mode with secure credentials, streaming, private state and offline recovery.

## Business/Product Rationale
Students need reliable access without sacrificing privacy or normal desktop reader quality.

## SDLC Requirements
FR-CLIENT-001..013, NFR-CLIENT-001..003.

## Current Repository State
`src/OgmaLibrary.Infrastructure/ClassroomClient/` contains credential/cache/sync services and client views partially; no physical host/client acceptance exists.

## Gap Analysis
Pairing, reconnect, conflict, eviction, erasure, diagnostics and accessibility are not proven end to end.

## Architectural Impact
Client remote library implements a separate repository adapter feeding shared catalogue/reader UI; it never impersonates local files.

## Database Work
Host-scoped cache, session, private reading/annotation state, sync cursor/conflicts and erasure records.

## Backend Work
Pairing/TOFU, credential store, resilient streaming/ranges, cache quota/eviction, offline state and idempotent sync.

## Frontend Work
Host switch/pairing, published catalogue, download/offline indicators, conflicts, unavailable/reconnect and clear-data controls.

## PDF Processing Impact
No processing of unapproved remote bytes outside reader/sandbox policy.

## Metadata Impact
Remote data is host-scoped/read-only.

## Search Impact
Remote/offline search clearly labeled.

## AI/RAG Impact
Student advisor waits for Phase 36.

## 3D Bookshelf Impact
No separate 3D remote catalogue requirement unless shared desktop contract is explicitly enabled later.

## External Integrations
Classroom host only.

## Privacy Requirements
Per-user state isolation, local clear/export, minimum diagnostics and minors-aware defaults.

## Security Requirements
DPAPI/Keychain, certificate pinning, host scope, replay/conflict protections and cache permissions.

## Performance Requirements
Accepted browse/search/page latency under LAN load and bounded cache.

## Error & Recovery Behaviour
Offline/reconnect/resume and conflict resolution preserve annotations/position.

## Logging/Observability
Host-scoped connection/sync/cache events with identity/content redaction.

## Testing
Unit cache/sync/conflict; DB host/user isolation; API/TLS/range; physical host-client E2E; network drop/reconnect/offline/erasure; AT/localisation; load/cache performance; hostile cross-user tests.

## Skills Engines Applied
`skills-web-dev` sync/security; design-system offline/client UX; platform secret-store guidance.

## Dependencies
Phases 21 and 34.

## Parallelisation
Cache/offline, sync and client UI tracks proceed from host API contract.

## Migration Considerations
Existing cached credentials/state are invalidated or securely migrated by host identity.

## Definition of Done
- [ ] Pairing and OS credential storage pass physically.
- [ ] Browse/stream/offline/reconnect work on both OSes.
- [x] Private state cannot cross users/hosts.
- [x] Cache quota/clear/export is complete.
- [ ] Accessibility/load gates pass.

## Kaizen Review
1. Complexity: offline/sync/security. 2. Reuse catalogue/reader UI. 3. Simplify remote repository. 4. Remove duplicate client views. 5. Document conflict/cache policy. 6. Pattern: host-scoped local projection. 7. Debt decreases.
