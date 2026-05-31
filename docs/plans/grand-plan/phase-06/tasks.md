# Phase 06 — Tasks

Task IDs: `P06-WPN-TN`. Traceability to FR/NFR, estimates in hours (ideal),
and within-phase dependencies.

---

## WP1 — Main shell layout

**Goal:** The application main window has a sidebar, a content area, and the Phase 05
status bar — all wired to the design tokens and the DI container.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP1-T1 | Design and implement `MainShellView` (Avalonia `DockPanel` or `Grid`): sidebar (240 px fixed) + content area (flex) + status bar (32 px fixed from Phase 05). | FR-CAT-001 | 3 | Phase 03 design tokens; Phase 05 `ScanProgressView` |
| P06-WP1-T2 | Implement `MainShellViewModel`: exposes `CurrentView` (enum: Grid/List/Directory/3DPlaceholder), `SidebarViewModel`, `ScanProgressViewModel`. | FR-CAT-001 | 2 | P06-WP1-T1 |
| P06-WP1-T3 | Add view-toggle toolbar: 4 icon buttons (Grid, List, Directory, 3D-placeholder); active state styling; keyboard shortcuts (Ctrl+1/2/3/4). | FR-CAT-001 | 2 | P06-WP1-T2; Phase 03 `IconCatalog` |
| P06-WP1-T4 | Wire `Ctrl+F` to open filter panel; `Ctrl+B` to toggle sidebar; command-palette hook (commands registered per Phase 00 confirmed set). | FR-CAT-002; Phase 03 command palette | 1 | P06-WP1-T3 |
| P06-WP1-T5 | Unit test: `MainShellViewModel_ViewToggle_ChangesCurrentView` — assert `CurrentView` changes on each toggle command. | FR-CAT-001 | 1 | P06-WP1-T2 |

---

## WP2 — Grid view

**Goal:** `CatalogueGridView` renders 2,000 books with covers in a virtualized responsive
grid at 60 FPS; cover images load lazily without UI-thread blocking.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP2-T1 | Implement `CatalogueGridView` using Avalonia `ItemsRepeater` + `UniformGridLayout`; cell template: cover + title + author + availability chip. | FR-CAT-001; NFR-PROD-005 | 4 | Phase 03 design tokens |
| P06-WP2-T2 | Implement lazy cover loading: `AsyncCoverLoader` — requests cover JPEG path from `ISidecarService` on cell materialization; decodes on thread pool; publishes `IImage` to binding; placeholder shown during load. | NFR-PROD-005 | 3 | P06-WP2-T1; Phase 04 `ISidecarService` |
| P06-WP2-T3 | Implement column-count responsive behavior (2-6 columns based on window width; user-configurable default). | FR-CAT-001 | 1 | P06-WP2-T1 |
| P06-WP2-T4 | Wire `ic_grid_view` icon to toggle button; apply `SelectedItem` → `IBookDetailNavigationService.OpenDetailAsync`. | FR-CAT-001; ICON-SYSTEM.md | 1 | P06-WP2-T1; WP8 |
| P06-WP2-T5 | Performance test: `GridScroll_60FPS_2000Books` — load 2,000 synthetic books with cover stubs; measure Avalonia frame rate during programmatic scroll; assert ≥ 60 FPS P95. | NFR-PROD-005; NFR-OGMA-002 | 3 | P06-WP2-T2 |
| P06-WP2-T6 | Accessibility: each grid cell has `AutomationProperties.Name = "{title} by {author}, {status}"`. | NFR-PROD-008 | 1 | P06-WP2-T1 |

---

## WP3 — List view

**Goal:** `CatalogueListView` renders the same 2,000 books as a virtualized table-style
list with richer per-row metadata.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP3-T1 | Implement `CatalogueListView` using Avalonia `ListBox` with `VirtualizingStackPanel`; row template: small cover (48x64) + title + author + year + rating stars + status chip. | FR-CAT-001; NFR-PROD-005 | 3 | Phase 03 design tokens |
| P06-WP3-T2 | Reuse `AsyncCoverLoader` from WP2 for small cover; same lazy-decode approach. | NFR-PROD-005 | 1 | P06-WP2-T2 |
| P06-WP3-T3 | Wire `ic_list_view` icon; `SelectedItem` → `IBookDetailNavigationService.OpenDetailAsync`. | FR-CAT-001 | 1 | P06-WP3-T1 |
| P06-WP3-T4 | Performance test: `ListScroll_2000Books_NoUIStall` — programmatic scroll; assert no UI stall > 100 ms. | NFR-PROD-005 | 2 | P06-WP3-T1 |
| P06-WP3-T5 | Accessibility: row `AutomationProperties.Name` includes title + author + rating + status. | NFR-PROD-008 | 1 | P06-WP3-T1 |

