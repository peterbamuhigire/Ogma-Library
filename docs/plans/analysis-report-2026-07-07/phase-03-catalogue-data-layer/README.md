# Phase 03: Canonical Test Recovery and Known Failure Fix

## Objective

Recover the full canonical suite and fix the documented 2000-book metadata health failure.

## Scope In

- Resolve findings: **F-TEST-002, F-FUNC-001, F-PERF-004**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `tests/OgmaLibrary.Tests/Metadata; src/OgmaLibrary.Infrastructure/Metadata; src/OgmaLibrary.Application/Metadata`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 02 completed with green verification and committed documentation.

## Governing Skills

advanced-testing-strategy; reliability-engineering; database-design-engineering

## Projected Score Uplift

- Starting projected score: **61.5%**.
- Ending projected score after successful verification: **64.0%**.

## Findings Addressed

F-TEST-002, F-FUNC-001, F-PERF-004
