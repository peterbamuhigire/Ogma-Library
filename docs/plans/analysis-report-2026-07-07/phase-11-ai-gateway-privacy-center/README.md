# Phase 11: Split View Reader Workflow

## Objective

Convert the split-view scaffold into a working two-document reader/comparison workflow or remove it from beta scope.

## Scope In

- Resolve findings: **F-ARCH-003, F-FUNC-003**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.App/ViewModels/Reader; src/OgmaLibrary.App/Views/Reader; src/OgmaLibrary.Reader; tests/OgmaLibrary.Tests.Ui`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 10 completed with green verification and committed documentation.

## Governing Skills

practical-ui-design; interaction-design-patterns; advanced-testing-strategy

## Projected Score Uplift

- Starting projected score: **78.5%**.
- Ending projected score after successful verification: **80.0%**.

## Findings Addressed

F-ARCH-003, F-FUNC-003
