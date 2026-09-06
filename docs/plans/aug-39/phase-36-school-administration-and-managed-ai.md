# Phase 36 — School Administration and Managed AI

> [Roadmap index](./README.md) · [Previous](./phase-35-classroom-client-offline-and-sync.md) · [Next](./phase-37-security-privacy-and-data-protection-hardening.md)

## Objective
Complete desktop administration, roles, quotas, audit, minors controls and optional host-managed AI.

## Business/Product Rationale
Schools require governance, not merely a shared folder and provider key.

## SDLC Requirements
FR-ADMIN-001..013, FR-CLIENT-008, CTRL-030..032 and DPIA.

## Current Repository State
`src/OgmaLibrary.Infrastructure/SchoolAdmin/` and `src/OgmaLibrary.App/Views/Classroom/` contain admin/policy/quota/audit pieces; core AI composition and governance remain incomplete.

## Gap Analysis
No accepted admin journey, school-key custody, purpose/consent, quota enforcement, minors policy or backup/restore proof.

## Architectural Impact
Admin commands are role-authorized; managed AI still passes the same gateway and published evidence boundary.

## Database Work
Finalize classes/users/roles/policies/quotas/consent/audit/retention/backup metadata.

## Backend Work
Provision/revoke, policy enforcement, quota ledger, managed AI proxy, per-user isolation, export/restore and health.

## Frontend Work
Admin dashboard, users/classes/publication/policies/quotas/AI/audit/health/backup and student smart search.

## PDF Processing Impact
None beyond published validated assets.

## Metadata Impact
Published-field policy.

## Search Impact
Role/published filters enforced server-side.

## AI/RAG Impact
Managed provider uses Phase 27–30 gateway/evidence; school key never reaches client.

## 3D Bookshelf Impact
None.

## External Integrations
Approved AI providers only; no school identity provider unless separately approved by SDLC change.

## Privacy Requirements
Minors DPIA, purpose limitation, consent/legal basis, retention/erasure and auditable admin access.

## Security Requirements
Least-privilege roles, key custody/rotation, quota/rate controls and tamper-evident audit.

## Performance Requirements
Concurrent quota enforcement and advisor latency/load budgets.

## Error & Recovery Behaviour
AI/provider failure leaves classroom catalogue/reader operational; backup restore is rehearsed.

## Logging/Observability
Admin/security/quota/provider events with student data minimisation.

## Testing
Unit RBAC/quota/policy; DB tenant/isolation/backup restore; API adversarial authorization; physical admin/student E2E; managed-AI privacy/eval; key rotation/revocation; load/soak; accessibility/localisation.

## Skills Engines Applied
`skills-web-dev` security/AI; `srs-skills` governance; digital research evidence; design-system admin UX.

## Dependencies
Phases 27 and 34–35.

## Parallelisation
RBAC/quota, admin UI and managed-AI conformance can proceed; final integration waits on all.

## Migration Considerations
Existing roles/policies default deny until reviewed; rotate any legacy keys.

## Definition of Done
- [x] All admin actions enforce roles server-side.
- [x] School keys are host-only and rotatable.
- [ ] Quotas/audit/retention/erasure pass.
- [x] Managed AI remains grounded/published-scope only.
- [ ] DPIA/minors and backup/restore evidence approved.

## Kaizen Review
1. Complexity: governance/managed AI. 2. Reuse gateway/authz policies. 3. Simplify admin commands. 4. Remove client-key paths. 5. Document school operations. 6. Pattern: policy-enforced capability. 7. Debt decreases.
