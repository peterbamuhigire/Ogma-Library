# Phase 21: Cross-Platform Release Candidate QA

## Objective

Run public-beta gates G1-G8 across Windows/macOS, reference hardware, accessibility, and localization.

## Scope In

- Resolve findings: **F-TEST-003, F-TEST-004, F-UI-004**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `docs/qa; docs/benchmarks; tests; release artifacts`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 20 completed with green verification and committed documentation.

## Governing Skills

advanced-testing-strategy; design-audit; deployment-release-engineering

## Projected Score Uplift

- Starting projected score: **90.0%**.
- Ending projected score after successful verification: **90.5%**.

## Findings Addressed

F-TEST-003, F-TEST-004, F-UI-004
