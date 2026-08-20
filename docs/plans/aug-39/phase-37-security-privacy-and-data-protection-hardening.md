# Phase 37 — Security, Privacy and Data Protection Hardening

> [Roadmap index](./README.md) · [Previous](./phase-36-school-administration-and-managed-ai.md) · [Next](./phase-38-performance-reliability-packaging-and-beta.md)

## Objective
Perform cross-cutting hostile review, close controls and prove erasure/backup/least privilege.

## Business/Product Rationale
Powerful local file/network/AI access creates a high trust burden before beta.

## SDLC Requirements
CTRL-001..032, DPIA, security/privacy NFRs and threat models.

## Current Repository State
Controls across `src/OgmaLibrary.Infrastructure/Pdf/`, `Security/`, `AI/` and `LanHost/` are largely conceptual or unit-tested; sandbox, path/writeback, provider, classroom and log risks remain.

## Gap Analysis
No complete penetration/adversarial assessment, data inventory verification, at-rest/backup decision, retention or control evidence pack.

## Architectural Impact
Security boundaries are tested as architecture; remaining exceptions require time-bounded risk acceptance.

## Database Work
Retention/erasure jobs, backup manifests, optional encryption decision/migration and tamper-evident audit controls.

## Backend Work
Threat-model fixes, path/API/AI/LAN hardening, secret lifecycle, redaction, export/delete and backup/restore.

## Frontend Work
Security/privacy status, data inventory, retention/export/delete, backup/restore and actionable warnings.

## PDF Processing Impact
Independent containment review and hostile corpus.

## Metadata Impact
Provider disclosure/retention verified.

## Search Impact
Private queries/text excluded from logs/export by default.

## AI/RAG Impact
Gateway bypass, injection, retention, erasure and provider claims tested.

## 3D Bookshelf Impact
WebView CSP/navigation/message/file boundary reviewed.

## External Integrations
Provider/subprocessor evidence and data-processing terms recorded.

## Privacy Requirements
Exact local/outbound data map, minimisation, consent, retention, export and verifiable deletion.

## Security Requirements
Hostile PDFs, traversal, SQL/XSS/markup, secret extraction, LAN authz, update design and dependency/supply-chain checks.

## Performance Requirements
Controls do not violate core budgets; security limits are benchmarked.

## Error & Recovery Behaviour
Backups restore; erasure is restartable; failed security controls fail closed without destroying data.

## Logging/Observability
Central redaction/classification, security events, retention and audit-integrity checks.

## Testing
Unit controls; DB erasure/backup/encryption; hostile PDF/filesystem/API/WebView/LAN/AI tests; secret-store physical tests; E2E privacy journeys; dependency/SAST/secret scans; performance under controls; independent penetration review.

## Skills Engines Applied
`skills-web-dev` security; Windows admin hardening; `srs-skills` control traceability; digital research evidence discipline.

## Dependencies
Phases 10, 15, 27 and 34–36.

## Parallelisation
Independent reviews may run by boundary; closure/acceptance is centralized.

## Migration Considerations
Encryption/retention changes require backup, dry run and rollback; do not encrypt without recovery proof.

## Definition of Done
- [ ] Every CTRL has executable evidence or approved risk acceptance.
- [ ] PDF/WebView/filesystem/LAN/AI boundaries pass hostile testing.
- [ ] Secret/backup/export/erasure lifecycle passes on both OSes.
- [ ] DPIA/data-flow is accurate.
- [ ] No unresolved P0/P1 security/privacy issue.

## Kaizen Review
1. Complexity: cross-boundary controls. 2. Centralize redaction/policies. 3. Simplify security adapters. 4. Remove obsolete exceptions. 5. Document evidence/runbooks. 6. Pattern: control-to-test mapping. 7. Debt decreases.
