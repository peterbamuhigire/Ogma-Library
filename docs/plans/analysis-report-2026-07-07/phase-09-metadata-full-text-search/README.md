# Phase 09: Metadata, OCR, and Health Reliability

## Objective

Make metadata enrichment, OCR status, FTS, and health dashboard reliable at large-library scale.

## Scope In

- Resolve findings: **F-FUNC-001, F-PERF-004, F-DATA-003**.
- Work only in these target areas unless an import, project reference, or test fixture must change mechanically: `src/OgmaLibrary.Infrastructure/Metadata; src/OgmaLibrary.Infrastructure/Search; src/OgmaLibrary.Infrastructure/Ocr; tests/OgmaLibrary.Tests/Metadata`.
- Re-open every named skill file before implementation; do not rely on this plan summary as the skill body.

## Scope Out

- Unrelated refactors, cosmetic rewrites outside the listed files, and weakening build/security/test gates.
- Suppressing NuGet audit, disabling warnings-as-errors, skipping failing tests, or removing release criteria.

## Prerequisites

Phase 08 completed with green verification and committed documentation.

## Governing Skills

advanced-testing-strategy; reliability-engineering; database-design-engineering

## Projected Score Uplift

- Starting projected score: **75.0%**.
- Ending projected score after successful verification: **77.0%**.

## Findings Addressed

F-FUNC-001, F-PERF-004, F-DATA-003
