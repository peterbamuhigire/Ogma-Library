# Phase 09 — Tasks

Work packages and tasks for Annotations, Bookmarks & Reading Memory.

---

## WP1 — DB Schema & Durable Write Infrastructure

**Goal:** migrations for all annotation tables; EF Core repositories; durable
write pattern (transaction + WAL + confirm-after-save); R1 fault-injection baseline.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP1-T1 | Review Phase 04 schema; add missing columns/tables: `AnnotationLayers` (`Id, BookId, Name, Color, IsVisible, SortOrder`); `Annotations` (`Id, BookId, LayerId, CreatedAtUtc`); `AnnotationBodies` (`Id, AnnotationId, Type [Highlight/Note], TextContent, Regions JSON`); `Bookmarks` (`Id, BookId, PageIndex, Label, CreatedAtUtc`); `ReadingMemory` (`Id, BookId, OpenedBecause, KeyInsight, OpenQuestions, Disposition, CreatedAtUtc, UpdatedAtUtc`) | 3 h | Phase 04 | FR-READ-007, FR-READ-008, FR-READ-011 |
| P09-WP1-T2 | EF Core idempotent migration for Phase 09 tables; cascade-delete FK for `AnnotationBodies → Annotations`; cascade-delete for `Annotations → AnnotationLayers` (set-null on layer delete, not cascade) | 2 h | P09-WP1-T1 | Phase 04 migration standard |
| P09-WP1-T3 | `IAnnotationRepository`, `IBookmarkRepository`, `IAnnotationLayerRepository`, `IReadingMemoryRepository` interfaces with `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `GetForBookAsync` | 2 h | P09-WP1-T1 | ADR-0008 |
| P09-WP1-T4 | EF Core implementations; all writes inside `BeginTransactionAsync` → `SaveChangesAsync` → `CommitAsync`; never return partial state | 3 h | P09-WP1-T3 | NFR-OGMA-008 |
| P09-WP1-T5 | Confirm SQLite WAL mode is set at catalogue open (`PRAGMA journal_mode=WAL`; verify in Phase 04 migration or add here) | 1 h | Phase 04 | NFR-OGMA-008 |
| P09-WP1-T6 | Write fault-injection test stubs (contracts): `FaultInjection_AbnormalTermination_AnnotationSurvives`, `FaultInjection_DiskFull_TransactionRolledBack`, `FaultInjection_PartialRegionWrite_IsAbsent` — red first | 2 h | P09-WP1-T4 | NFR-OGMA-008, R1 |

**WP1 exit:** migrations pass; transaction tests red (TDD contract established).

---

## WP2 — Highlight & Note Engine

**Goal:** text-selection → normalized `AnnotationRegion` → persisted annotation;
rotation-correct reload on all fixtures.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP2-T1 | `AnnotationRegion` record: normalized `[0,1]` coordinates relative to un-rotated page; XML doc note on rotation-correction requirement | 1 h | Phase 08 `IPdfRenderer` | FR-READ-008 |
| P09-WP2-T2 | `TextSelectionService.GetRegionsForSelection(pageIndex, selectionRect, currentZoom, currentRotation)`: maps screen-space selection to normalized `AnnotationRegion[]` | 3 h | P09-WP2-T1, Phase 08 TextLayer | FR-READ-008 |
| P09-WP2-T3 | `AnnotationService.CreateHighlightAsync(bookId, layerId, regions, color, textContent, ct)`: build `Annotation` + `AnnotationBody`; call repository; notify `IAnnotationReadModel` | 2 h | P09-WP1-T4, P09-WP2-T1 | FR-READ-008 |
| P09-WP2-T4 | `AnnotationService.CreateNoteAsync(bookId, layerId, region, noteText, ct)`: same pattern; `Type = Note` | 2 h | P09-WP2-T3 | FR-READ-008 |
| P09-WP2-T5 | `AnnotationService.GetForPageAsync(bookId, pageIndex)`: return `Annotation[]` for a page; used by overlay panel | 1 h | P09-WP1-T3 | FR-READ-008 |
| P09-WP2-T6 | Rotation-correct render helper: `AnnotationRenderHelper.ToScreenRect(region, pageSize, rotation, zoom)` transforms normalized coords through rotation matrix | 2 h | P09-WP2-T1 | FR-READ-008, NFR-OGMA-008 |
| P09-WP2-T7 | Integration test: create highlight on `rotated-pages` fixture page at 90°; reload; `ToScreenRect` produces same bounding box as at creation — within 1 px | 3 h | P09-WP2-T6 | FR-READ-008, NFR-OGMA-008 |
| P09-WP2-T8 | Make fault-injection tests P09-WP1-T6 go green with production implementation | 2 h | P09-WP2-T3, P09-WP2-T4 | NFR-OGMA-008, R1 |

**WP2 exit:** rotated-page golden-fixture test green; fault-injection tests green.

---

## WP3 — Annotation Overlay UI

**Goal:** visible highlights and note icons rendered over page bitmaps at
correct positions for all zoom levels and page rotations.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP3-T1 | `AnnotationOverlayPanel`: Avalonia custom control that draws highlight rectangles and note-anchor icons over the page render panel; subscribes to `GetForPageAsync` on page change | 4 h | P09-WP2-T6, Phase 08 render panel | FR-READ-008 |
| P09-WP3-T2 | Text-selection gesture: mouse drag / touch drag creates selection rect; on release shows context menu "Highlight" / "Add note" / "Cite" | 3 h | P09-WP3-T1 | FR-READ-008, FR-READ-011 |
| P09-WP3-T3 | Note pop-over: click note icon → inline popover with `TextArea` for note body; auto-save on focus-out; dismiss on Escape | 2 h | P09-WP3-T2 | FR-READ-008 |
| P09-WP3-T4 | Highlight color picker: choose from active layer's color or override; color visually distinct in both light and dark theme | 1 h | P09-WP3-T2 | FR-READ-008 |
| P09-WP3-T5 | Delete annotation: right-click → "Delete"; confirm dialog; delete from DB; overlay redraws | 2 h | P09-WP3-T3 | FR-READ-008 |
| P09-WP3-T6 | Overlay performance: assert overlay panel re-draw adds ≤ 10 ms to page-render cycle (within NFR-OGMA-005 budget); dirty-flag pattern | 2 h | P09-WP3-T1 | NFR-OGMA-005, NFR-PROD-005 |

**WP3 exit:** highlights visible at correct positions at 100%, 150%, 200% zoom and on rotated pages.

---

## WP4 — Annotation Layers

**Goal:** named layers with color, visibility toggle, rename, delete, and merge.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP4-T1 | `AnnotationLayerService`: `CreateLayerAsync`, `RenameLayerAsync`, `DeleteLayerAsync` (move orphaned annotations to default layer), `SetVisibilityAsync`, `MergeLayersAsync` | 3 h | P09-WP1-T4 | World-class addition |
| P09-WP4-T2 | Layer sidebar UI: list of layers with colored swatch, visibility eye toggle, active-layer indicator; "+" to add, "..." for rename/merge/delete | 3 h | P09-WP4-T1 | World-class addition |
| P09-WP4-T3 | Constraint: at-least-one-layer always present; `DeleteLayerAsync` throws if only one layer remains; UI disables delete button | 1 h | P09-WP4-T1 | Data integrity |
| P09-WP4-T4 | Layer filter: viewer can filter annotation list and overlay by selected layer | 1 h | P09-WP4-T2 | World-class addition |
| P09-WP4-T5 | Tests: create/rename/delete/merge/visibility; cascade delete moves annotations; constraint test; layer filter | 2 h | P09-WP4-T4 | World-class addition |

**WP4 exit:** full layer lifecycle works; at-least-one constraint enforced.

---

## WP5 — Bookmarks

**Goal:** create/rename/delete bookmarks with labels; bookmark panel; navigate
to bookmarked page.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP5-T1 | `BookmarkService.CreateAsync(bookId, pageIndex, label, ct)`: persist `Bookmark` row; return id | 1 h | P09-WP1-T4 | FR-READ-007 |
| P09-WP5-T2 | `BookmarkService.RenameAsync`, `DeleteAsync`, `GetForBookAsync` | 1 h | P09-WP5-T1 | FR-READ-007 |
| P09-WP5-T3 | Bookmark toolbar button: "Add bookmark" on current page; auto-label "Page N"; user can rename inline | 2 h | P09-WP5-T2 | FR-READ-007 |
| P09-WP5-T4 | Bookmark panel: sortable list (by page number or creation date); click → navigate; right-click → rename/delete | 2 h | P09-WP5-T3 | FR-READ-007 |
| P09-WP5-T5 | Keyboard shortcut: Ctrl+B = add/remove bookmark on current page; Ctrl+Shift+B = open bookmark panel | 1 h | P09-WP5-T4 | FR-READ-007, NFR-PROD-007 |
| P09-WP5-T6 | Fault-injection test: abnormal termination after `SaveChangesAsync`; reopen; bookmark present. Fault before `SaveChangesAsync`; reopen; bookmark absent | 2 h | P09-WP5-T2 | FR-READ-007, NFR-OGMA-008 |
| P09-WP5-T7 | Tests: create/rename/delete/jump; sort orders; keyboard shortcuts | 2 h | P09-WP5-T5 | FR-READ-007 |

**WP5 exit:** bookmarks survive restart and abnormal termination; panel navigates correctly.

---

## WP6 — Citation Cards

**Goal:** one-shortcut citation capture; card shows metadata + selection;
copy and export.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP6-T1 | `CitationService.CaptureAsync(bookId, pageIndex, selectedText, ct)`: read title/author from `IBookMetadataReader`; build `CitationCard` record | 2 h | Phase 04 Books table | FR-READ-011 |
| P09-WP6-T2 | Citation card UI: modal card showing "Title · Author · Page N · Selected text"; copy-to-clipboard button; close button | 2 h | P09-WP6-T1 | FR-READ-011 |
| P09-WP6-T3 | Export action: writes plain-text citation to clipboard and optionally to a `.txt` file in sidecar; format: `"<text>" — <Author>, <Title>, p.<N>` | 1 h | P09-WP6-T2 | FR-READ-011 |
| P09-WP6-T4 | Keyboard trigger: Ctrl+Shift+C when text selected invokes `CitationService.CaptureAsync`; if no selection, shows "No text selected" notice | 1 h | P09-WP6-T3 | FR-READ-011, NFR-PROD-007 |
| P09-WP6-T5 | Tests: capture includes correct title/author/page/selection; export produces correctly formatted string; keyboard trigger works | 2 h | P09-WP6-T4 | FR-READ-011 |

**WP6 exit:** citation card captures and exports correct data; keyboard shortcut works.

---

## WP7 — Reading Memory Journal

**Goal:** structured reading journal per book; auto-save; book-detail summary.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP7-T1 | `ReadingMemoryService.LoadAsync(bookId)` / `SaveAsync(bookId, memory)`: upsert `ReadingMemory` row; auto-save triggered on field focus-out with 1 s debounce | 2 h | P09-WP1-T4 | World-class addition |
| P09-WP7-T2 | Reading memory panel (reader sidebar): four fields — "Why I opened this", "Key insight", "Open questions", "Disposition (1–5)"; each field auto-saves on blur | 3 h | P09-WP7-T1 | World-class addition |
| P09-WP7-T3 | Book-detail summary card (Phase 06 view): shows last-saved disposition, key insight truncated to 80 chars, and annotation count | 2 h | P09-WP7-T1, Phase 06 | World-class addition |
| P09-WP7-T4 | Tests: load/save round-trip; auto-save fires after focus-out; disposition range validation (1–5); book-detail summary displays correct excerpt | 2 h | P09-WP7-T3 | World-class addition |

**WP7 exit:** reading memory persists and surfaces in book-detail; auto-save debounced.

---

## WP8 — UI, Icons, i18n, Accessibility

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP8-T1 | Create `annotations.en.resx` and `annotations.fr.resx`; externalize all strings for annotation actions, bookmark panel, layer sidebar, citation card, reading memory journal | 3 h | Phase 03 i18n scaffold | I18N-STRATEGY.md |
| P09-WP8-T2 | Wire premium icon PNGs (or placeholders) for all annotation surfaces; register in `IconCatalog` | 2 h | icons.md, Phase 03 | ICON-SYSTEM.md |
| P09-WP8-T3 | Accessibility: highlight color is never sole differentiator — each highlight carries an ARIA label with the layer name; note icons have `aria-label`; bookmark list keyboard-navigable | 2 h | P09-WP3-T1, P09-WP4-T2, P09-WP5-T4 | NFR-PROD-008 |
| P09-WP8-T4 | Screen-reader walkthrough: VoiceOver/Narrator announces "Highlight on page N, layer X"; bookmark list announces count and item labels | 1 h | P09-WP8-T3 | NFR-PROD-008 |
| P09-WP8-T5 | Pseudolocale render: annotation panel, layer sidebar, reading memory — no truncation/overflow | 1 h | P09-WP8-T1 | I18N-STRATEGY.md |

**WP8 exit:** pseudolocale clean; no hard-coded strings; all icons registered; SR walkthrough passes.

---

## WP9 — Tests & Fault Injection

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P09-WP9-T1 | Architecture tests: `Architecture_Annotations_DoesNotDependOnSearch`, `Architecture_Annotations_DoesNotDependOnAI` | 1 h | all WPs | Bounded-context discipline |
| P09-WP9-T2 | Complete R1 fault-injection suite: abnormal termination, disk-full, concurrent write, partial region JSON (corrupted mid-write) — all leave catalogue consistent | 4 h | WP2, WP5 | NFR-OGMA-008, R1 |
| P09-WP9-T3 | Performance regression: add 100 annotations to `simple-text` fixture; navigate 20 pages; assert page-turn time stays ≤ 100 ms P95 (NFR-OGMA-005) | 2 h | WP3 | NFR-OGMA-005 |
| P09-WP9-T4 | Golden-corpus rotated fixture: create highlight on rotated page; restart; compare bounding boxes within 1 px | 2 h | WP2 | FR-READ-008, NFR-OGMA-008 |
| P09-WP9-T5 | End-to-end smoke test: add bookmark → highlight → note → cite → layer rename → memory entry; restart; all items present | 2 h | all WPs | Global DoD |
| P09-WP9-T6 | CI matrix (Windows + macOS): `dotnet format`, `dotnet build`, `dotnet test` all pass | 1 h | all WPs | Global DoD §3 |
