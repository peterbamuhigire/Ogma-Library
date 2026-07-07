# Phase 07: Database Migration Integrity

## Objective

Clean up migration repair debt, schema comments, durability proof, and backup/corruption evidence.

## Scope In

- Resolve findings: **F-DATA-001, F-DATA-002, F-DATA-003**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/Persistence/Migrations; tests/OgmaLibrary.Tests/Catalogue`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 06 completed with green verification and committed documentation.

## Governing Skills

database-design-engineering; database-reliability; advanced-testing-strategy

## Projected Score Uplift

- Starting projected score: **71.0%**.
- Ending projected score after successful verification: **73.0%**.

## Findings Addressed

F-DATA-001, F-DATA-002, F-DATA-003
