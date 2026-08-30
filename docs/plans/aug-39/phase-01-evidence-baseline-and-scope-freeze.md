# Phase 1 — Evidence Baseline and Scope Freeze

> [Roadmap index](./README.md) · Previous: none · [Next](./phase-02-composition-configuration-and-startup.md)

## Objective
Ratify the v2.1 requirement baseline, current commit evidence and desktop-only scope.

## Business/Product Rationale
Conflicting phase counts, internal v2.0 labels and historical implementation claims would otherwise reproduce false completion.

## SDLC Requirements
All SRS/PRD/HLD requirements; audit governance; owner correction: exactly 39 Windows/macOS C# application phases, no mobile.

## Current Repository State
`docs/references/` is rich but internally inconsistent; `.github/workflows/ci.yml` has an informational hybrid gate; `CLAUDE.md` is stale.

## Gap Analysis
No approved conflict log or current evidence manifest links v2.1 to `5514276...`.

## Architectural Impact
Creates authoritative decision, requirement and evidence registers; no runtime change.

## Database Work
None; record current schema/migration hashes.

## Backend Work
Define executable gate commands and ownership.

## Frontend Work
None.

## PDF Processing Impact
Record current adapter/fixture versions.

## Metadata Impact
Freeze vocabulary pending Phase 12.

## Search Impact
Record current index/version assumptions.

## AI/RAG Impact
Mark advisor/answer claims non-release until Phases 27–30.

## 3D Bookshelf Impact
Mark 3D scaffold until Phases 31–33.

## External Integrations
Inventory provider endpoints, terms evidence owners and credentials.

## Privacy Requirements
Approve data-classification and evidence redaction rules.

## Security Requirements
No secret or private document content in evidence artifacts.

## Performance Requirements
Name reference Windows/macOS hardware and data sets.

## Error & Recovery Behaviour
An unavailable gate is `NOT ASSESSED`, never silently passed.

## Logging/Observability
Define evidence IDs, timestamps, commit and environment metadata.

## Testing
Run unit, integration, pipeline, API, filesystem, AI-evaluation scaffold, E2E inventory and performance baseline commands; classify non-executed gates.

## Skills Engines Applied
`srs-skills` for baseline/traceability; `digital-research-skills` for evidence; `skills-web-dev` for executable gates.

## Dependencies
None.

## Parallelisation
Document-control cleanup and environment inventory may run concurrently.

## Migration Considerations
Archive superseded status claims without deleting history.

## Definition of Done
- [ ] Owner approves scope/conflict log.
- [ ] All 101 FRs, 29 NFRs and 32 controls have accountable IDs.
- [ ] Evidence manifest identifies commit/environment and exclusions.
- [ ] Stale contributor guidance is corrected or clearly archived.
- [ ] Freeze decision is recorded.

## Kaizen Review
1. Complexity: evidence governance. 2. Duplication removed: competing status tables. 3. Simplify: one requirement index. 4. Delete: none before archive. 5. Document: conflict policy. 6. Pattern: evidence manifest. 7. Debt decreases.
