# Phase 05: Untrusted PDF Worker Isolation

## Objective

Harden untrusted PDF handling so malformed PDFs cannot access network, spawn processes, or write outside the worker sandbox.

## Scope In

- Resolve findings: **F-SEC-001, F-SEC-002**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; tests/OgmaLibrary.Tests/Security`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 04 completed with green verification and committed documentation.

## Governing Skills

web-app-security-audit; code-safety-scanner; system-architecture-design

## Projected Score Uplift

- Starting projected score: **66.5%**.
- Ending projected score after successful verification: **69.0%**.

## Findings Addressed

F-SEC-001, F-SEC-002
