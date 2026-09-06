# Phase 12 — Canonical Metadata and Provenance

> [Roadmap index](./README.md) · [Previous](./phase-11-pdf-extraction-and-isbn-primitives.md) · [Next](./phase-13-bibliographic-provider-gateway.md)

## Objective
Define canonical fields, scopes, sources, confidence and user-override precedence.

## Business/Product Rationale
Ogma must explain where every important value came from and never erase curation.

## SDLC Requirements
FR-META-001/003/004/007/008, FR-CAT-007, provenance/confidence requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Metadata/ConfidenceMergeService.cs` and metadata provenance entities exist; canonical scope/normalization and auto-apply rules are unsafe.

## Gap Analysis
Work vs edition fields, contributors, external IDs, user locks, null/unknown and conflict rules are incomplete.

## Architectural Impact
Canonical metadata aggregate plus proposal/reconciliation policy.

## Database Work
Normalized contributors/roles, identifiers, field values/provenance, override locks, confidence model version and proposals.

## Backend Work
Normalization, precedence, calibrated rules and review thresholds.

## Frontend Work
Reusable provenance/confidence presentation contract.

## PDF Processing Impact
Extraction writes proposals only.

## Metadata Impact
Primary deliverable; user override > confirmed match > high-confidence provider > extraction > filename.

## Search Impact
Canonical normalized fields feed indexes.

## AI/RAG Impact
Evidence includes source and certainty.

## 3D Bookshelf Impact
Stable display title/author/spine strings.

## External Integrations
Providers map through adapters, never directly to canonical rows.

## Privacy Requirements
Provenance records data source/purpose without storing excess raw payload forever.

## Security Requirements
Validate lengths/markup/identifiers; user locks require explicit actor.

## Performance Requirements
Batch proposal/merge queries avoid N+1.

## Error & Recovery Behaviour
Conflicts remain proposals; existing canonical values stay visible.

## Logging/Observability
Field decisions and model version without sensitive descriptions in logs.

## Testing
Unit/property precedence; DB constraints; extraction/provider pipeline proposals; API DTO validation; UI provenance snapshots; E2E user override; scale merge performance.

## Skills Engines Applied
`srs-skills` requirement/state/traceability; `skills-web-dev` domain/database design.

## Dependencies
Phases 3–4 and 11.

## Parallelisation
Schema and normalization can proceed; UI consumes frozen DTOs.

## Migration Considerations
Legacy canonical values become preserved provenance, not re-inferred.

## Definition of Done
- [x] Field scope and precedence are documented/executable.
- [x] User overrides cannot be automated away.
- [x] Contributors/identifiers are normalized appropriately.
- [x] Confidence is versioned/calibrated.
- [x] Proposal-only ingestion is enforced.

## Kaizen Review
1. Complexity: field histories. 2. Centralize precedence. 3. Simplify provider/extractor writes. 4. Delete direct canonical mutation. 5. Document field dictionary. 6. Pattern: sourced field proposal. 7. Debt decreases.
