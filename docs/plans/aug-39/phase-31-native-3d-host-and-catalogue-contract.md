# Phase 31 — Native 3D Host and Catalogue Contract

> [Roadmap index](./README.md) · [Previous](./phase-30-advisor-ux-and-quality-evaluation.md) · [Next](./phase-32-virtual-bookshelf-visuals-and-interaction.md)

## Objective
Implement real WebView2 and WKWebView hosts with a secure, versioned C#↔Three.js contract.

## Business/Product Rationale
The 3D feature cannot exist until the desktop application can actually host and communicate with it.

## SDLC Requirements
FR-CAT-001, 3D architecture, accessibility fallback and platform requirements.

## Current Repository State
`src/OgmaLibrary.Bookshelf3D/Bridge/Shelf3DWebViewBootstrapper.cs` and `src/OgmaLibrary.App/Views/Shelf3D/Bookshelf3DView.axaml.cs` contain contracts/facades but no operational host adapter; catalogue navigation is disabled.

## Gap Analysis
No runtime creation/navigation/message lifecycle, local asset scheme, crash/reload or physical-platform evidence.

## Architectural Impact
`IBookshelf3DHost` platform adapters; 3D is a client of the shared catalogue projection.

## Database Work
Optional per-user camera/layout preferences only; no duplicate catalogue.

## Backend Work
Paginated shelf DTO, asset authorization, message schema/version/validation and capability health.

## Frontend Work
Embed host route, loading/error/retry and always-available 2D alternative.

## PDF Processing Impact
None.

## Metadata Impact
Only sanitized display strings enter WebView.

## Search Impact
Shared result/filter IDs.

## AI/RAG Impact
Shared recommendation IDs.

## 3D Bookshelf Impact
Primary host/bridge deliverable.

## External Integrations
No remote web content/CDN; packaged Three.js bundle and local assets only.

## Privacy Requirements
No analytics/external fetch; messages contain minimum catalogue data.

## Security Requirements
Disable navigation/devtools in release, validate messages, CSP/local scheme, prevent arbitrary file access.

## Performance Requirements
Host initializes within approved budget; incremental batched messages.

## Error & Recovery Behaviour
WebView crash/reload returns to 2D without app failure or state loss.

## Logging/Observability
Host creation, bundle/version, message failures, crashes and load timings.

## Testing
Unit message schema; API asset authorization; Windows WebView2/macOS WKWebView integration; UI E2E load/select/reload/fallback; security navigation/path tests; startup performance.

## Skills Engines Applied
`skills-web-dev` desktop/WebView/security; `design-system-skills` fallback/states; platform admin guidance.

## Dependencies
Phases 16 and 18–19.

## Parallelisation
Windows/macOS host adapters and message conformance suite can proceed in parallel.

## Migration Considerations
Replace facades without preserving non-working implementation behavior.

## Definition of Done
- [ ] Physical Windows and macOS hosts render packaged scene.
- [x] Bidirectional messages validate/version.
- [x] No external navigation/file exposure.
- [ ] Crash/reload/fallback works.
- [x] 3D route is capability-gated, not “coming soon.”

## Kaizen Review
1. Complexity: two native hosts. 2. One conformance suite. 3. Simplify shelf view. 4. Delete empty bridge facades. 5. Document message/CSP. 6. Pattern: secure embedded client. 7. Debt decreases.