---

## WP4 — Directory view

**Goal:** `CatalogueDirectoryView` mirrors the file-system folder tree; clicking a folder
filters the content area to that sub-tree.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP4-T1 | Implement `DirectoryTreeViewModel`: build tree from `Books.RelativePath` segments; each node has a book count; lazy-expand. | FR-CAT-001 | 3 | Phase 04 `ICatalogueReadModel` |
| P06-WP4-T2 | Implement `CatalogueDirectoryView` with Avalonia `TreeView`; node click sets `CatalogueFilter.FolderPathPrefix` → re-applies filter on `CatalogueViewModel`. | FR-CAT-001; FR-CAT-002 | 2 | P06-WP4-T1 |
| P06-WP4-T3 | Wire `ic_directory_view` and `ic_folder` icons. | FR-CAT-001 | 1 | P06-WP4-T2 |
| P06-WP4-T4 | Unit test: `DirectoryView_FolderClick_FiltersToSubtree` — seed books in 3 folders; click folder 2; assert `CatalogueViewModel.FilteredItems` contains only folder-2 books. | FR-CAT-001 | 2 | P06-WP4-T2 |

---

## WP5 — Sort & filter

**Goal:** Sort and filter are conjunctive, fast (< 150 ms P95 on 2,000 books), and
cleared with a single action.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP5-T1 | Implement `FilterPanelViewModel`: `StatusFilter`, `RatingRange`, `SelectedTagIds`, `SelectedShelfIds`, `AvailabilityFilter`; `HasActiveFilters` computed property; `ClearAll()` command. | FR-CAT-002 | 2 | Phase 04 projections |
| P06-WP5-T2 | Implement `CatalogueViewModel.ApplyFilterAndSort()`: LINQ over `_allItems` (`ObservableCollection<BookSummaryProjection>`); conjunctive AND logic; produces `FilteredItems`. | FR-CAT-002; NFR-OGMA-003 | 3 | P06-WP5-T1 |
| P06-WP5-T3 | Build `FilterPanelView`: filter chips for each dimension; multi-select tags + shelves via chip-group; star-range slider for rating; Clear-all button. | FR-CAT-002 | 3 | P06-WP5-T1; Phase 03 tokens |
| P06-WP5-T4 | Build sort bar: dropdown (Title, Author, Year, Rating, Status, Date Added) + ascending/descending toggle; `ic_sort_asc` / `ic_sort_desc` icons. | FR-CAT-002 | 2 | P06-WP5-T2 |
| P06-WP5-T5 | Performance test: `FilterAndSort_2000Books_Under150ms` — apply each filter dimension on 2,000 items; assert P95 < 150 ms. | NFR-OGMA-003 | 2 | P06-WP5-T2 |
| P06-WP5-T6 | Unit test: `FilterClear_ResetsAllFilters` — apply 3 filters; call `ClearAll()`; assert `HasActiveFilters = false`, `FilteredItems.Count = total`. | FR-CAT-002 | 1 | P06-WP5-T1 |
| P06-WP5-T7 | Unit test: `FilterConjunctive_AllConditionsMustMatch` — status + rating + tag filters; assert only books matching all three are in `FilteredItems`. | FR-CAT-002 | 1 | P06-WP5-T2 |
| P06-WP5-T8 | Externalize all filter chip labels and sort option labels to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P06-WP5-T3 |

---

## WP6 — Shelf sidebar

**Goal:** The sidebar lists virtual and smart shelves; the user can create, rename,
delete, and drag books onto shelves.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP6-T1 | Implement `ShelfSidebarViewModel`: loads `ShelfProjection` list; exposes `CreateShelfCommand`, `RenameShelfCommand`, `DeleteShelfCommand`; `SelectedShelf` → sets `CatalogueFilter.ShelfIds`. | FR-CAT-003 | 3 | Phase 04 `ICatalogueReadModel`; `ICatalogueWriteService` |
| P06-WP6-T2 | Implement `ICatalogueWriteService` shelf operations: `CreateShelfAsync`, `RenameShelfAsync`, `DeleteShelfAsync`, `AddBookToShelfAsync`, `RemoveBookFromShelfAsync`. | FR-CAT-003 | 3 | Phase 04 schema |
| P06-WP6-T3 | Build `ShelfSidebarView`: Avalonia `ListBox` with `ic_shelf` icon per item; book count badge; context menu (Rename, Delete); "New Shelf" button; keyboard: F2 = rename, Delete = delete. | FR-CAT-003 | 3 | P06-WP6-T1; Phase 03 tokens |
| P06-WP6-T4 | Implement drag-and-drop: drag `BookId` from grid/list cell; drop onto shelf item → `AddBookToShelfAsync`. | FR-CAT-003 | 3 | P06-WP6-T2; P06-WP2-T1 |
| P06-WP6-T5 | Build `SmartShelfEditorView`: inline condition builder (field dropdown + operator dropdown + value input); "Test" button evaluates against current catalogue. | FR-CAT-003 | 3 | `SmartShelfEvaluator` |
| P06-WP6-T6 | Implement `SmartShelfEvaluator`: deserializes `Shelves.Query` JSON → builds LINQ expression tree over `BookSummaryProjection`; validates at creation. | FR-CAT-003 | 3 | Phase 04 `Shelves` table |
| P06-WP6-T7 | Unit test: `Shelf_BookInMultipleShelves_NoPathDependency` — add book to 3 shelves; assert 3 `ShelfBook` rows; no file-path stored in `ShelfBook`. | FR-CAT-003 | 1 | P06-WP6-T2 |
| P06-WP6-T8 | Unit test: `SmartShelf_EvaluatesCondition` — smart shelf `Rating >= 4`; seed 5 books (3 rated ≥ 4, 2 rated < 4); assert `SmartShelfEvaluator` returns the 3. | FR-CAT-003 | 1 | P06-WP6-T6 |
| P06-WP6-T9 | Externalize shelf labels and smart-shelf editor strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P06-WP6-T3 |

