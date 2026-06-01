# Phase 09 Closeout Audit

Date: 2026-05-31

Scope: `docs/plans/grand-plan/phase-09`, Phase 09 reader annotation code,
tests, accessibility evidence, icon manifest, and current verification records.

## Audit Result

Phase 09 is locally implementation-complete for code, automated tests, and
repository documentation. Follow-up independent sub-agent findings for
direct-PDF runtime refresh, startup schema repair, and stale evidence have been addressed. No remaining
locally actionable Phase 09 gaps are known.

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

Recorded local verification. The latest full-suite aggregate is authoritative:

| Command | Result |
| --- | --- |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --no-build --filter "FullyQualifiedName~IconCatalogPhase09Tests\|FullyQualifiedName~ReaderViewRenderTests"` | Passed: 73 UI/resource tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~InDocumentSearchTests"` | Passed: 38 backend reader/search tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --no-build --filter "FullyQualifiedName~ArchitectureTests"` | Passed: 15 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase09AnnotationTests"` | Passed: 33 Phase 09 backend annotation/bookmark/layer/citation/memory tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReaderViewRenderTests"` | Passed: 63 Phase 09 reader UI/render/accessibility tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 13 startup/direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests"` | Passed: 15 startup/direct-PDF/job recovery regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests\|FullyQualifiedName~Ingestion\|FullyQualifiedName~BookIdentityServiceTests"` | Passed: 40 startup/direct-PDF/ingestion identity regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReadingProgressServiceTests\|FullyQualifiedName~ReaderSessionServiceTests"` | Passed: 46 reader persistence/session regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShelfTests\|FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReaderSessionServiceTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 56 catalogue/read-model/citation/session/direct-open regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~Ingestion\|FullyQualifiedName~Metadata\|FullyQualifiedName~Catalogue\|FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests\|FullyQualifiedName~BookIdentityServiceTests\|FullyQualifiedName~ShelfTests\|FullyQualifiedName~ReadingProgressServiceTests\|FullyQualifiedName~ReaderSessionServiceTests"` | Passed: 195 startup, ingestion, metadata, catalogue, direct-PDF, job, and Phase 09 reader regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~BookMetadataEnrichment\|FullyQualifiedName~Metadata"` | Passed: 76 metadata/direct-PDF/write-back regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~Metadata\|FullyQualifiedName~JobManagementTests"` | Passed: 78 direct-PDF, metadata, write-back, and job regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BookDetailViewModelTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 79 selected-book enrichment, metadata, and direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 76 direct-PDF and metadata regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests"` | Passed: 3 shell reader/direct-PDF/background-refresh navigation tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MigrationTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 14 migration/direct-PDF regression tests including production-DI missing-`BookFiles` direct-open repair |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build` | Passed: 15 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build` | Passed: 85 UI tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 15, UI 85, Core 234 |
| `.github/workflows/ci.yml` | Configured: `windows-latest` + `macos-latest` matrix runs restore, format, Release build, and Release tests on push/PR |

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
| Selected-book deterministic metadata enrichment UI | Covered by book-detail view-model tests for Enrich invocation, projection refresh, provider provenance display, provider failure reporting, and refresh failure reporting; architecture guard verifies metadata enrichment stays independent from AI/OpenAI/token-consuming model namespaces. |
| Direct PDF open, folder scan, startup migration, and metadata write-back | Covered by startup migration regression, missing-model-table repair regression, production-DI direct-open and folder-scan missing-`BookFiles` repair regressions, same-hash unregistered-path folder-scan registration, external-PDF registration regression, rematched direct-open job queueing, selected-file-version job queueing, shell queued-status coverage, PDF extraction-to-catalogue projection coverage, running-shell background refresh coverage, and registered-external-PDF write-back regression; selected PDFs outside the library are added without replacing the library root, `CatalogueMigrator` repairs missing tables such as `BookFiles` when migration history incorrectly reports the database current, coexisting same-hash PDFs discovered at new present folder-scan paths are registered as new books while true moves/renames still rematch existing books, metadata/thumbnail jobs are keyed by selected file content hash, extracted title/author fields become visible in catalogue summary/detail projections and refresh into the live shell after background completion, and writable registered external PDFs can receive DocInfo metadata updates. |
| Desktop hosted-service lifecycle | Covered by startup lifecycle regression; `BookIngestionWorker` now starts in the Avalonia app instead of only being registered for a generic host that the app does not create. |
| Foreground/background context isolation | Covered by distinct-context registration regression and ingestion identity/direct-open regression set; direct PDF registration and background job polling no longer share one singleton EF Core unit-of-work. |
| Phase 09 reader repository context isolation | Annotation, bookmark, layer, reading-memory, and reading-progress repositories use factory-created contexts per method; verified by reader persistence/session and full UI regression suites. |
| Reader-facing read-path context isolation | Catalogue read model and book-file locator use factory-created contexts per operation; verified by catalogue/read-model, citation, reader-session, direct-open, and full UI suites. |
| App-lived catalogue and metadata context isolation | Background job recovery, scan health, unavailable-file flagging, ingestion orchestration, metadata enrichment/apply/merge/quality/write-back, batch enrichment, catalogue writes, audit, book, shelf, legacy annotation repositories, and `CatalogueMigrator` use factory-created contexts per operation at runtime. |
| Windows/macOS CI matrix | `.github/workflows/ci.yml` wires the Phase 09 WP9-T6 cross-platform gate: restore, format, Release build, and Release tests on `windows-latest` and `macos-latest` for pushes and PRs to `main`/`develop`. |

## Closeout Position

Treat Phase 09 as ready for owner/manual signoff. Do not mark the phase fully
closed until the remaining gates above have dated evidence.
