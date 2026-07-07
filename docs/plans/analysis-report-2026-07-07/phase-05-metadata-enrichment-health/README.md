# Phase 05: Untrusted PDF Worker Isolation

## Objective

Harden untrusted PDF handling so malformed PDFs cannot access network, spawn processes, or write outside the worker sandbox.

## Scope In

- Resolve finding **F-SEC-001** and close the Phase 04 worker-isolation risk/control evidence target (**P04-R1 / CTRL-OGMA-004..007**). **F-SEC-002** remains assigned to Phase 06.
- Work in these target areas, with the approved Deviation Protocol expansion to the PDF adapter and DI boundary required for a real subprocess isolation path: `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; src/OgmaLibrary.Infrastructure/Pdf; src/OgmaLibrary.App/CompositionRoot.cs; tests/OgmaLibrary.Tests/Security`.
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

F-SEC-001; P04-R1 / CTRL-OGMA-004..007