---

## WP7 — Book-detail panel

**Goal:** `BookDetailView` shows all five field groups with inline edit; the "Read" and
"Enrich" buttons are wired to their navigation contracts.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP7-T1 | Implement `BookDetailViewModel`: loads `BookDetailProjection` for selected `BookId`; exposes grouped field view-models; `OpenReaderCommand`; active `OpenEnrichCommand` when the deterministic metadata-enrichment service is registered. | FR-CAT-004 | 3 | Phase 04 `ICatalogueReadModel` |
| P06-WP7-T2 | Build `BookDetailView`: slide-in panel from right; cover image (200x300); five tab groups; inline edit on text fields; Rating star control; Tags chip editor; Shelf membership list. | FR-CAT-004 | 5 | P06-WP7-T1; Phase 03 tokens |
| P06-WP7-T3 | Implement inline-edit commit via `ICatalogueWriteService.UpdateMetadataFieldAsync`; write `AuditEvent`; Undo via `ICommandHistory`. | FR-CAT-004; NFR-PROD-010 | 2 | P06-WP7-T1 |
| P06-WP7-T4 | Add "Enrich" button (oak-amber, `ic_enrich`) that runs deterministic provider metadata lookup for the selected book; "Read" button (`ic_open_reader`) calls `IReaderNavigationService`. | FR-CAT-004; FR-META-003 | 1 | P06-WP7-T2; WP8 |
| P06-WP7-T5 | Add cover-image empty state: `ic_book_no_cover` icon + "Cover not yet generated" label when sidecar cover absent. | FR-CAT-004 | 1 | P06-WP7-T2 |
| P06-WP7-T6 | Unit test: `BookDetail_AllFiveFieldGroups_Populated` — seed fully-enriched book; assert each group's ViewModel has ≥ 1 non-null field. | FR-CAT-004 | 2 | P06-WP7-T1 |
| P06-WP7-T7 | Accessibility: panel opened by keyboard (`Enter` on grid/list item); all fields Tab-navigable; close by `Escape`. | NFR-PROD-007 | 1 | P06-WP7-T2 |
| P06-WP7-T8 | Externalize all five field-group labels and book-detail action strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P06-WP7-T2 |

---

## WP8 — Shared navigation contract

**Goal:** Define the navigation interfaces that grid/list/directory/3D all use to
open the book-detail panel and the reader — preventing any view from holding a direct
reference to another.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP8-T1 | Define `IBookDetailNavigationService.OpenDetailAsync(BookId)` in `OgmaLibrary.Application/Navigation/`. | FR-CAT-001 | 1 | Phase 02 DI |
| P06-WP8-T2 | Define `IReaderNavigationService.OpenReaderAsync(BookId, int? pageHint)` (stub implementation that logs until Phase 08). | FR-CAT-001; FR-READ-001 | 1 | P06-WP8-T1 |
| P06-WP8-T3 | Wire `MainShellViewModel` as the implementation of both interfaces (it owns the content-area navigation); register in DI. | FR-CAT-001 | 1 | P06-WP8-T1 |
| P06-WP8-T4 | Architecture test: `AllViews_NavigateVia_IBookDetailNavigationService` — assert no direct `MainShellViewModel` reference in `Grid/List/DirectoryView` code-behind or ViewModel. | Phase 02 arch-test | 1 | P06-WP8-T3 |

---

## WP9 — Bulk edit (V1)

