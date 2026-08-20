# Phase 2 — Composition, Configuration and Startup

> [Roadmap index](./README.md) · [Previous](./phase-01-evidence-baseline-and-scope-freeze.md) · [Next](./phase-03-canonical-library-identity-model.md)

## Objective
Replace the oversized/incomplete composition root with validated modules and non-blocking startup.

## Business/Product Rationale
Missing AI/3D registrations and synchronous startup make existing capabilities inaccessible and fragile.

## SDLC Requirements
NFR-OGMA-001/005, NFR-PROD-001/009; Development Standards and HLD composition rules.

## Current Repository State
`src/OgmaLibrary.App/CompositionRoot.cs` manually binds many services; `src/OgmaLibrary.App/App.axaml.cs` blocks on async initialization.

## Gap Analysis
Configuration, health, provider and platform bindings are inconsistent.

## Architectural Impact
Introduce module registrars, typed options, capability flags and an async startup coordinator.

## Database Work
Move migration execution behind a recoverable startup service; no schema change.

## Backend Work
Register modules deterministically; validate dependencies and feature flags.

## Frontend Work
Add startup/degraded/retry shell states.

## PDF Processing Impact
Validate worker executable/native dependency availability without opening PDFs.

## Metadata Impact
Provider availability becomes a health capability.

## Search Impact
Index readiness no longer blocks catalogue startup.

## AI/RAG Impact
Register disabled-by-default placeholders only; concrete gateway arrives Phase 27.

## 3D Bookshelf Impact
Expose capability detection without claiming the host works.

## External Integrations
Typed provider configuration with no embedded secrets.

## Privacy Requirements
External capabilities remain disabled until explicit configuration.

## Security Requirements
Fail closed on invalid security options; avoid secret values in validation errors.

## Performance Requirements
Shell visible without UI-thread work over 100 ms; measure cold startup.

## Error & Recovery Behaviour
Migration/provider/index failure opens a usable degraded shell with retry/export diagnostics.

## Logging/Observability
Structured startup spans and module health, with redaction.

## Testing
Unit options tests; integration composition resolution; migration/startup pipeline; API adapter health; filesystem native dependency tests; AI-disabled tests; headless E2E degraded shell; startup performance.

## Skills Engines Applied
`skills-web-dev` architecture/configuration; `design-system-skills` degraded states; Windows guidance for startup/platform probes.

## Dependencies
Phase 1.

## Parallelisation
Module extraction and startup UX can proceed behind agreed contracts.

## Migration Considerations
Preserve existing settings keys with explicit adapters and warnings.

## Definition of Done
- [ ] All modules resolve in enabled/disabled matrices.
- [ ] Startup is asynchronous and cancellable.
- [ ] Catalogue opens when optional providers fail.
- [ ] Configuration is validated and redacted.
- [ ] Architecture tests prevent composition drift.

## Kaizen Review
1. Complexity: explicit module lifecycle. 2. Remove duplicate registrations. 3. Simplify constructors/options. 4. Delete stale phase comments. 5. Document module graph. 6. Pattern: capability health. 7. Debt decreases.
