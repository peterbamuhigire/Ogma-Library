# Phase 06: At-Rest Encryption and Secret Lifecycle

## Objective

Close local data and provider-secret protection gaps with explicit storage, redaction, and migration behavior.

## Scope In

- Resolve findings: **F-SEC-002, F-SEC-003, F-SEC-005**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.Infrastructure/Security; src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/AI; docs/security`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 05 completed with green verification and committed documentation.

## Governing Skills

ai-security; code-safety-scanner; database-design-engineering

## Projected Score Uplift

- Starting projected score: **69.0%**.
- Ending projected score after successful verification: **71.0%**.

## Findings Addressed

F-SEC-002, F-SEC-003, F-SEC-005
