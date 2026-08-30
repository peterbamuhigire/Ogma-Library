# Phase 5 — Library Roots and Path Security

> [Roadmap index](./README.md) · [Previous](./phase-04-identity-schema-and-data-migration.md) · [Next](./phase-06-processing-state-machine-and-scan-sessions.md)

## Objective
Make multiple Windows/macOS roots first-class, canonical, permission-aware and portable.

## Business/Product Rationale
External disks and moved roots are normal personal-library behavior, not deletion events.

## SDLC Requirements
FR-LIB-001/002/004, CTRL-009/010, NFR-OGMA portability.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Ingestion/PdfDiscoveryService.cs` and `src/OgmaLibrary.App/CompositionRoot.cs` implement one effective root and string-prefix containment; symlink/mount semantics are incomplete.

## Gap Analysis
Root identity, health, bookmarks, volume identity and platform comparison rules are absent.

## Architectural Impact
Introduce `ILibraryRootPlatformAdapter` for Windows and macOS.

## Database Work
Root ID, display path, canonical locator/bookmark, volume hints, permission/health/last-success state and policies.

## Backend Work
Canonical containment, boundary-aware comparisons, symlink consent and root health probes.

## Frontend Work
Root manager with add/remove/relink, permissions, health and safe explanations.

## PDF Processing Impact
Workers receive brokered root/file IDs, not unchecked arbitrary paths.

## Metadata Impact
None beyond preserving records during root changes.

## Search Impact
Availability/root filters become explicit.

## AI/RAG Impact
Unavailable assets are excluded from actionable recommendations.

## 3D Bookshelf Impact
Unavailable state appears consistently from the catalogue contract.

## External Integrations
None.

## Privacy Requirements
UI may show paths; diagnostics redact user names by default.

## Security Requirements
Reject traversal, prefix confusion and unconsented symlink escape.

## Performance Requirements
Root probes are bounded and never enumerate entire volumes on UI thread.

## Error & Recovery Behaviour
Disconnected/denied roots become unavailable roots; records remain intact and relinkable.

## Logging/Observability
Root health transitions with stable ID and redacted locator.

## Testing
Unit canonicalization; DB root constraints; integration multiple roots; filesystem case/symlink/reparse/network/external-drive tests on both OSes; API DTO; E2E relink; performance probes.

## Skills Engines Applied
Windows admin path/ACL guidance; `skills-web-dev` platform adapters; `srs-skills` failure states.

## Dependencies
Phases 3–4.

## Parallelisation
Windows and macOS adapters can proceed against one conformance suite.

## Migration Considerations
Convert the existing setting into a root row without changing its selected folder.

## Definition of Done
- [ ] Multiple roots work.
- [ ] Canonical containment passes hostile cases.
- [ ] Disconnect never means delete.
- [ ] Relink preserves file/catalogue identity.
- [ ] Both platform adapters pass physical acceptance.

## Kaizen Review
1. Complexity: platform path differences. 2. Centralize validators. 3. Simplify consumers to root/file IDs. 4. Delete prefix checks. 5. Document root semantics. 6. Pattern: platform conformance suite. 7. Debt decreases.
