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
| 8 — Performance | Yes — overlay overhead ≤ 10 ms; page-turn regression with 100 annotations | BenchmarkDotNet |
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
| `AnnotationLayerRepository_DeleteLayer_OrphansMovedToDefault` | Annotations in deleted layer now have `LayerId = defaultLayerId` |
| `ReadingMemoryRepository_Upsert_UpdatesExistingRow` | Second save updates, does not insert duplicate |

---

## 4. Fault-injection tests (R1 tier — unwaivable)

All fault-injection tests use an in-process mock that throws at the specified
point; no real kill signal is required. The oracle is always "catalogue is
consistent": either the full record is present or it is absent; never a partial row,
never a corrupt JSON region.

| Test | Injected fault | Oracle |
| --- | --- | --- |
| `FaultInjection_AbnormalTermination_AnnotationSurvives` | Dispose `DbContext` after `SaveChangesAsync` but before observer notification; reopen | Annotation present in DB |
| `FaultInjection_AbnormalTermination_BeforeSave_Absent` | Dispose `DbContext` before `SaveChangesAsync`; reopen | Annotation absent |
| `FaultInjection_DiskFull_TransactionRolledBack` | Mock `IFileSystem` throws on WAL write; exception propagates to caller | No row inserted; catalogue consistent |
| `FaultInjection_PartialRegionJSON_IsAbsent` | Mock serializer throws mid-serialization of `Regions`; transaction rolls back | `AnnotationBodies` row absent |
| `FaultInjection_ConcurrentWrite_OneWins` | Two threads call `CreateAsync` simultaneously for the same page; both use `BeginTransactionAsync` | Both succeed (no unique constraint on position) or one retries; no deadlock |
| `FaultInjection_BookmarkAbortBeforeSave` | Kill scope before `SaveChangesAsync` for bookmark | Bookmark absent on reload |

---

## 5. Integration tests

| Test | ID | Oracle |
| --- | --- | --- |
| `Annotation_Reload_CorrectPosition` | FR-READ-008 | Highlight at page 3 coords reloads within 1 px after restart |
| `Annotation_RotatedPage_Reload` | FR-READ-008, NFR-OGMA-008 | `rotated-pages` fixture: bounding box identical after restart |
| `Bookmark_SaveAndJump_RoundTrip` | FR-READ-007 | Create bookmark at page 7; restart; panel shows "Page 7"; click → navigates to 7 |
| `Bookmark_AbnormalTermination_Survives` | FR-READ-007, NFR-OGMA-008 | Fault-injection bookmark test green |
| `Layer_Create_Rename_Delete_Merge` | World-class | Full lifecycle; row counts match expected after each step |
| `Layer_AtLeastOneConstraint` | World-class | Delete attempt on sole remaining layer returns error |
| `CitationCard_CaptureAndExport` | FR-READ-011 | Card title/author/page/selection match `simple-text` fixture metadata |
| `ReadingMemory_AutoSave_OnFocusOut` | World-class | Field edited; focus moved; wait 1.5 s; row updated in DB |

---

## 6. Performance benchmarks

| Benchmark | Gate | Method |
| --- | --- | --- |
| `AnnotationOverlay_RenderOverhead` | ≤ 10 ms additional per page-turn | Measure render time with 0 vs. 100 annotations on `simple-text` page |
| `PageTurn_P95_With100Annotations` | ≤ 100 ms P95 (NFR-OGMA-005) | Extends Phase 08 benchmark with 100 highlights on each page |
| `AnnotationWrite_P95` | ≤ 200 ms P95 | 50 sequential `CreateHighlightAsync` calls; measure wall time |

---

## 7. UI / accessibility tests

| Test | Tooling | Oracle |
| --- | --- | --- |
| `AnnotationOverlay_Selection_OpensContextMenu` | Avalonia headless | Mouse drag selects; context menu appears with "Highlight", "Add note", "Cite" |
| `NotePopover_Escape_Dismisses` | Avalonia headless | Escape closes pop-over; no navigation |
| `BookmarkPanel_KeyboardNavigable` | Avalonia headless | Tab enters panel; arrow keys move between items; Enter navigates |
| `LayerSidebar_VisibilityToggle_HidesHighlights` | Avalonia headless | Toggle off → overlay redraws without that layer's highlights |
| Screen-reader pass (manual) | VoiceOver / Narrator | "Highlight, layer Key arguments, page 3" announced on focus; bookmark list item labels announced |
| Color accessibility (manual / automated) | axe-style | Highlight overlay ≥ 3:1 contrast against page background for each layer color; never color-only meaning |

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
