# Phase 09 Continuation Audit

Date: 2026-06-01

Scope: Phase 09 closeout evidence through the active-layer, direct-PDF
hardening, delivered reader-icon rendering, keyboard shortcut verification,
performance gate review, annotation delete workflow, touch-capable selection,
and layer visibility UI passes, reading-memory blur auto-save, book-detail
reading-memory editing, note editor blur-save, and Phase 09 French localization cleanup passes,
text-selection service traceability, and Choose Library Folder
missing-`BookFiles` scan repair and same-hash unregistered-path registration
coverage on 2026-06-01.

## Current Position

Phase 09 remains locally implementation-complete for code, automated tests, and
repository documentation. The remaining closure gates are still manual or
owner-gated:

| Gate | Status |
| --- | --- |
| Narrator and VoiceOver walkthrough | Pending manual evidence |
| Color accessibility visual review | Pending manual evidence |
| Pseudolocale visual review | Pending manual evidence |
| Owner confirmations | Pending palette, citation export, and final reading-memory wording decisions |

## Local Evidence Reviewed

| Area | Evidence |
| --- | --- |
| Latest reviewed implementation scope | Bookmark sorting, reading-memory disposition label, active writable layer marker, strict direct-PDF selected-path registration, production-DI missing-`BookFiles` repair for direct open and Choose Library Folder scans, same-hash unregistered-path folder-scan registration, delivered reader-icon rendering, reader keyboard shortcuts, touch-capable selection, performance gates, annotation context-flyout delete confirmation, layer visibility checkbox filtering, reading-memory blur auto-save, book-detail reading-memory editing, note editor blur-save, French Phase 09 mojibake cleanup, explicit text-selection mapping service, and manual signoff documentation source-of-truth cleanup |
| Worktree | Expected to remain clean after this pass except unrelated/generated `docs/developer-guide/images/scan-en.png` and `docs/developer-guide/images/reader-en.png` |
| CI workflow definition | `.github/workflows/ci.yml` includes Windows and macOS matrix jobs for restore, format, Release build, and Release tests |
| Phase 09 evidence | `docs/plans/grand-plan/phase-09/evidence.md` dated 2026-06-01 with current focused and full-suite local test counts |
| Bookmark sorting | `23abfaa feat: add bookmark panel sorting`; `ReaderViewModel_BookmarkSortOptions_ReorderByPageOrCreationDate` covers page/date sorting |
| Reading-memory disposition label | `29fa18d fix: expose reading memory disposition range`; focused reader UI tests cover the announced `Disposition (1-5)` label and existing validation |
| Active writable layer marker | `ReaderViewModel_ActiveWritableLayerMarker_FollowsFirstVisibleLayer`; `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames` covers the visible and announced active-layer marker |
| Direct PDF open and folder scan hardening | `DirectPdfOpen_FuzzyMatch_RegistersSelectedPdfAsNewBook`; `DirectPdfOpen_SameHashAtUnregisteredPath_RegistersSelectedPdfAsNewBook`; `DirectPdfOpen_ProductionDi_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf`; `IngestionPipeline_ProductionDi_RepairsMissingBookFilesTableBeforeScanning`; `IngestionPipeline_SameHashAtUnregisteredPresentPath_RegistersNewBook`; migration/direct-open/startup/ingestion focused slices cover missing-table repair with production service registration, explicit selected-file registration behavior, Choose Library Folder scan registration, and coexisting same-hash path registration without breaking move/rename rematches |
| Delivered reader-icon rendering | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; `ReaderViewModel_NoteOverlay_ExposesAnchorMarker`; `IconCatalogPhase09Tests` cover rendered premium SVG paths for toolbar actions, selection actions, citation card actions, sidebar panel tabs, bookmark/layer panels, note anchors, highlight color, bookmark rename, and reading-memory disposition |
| Reader keyboard shortcuts | `ReaderView_CtrlB_TogglesCurrentPageBookmark`, `ReaderView_CtrlShiftB_OpensBookmarkPanel`, and `ReaderView_CtrlShiftC_CapturesSelectedCitation` verify the planned Phase 09 bookmark and citation shortcuts against the Avalonia reader view |
| Touch-capable selection | `ReaderView_CanTrackSelectionPointer_AllowsTouchAndPenDrag` verifies touch and pen drags can drive text selection without the mouse-left-button flag, while mouse selection still requires the primary button |
| Performance gates | `AnnotationOverlay_RenderOverhead_100Annotations_Under10msP95`, `AnnotationWrite_P95_Under200ms`, `CachedPageTurn_P95_With100Annotations_Under100ms`, and `ReaderViewModel_PageTurnP95_With100AnnotationsPerPage_Under100ms` verify the documented overlay, write, and page-turn budgets |
| Annotation delete workflow | `ReaderView_AnnotationContextFlyout_DeleteOpensConfirmation` verifies the annotation row context-flyout delete path opens the same confirmation flow as the explicit delete button before any repository delete occurs |
| Layer visibility UI | `ReaderView_LayerVisibilityCheckbox_FiltersAnnotationsAndOverlays` verifies the rendered layer checkbox route persists visibility changes and refreshes annotation rows/overlays |
| Reading-memory blur auto-save | `ReaderView_ReadingMemoryFieldLostFocus_AutoSavesEditedField` verifies the rendered reader field blur route schedules and persists edited reading-memory text |
| Book-detail reading-memory editing | `BookDetailViewModel_ReadingMemoryEditor_SavesAndRefreshesSummary` verifies the catalogue book-detail path saves opened-because, key-insight, open-questions, and disposition through `IReadingMemoryService` and refreshes the summary projection |
| Note editor blur-save | `ReaderView_NoteEditorLostFocus_SavesEditedNote` verifies the rendered note editor focus-out route persists edited note text, closes the editor, and reports save completion |
| French Phase 09 localization | `Phase09AnnotationResources_DoNotContainMojibake`, `InMemoryLocalization_Phase09FrenchStrings_DoNotContainMojibake`, and `InMemoryLocalization_Phase09Strings_MatchResourceValues` verify resource/runtime labels reject common mojibake markers and match the committed Phase 09 resource values |
| Text-selection mapping service | `TextSelectionService.GetRegionsForSelection` and `TextSelectionService_GetRegionsForSelection_MapsScreenRectToUnrotatedRegion` verify the planned P09-WP2-T2 mapping from screen-space selection rectangles to normalized unrotated annotation regions |

