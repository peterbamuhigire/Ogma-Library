# Phase 19 — Production 2D Catalogue

> [Roadmap index](./README.md) · [Previous](./phase-18-ogma-design-system-and-application-shell.md) · [Next](./phase-20-book-detail-organisation-and-reading-state.md)

## Objective
Deliver excellent grid, list and directory views with correct covers, filters, sorting and scale.

## Business/Product Rationale
The ordinary catalogue is the core product and the accessibility fallback for 3D.

## SDLC Requirements
FR-CAT-001/002, FR-UX-001/002/005, catalogue performance NFRs.

## Current Repository State
`src/OgmaLibrary.App/Views/Catalogue/CatalogueShellView.axaml` has grid/list, directory and 3D placeholders; `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueReadModel.cs` returns null covers.

## Gap Analysis
No parity contract, saved views, virtualised large-library flow or comprehensive states.

## Architectural Impact
One paged catalogue query contract serves all 2D and later 3D views.

## Database Work
Covering indexes for filters/sorts and optional saved view settings.

## Backend Work
Validated pagination, facets, stable sorting, availability and processing projections.

## Frontend Work
Virtualised responsive grid/list/directory, cover component, filters, sort, selection and saved view.

## PDF Processing Impact
Show stage status without blocking browsing.

## Metadata Impact
Quality/review badges.

## Search Impact
Catalogue query and filters become reusable search inputs.

## AI/RAG Impact
None.

## 3D Bookshelf Impact
Defines the shared presentation DTO.

## External Integrations
None.

## Privacy Requirements
Paths are hidden by default in browse views.

## Security Requirements
File actions use IDs, never user-entered paths.

## Performance Requirements
≤2 s catalogue load and smooth virtualisation at 50k records on reference hardware.

## Error & Recovery Behaviour
Partial cover/index/provider failure does not hide books.

## Logging/Observability
Query latency, page size, result count and UI render duration.

## Testing
Unit queries; DB index plans; API paging/filter contracts; headless/screenshots/accessibility; E2E view parity; 2k/5k/50k performance; filesystem availability state.

## Skills Engines Applied
`design-system-skills` catalogue UX; `skills-web-dev` queries/virtualisation/performance.

## Dependencies
Phases 16 and 18.

## Parallelisation
Query optimization and three view components can proceed against one DTO.

## Migration Considerations
Legacy sort/filter preferences map to validated values.

## Definition of Done
- [x] Grid/list/directory expose equivalent catalogue actions.
- [x] Real covers and processing states render.
- [x] Filters/sorts are persisted and correct.
- [ ] Full keyboard/screen-reader flow passes.
- [x] 50k performance budget passes.

## Kaizen Review
1. Complexity: three views/facets. 2. One query/selection contract. 3. Simplify read models. 4. Delete placeholders. 5. Document parity. 6. Pattern: virtualised catalogue surface. 7. Debt decreases.
