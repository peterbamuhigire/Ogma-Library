# Phase 09 Closeout Audit

Date: 2026-05-31

Scope: `docs/plans/grand-plan/phase-09`, Phase 09 reader annotation code,
tests, accessibility evidence, icon manifest, and current verification records.

## Audit Result

Phase 09 is locally implementation-complete for code, automated tests, and
repository documentation. No remaining locally actionable Phase 09 gaps were
found after direct audit and independent sub-agent review.

The phase is not final-release complete because the remaining gates require
owner or manual evidence. Premium SVG icon assets have been delivered.

## Remaining Gates

| Gate | Type | Evidence needed |
| --- | --- | --- |
| Narrator and VoiceOver walkthrough | Manual | Reviewer-dated pass in `docs/qa/PHASE-09-A11Y-SIGNOFF.md`. |
| Color accessibility visual review | Manual | Human confirmation that rendered highlights match the automated contrast gate and are not color-only meaning. |
| Pseudolocale visual review | Manual | Human review of `artifacts/screenshots/reader-qps-ploc.png` for polish and truncation. |
| Owner confirmations | Owner-gated | Palette, citation export format, and reading-memory disposition wording confirmed in `README.md` owner asks. |

## Automated Evidence

The current Phase 09 evidence record is
`docs/plans/grand-plan/phase-09/evidence.md`.

Latest recorded local verification:

| Command | Result |
| --- | --- |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --no-build --filter "FullyQualifiedName~IconCatalogPhase09Tests\|FullyQualifiedName~ReaderViewRenderTests"` | Passed: 56 UI/resource tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~InDocumentSearchTests"` | Passed: 38 backend reader/search tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --no-build --filter "FullyQualifiedName~ArchitectureTests"` | Passed: 14 architecture tests |
| `dotnet test OgmaLibrary.sln --no-build` | Passed: Architecture 14, UI 65, Core 210 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 4 startup/direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests"` | Passed: 7 startup/direct-PDF/job recovery regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests\|FullyQualifiedName~Ingestion\|FullyQualifiedName~BookIdentityServiceTests"` | Passed: 31 startup/direct-PDF/ingestion identity regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReadingProgressServiceTests\|FullyQualifiedName~ReaderSessionServiceTests"` | Passed: 46 reader persistence/session regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShelfTests\|FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReaderSessionServiceTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 49 catalogue/read-model/citation/session/direct-open regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build` | Passed: 65 UI tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 14, UI 65, Core 219 |

Manual signoff runbook: `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`.

## Coverage Map

| Area | Status |
| --- | --- |
| Durable annotation, bookmark, layer, citation, and reading-memory persistence | Covered by Phase 09 backend tests and end-to-end SQLite restart smoke. |
| Rotated-page annotation reload | Covered by golden-corpus oracle and one-pixel reload assertion. |
| R1 data-loss paths | Covered by repository rollback, disk-full, failed-publisher, invalid-FK, partial-region repair, concurrent-write, bookmark-abort, bookmark-reopen, and failed/cross-book layer-delete tests. |
| Reader UI interaction | Covered by Avalonia headless tests for selection menu, note dismissal, bookmarks, layers, citation, and reading-memory controls. |
| Accessibility automation | Covered for automation names, keyboard focus, bookmark count label, overlay label, icon labels, contrast math, and pseudolocale bounds. |
| Premium icons | Delivered SVG assets copied into all 22 Phase 09 key-named runtime paths and recorded in `docs/plans/grand-plan/phase-09/icons.md`. |
| Architecture | Covered by annotations/Search isolation, annotations/AI isolation, catalogue contract boundary, and no PDF write-back tests. |
| Direct PDF open and startup migration | Covered by startup migration regression and direct external-PDF registration regression; selected PDFs outside the library are added without replacing the library root. |
| Desktop hosted-service lifecycle | Covered by startup lifecycle regression; `BookIngestionWorker` now starts in the Avalonia app instead of only being registered for a generic host that the app does not create. |
| Foreground/background context isolation | Covered by distinct-context registration regression and ingestion identity/direct-open regression set; direct PDF registration and background job polling no longer share one singleton EF Core unit-of-work. |
| Phase 09 reader repository context isolation | Annotation, bookmark, layer, reading-memory, and reading-progress repositories use factory-created contexts per method; verified by reader persistence/session and full UI regression suites. |
| Reader-facing read-path context isolation | Catalogue read model and book-file locator use factory-created contexts per operation; verified by catalogue/read-model, citation, reader-session, direct-open, and full UI suites. |

## Closeout Position

Treat Phase 09 as ready for owner/manual signoff. Do not mark the phase fully
closed until the remaining gates above have dated evidence.
