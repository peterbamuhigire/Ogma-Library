# Phase 32 — Virtual Bookshelf Visuals and Interaction

> [Roadmap index](./README.md) · [Previous](./phase-31-native-3d-host-and-catalogue-contract.md) · [Next](./phase-33-3d-scale-accessibility-and-performance.md)

## Objective
Build a recognisable, tactile shelf with real covers/spines, shelves, labels, camera and selection.

## Business/Product Rationale
Ogma's 3D metaphor should improve browsing and delight, not show anonymous boxes.

## SDLC Requirements
3D shelf rendering/interaction requirements and FR-CAT-001/003.

## Current Repository State
The renderer under `src/shelf3d/src/` creates instanced brown boxes, ignores cover/spine URIs and lacks shelf geometry/title/author and complete controls.

## Gap Analysis
Visual assets, texture atlas, grouping, hover/focus/camera and search navigation absent.

## Architectural Impact
Renderer consumes immutable paged shelf scenes and emits semantic user actions.

## Database Work
Persist user layout/grouping/camera preferences only if required.

## Backend Work
Scene paging/grouping and texture-atlas descriptors from shared assets.

## Frontend Work
Shelf geometry, book dimensions, spine title/author, atlas textures, lighting, hover/focus/select, zoom/camera, grouping and focus-on-result.

## PDF Processing Impact
None.

## Metadata Impact
Sanitized/truncated display labels and missing-metadata fallbacks.

## Search Impact
Search/filter changes scene via catalogue IDs.

## AI/RAG Impact
Recommendation focus uses same selection action.

## 3D Bookshelf Impact
Primary visual/interactivity deliverable.

## External Integrations
None.

## Privacy Requirements
All textures/data local.

## Security Requirements
Texture/image bounds; no runtime arbitrary URLs or HTML injection.

## Performance Requirements
Texture atlas/instancing/lazy LOD budgets designed before polish.

## Error & Recovery Behaviour
Missing/corrupt texture uses deterministic spine; scene remains interactive.

## Logging/Observability
Scene/book/texture counts, load timing, interaction errors.

## Testing
Unit layout/actions; API scene contract; WebView screenshot/interaction E2E both OSes; corrupt assets; search/advisor focus; keyboard bridge; early 50/250/1k rendering performance.

## Skills Engines Applied
`design-system-skills` visual hierarchy/motion; `skills-web-dev` Three.js/performance/security.

## Dependencies
Phase 31.

## Parallelisation
Scene/texture pipeline and interaction controls proceed against message contract.

## Migration Considerations
Generated JS stays reproducible from TypeScript; do not hand-edit bundle.

## Definition of Done
- [ ] Books show real/fallback spines/covers and readable labels.
- [ ] Shelf/camera/hover/focus/select work.
- [ ] Search/advisor focus correct book.
- [ ] Corrupt assets degrade safely.
- [ ] Both platform screenshots/interactions accepted.

## Kaizen Review
1. Complexity: scene/texture/camera. 2. Reuse asset resolver/IDs. 3. Simplify renderer inputs. 4. Remove brown-box material path. 5. Document visual/interaction system. 6. Pattern: semantic scene action. 7. Debt decreases.
