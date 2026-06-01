# Phase 09 Continuation Audit

Date: 2026-06-01

Scope: Phase 09 closeout evidence through the active-layer, direct-PDF
hardening, delivered reader-icon rendering, keyboard shortcut verification,
performance gate review, and annotation delete workflow passes on 2026-06-01.

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
| Latest reviewed implementation scope | Bookmark sorting, reading-memory disposition label, active writable layer marker, strict direct-PDF selected-path registration, production-DI missing-`BookFiles` repair, delivered reader-icon rendering, reader keyboard shortcuts, performance gates, annotation context-flyout delete confirmation, and manual signoff documentation source-of-truth cleanup |
| Worktree | Expected to remain clean after this pass except unrelated/generated `docs/developer-guide/images/scan-en.png` and `docs/developer-guide/images/reader-en.png` |
| CI workflow definition | `.github/workflows/ci.yml` includes Windows and macOS matrix jobs for restore, format, Release build, and Release tests |
| Phase 09 evidence | `docs/plans/grand-plan/phase-09/evidence.md` dated 2026-06-01 with current focused and full-suite local test counts |
| Bookmark sorting | `23abfaa feat: add bookmark panel sorting`; `ReaderViewModel_BookmarkSortOptions_ReorderByPageOrCreationDate` covers page/date sorting |
| Reading-memory disposition label | `29fa18d fix: expose reading memory disposition range`; focused reader UI tests cover the announced `Disposition (1-5)` label and existing validation |
| Active writable layer marker | `ReaderViewModel_ActiveWritableLayerMarker_FollowsFirstVisibleLayer`; `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames` covers the visible and announced active-layer marker |
| Direct PDF open hardening | `DirectPdfOpen_FuzzyMatch_RegistersSelectedPdfAsNewBook`; `DirectPdfOpen_SameHashAtUnregisteredPath_RegistersSelectedPdfAsNewBook`; `DirectPdfOpen_ProductionDi_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf`; migration/direct-open/startup focused slice covers missing-table repair with production service registration and explicit selected-file registration behavior |
| Delivered reader-icon rendering | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; `ReaderViewModel_NoteOverlay_ExposesAnchorMarker`; `IconCatalogPhase09Tests` cover rendered premium SVG paths for toolbar actions, selection actions, citation card actions, sidebar panel tabs, bookmark/layer panels, note anchors, highlight color, bookmark rename, and reading-memory disposition |
| Reader keyboard shortcuts | `ReaderView_CtrlB_TogglesCurrentPageBookmark`, `ReaderView_CtrlShiftB_OpensBookmarkPanel`, and `ReaderView_CtrlShiftC_CapturesSelectedCitation` verify the planned Phase 09 bookmark and citation shortcuts against the Avalonia reader view |
| Performance gates | `AnnotationOverlay_RenderOverhead_100Annotations_Under10msP95`, `AnnotationWrite_P95_Under200ms`, `CachedPageTurn_P95_With100Annotations_Under100ms`, and `ReaderViewModel_PageTurnP95_With100AnnotationsPerPage_Under100ms` verify the documented overlay, write, and page-turn budgets |
| Annotation delete workflow | `ReaderView_AnnotationContextFlyout_DeleteOpensConfirmation` verifies the annotation row context-flyout delete path opens the same confirmation flow as the explicit delete button before any repository delete occurs |

## Remote CI Status Check

Remote CI result evidence is not available from this local environment:

| Check | Result |
| --- | --- |
| GitHub Actions REST API for the then-current pushed `main` evidence commit `b58b12a` | `GET /repos/peterbamuhigire/Ogma-Library/actions/runs?branch=main&per_page=5` returned `404 Not Found` |
| GitHub CLI | `gh` is not installed in this environment |
| Public Actions URL / unauthenticated status | No usable Actions run result was available from this environment |

The workflow configuration itself is present and documented, but this audit does
not claim a remote CI pass.

## Locally Actionable Findings

The continuation pass found seven locally actionable mismatches and fixed
them:

| Finding | Resolution |
| --- | --- |
| Bookmark panel did not expose the planned page/date sort selector. | Added a localized bookmark sort selector with page-number default and creation-date alternate ordering. |
| Reading-memory disposition field did not visibly include the planned 1-5 range in its label. | Updated English/French runtime strings and accessibility tests to announce `Disposition (1-5)`. |
| Layer sidebar did not visibly identify which layer receives new annotations. | Added a localized active writable layer marker and automation label that follows the first visible layer. |
| Direct PDF open could still surface `no such table: BookFiles` in damaged catalogues and selected-file identity matches could update an existing book that did not already own the selected path. | Forced direct-open DI to receive the catalogue migrator, retries repair on SQLite missing-table errors, and registers selected PDFs as new books unless the selected path already has a present `BookFiles` row. |
| Production direct-PDF repair still depended on a long-lived migrator context, leaving a runtime-specific gap not covered by the single-context repair test. | Changed `CatalogueMigrator` to lease fresh factory-created contexts for each migration/repair pass and added production-DI coverage for missing-`BookFiles` direct-open repair. |
| Delivered Phase 09 premium SVG assets were registered but some reader surfaces still used text-only or placeholder controls. | Rendered delivered SVGs on note anchors, sidebar panel tabs, highlight color picker, bookmark rename affordance, and reading-memory disposition while preserving automation labels. |
| `testing.md` duplicated stale unchecked manual checklist rows instead of pointing reviewers to the expanded manual signoff packet. | Replaced the duplicate checklist with a source-of-truth reference to `PHASE-09-MANUAL-SIGNOFF-PACKET.md` and `PHASE-09-A11Y-SIGNOFF.md`, including the expanded direct-PDF, enrichment, owner, and visual-review checks. |

The final code-level pass also rechecked the planned Ctrl+B, Ctrl+Shift+B, and
Ctrl+Shift+C shortcuts against `ReaderView.axaml.cs` and the focused UI tests.
The performance pass re-ran the focused backend and UI gates for overlay
transform overhead, annotation write latency, cached page-turn latency, and
reader view-model page-turn latency with 100 annotations.
The annotation workflow pass added focused UI coverage for the row context-flyout
delete action so the planned right-click delete path is no longer covered only
by view-model confirmation tests.
No further locally actionable Phase 09 implementation gaps were found in this
pass.
Do not mark Phase 09 fully closed until the manual and owner-gated rows above
have dated evidence or explicit waivers.