## Remote CI Status Check

Remote CI result evidence is not available from this local environment:

| Check | Result |
| --- | --- |
| GitHub Actions REST API for pushed `main` Actions runs | `GET /repos/peterbamuhigire/Ogma-Library/actions/runs?branch=main&per_page=5` returned `404 Not Found` when checked unauthenticated from this environment |
| GitHub CLI | `gh` is not installed in this environment |
| Public Actions URL / unauthenticated status | No usable Actions run result was available from this environment |

The workflow configuration itself is present and documented, but this audit does
not claim a remote CI pass.

## Locally Actionable Findings

The continuation pass found fourteen locally actionable mismatches and fixed
them:

| Finding | Resolution |
| --- | --- |
| Bookmark panel did not expose the planned page/date sort selector. | Added a localized bookmark sort selector with page-number default and creation-date alternate ordering. |
| Reading-memory disposition field did not visibly include the planned 1-5 range in its label. | Updated English/French runtime strings and accessibility tests to announce `Disposition (1-5)`. |
| Layer sidebar did not visibly identify which layer receives new annotations. | Added a localized active writable layer marker and automation label that follows the first visible layer. |
| Direct PDF open could still surface `no such table: BookFiles` in damaged catalogues and selected-file identity matches could update an existing book that did not already own the selected path. | Forced direct-open DI to receive the catalogue migrator, retries repair on SQLite missing-table errors, and registers selected PDFs as new books unless the selected path already has a present `BookFiles` row. |
| Production direct-PDF repair still depended on a long-lived migrator context, leaving a runtime-specific gap not covered by the single-context repair test. | Changed `CatalogueMigrator` to lease fresh factory-created contexts for each migration/repair pass and added production-DI coverage for missing-`BookFiles` direct-open repair. |
| The Choose Library Folder scan path did not have explicit production-DI evidence for repairing a damaged catalogue missing `BookFiles` before discovered PDFs are registered. | Added `IngestionPipeline_ProductionDi_RepairsMissingBookFilesTableBeforeScanning`, which drops `BookFiles`, runs the production ingestion service registration, and verifies the folder-scan path repairs the table and registers the discovered PDF. |
| Folder scans could overwrite an existing book path when discovering a same-hash PDF at a new unregistered path that coexisted with the original present file. | Added coexisting-path detection in the scan matcher: true moves/renames still update the existing book when the old file is gone, but present same-hash unregistered paths are registered as new books. Covered by `IngestionPipeline_SameHashAtUnregisteredPresentPath_RegistersNewBook`. |
| Delivered Phase 09 premium SVG assets were registered but some reader surfaces still used text-only or placeholder controls. | Rendered delivered SVGs on note anchors, sidebar panel tabs, highlight color picker, bookmark rename affordance, and reading-memory disposition while preserving automation labels. |
| `testing.md` duplicated stale unchecked manual checklist rows instead of pointing reviewers to the expanded manual signoff packet. | Replaced the duplicate checklist with a source-of-truth reference to `PHASE-09-MANUAL-SIGNOFF-PACKET.md` and `PHASE-09-A11Y-SIGNOFF.md`, including the expanded direct-PDF, enrichment, owner, and visual-review checks. |
| Note editor focus-out was implemented but only indirectly covered by text-binding and Escape-dismissal tests. | Added `ReaderView_NoteEditorLostFocus_SavesEditedNote` to drive the rendered `NoteEditor_LostFocus` route and verify persistence, closure, and save status. |
| French Phase 09 resource/runtime labels included mojibake in bookmark sorting and related accented labels, and the runtime dictionary was not cross-checked against committed resources. | Rewrote the Phase 09 French `.resx` values with entity-safe accents, normalized the runtime Phase 09 French dictionary, and added resource/runtime mojibake and resource-match regression tests. |
| The P09-WP2-T2 text-selection mapping behavior lived only as a private reader view-model method, weakening traceability to the planned `TextSelectionService.GetRegionsForSelection` artifact. | Extracted the mapping into `TextSelectionService.GetRegionsForSelection`, kept the reader selection flow on that helper, and added direct rotation-aware mapping coverage. |
| The book-detail panel only exposed a compact read-only reading-memory summary, leaving the catalogue-detail editing path weaker than the Phase 09 closeout wording. | Added editable opened-because, key-insight, open-questions, and disposition fields in the book-detail Reading tab, backed by `IReadingMemoryService`, with save/status handling and summary refresh coverage. |
| P09-WP3-T2 called for mouse and touch drag selection, but reader selection gating required the mouse-left-button flag for all pointer types. | Added pointer eligibility that keeps mouse selection on primary-button drag while allowing touch and pen drags, with focused coverage for mouse, touch, and pen cases. |

