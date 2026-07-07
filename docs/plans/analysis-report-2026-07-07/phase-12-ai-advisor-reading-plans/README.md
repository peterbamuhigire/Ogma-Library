# Phase 12: 3D Shelf Platform Acceptance

## Objective

Close the 3D shelf macOS FPS gate and fallback behavior with reference hardware evidence.

## Scope In

- Resolve findings: **F-ARCH-004, F-PERF-001**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.Bookshelf3D; src/shelf3d; src/OgmaLibrary.App/Views/Shelf3D; docs/benchmarks`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 11 completed with green verification and committed documentation.

## Governing Skills

frontend-performance; practical-ui-design; system-architecture-design

## Projected Score Uplift

- Starting projected score: **80.0%**.
- Ending projected score after successful verification: **81.5%**.

## Findings Addressed

F-ARCH-004, F-PERF-001
