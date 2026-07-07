# Phase 05 Completed: Untrusted PDF Worker Isolation

Date: 2026-07-07

## Summary

Phase 05 routes production PDF rendering, page metadata, text extraction, and cover/spine asset generation through the `OgmaLibrary.Workers pdf-worker` subprocess boundary. Worker outputs are written only inside a per-operation sandbox; the parent process copies completed assets to sidecar paths after successful worker exit. Windows worker launches attempt a Job Object child-process limit, and tests now cover network/process policy denial, temp traversal denial, malformed PDF behavior, and valid subprocess rendering.

The phase also isolates testhost composition from the user's real app-data catalogue so verification cannot crawl a local library during UI tests.

## Acceptance Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| AC05-1: Every assigned finding/control has a concrete change. | Pass | `IsolatedPdfRendererFactory`, `PdfWorkerClient`, `PdfWorkerCommand`, `PdfWorkerIsolationTests`, `phase-05-pdf-worker-isolation.md`; `F-SEC-001` updated and `P04-R1 / CTRL-OGMA-004..007` closed. |
| AC05-2: No safety gate weakened. | Pass | Warnings-as-errors remain enabled; NuGet audit, analyzer, format, secret, restore, build, targeted, and full-suite gates passed. |
| AC05-3: Targeted affected-module tests pass. | Pass | `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Security|FullyQualifiedName~Reader|FullyQualifiedName~Ocr|FullyQualifiedName~Ingestion|FullyQualifiedName~Pdf" --logger "console;verbosity=minimal"`: 147 passed. |
| AC05-4: Full repository verification passes. | Pass | `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"`: architecture 37 passed, UI 126 passed, core 637 passed. |
| AC05-5: Documentation is current and traceable. | Pass | Updated ADR-0004, Phase 05 plan docs, `phase-05-pdf-worker-isolation.md`, control matrix, risk register, findings register, master plan, and benchmark smoke evidence. |
| AC05-6: Projected score moves to 69.0% only after all criteria pass. | Pass | Score recorded below after all verification gates passed. |

## Verification Evidence

| Command | Result |
| --- | --- |
| `dotnet restore OgmaLibrary.sln` | Pass. All projects up-to-date for restore. |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Pass. 0 warnings, 0 errors. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Security|FullyQualifiedName~Reader|FullyQualifiedName~Ocr|FullyQualifiedName~Ingestion|FullyQualifiedName~Pdf" --logger "console;verbosity=minimal"` | Pass. 147 tests passed. |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` | Pass. 37 architecture, 126 UI, 637 core tests passed. |
| `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | Pass. No vulnerable packages reported for any project. |
| `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal` | Pass. No analyzer changes required. |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Pass after mechanical CRLF normalization by `dotnet format OgmaLibrary.sln --no-restore`. |
| Secret pattern scan from `.github/workflows/ci.yml` | Pass. `No high-confidence secret patterns found.` |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PdfWorkerIsolationTests.IsolatedPdfRenderer_ValidPdf_RendersThroughWorker" --logger "console;verbosity=normal"` | Pass. 1 test passed; xUnit reported 1 s test duration, 2.5907 s total VSTest time. |

## Deviations

| Deviation | Approval | Outcome |
| --- | --- | --- |
| Phase 05 initially listed `F-SEC-002`, but that finding is at-rest encryption and belongs to Phase 06. | User selected option 3 on 2026-07-07. | Phase 05 plan/master docs now target `F-SEC-001` plus `P04-R1 / CTRL-OGMA-004..007`; `F-SEC-002` remains open for Phase 06. |
| Original target-file scope could not honestly satisfy subprocess PDF worker isolation because production PDF parsing lived behind `src/OgmaLibrary.Infrastructure/Pdf` and DI registration. | User selected option 1 on 2026-07-07. | Scope expanded to the PDF adapter and composition boundary; production DI now uses `IsolatedPdfRendererFactory`. |

## Extra Touches

- `src/OgmaLibrary.Infrastructure/Pdf`: new worker client and isolated renderer proxy required by the approved boundary expansion.
- `src/OgmaLibrary.App/CompositionRoot.cs`: registers the isolated renderer factory.
- `src/OgmaLibrary.Infrastructure/Catalogue/CatalogueServiceExtensions.cs`: testhost default data directory now uses temp storage to prevent verification runs from reading the user's real library.
- `docs/adrs/0004-pdfium-wrapper-adapter.md`: records the Phase 05 subprocess isolation amendment.
- Mechanical CRLF normalization was applied by `dotnet format OgmaLibrary.sln --no-restore` to satisfy the repository formatting gate.

## Findings Resolved

| Finding / control | Status |
| --- | --- |
| `F-SEC-001` | Remains resolved with Phase 05 worker-isolation evidence added to the findings register. |
| `P04-R1 / CTRL-OGMA-004..007` | Closed by subprocess worker boundary, sandboxed output handoff, platform notes, and `PdfWorkerIsolationTests`. |

## Score

| Metric | Before | After |
| --- | ---: | ---: |
| Projected audit score | 66.5% | 69.0% |
