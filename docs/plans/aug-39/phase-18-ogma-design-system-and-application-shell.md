# Phase 18 — Ogma Design System and Application Shell

> [Roadmap index](./README.md) · [Previous](./phase-17-worker-reliability-and-observability.md) · [Next](./phase-19-production-2d-catalogue.md)

## Objective
Create the coherent, accessible Ogma visual language and reachable application navigation/settings.

## Business/Product Rationale
“Beautifully managed” is a product requirement, and users need reachable controls before advanced features.

## SDLC Requirements
FR-UX-001..008, accessibility/localisation NFRs, v2.1 design direction.

## Current Repository State
`src/OgmaLibrary.App/App.axaml`, `OgmaLibrary.App.csproj` and the AXAML under `Views/` use FluentTheme/Inter, hard-coded values/strings, emoji/mojibake icons and fragmented settings.

## Gap Analysis
Typography conflicts, incomplete token hierarchy, navigation, command palette and state matrix.

## Architectural Impact
Presentation modules consume shared tokens/components/navigation contracts.

## Database Work
Theme, density, language and user preference schema/versioning.

## Backend Work
Settings application services and capability/degraded-state projections.

## Frontend Work
Spectral/Public Sans/JetBrains Mono assets if licensing is confirmed; shell, navigation, command palette, notifications, buttons/forms/dialogs/states and licensed SVG icons.

## PDF Processing Impact
Shared progress/error components.

## Metadata Impact
Reusable provenance/review components.

## Search Impact
Shared query/filter controls.

## AI/RAG Impact
Navigation remains hidden/disabled until Phase 27 capability is healthy.

## 3D Bookshelf Impact
2D remains the accessible primary/fallback route.

## External Integrations
None.

## Privacy Requirements
Privacy/settings are first-class and understandable.

## Security Requirements
Safe text rendering, no arbitrary markup/link activation.

## Performance Requirements
Virtualised lists, fast theme switch and no UI-thread I/O.

## Error & Recovery Behaviour
Complete empty/loading/processing/degraded/offline/error/retry matrix.

## Logging/Observability
UI errors and command failures by event ID, never private content.

## Testing
Unit view models; settings DB; API capability projections; headless UI/screenshots; keyboard/focus/contrast/pseudo-localisation; Windows Narrator/macOS VoiceOver E2E; UI performance.

## Skills Engines Applied
`design-system-skills` primary; Avalonia guidance from `skills-web-dev`; `srs-skills` UX acceptance.

## Dependencies
Phases 2 and 17.

## Parallelisation
Tokens/components, shell/navigation and localisation extraction can proceed in parallel.

## Migration Considerations
Map legacy theme/language; retain fallback font until packaged assets validate.

## Definition of Done
- [ ] No Inter/hard-coded color/string drift in audited surfaces.
- [ ] All existing views are reachable or explicitly removed.
- [ ] State matrix and command palette work.
- [ ] en/fr resource coverage is complete for current features.
- [ ] Keyboard/Narrator/VoiceOver baseline passes.

## Kaizen Review
1. Complexity: token/component system. 2. Remove repeated AXAML styles. 3. Simplify navigation/state. 4. Delete mojibake/phase labels. 5. Document tokens/components. 6. Pattern: capability-aware route. 7. Debt decreases.
