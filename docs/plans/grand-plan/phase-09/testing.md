# Phase 09 — Test Plan

All nine test layers applied to Annotations, Bookmarks & Reading Memory.

---

## 1. Test layers in scope

| Layer | In scope | Notes |
| --- | --- | --- |
| 1 — Domain unit | Yes — `AnnotationRenderHelper`, `AnnotationRegion` math, `NavigationHistory` extension | Pure logic |
| 2 — Infrastructure unit | Yes — repositories, durable write, WAL | Requires in-memory SQLite |
| 3 — PDF fixture | Yes — rotated-page golden fixture (annotation reload) | Oracle: stored bounding boxes |
| 4 — Search unit | No — annotation text indexed in Phase 10 | N/A |
| 5 — AI unit | No | N/A |
| 6 — UI / component | Yes — overlay panel, layer sidebar, bookmark panel, citation card, memory journal | Avalonia headless |
| 7 — 3D | No | N/A |
| 8 — Performance | Yes — overlay overhead <= 10 ms; page-turn regression with 100 annotations | xUnit stopwatch gates |
| 9 — Packaging | No | N/A |
| Manual | Yes — screen-reader pass; highlight color accessibility | Documented below |

---

## 2. Golden-corpus fixtures

| Fixture | Test | Oracle |
| --- | --- | --- |
| `rotated-pages` | Highlight created on 90°-rotated page; app restarted; reload position | Bounding boxes within 1 px of creation-time values |
| `simple-text` | 100 annotations added; page-turn benchmark regression | P95 page-turn still ≤ 100 ms |
| Any fixture | Abnormal-termination fault injection | Annotation fully present or fully absent; never partially corrupt |

---

## 3. Unit tests

### 3.1 `AnnotationRenderHelper`

| Test | Oracle |
| --- | --- |
| `ToScreenRect_NoRotation_IsIdentity` | `normalizedLeft=0.1, normalizedTop=0.2, w=0.3, h=0.1` at zoom 1.0, page 1000×1414 → `Rect(100, 283, 300, 141)` |
| `ToScreenRect_90DegRotation_CorrectlyTransposed` | After 90° CW rotation, x and y swap and y origin inverts |
| `ToScreenRect_180DegRotation_BothAxisInvert` | At 180°, left+width → right-anchored; top+height → bottom-anchored |
| `ToScreenRect_AtZoom2_DoublesDimensions` | All coordinate components × 2 |

### 3.2 Repositories — durable write

| Test | Oracle |
| --- | --- |
| `AnnotationRepository_Create_PersistsBeforeReturn` | Row present in DB immediately after `CreateAsync` returns |
| `AnnotationRepository_TransactionAbort_LeavesNoRow` | Simulate exception in `SaveChangesAsync`; row absent |
| `BookmarkRepository_Create_PersistsBeforeReturn` | Row present |
| `BookmarkRepository_Delete_RemovesRow` | Row absent after `DeleteAsync` |
| `AnnotationLayerService_Delete_MovesAnnotationsToDefaultLayer` | Annotations in deleted layer now have `LayerId = defaultLayerId` |
| `AnnotationLayerService_Delete_IgnoresLayerFromDifferentBook` | Cross-book layer delete request leaves the other book's layer intact and emits no wrong-book event |
| `ReadingMemoryRepository_Upsert_UpdatesExistingRow` | Second save updates, does not insert duplicate |

---

## 4. Fault-injection tests (R1 tier — unwaivable)

All fault-injection tests use an in-process mock that throws at the specified
point; no real kill signal is required. The oracle is always "catalogue is
consistent": either the full record is present or it is absent; never a partial row,
never a corrupt JSON region.

| Test | Injected fault | Oracle |
| --- | --- | --- |
| `FaultInjection_RepositoryFailure_DoesNotEmitAnnotationEvents` | Repository throws before persisted annotation is returned | Exception propagates; no read-model event is emitted |
| `AnnotationRepository_InvalidBook_RollsBackAnnotationAndBody` | Invalid book FK makes the annotation transaction fail | `AnnotationsV2` row is absent; legacy `AnnotationBodies` row for the same ID is absent; the context recovers |
| `FaultInjection_DiskFull_TransactionRolledBack` | Save interceptor throws an `IOException` while flushing the annotation transaction | No annotation row is committed; the same database accepts a later valid annotation |
| `FaultInjection_PartialRegionJson_LoadsEmptyRegionsAndCanBeRepaired` | Corrupt persisted region JSON is encountered on reload | Regions load empty instead of crashing; subsequent update repairs JSON |
| `FaultInjection_ConcurrentWrite_CompletesWithoutPartialRows` | Multiple concurrent annotation writes target the same book/page | All completed writes are full rows with bodies; no partial rows |
| `FaultInjection_BookmarkAbortBeforeSave_LeavesNoRowAndRecovers` | Invalid bookmark write fails before save; next valid write follows | Failed bookmark absent; later bookmark persists |
| `FaultInjection_BookmarkAfterSave_Reopen_Present` | Bookmark write completes and the database context is disposed before reopening | Bookmark is present after reopening the catalogue |
| `FaultInjection_LastLayerDeleteFailure_DoesNotEmitProjectionEvent` | Layer delete fails at repository boundary | No layer-change event is emitted |

---

## 5. Integration tests

