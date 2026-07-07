# Phase 18: Packaging and Installers

## Objective

Create executable Velopack/MSIX/DMG packaging configuration and CI artifacts.

## Scope In

- Resolve findings: **F-REL-001, F-FUNC-002**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `build; scripts; .github/workflows; src/OgmaLibrary.App; docs/deployment`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 17 completed with green verification and committed documentation.

## Governing Skills

deployment-release-engineering; cicd-pipelines; docker-development

## Projected Score Uplift

- Starting projected score: **87.0%**.
- Ending projected score after successful verification: **88.0%**.

## Findings Addressed

F-REL-001, F-FUNC-002