The final code-level pass also rechecked the planned Ctrl+B, Ctrl+Shift+B, and
Ctrl+Shift+C shortcuts against `ReaderView.axaml.cs` and the focused UI tests.
The performance pass re-ran the focused backend and UI gates for overlay
transform overhead, annotation write latency, cached page-turn latency, and
reader view-model page-turn latency with 100 annotations.
The annotation workflow pass added focused UI coverage for the row context-flyout
delete action so the planned right-click delete path is no longer covered only
by view-model confirmation tests.
The layer UI pass rechecked a stale agent finding against current code, then
added rendered-checkbox coverage so layer filtering is proven through the
Avalonia handler path as well as the view-model method.
The reading-memory pass added rendered-view coverage for `ReadingMemoryField_LostFocus`,
so the planned focus-out auto-save route is proven beyond direct view-model calls.
The book-detail reading-memory pass added catalogue-detail editing so users can
maintain the journal from the selected-book detail panel as well as the reader
sidebar, with focused save and summary-refresh regression coverage.
The touch-selection pass addressed the P09-WP3-T2 mouse/touch wording by
removing the mouse-button assumption from touch and pen pointer paths.
The note editor pass added rendered-view coverage for `NoteEditor_LostFocus`,
so the planned note focus-out save route is proven beyond text-binding and
Escape-dismissal checks.
The localization pass cleaned up Phase 09 French accented labels in resources
and runtime dictionaries, then added automated guards against common mojibake
markers and runtime/resource drift.
The text-selection pass extracted the planned selection-region mapping helper
so screen-space drag rectangles have an explicit tested service before
annotation persistence.
No further locally actionable Phase 09 implementation gaps were found in this
pass.
Do not mark Phase 09 fully closed until the manual and owner-gated rows above
have dated evidence or explicit waivers.
