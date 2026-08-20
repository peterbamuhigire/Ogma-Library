# Phase 39 — Cross-Platform Release Acceptance and Handover

> [Roadmap index](./README.md) · [Previous](./phase-38-performance-reliability-packaging-and-beta.md) · Next: release

## Objective
Prove the complete product on physical Windows and macOS, close traceability and release a supported build.

## Business/Product Rationale
Ogma is complete only when users can safely install, ingest, curate, search, read, receive grounded advice and use the shelf on both supported platforms.

## SDLC Requirements
All 101 FRs, 29 NFRs, 32 controls, deployment/operations/test strategy and acceptance criteria.

## Current Repository State
Phase 38 must produce artifacts from `.github/workflows/` and release configuration; the historical `docs/references/Ogma-Library_TestCompletionReport_v2.1_2026-08-13.docx` alone is insufficient.

## Gap Analysis
Final independent evidence, owner acceptance, support readiness, store/direct channel confirmation and no-go closure remain.

## Architectural Impact
No new architecture; only approved release-blocking fixes. Post-freeze changes restart affected gates.

## Database Work
Final migration/backup/export/reimport/restore evidence and schema compatibility record.

## Backend Work
Fix only release defects; finalize diagnostics, support and operational runbooks.

## Frontend Work
Complete visual/accessibility/localisation polish and signed-build acceptance.

## PDF Processing Impact
Representative/hostile corpus acceptance in installed builds.

## Metadata Impact
Messy-library and user-correction/writeback acceptance.

## Search Impact
Structured/fuzzy/FTS/semantic relevance and scale acceptance.

## AI/RAG Impact
Provider-off/local/cloud, privacy, grounding and evaluation acceptance.

## 3D Bookshelf Impact
Physical WebView/GPU/accessibility/fallback acceptance.

## External Integrations
Live metadata/AI/update/channel smoke tests with controlled credentials and rollback.

## Privacy Requirements
Published privacy/data-flow text matches captured behavior; erasure/retention proven.

## Security Requirements
No unresolved P0/P1; signed/notarized artifacts and independent security sign-off.

## Performance Requirements
Final named-hardware results attached; regressions block release.

## Error & Recovery Behaviour
Run external-drive, provider, parser, network, crash, update and restore scenarios end to end.

## Logging/Observability
Support bundle, SLO dashboard/readout, alert/runbook ownership and retention verified.

## Testing
Complete regression across unit, DB, pipeline, API, filesystem, AI evaluation, Windows/macOS E2E, accessibility, security, performance, packaging, update, rollback and customer acceptance.

## Skills Engines Applied
All selected engines: `srs-skills` traceability/acceptance, `skills-web-dev` engineering gates, `design-system-skills` visual/a11y, platform engines, and evidence discipline.

## Dependencies
Phase 38 and all preceding acceptance dependencies.

## Parallelisation
Independent Windows/macOS, security, accessibility and product acceptance teams may execute in parallel; release decision waits for all.

## Migration Considerations
Only tested release migrations; immutable backups; documented rollback/support window.

## Definition of Done
- [ ] Every major requirement has acceptance evidence or approved explicit deferral; no silent gaps.
- [ ] All applicable final quality-gate questions answer yes.
- [ ] Physical Windows and macOS installed-build journeys pass.
- [ ] No unresolved P0/P1; owner accepts residual risks.
- [ ] Signed artifacts are promoted without rebuild; support/operations/handover are complete.

## Kaizen Review
1. Complexity: release coordination only. 2. Consolidate evidence/runbooks. 3. Simplify handover and support paths. 4. Archive obsolete plans/scaffolds. 5. Document final architecture/operations/user guidance. 6. Pattern: immutable evidence-backed promotion. 7. Technical debt reaches approved release threshold.
