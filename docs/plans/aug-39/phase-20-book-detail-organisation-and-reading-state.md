# Phase 20 — Book Detail, Organisation and Reading State

> [Roadmap index](./README.md) · [Previous](./phase-19-production-2d-catalogue.md) · [Next](./phase-21-reader-completion-and-portability.md)

## Objective
Complete the book detail and personal curation workflows.

## Business/Product Rationale
A living library needs more than thumbnails: users must understand, organise and act on each book.

## SDLC Requirements
FR-CAT-003/004/006, relevant reading-state and organisation requirements.

## Current Repository State
`src/OgmaLibrary.App/Views/Catalogue/BookDetailView.axaml` and catalogue entities/services contain partial detail, shelves/tags and reading records; provenance/editor/actions are disconnected.

## Gap Analysis
No coherent detail composition, smart shelf evaluator or complete reading status/history actions.

## Architectural Impact
Book detail composes identity, metadata, assets, files, reading state and processing projections.

## Database Work
Validate collections/tags/favourites/rating/status/notes/history constraints and smart-shelf saved queries.

## Backend Work
Commands/queries for organisation, open/reanalyse/relink and smart shelf evaluation.

## Frontend Work
Rich detail, organisation controls, file/availability, TOC, provenance, related items and safe actions.

## PDF Processing Impact
Reprocess action targets stages/versions.

## Metadata Impact
Embeds Phase 14 editor/review.

## Search Impact
Tags/collections/status become filterable.

## AI/RAG Impact
AI-derived sections are labeled and optional.

## 3D Bookshelf Impact
Selection opens the same detail route.

## External Integrations
None.

## Privacy Requirements
Notes/history are local and excluded from AI by default.

## Security Requirements
Open/reveal actions resolve authorized file IDs.

## Performance Requirements
Lazy-load heavy TOC/provenance/history tabs.

## Error & Recovery Behaviour
Missing file still shows catalogue/curation and recovery actions.

## Logging/Observability
Action failures and latency, no note content.

## Testing
Unit commands/smart shelves; DB constraints; API detail contract; UI/accessibility/E2E curation; filesystem missing/relink; performance/N+1 checks; AI exclusion tests.

## Skills Engines Applied
`design-system-skills`, `skills-web-dev`, `srs-skills` use-case acceptance.

## Dependencies
Phases 14 and 18–19.

## Parallelisation
Organisation services and detail tabs can proceed behind one projection.

## Migration Considerations
Normalize legacy status/tag data without losing notes.

## Definition of Done
- [x] Detail exposes identity, metadata, files, status and actions.
- [x] Collections/tags/favourites/status/history persist.
- [x] Smart shelves are deterministic and editable.
- [x] Missing-file detail remains useful.
- [x] Notes stay private by default.

## Kaizen Review
1. Complexity: composed detail. 2. Reuse catalogue/detail components. 3. Simplify action routing. 4. Remove duplicate panels. 5. Document status semantics. 6. Pattern: lazy composite projection. 7. Debt decreases.
