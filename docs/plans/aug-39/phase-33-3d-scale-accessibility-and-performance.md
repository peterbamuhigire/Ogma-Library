# Phase 33 — 3D Scale, Accessibility and Performance

> [Roadmap index](./README.md) · [Previous](./phase-32-virtual-bookshelf-visuals-and-interaction.md) · [Next](./phase-34-classroom-host-security-and-read-model.md)

## Objective
Virtualise the 3D shelf, prove realistic scale and make all actions available without 3D.

## Business/Product Rationale
A beautiful shelf that fails at 500 books or excludes keyboard/screen-reader users is a failed feature.

## SDLC Requirements
3D ≥60 fps/500 books, large-library, reduced-motion and accessible fallback requirements.

## Current Repository State
`src/OgmaLibrary.App/ViewModels/Shelf3D/Bookshelf3DViewModel.cs` caps 500; the `src/shelf3d` performance script measures arithmetic, not WebView/GPU; fallback depends on the missing bridge.

## Gap Analysis
No frustum/page/LOD/texture eviction, GPU metrics, 5k/10k strategy or physical accessibility proof.

## Architectural Impact
Scene windowing and asset residency are explicit; 2D and 3D share command semantics.

## Database Work
No scene duplication; optional last-view preference only.

## Backend Work
Paged/windowed scene queries and deterministic grouping anchors.

## Frontend Work
Frustum/section virtualisation, atlas/LOD eviction, reduced motion, keyboard camera, focus synchronization and instant 2D switch.

## PDF Processing Impact
None.

## Metadata Impact
None.

## Search Impact
Filtered scene does not load excluded assets.

## AI/RAG Impact
None.

## 3D Bookshelf Impact
Primary scale/accessibility gate and contract freeze.

## External Integrations
None.

## Privacy Requirements
Telemetry local and content-free.

## Security Requirements
Bound scene/message/texture sizes.

## Performance Requirements
Measure FPS/frame-time, time-to-interactive, draw calls, CPU/GPU memory and camera latency at 50/250/1k/5k/10k; accepted fallback when hardware cannot meet target.

## Error & Recovery Behaviour
GPU/WebView degradation switches to 2D while preserving filters/selection.

## Logging/Observability
Local performance overlay/export with hardware, bundle and scene versions.

## Testing
Unit windowing/eviction; API paging; physical WebView/GPU E2E both OSes; keyboard/reduced-motion/fallback accessibility; lost-context recovery; benchmark matrix and soak.

## Skills Engines Applied
`design-system-skills` accessibility/motion; `skills-web-dev` 3D/performance; platform evidence guidance.

## Dependencies
Phases 19 and 32.

## Parallelisation
Performance engineering and accessibility parity proceed together; neither can be deferred after polish.

## Migration Considerations
Reset incompatible old camera prefs with notice.

## Definition of Done
- [ ] Real GPU/WebView metrics replace arithmetic-only claims.
- [ ] 500-book target meets accepted frame budget on reference hardware.
- [ ] 1k/5k/10k strategy remains bounded and usable.
- [ ] All actions work in accessible 2D/keyboard paths.
- [ ] 3D contract freeze recorded.

## Kaizen Review
1. Complexity: virtualisation/LOD/parity. 2. Share selection/filter commands. 3. Simplify scene residency. 4. Remove hard 500 cap. 5. Document benchmark/fallback. 6. Pattern: capability-adaptive renderer. 7. Debt decreases.
