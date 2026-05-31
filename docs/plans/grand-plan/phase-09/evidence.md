# Phase 09 Verification Evidence

Last updated: 2026-06-01

This file tracks current evidence for Phase 09 closeout. It is intentionally
separate from the plan so unresolved owner/manual items remain visible.

## Closeout position

Current status: locally implementation-complete. Direct audit and independent
sub-agent review findings have been addressed in code, tests, and evidence; no
remaining locally actionable Phase 09 gaps are known. Final phase closure is still blocked on owner/manual gates:
Narrator/VoiceOver walkthrough, manual color review, manual pseudolocale review,
and owner confirmations. Premium SVG icons have been delivered and committed.

## Automated verification

Recorded local commands. The latest full-suite aggregate is authoritative:

| Command | Result |
| --- | --- |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --no-build --filter "FullyQualifiedName~IconCatalogPhase09Tests\|FullyQualifiedName~ReaderViewRenderTests"` | Passed: 56 UI/resource tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~InDocumentSearchTests"` | Passed: 38 backend reader/search tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --no-build --filter "FullyQualifiedName~ArchitectureTests"` | Passed: 14 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase09AnnotationTests"` | Passed: 33 Phase 09 backend annotation/bookmark/layer/citation/memory tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReaderViewRenderTests"` | Passed: 51 Phase 09 reader UI/render/accessibility tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 4 startup/direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests"` | Passed: 7 startup/direct-PDF/job recovery regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests\|FullyQualifiedName~Ingestion\|FullyQualifiedName~BookIdentityServiceTests"` | Passed: 31 startup/direct-PDF/ingestion identity regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReadingProgressServiceTests\|FullyQualifiedName~ReaderSessionServiceTests"` | Passed: 46 reader persistence/session regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShelfTests\|FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~ReaderSessionServiceTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 49 catalogue/read-model/citation/session/direct-open regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApplicationStartupTests\|FullyQualifiedName~Ingestion\|FullyQualifiedName~Metadata\|FullyQualifiedName~Catalogue\|FullyQualifiedName~Phase09AnnotationTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~JobManagementTests\|FullyQualifiedName~BookIdentityServiceTests\|FullyQualifiedName~ShelfTests\|FullyQualifiedName~ReadingProgressServiceTests\|FullyQualifiedName~ReaderSessionServiceTests"` | Passed: 180 startup, ingestion, metadata, catalogue, direct-PDF, job, and Phase 09 reader regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~BookMetadataEnrichment\|FullyQualifiedName~Metadata"` | Passed: 66 metadata/direct-PDF/write-back regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~PdfWriteBackTests\|FullyQualifiedName~Metadata\|FullyQualifiedName~JobManagementTests"` | Passed: 69 direct-PDF, metadata, write-back, and job regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BookDetailViewModelTests\|FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 74 selected-book enrichment, metadata, and direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DirectPdfOpenServiceTests\|FullyQualifiedName~Metadata"` | Passed: 72 direct-PDF and metadata regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests"` | Passed: 3 shell reader/direct-PDF/background-refresh navigation tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MigrationTests\|FullyQualifiedName~DirectPdfOpenServiceTests"` | Passed: 10 migration/direct-PDF regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build` | Passed: 15 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build` | Passed: 67 UI tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 15, UI 67, Core 228 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectPdfOpen\|FullyQualifiedName~Migration_RepairsMissingModelTable_WhenHistorySaysCurrent\|FullyQualifiedName~IngestionPipeline_RegistersNewBooks"` | Passed: 9 direct-PDF, catalogue migration repair, and ingestion registration regression tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --filter "FullyQualifiedName~ReaderView_Phase09ControlsExposeActionSpecificAutomationNames\|FullyQualifiedName~ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames\|FullyQualifiedName~IconCatalogPhase09Tests"` | Passed: 7 focused Phase 09 accessibility/resource tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --filter "FullyQualifiedName~ReaderView_Phase09ControlsExposeActionSpecificAutomationNames\|FullyQualifiedName~ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames\|FullyQualifiedName~ReaderView_LayerActions_DisabledWhenOnlyOneLayerRemains\|FullyQualifiedName~ReaderViewModel_DeleteLayer_ReportsLastLayerConstraint\|FullyQualifiedName~ReaderViewModel_MergeLayer_ReportsLastLayerConstraintWhenNoTargetExists"` | Passed: 5 focused layer accessibility/integrity tests |
| `.github/workflows/ci.yml` | Configured: `windows-latest` + `macos-latest` matrix runs `dotnet restore`, `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore`, `dotnet build OgmaLibrary.sln --configuration Release --no-restore`, and `dotnet test OgmaLibrary.sln --configuration Release --no-build --verbosity normal` on push/PR |

## Evidence by area

| Area | Evidence | Status |
| --- | --- | --- |
| Annotation/bookmark/layer persistence | `tests/OgmaLibrary.Tests/Reader/Phase09AnnotationTests.cs`; null bookmark labels remain presentation-localized | Automated coverage present |
| End-to-end restart smoke | `Phase09_EndToEndRestartSmoke_PersistsReaderArtifacts`; persists bookmark, layer rename, highlight, note, reading memory, and citation export through a real SQLite reopen | Automated coverage present |
| Closeout audit | `docs/implementation/review-31-May-2026/phase-09-closeout-audit.md`; independent sub-agent audit findings for direct-PDF runtime refresh, startup schema repair, and stale evidence were addressed | Locally complete; manual/owner gates remain |
| Manual signoff packet | `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`; exact reviewer steps, evidence fields, and owner decisions documented | Ready for reviewer/owner completion |
| Continuation audit | `docs/implementation/review-01-Jun-2026/phase-09-continuation-audit.md`; current local state, remaining manual/owner gates, and unavailable remote CI status check recorded | Locally complete; remote CI result not available from this environment |
| Code-review findings | Sub-agent review findings addressed: reader VM continuations stay on UI context; reader async handlers await VM work; cross-book layer delete is ignored; direct layer delete moves annotations to the default remaining layer; citation export fallback strings localize through `ILocalizationService` | Resolved locally |
| Rotated-page annotation oracle | `tests/GoldenCorpus/annotations/rotated-page-annotation.json` and `Annotation_RotatedPage_Reload_KeepsScreenRectWithinOnePixel` | Automated coverage present |
| Citation capture/export | `CitationService_CaptureAndExport_UsesCatalogueMetadata`, `CitationService_ExportWithoutBookHash_UsesStableBookIdFallback`, `CitationService_Export_UsesLocalizedFallbackStrings`, reader UI tests | Automated coverage present |
| Real selected citation text | `ReaderViewModel_SelectionCitation_UsesTextLayerWordsWhenAvailable`; `TextLayerService_ExtractAsync_UsesOpenSessionRenderer` | Automated coverage present |
| Reading memory | Reader UI tests plus catalogue read-model projection tests | Automated coverage present |
| LAN-ready annotation read model | `AnnotationReadModel_SharedProjection_EmitsBookmarkAndLayerEvents`; shared `AnnotationReadModel` publisher | Automated coverage present |
| R1 disk-full simulation | `FaultInjection_DiskFull_TransactionRolledBack` | Automated rollback coverage present |
| Bookmark abnormal-termination model | `FaultInjection_BookmarkAfterSave_Reopen_Present`; `FaultInjection_BookmarkAbortBeforeSave_LeavesNoRowAndRecovers` | Automated coverage present |
| Phase 09 strings | `src/OgmaLibrary.App/Assets/Strings/annotations.en.resx`, `annotations.fr.resx`; `Phase09AnnotationResources_ContainRequiredKeys` | Automated coverage present |
| Selection action menu | `ReaderView_PageSurfaceDrag_OpensSelectionActionMenuWithFocusableActions`; `SelectionActionMenu` opens from a headless pointer drag and exposes focusable named actions | Automated coverage present |
| Keyboard-focusable Phase 09 controls | `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames`; toolbar, note editor, bookmark, layer, and reading-memory controls expose purpose-specific names and accept focus | Automated focus coverage present |
| Target-specific layer actions | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; layer visibility, merge, and delete controls now include the target layer name in automation names, e.g. `Layer visible: Key arguments`, `Merge layer Key arguments`, and `Delete layer Key arguments` | Automated accessibility coverage present |
| Last-layer UI integrity | `ReaderView_LayerActions_DisabledWhenOnlyOneLayerRemains`; the layer panel disables merge/delete controls when only one layer remains, matching the Phase 09 at-least-one-layer invariant before the service boundary is reached | Automated UI integrity coverage present |
| Bookmark keyboard navigation | `ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates`; `ReaderView.axaml` wires `Bookmarks_KeyDown` | Automated coverage present |
| Bookmark context menu | `ReaderView_BookmarkContextFlyout_RenameFocusesEditorAndDeleteRemovesBookmark`; bookmark rows expose right-click rename/delete actions | Automated coverage present |
| Note editor keyboard dismissal | `ReaderView_NoteEditorEscape_ClosesEditorWithoutNavigating`; `ReaderView.axaml.cs` handles Escape on `NoteEditorTextBox` | Automated coverage present |
| Icon registration and labels | `IconCatalog_Phase09ManifestKeys_AllResolve`; `IconCatalog_Phase09ManifestKeys_HaveAccessibleLabels`; `docs/plans/grand-plan/phase-09/icons.md` delivered asset mapping | Key-named premium SVG registration verified |
| Overlay contrast | `ReaderViewModel_AnnotationOverlayColors_MeetContrastGate` | Automated contrast gate present |
| Pseudolocale render | `ReaderView_PseudolocalePhase09Panels_RendersWithoutOversizedTextBounds`; screenshot `artifacts/screenshots/reader-qps-ploc.png` | Automated render evidence present |
| No PDF write-back | `Architecture_Phase09Annotations_DoNotDependOnPdfWriteBack`; source audit of Phase 09 annotation path | Automated guard present |
| Selected-book deterministic metadata enrichment UI | `BookDetail_EnrichMetadata_RunsProviderFlowAndRefreshesDetail`; `BookDetail_MetadataDisplayRows_ShowBibliographicAndProviderProvenance`; `BookDetail_EnrichMetadata_ServiceReturnsFailure_ShowsErrorAndReenables`; `BookDetail_EnrichMetadata_RefreshThrows_ShowsErrorAndReenables`; `Architecture_MetadataEnrichment_DoesNotDependOnAiOrOpenAi`; the book-detail Enrich button now invokes the no-AI provider flow, refreshes the projection, displays provider-sourced metadata/provenance, and reports failures instead of dropping fire-and-forget task errors | Resolved locally |
| Direct PDF open startup and metadata regression | `ApplicationStartupTests.InitializeAsync_AppliesCatalogueMigrations_BeforeShellQueries`; `Migration_RepairsMissingModelTable_WhenHistorySaysCurrent`; `DirectPdfOpen_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf`; `DirectPdfOpenServiceTests.DirectPdfOpen_ExternalPdf_AddsBookWithoutChangingExistingLibraryRoot`; `DirectPdfOpen_ExistingMatch_QueuesMetadataAndThumbnailJobs`; `DirectPdfOpen_ExistingMatchWithPriorJobs_QueuesJobsForSelectedFileVersion`; `DirectPdfOpen_MetadataExtraction_VisibleInCatalogueProjection`; `MainShell_OpenPdfPathAsync_OpensReaderAndReportsMetadataQueued`; `MainShell_BackgroundCompletion_RefreshesCatalogueAndLoadedDetail`; `PdfWriteBack_RegisteredExternalDirectPdf_AllowsWriteBack`; app startup, direct PDF open, and folder scans now apply the catalogue migrator before querying `BookFiles`, repair missing catalogue model tables when migration history incorrectly reports the database current, register any selected PDF that is not already in the library, surface folder-scan failures in the status bar, keep external direct opens from changing the current library root, queue metadata/thumbnail work for selected file content hashes, refresh catalogue projections after metadata extraction, and allow registered writable external PDFs to receive PDF DocInfo write-back | Resolved locally |
| Desktop ingestion worker lifecycle | `ApplicationStartupTests.InitializeAsync_StartsHostedServices_AndStopAsyncStopsThem`; startup now recovers interrupted jobs and starts registered hosted services so queued metadata/thumbnail/enrichment jobs are processed in the Avalonia app | Resolved locally |
| Foreground/background catalogue context isolation | `ApplicationStartupTests.CatalogueContext_ResolvesDistinctInstances_ForForegroundAndWorkerSafety`; `BookIngestionWorker` now uses `IDbContextFactory<CatalogueDbContext>` per polling cycle, and direct-open identity/registration/metadata extraction use factory-created contexts per operation | Resolved locally |
| Phase 09 reader repository context isolation | Annotation, bookmark, layer, reading-memory, and reading-progress repositories now use `IDbContextFactory<CatalogueDbContext>` per method while preserving legacy test constructors; verified by 46 reader persistence/session regression tests and the full 67-test UI suite | Resolved locally |
| Reader-facing read-path context isolation | `CatalogueReadModel` and `BookFileLocator` now use factory-created contexts per operation so citation capture, book-detail memory summaries, catalogue grids, and reader session file location do not hold long-lived EF contexts | Resolved locally |
| App-lived catalogue and metadata context isolation | Background job recovery, scan health, unavailable-file flagging, ingestion orchestration, metadata provider aggregation, confidence merge, metadata apply, metadata quality, batch enrichment, PDF metadata write-back, catalogue write service, audit, book, shelf, and legacy annotation repositories now lease factory-created contexts per operation at runtime; `CatalogueMigrator` remains the only startup-owned direct context | Resolved locally |
| Windows/macOS CI matrix | `.github/workflows/ci.yml`; Phase 09 WP9-T6 cross-platform format/build/test gate is wired for `main`/`develop` pushes and PRs on `windows-latest` and `macos-latest`; 2026-06-01 remote status check attempted via GitHub connector and local CLI | Workflow configured; remote run result unavailable from this environment |

## Manual and owner-gated evidence

| Item | Current state | Closeout evidence needed |
| --- | --- | --- |
| Screen-reader walkthrough | Automation names are covered by UI tests and summarized in `docs/qa/PHASE-09-A11Y-SIGNOFF.md`; runbook exists in `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`, but Narrator/VoiceOver has not been manually recorded in this repository. | Manual pass on Windows Narrator and macOS VoiceOver with bookmark count, annotation overlay, note anchor, layer controls, citation card. |
| Color accessibility manual review | Automated contrast math is present; human review against actual platform rendering is not archived. | Manual review of highlight overlays and color-only meaning. |
| Pseudolocale manual review | Headless pseudolocale screenshot is archived in artifacts; human review is not archived. | Manual review of pseudolocale screenshot for polish. |
