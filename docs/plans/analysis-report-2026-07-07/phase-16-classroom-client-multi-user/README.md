# Phase 16: Reference-Hardware Performance Gate

## Objective

Run and stabilize performance budgets for cold start, reader, search, OCR, 3D, and health dashboard.

## Scope In

- Resolve findings: **F-PERF-002, F-PERF-004**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `docs/governance/REFERENCE-HARDWARE.md; docs/benchmarks; tests/OgmaLibrary.Tests/Catalogue; tests/OgmaLibrary.Tests/LanHost`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 15 completed with green verification and committed documentation.

## Governing Skills

frontend-performance; reliability-engineering; advanced-testing-strategy

## Projected Score Uplift

- Starting projected score: **85.0%**.
- Ending projected score after successful verification: **86.0%**.

## Findings Addressed

F-PERF-002, F-PERF-004