| Test | ID | Oracle |
| --- | --- | --- |
| `AnnotationRepository_CommittedAnnotation_SurvivesFreshContextReopen` | FR-READ-008 | Persisted highlight reloads from a fresh catalogue context |
| `Annotation_RotatedPage_Reload_KeepsScreenRectWithinOnePixel` | FR-READ-008, NFR-OGMA-008 | `rotated-pages` fixture: bounding box remains within 1 px after reload |
| `BookmarkService_CreateRenameDelete_RoundTripsAndEmitsBookScopedDelete` | FR-READ-007 | Bookmark create, rename, delete, and book-scoped event behavior round-trip |
| `FaultInjection_BookmarkAfterSave_Reopen_Present` | FR-READ-007, NFR-OGMA-008 | Bookmark written before abnormal termination model is present after catalogue reopen |
| `AnnotationLayerService_RenameVisibilityMergeAndLastLayerConstraint_Work` | World-class | Full lifecycle and last-layer constraint behavior match expected state |
| `AnnotationLayerService_Delete_MovesAnnotationsToDefaultLayer` | World-class | Deleting a non-default layer moves annotations to the default remaining layer |
| `CitationService_CaptureAndExport_UsesCatalogueMetadata` | FR-READ-011 | Card title/author/page/selection match catalogue metadata and selected text |
| `CitationService_Export_UsesLocalizedFallbackStrings` | FR-READ-011, i18n | Citation sidecar export uses localized unknown title/author/page fallbacks |
| `ReaderViewModel_AutoSaveReadingMemory_PersistsEditedFields` | World-class | Field edited; debounced auto-save updates the reading-memory row |
| `Phase09_EndToEndRestartSmoke_PersistsReaderArtifacts` | Global DoD | Bookmark, layer rename, highlight, note, memory, and citation export survive a real SQLite reopen |

---

## 6. Performance gates

Current implementation uses deterministic xUnit stopwatch gates rather than
BenchmarkDotNet, so the checks run in the normal local/CI test path.

| Benchmark | Gate | Method |
| --- | --- | --- |
| `AnnotationOverlay_RenderOverhead` | ≤ 10 ms additional per page-turn | Measure render time with 0 vs. 100 annotations on `simple-text` page |
| `PageTurn_P95_With100Annotations` | ≤ 100 ms P95 (NFR-OGMA-005) | Extends Phase 08 benchmark with 100 highlights on each page |
| `AnnotationWrite_P95` | ≤ 200 ms P95 | 50 sequential `CreateHighlightAsync` calls; measure wall time |

---

## 7. UI / accessibility tests

| Test | Tooling | Oracle |
| --- | --- | --- |
| `ReaderView_PageSurfaceDrag_OpensSelectionActionMenuWithFocusableActions` | Avalonia headless pointer + keyboard focus harness | Mouse drag selects; action controls expose "Highlight", "Add note", "Capture citation" and accept keyboard focus |
| `ReaderView_NoteEditorEscape_ClosesEditorWithoutNavigating` | Avalonia headless | Escape closes note editor and does not change the active page |
| `ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames` | Avalonia headless focus harness | Phase 09 toolbar, note editor, bookmark, layer, and reading-memory controls expose purpose-specific names and accept focus |
| `ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates` | Avalonia headless | Bookmark list takes focus; ArrowDown selects a bookmark without navigating; Enter navigates |
| `ReaderView_BookmarkContextFlyout_RenameFocusesEditorAndDeleteRemovesBookmark` | Avalonia headless | Bookmark row context flyout exposes rename/delete; rename focuses inline editor; delete removes bookmark |
| `ReaderViewModel_HidingLayer_FiltersAnnotationsAndOverlays` | Avalonia headless | Toggle off -> overlay redraws without that layer's highlights |
| Screen-reader pass (manual) | VoiceOver / Narrator | "Highlight, layer Key arguments, page 3" announced on focus; bookmark list item labels announced |
| `ReaderViewModel_AnnotationOverlayColors_MeetContrastGate` | Avalonia headless + WCAG contrast math | Rendered overlay display colors composite to ≥ 3:1 contrast against the white page surface |
| `ReaderView_PseudolocalePhase09Panels_RendersWithoutOversizedTextBounds` | Avalonia headless | Pseudolocale reader panels render and capture `reader-qps-ploc.png`; text bounds stay within parent controls |
| Color accessibility (manual) | Reviewer checklist | Confirm contrast gate matches actual platform rendering; verify highlight meaning is not color-only |

---

## 8. Architecture tests

| Test | Oracle |
| --- | --- |
| `Architecture_Annotations_DoesNotDependOnSearch` | No type in `OgmaLibrary.Reader.Annotations` references `OgmaLibrary.Search.*` |
| `Architecture_Annotations_DoesNotDependOnAI` | No type references `OgmaLibrary.AI.*` |
| `Architecture_Annotations_AccessesCatalogueOnlyViaContracts` | No `DbContext` in `OgmaLibrary.Reader`; only `IAnnotationRepository` etc. |

---

## 9. Manual test checklist

- [ ] Create a highlight on a rotated page; restart the application; confirm
      highlight appears at the same visual position.
- [ ] Create 5 bookmarks across different pages; kill the app via Task Manager;
      reopen; confirm all 5 bookmarks present in the panel.
- [ ] Add a note; type text; click elsewhere (focus-out); wait 1 s; restart;
      confirm note text preserved.
- [ ] In French locale: open annotation panel; confirm all labels in French;
      confirm layer name input accepts accented characters (é, è, ê).
- [ ] Under VoiceOver (macOS): focus a highlight; confirm layer name and page
      number announced.
- [ ] Delete a layer with annotations; confirm annotations moved to default
      layer (not deleted); reload; annotations visible under default layer.
- [ ] Cite a passage with Ctrl+Shift+C; copy to clipboard; paste into a text
      editor; confirm format matches specification.
