# Phase 01: Restore and Build Stabilization

## Objective

Make the repository restore/buildable with NuGet audit enabled and warnings-as-errors preserved.

## Scope In

- Resolve findings: **F-BLD-001, F-TEST-001**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `Directory.Build.props; OgmaLibrary.sln; src/*/*.csproj; tests/*/*.csproj; package lock/audit artifacts`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

None. This is the entry phase.

## Governing Skills

code-safety-scanner; deployment-release-engineering; advanced-testing-strategy

## Projected Score Uplift

- Starting projected score: **57.0%**.
- Ending projected score after successful verification: **60.0%**.

## Findings Addressed

F-BLD-001, F-TEST-001
