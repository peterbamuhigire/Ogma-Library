# Phase 04: Security Threat Model and SAST Baseline

## Objective

Execute the Phase 19 security-hardening gate: threat model, SAST, dependency scan, and abuse-case test plan.

## Scope In

- Resolve findings: **F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `docs/security; docs/qa; .github/workflows; tests/OgmaLibrary.Tests/Security; tests/OgmaLibrary.Tests/LanHost`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 03 completed with green verification and committed documentation.

## Governing Skills

web-app-security-audit; code-safety-scanner; security-scanning:stride-analysis-patterns

## Projected Score Uplift

- Starting projected score: **64.0%**.
- Ending projected score after successful verification: **66.5%**.

## Findings Addressed

F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001
