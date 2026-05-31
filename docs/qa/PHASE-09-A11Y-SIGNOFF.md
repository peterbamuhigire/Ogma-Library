# Phase 09 Accessibility Signoff

Status: automated evidence present; manual assistive-technology pass pending.

Date: 2026-05-31

Manual runbook: `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`

## Automated Evidence

| Requirement | Evidence |
| --- | --- |
| Annotation overlays expose a non-color label | `ReaderViewModel_AnnotationOverlayAccessibleLabel_IncludesPageAndLayer` |
| Selection action menu opens from mouse drag with focusable named actions | `ReaderView_PageSurfaceDrag_OpensSelectionActionMenuWithFocusableActions` |
| Core Phase 09 controls expose names and accept keyboard focus | `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames` |
| Bookmark panel announces count | `ReaderViewModel_BookmarkPanelAccessibleLabel_IncludesCount` |
| Bookmark panel supports arrow selection and Enter navigation | `ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates` |
| Bookmark context flyout exposes rename/delete actions | `ReaderView_BookmarkContextFlyout_RenameFocusesEditorAndDeleteRemovesBookmark` |
| Note editor supports Escape dismissal without navigation | `ReaderView_NoteEditorEscape_ClosesEditorWithoutNavigating` |
| Bookmark, layer, citation, and memory actions expose automation names | `ReaderView_Phase09ControlsExposeActionSpecificAutomationNames` |
| Note overlays expose an anchor marker state | `ReaderViewModel_NoteOverlay_ExposesAnchorMarker` |
| Phase 09 icon labels exist in English and French | `IconCatalog_Phase09ManifestKeys_HaveAccessibleLabels` |
| Phase 09 resource artifacts contain required keys | `Phase09AnnotationResources_ContainRequiredKeys` |
| Highlight overlay colors meet contrast gate | `ReaderViewModel_AnnotationOverlayColors_MeetContrastGate` |
| Pseudolocale reader panels render without oversized text bounds | `ReaderView_PseudolocalePhase09Panels_RendersWithoutOversizedTextBounds`; screenshot `artifacts/screenshots/reader-qps-ploc.png` |

Latest focused command:

```powershell
dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --no-build --filter "FullyQualifiedName~IconCatalogPhase09Tests|FullyQualifiedName~ReaderViewRenderTests"
```

Result: passed, 56 tests.

Latest full UI release command:

```powershell
dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build
```

Result: passed, 67 tests.

## Manual Checklist

These items require a human pass on the target operating systems before Phase 09
can be called fully closed:

| Check | Windows Narrator | macOS VoiceOver | Notes |
| --- | --- | --- | --- |
| Annotation overlay announces type, layer, and page | Pending | Pending | Expected shape: "Highlight - Key arguments - Page N of M". |
| Note anchor announces a note-specific label | Pending | Pending | Verify the visual marker is not the only signal. |
| Bookmark panel announces list count and item labels | Pending | Pending | Keyboard focus must reach each bookmark action. |
| Layer visibility and delete controls announce action-specific labels | Pending | Pending | Includes visible/hidden state naming. |
| Citation card copy/export buttons announce distinct actions | Pending | Pending | Export must not sound identical to copy. |
| Reading-memory fields announce field purpose | Pending | Pending | Opened because, key insight, questions, disposition. |
| Keyboard-only walkthrough reaches every Phase 09 control | Pending | Pending | Include selection action menu and sidebars. |
| Highlight colors are not color-only meaning | Pending | Pending | Automated contrast math is present; human check should verify actual rendered appearance. |
| Pseudolocale render visual polish | Pending | Pending | Automated headless bounds check and screenshot exist; human review still needed. |

## Current Closeout Position

The implementation has automated accessibility guards for labels and keyboard-visible
control surfaces, but this document is not a final signoff until the manual rows
above are completed and dated by the reviewer.
