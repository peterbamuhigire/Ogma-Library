# Phase 01 Completion Record: Restore and Build Stabilization

Date: 2026-07-07

## Summary

Phase 01 restored the canonical build and test path while preserving NuGet audit
and `TreatWarningsAsErrors=true`.

Changes made:

- Updated `Microsoft.EntityFrameworkCore.Design` and `Microsoft.EntityFrameworkCore.Sqlite` from 9.0.16 to 9.0.17 in `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj`.
- Added explicit `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 references in the production SQLite-owning project and the direct SQLite test project so the native package resolves outside the vulnerable 2.1.x line.
- Updated `tests/OgmaLibrary.Tests/Search/Phase15OcrSchemaTests.cs` to seed and verify the pre-Phase-15 schema through raw SQLite commands during migration down/up testing.
- Updated `CHANGELOG.md`, this completion record, the findings register, and the master plan status.

## Acceptance Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| AC01-1: Every phase finding has a concrete change. | Pass | F-BLD-001 resolved by package graph changes in `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj` and `tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj`; F-TEST-001 resolved by passing canonical verification commands. |
| AC01-2: No safety gate is weakened. | Pass | `Directory.Build.props` still contains `TreatWarningsAsErrors=true`; no `NoWarn` for NU1903 was added; `dotnet restore OgmaLibrary.sln` passed with audit enabled. |
| AC01-3: Targeted tests for affected modules pass. | Pass | `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase15OcrSchemaTests.Migration_M015_UpAndDown_LeavesData_Intact"` passed: 1/1. `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReaderView_ReadingMemoryFieldLostFocus_AutoSavesEditedField"` passed: 1/1. |
| AC01-4: Full repository verification passes. | Pass | `dotnet restore OgmaLibrary.sln` passed. `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed with 0 warnings and 0 errors. `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` passed: 35 architecture tests, 628 core tests, 126 UI tests. |
| AC01-5: Documentation is current and traceable. | Pass | Updated `CHANGELOG.md`, `docs/analysis-report-2026-07-07/99-findings-register.md`, `docs/plans/analysis-report-2026-07-07/00-master-plan.md`, and this file. |
| AC01-6: Projected score moves from 57.0% to 60.0% only if all criteria pass. | Pass | All criteria above passed; projected score is now 60.0%. |

## Verification Commands

| Command | Result |
| --- | --- |
| `dotnet restore OgmaLibrary.sln` | Pass. Restore completed for all solution projects with NuGet audit enabled. |
| `dotnet list src\OgmaLibrary.Infrastructure\OgmaLibrary.Infrastructure.csproj package --include-transitive --vulnerable` | Pass. Reported no vulnerable packages for `OgmaLibrary.Infrastructure`. |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Pass. Build succeeded with 0 warnings and 0 errors. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase15OcrSchemaTests.Migration_M015_UpAndDown_LeavesData_Intact"` | Pass. 1/1 tests passed. |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReaderView_ReadingMemoryFieldLostFocus_AutoSavesEditedField"` | Pass. 1/1 tests passed. |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` | Pass. 789 total tests passed. |

## Findings Resolved

| Finding | Resolution |
| --- | --- |
| F-BLD-001 | Resolved by moving the SQLite native dependency graph off the vulnerable 2.1.x line without suppressing NU1903 or disabling warnings-as-errors. |
| F-TEST-001 | Resolved by restoring a passing canonical restore/build/test baseline with NuGet audit enabled. |

## Deviations

None.

## Extra Touches

- `tests/OgmaLibrary.Tests/Search/Phase15OcrSchemaTests.cs`: updated because the patched SQLite native engine exposed an invalid migration-test setup that used the current EF model against an older schema.
- `CHANGELOG.md`: updated because dependency and verification behavior changed.

## Score

| Metric | Before | After |
| --- | ---: | ---: |
| Projected audit score | 57.0% | 60.0% |