**Goal:** Multi-select + bulk edit with before/after preview and undo (FR-CAT-005).

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP9-T1 | Implement multi-select in `CatalogueViewModel`: `SelectedBookIds` `HashSet<BookId>`; `SelectAll`, `DeselectAll`, `ToggleSelection` commands; Ctrl+Click + Shift+Click + Space support. | FR-CAT-005 | 3 | P06-WP2-T1; P06-WP3-T1 |
| P06-WP9-T2 | Implement `BulkEditViewModel`: collects `SelectedBookIds`; `TagsToAdd`, `TagsToRemove`, `ShelfToAdd`, `ShelfToRemove`, `NewStatus`, `NewRating` properties; `PreviewCommand`. | FR-CAT-005 | 2 | P06-WP9-T1 |
| P06-WP9-T3 | Build `BulkEditView`: floating toolbar appearing when ≥ 2 items selected; shows selection count; action dropdowns; Preview button. | FR-CAT-005 | 3 | P06-WP9-T2; Phase 03 tokens |
| P06-WP9-T4 | Build preview modal (`BulkEditPreviewView`): before/after table (one row per selected book, columns per changed field); Confirm / Cancel buttons. | FR-CAT-005 | 2 | P06-WP9-T3 |
| P06-WP9-T5 | Implement `ICatalogueWriteService.BulkEditAsync(BulkEditCommand)`: apply changes; snapshot `BeforeJson` + `AfterJson` to `AuditEvents`. | FR-CAT-005; NFR-PROD-013 | 3 | P06-WP6-T2 |
| P06-WP9-T6 | Implement `ICommandHistory` (ring-buffer, max 20) + `UndoBulkEditCommand`; wire `Ctrl+Z`. | FR-CAT-005; NFR-PROD-010 | 3 | P06-WP9-T5 |
| P06-WP9-T7 | Unit test: `BulkEdit_PreviewShownBeforeApply` — assert preview modal is presented before `BulkEditAsync` is called. | FR-CAT-005 | 1 | P06-WP9-T4 |
| P06-WP9-T8 | Unit test: `BulkEdit_Undo_RevertChanges` — bulk-edit 5 books; undo; assert all fields reverted to pre-edit values. | FR-CAT-005 (R1) | 2 | P06-WP9-T6 |

---

## WP10 — Settings panel (minimal)

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP10-T1 | Build `SettingsView` with `ExcludedFoldersPanel` (list + add/remove buttons) backed by `ILibraryRootService.SetExcludedFoldersAsync`. | FR-LIB-002 | 2 | Phase 05 `ILibraryRootService` |
| P06-WP10-T2 | Add display preferences: default view mode (Grid/List/Directory), default column count, dark/light theme toggle (from Phase 03 theme tokens). | FR-CAT-001 | 1 | P06-WP10-T1 |
| P06-WP10-T3 | Externalize settings labels to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P06-WP10-T2 |

---

## WP11 — Performance gate

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP11-T1 | Run `CatalogueLoad_2000Books_Under2s` benchmark; record results in CI artifact. | NFR-OGMA-002 | 1 | WP2; Phase 04 perf corpus |
| P06-WP11-T2 | Run `FilterAndSort_2000Books_Under150ms` benchmark; record. | NFR-OGMA-003 | 1 | WP5 |
| P06-WP11-T3 | Run `GridScroll_60FPS_2000Books`; if < 60 FPS, profile and fix before phase close. | NFR-PROD-005 | 2 | WP2 |
| P06-WP11-T4 | Run `MainWindow_FirstScreen_Under1s` — time from app launch to first visible frame; assert < 1 s. | NFR-PROD-003 | 1 | WP1 |

---

## WP12 — Accessibility, i18n, icons (consolidation)

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P06-WP12-T1 | Full keyboard walkthrough: Tab order verified for grid → filter → shelf → book detail → settings; no mouse required. | NFR-PROD-007 | 2 | All WPs |
| P06-WP12-T2 | Screen-reader walkthrough: grid cell, list row, book-detail panel, shelf item, filter chip, bulk-edit toolbar — all readable. | NFR-PROD-008 | 2 | All WPs |
| P06-WP12-T3 | Run pseudolocale render test; fix any truncation or overflow in icon+label layouts. | I18N-STRATEGY.md | 1 | All `en`/`fr` string tasks |
| P06-WP12-T4 | Wire all procured/placeholder icons into `IconCatalog`; update `icons.md` status column. | ICON-SYSTEM.md | 2 | All WPs |
| P06-WP12-T5 | Run `design-audit` skill against the grid, list, book-detail, and shelf views; resolve any icon coherence or color-palette issues. | ICON-SYSTEM.md | 2 | P06-WP12-T4 |
| P06-WP12-T6 | Confirm `BookshelfPresentation_DoesNotInstantiate_CatalogueDbContext` architecture test green. | Phase 02 arch-test | 1 | All WPs |
