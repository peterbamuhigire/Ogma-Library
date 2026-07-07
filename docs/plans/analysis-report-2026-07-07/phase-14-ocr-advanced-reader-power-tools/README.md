# Phase 14: Localization and Copy Debt Closure

## Objective

Move hard-coded user-facing strings into resources and complete release-language/pseudolocale coverage.

## Scope In

- Resolve findings: **F-UI-002, F-DOC-002**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.App/Views; src/OgmaLibrary.App/ViewModels; src/OgmaLibrary.App/Localization; tests/OgmaLibrary.Tests.Ui`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 13 completed with green verification and committed documentation.

## Governing Skills

ux-content-strategy; practical-ui-design; advanced-testing-strategy

## Projected Score Uplift

- Starting projected score: **82.5%**.
- Ending projected score after successful verification: **83.5%**.

## Findings Addressed

F-UI-002, F-DOC-002
