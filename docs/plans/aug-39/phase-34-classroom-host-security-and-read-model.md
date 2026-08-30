# Phase 34 — Classroom Host Security and Read Model

> [Roadmap index](./README.md) · [Previous](./phase-33-3d-scale-accessibility-and-performance.md) · [Next](./phase-35-classroom-client-offline-and-sync.md)

## Objective
Complete the opt-in desktop host with TLS, published-scope isolation, roles and safe streaming/search.

## Business/Product Rationale
Classroom sharing is valuable only if it never exposes the private standalone library.

## SDLC Requirements
FR-LAN-001..010, CTRL-024..029, NFR-LAN-001..003.

## Current Repository State
`src/OgmaLibrary.Infrastructure/LanHost/` contains certificate, bind, host, range and read-model code/tests; no physical multi-machine/hostile proof exists.

## Gap Analysis
Operational TLS/mDNS/TOFU, firewall, session, publication revocation, attack and load acceptance incomplete.

## Architectural Impact
Host is an explicitly enabled module using published projections only; no direct catalogue/file repository exposure.

## Database Work
Finalize host identity, publication, roles, sessions, audit, revocation and retention indexes.

## Backend Work
Bind policy, TLS lifecycle, discovery/manual fallback, auth, authorization, rate/range limits and immutable published read model.

## Frontend Work
Host setup, certificate/fingerprint, published scope, users/sessions, health and stop/revoke.

## PDF Processing Impact
No parsing on request path; serve validated published assets only.

## Metadata Impact
Published field whitelist.

## Search Impact
Published structured/FTS endpoints only.

## AI/RAG Impact
No AI in host until Phase 36.

## 3D Bookshelf Impact
No browser/web 3D client; classroom remains C# desktop client.

## External Integrations
LAN mDNS/TLS only; standalone opens no listener.

## Privacy Requirements
Explicit publication, private-state separation, redacted audit and clear network disclosure.

## Security Requirements
TOFU/fingerprint, RBAC, traversal prevention, session expiry/revocation, rate limits and hostile tests.

## Performance Requirements
Meet accepted concurrent catalogue/search/range-load budgets.

## Error & Recovery Behaviour
Network/certificate/discovery failure does not affect standalone; revocation is prompt and observable.

## Logging/Observability
Connections/authz/range/rate/health events without content or secrets.

## Testing
Unit authz; DB isolation; API hostile/range/TLS; multi-machine Windows/macOS E2E; firewall/mDNS/manual fallback; publication revocation; load/soak; privacy capture.

## Skills Engines Applied
`skills-web-dev` API/security; Windows networking/admin guidance; `srs-skills` LAN controls.

## Dependencies
Phases 5, 17, 22–23; enablement waits for Phase 37.

## Parallelisation
TLS/discovery, published read model and admin setup can proceed against security contract.

## Migration Considerations
Existing host records are disabled until revalidated; rotate incompatible certificates/sessions.

## Definition of Done
- [ ] Standalone opens no listener.
- [ ] Only published IDs/fields/files are reachable.
- [ ] TLS/TOFU/authz/revocation pass hostile tests.
- [ ] Physical two-machine both-OS matrix passes.
- [ ] Load and privacy gates pass.

## Kaizen Review
1. Complexity: network/security lifecycle. 2. One published projection. 3. Simplify endpoints. 4. Remove direct repository paths. 5. Document threat/runbooks. 6. Pattern: explicit exposure boundary. 7. Debt decreases.
