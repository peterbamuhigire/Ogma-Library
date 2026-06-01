# Phase 09 Accessibility Signoff

Status: automated evidence present; manual assistive-technology pass pending.

Date prepared: 2026-05-31
Last updated: 2026-06-01

Manual runbook: `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`

## Evidence Recording Rules

Use exact dates in `YYYY-MM-DD` format. Every completed manual row must include
the reviewer initials, target OS where applicable, result, and a durable
evidence reference such as a screenshot path, audio note path, exported artifact
path, or short dated reviewer note. If a check is intentionally waived, record
`Waived` plus the owner initials, waiver date, and reason. Do not replace
`Pending` with `Passed` unless the evidence exists in the repository or in the
owner-controlled release folder named in the row.

## Automated Evidence

| Requirement | Evidence |
| --- | --- |
| Annotation overlays expose a non-color label | `ReaderViewModel_AnnotationOverlayAccessibleLabel_IncludesPageAndLayer` |
| Selection action menu opens from pointer drag with focusable named actions; touch and pen drags are eligible selection pointers | `ReaderView_PageSurfaceDrag_OpensSelectionActionMenuWithFocusableActions`; `ReaderView_CanTrackSelectionPointer_AllowsTouchAndPenDrag` |
| Core Phase 09 controls expose names and accept keyboard focus | `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames` |
| Bookmark panel announces count | `ReaderViewModel_BookmarkPanelAccessibleLabel_IncludesCount` |
| Bookmark panel sort selector is named and keyboard focusable | `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames`; `ReaderViewModel_BookmarkSortOptions_ReorderByPageOrCreationDate` |
| Bookmark panel supports arrow selection and Enter navigation | `ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates` |
| Bookmark context flyout exposes rename/delete actions | `ReaderView_BookmarkContextFlyout_RenameFocusesEditorAndDeleteRemovesBookmark` |
| Note editor supports Escape dismissal without navigation | `ReaderView_NoteEditorEscape_ClosesEditorWithoutNavigating` |
| Bookmark, layer, citation, and memory actions expose action-specific automation names | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; layer visibility, merge, and delete names include the layer name; citation copy/export buttons are asserted as distinct `Copy citation` and `Export citation` actions |
| Active writable annotation layer is visible and announced | `ReaderViewModel_ActiveWritableLayerMarker_FollowsFirstVisibleLayer`; `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames` |
| Last-layer destructive controls are unavailable before data loss can occur | `ReaderView_LayerActions_DisabledWhenOnlyOneLayerRemains` |
| Note overlays expose an anchor marker state | `ReaderViewModel_NoteOverlay_ExposesAnchorMarker` |
| Phase 09 icon labels exist in English and French | `IconCatalog_Phase09ManifestKeys_HaveAccessibleLabels` |
| Delivered Phase 09 SVG icons render on reader surfaces without replacing automation names | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames`; `ReaderViewModel_NoteOverlay_ExposesAnchorMarker`; `IconCatalogPhase09Tests` |
| Phase 09 resource artifacts contain required keys | `Phase09AnnotationResources_ContainRequiredKeys` |
| Phase 09 resource/runtime labels reject mojibake | `Phase09AnnotationResources_DoNotContainMojibake`; `InMemoryLocalization_Phase09FrenchStrings_DoNotContainMojibake` |
| Phase 09 runtime labels match resources | `InMemoryLocalization_Phase09Strings_MatchResourceValues` |
| Highlight overlay colors meet contrast gate | `ReaderViewModel_AnnotationOverlayColors_MeetContrastGate` |
| Pseudolocale reader panels render without oversized text bounds | `ReaderView_PseudolocalePhase09Panels_RendersWithoutOversizedTextBounds`; screenshot `artifacts/screenshots/reader-qps-ploc.png` |

Latest focused command:

```powershell
dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReaderViewRenderTests|FullyQualifiedName~IconCatalogPhase09Tests"
```

Result: passed, 73 Phase 09 UI/resource tests.

Latest full UI release command:

```powershell
dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build
```

Result: passed, 93 tests.

## Assistive Technology Walkthrough

These items require a human pass on the target operating systems before Phase 09
can be called fully closed:

| Check | Windows Narrator | macOS VoiceOver | Reviewer/date | Evidence reference | Notes |
| --- | --- | --- | --- | --- | --- |
| Annotation overlay announces type, layer, and page | Pending | Pending | Pending | Audio note or reviewer note. | Expected shape: "Highlight - Key arguments - Page N of M". |
| Note anchor announces a note-specific label | Pending | Pending | Pending | Audio note or reviewer note. | Verify the visual marker is not the only signal. |
| Bookmark panel announces list count and item labels | Pending | Pending | Pending | Audio note or reviewer note. | Keyboard focus must reach each bookmark action. |
| Layer visibility, active marker, and delete controls announce action-specific labels | Pending | Pending | Pending | Audio note or reviewer note. | Includes visible/hidden state naming and active writable layer marker. |
| Citation card copy/export buttons announce distinct actions | Pending | Pending | Pending | Audio note or reviewer note. | Export must not sound identical to copy. |
| Reading-memory fields announce field purpose | Pending | Pending | Pending | Reviewer note. | Opened because, key insight, questions, and `Disposition (1-5)` in the reader and book-detail surfaces. |
| Keyboard-only walkthrough reaches every Phase 09 control | Pending | Pending | Pending | Reviewer note. | Include selection action menu and sidebars. |

## Visual Accessibility Review

| Check | Expected result | Result | Reviewer/date | Evidence reference | Notes |
| --- | --- | --- | --- | --- | --- |
| Highlight colors are not the only signal | Annotation labels and note anchors remain visible/understandable without relying on color alone. | Pending | Pending | Reviewer note. | Automated contrast math is present; human check should verify actual rendered appearance. |
| Highlight contrast matches automated gate in real rendering | Highlights remain legible over page content in the target platform renderer. | Pending | Pending | Screenshot set. | Compare rendered output against the automated contrast gate. |
| Pseudolocale screenshot polish | `artifacts/screenshots/reader-qps-ploc.png` has no unacceptable clipping or overlapping text. | Pending | Pending | Reviewer note. | Automated headless bounds check and screenshot exist; human review still needed. |

## Current Closeout Position

The implementation has automated accessibility guards for labels and keyboard-visible
control surfaces, but this document is not a final signoff until the manual rows
above are completed and dated by the reviewer.
