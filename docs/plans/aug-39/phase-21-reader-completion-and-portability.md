# Phase 21 — Reader Completion and Portability

> [Roadmap index](./README.md) · [Previous](./phase-20-book-detail-organisation-and-reading-state.md) · [Next](./phase-22-structured-and-fuzzy-catalogue-search.md)

## Objective
Finish the cross-platform reader, split view, annotations, citation/export and durable recovery.

## Business/Product Rationale
Opening and using owned books must be reliable even with all network/AI services disabled.

## SDLC Requirements
FR-READ-001..015 and reader performance/durability/accessibility NFRs.

## Current Repository State
`src/OgmaLibrary.App/Views/Reader/ReaderView.axaml`, `ViewModels/Reader/ReaderViewModel.cs` and `src/OgmaLibrary.Reader/` are substantial; split view is a placeholder and export/physical evidence is incomplete.

## Gap Analysis
Platform file behavior, crash durability, coordinate fidelity, split workflow and portable export are incomplete.

## Architectural Impact
Reader consumes file broker/page service and persists user state through application commands.

## Database Work
Finalize reading positions, annotation layers, citations and export version metadata.

## Backend Work
Range/page navigation, search jump, split sessions, export/import and conflict-safe persistence.

## Frontend Work
Reader controls, side panels, split view, keyboard/fullscreen/reduced motion and actionable password/error states.

## PDF Processing Impact
Render/cache through Phase 10; coordinate mappings versioned.

## Metadata Impact
Citation metadata uses canonical fields with source.

## Search Impact
Reader search and global result jump share page anchors.

## AI/RAG Impact
Reader works independently; later evidence links may open pages.

## 3D Bookshelf Impact
Open action is identical from 2D/3D.

## External Integrations
System viewer is optional explicit action, not core dependency.

## Privacy Requirements
Annotations/history local; exports user-controlled.

## Security Requirements
File IDs only, sandboxed rendering, safe exported filenames/content.

## Performance Requirements
Cached page ≤100 ms target; no UI block >100 ms; bounded cache.

## Error & Recovery Behaviour
Resume after crash; corrupt page does not end session; password retry is bounded.

## Logging/Observability
Render/cache latency and reason codes without text/annotations.

## Testing
Unit reader state/coordinates; DB crash persistence; PDF/password/malformed pipeline; API page contract; UI keyboard/AT/split E2E; filesystem move/unavailable; export round-trip; cache/performance.

## Skills Engines Applied
`design-system-skills` reader/accessibility; `skills-web-dev` caching/state/security.

## Dependencies
Phases 10–11, 18 and 20.

## Parallelisation
Split view, export and physical accessibility tracks can proceed against reader contracts.

## Migration Considerations
Version existing annotation coordinates and provide fallback conversion.

## Definition of Done
- [ ] All FR-READ requirements have end-to-end evidence.
- [x] Split view is functional, not placeholder.
- [ ] Annotation/export round-trip and crash recovery pass.
- [ ] Physical Narrator/VoiceOver journeys pass.
- [ ] Reader budgets pass on both OSes.

## Kaizen Review
1. Complexity: split/export/versioned coordinates. 2. Consolidate reader commands. 3. Simplify panels. 4. Delete placeholder tests/views. 5. Document formats. 6. Pattern: page anchor. 7. Debt decreases.
