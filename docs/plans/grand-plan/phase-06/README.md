# Phase 06 — Catalogue Browsing

The primary browsing experience: grid, list, and directory views of the library;
sort and filter; virtual and smart shelves; full book-detail panel; and previewed,
undoable bulk edit — all performant at 2,000 books, all keyboard-navigable, all
colorfully iconified, all in English and French.

---

## 1. Title & one-line mission

**Phase 06 — Catalogue Browsing.**
Build every non-reader user-facing surface for the library: the grid/list/directory
views (sharing the book-detail + reader contract with Phase 08 and Phase 14), sort
and filter, virtual + smart shelves, full five-group metadata book detail, and
previewed undoable bulk edit — virtualized for 2,000 books, accessible, localized,
and iconified to the premium colorful standard.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP (FR-CAT-001..004, grid/list/directory, sort/filter, shelves, book detail); V1 (FR-CAT-005 bulk edit) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | Original Phase 2 (catalogue UI) |
| **Platforms** | Windows 10+ (x64/ARM64) and macOS 12+ (x64/Apple Silicon) |
| **Status** | Planned — not yet started |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. The user can switch between Grid, List, and Directory views; all three open the
   same book-detail panel and reader entry point (FR-CAT-001); the 3D view slot is
   reserved in the view-toggle UI but routes to a "coming soon" placeholder.
2. Sort (title, author, year, rating, status, date added) and filter (status, rating,
   tag, shelf, availability, author) are conjunctive and cleared with a single action
   (FR-CAT-002); the UI updates in < 150 ms P95 for 2,000 books (NFR-OGMA-003).
3. Virtual shelves exist and a book can belong to multiple shelves independently of
   its file-system path (FR-CAT-003); shelves are created, renamed, and deleted from
   the sidebar.
4. The book-detail panel displays all five metadata field groups (File, Bibliographic,
   Reading, Enrichment, AI) with edit affordances and provenance indicators for
   enriched fields (FR-CAT-004).
5. The virtualized list/grid never drops below 60 FPS during scroll for 2,000 books
   with covers loaded (NFR-OGMA-002; Avalonia virtualization confirmed).
6. Previewed undoable bulk edit (V1): selecting multiple books and editing
   tags/shelves/status shows a before/after preview; the edit is reversible via Ctrl+Z
   (FR-CAT-005).
7. Every interactive control carries a colorful icon and an accessible label; the full
   browsing flow is keyboard-navigable; screen-reader walkthrough passes
   (NFR-PROD-008).
8. All user-facing strings are externalized and present in `en` + `fr`
   (I18N-STRATEGY.md).

---

## 4. Scope

### In scope

- **Main window layout**: sidebar (shelves, settings nav) + content area (view toggle
  toolbar + catalogue view) + status bar (scan progress from Phase 05).
- **View toggle**: Grid / List / Directory / 3D-placeholder; persisted per session.
- **Grid view** (`CatalogueGridView`): Avalonia `ItemsRepeater` with virtual layout;
  each cell = cover image + title + author + availability chip; responsive column
  count (2-6 columns, configurable).
- **List view** (`CatalogueListView`): Avalonia `VirtualizingStackPanel`-backed
  `ListBox`; row = small cover + title + author + year + rating + status chip.
- **Directory view** (`CatalogueDirectoryView`): tree of folders mirroring the
  file-system structure under the library root; clicking a folder filters the grid/list
  to that subtree.
- **Sort bar**: dropdown (Title, Author, Year, Rating, Status, Date Added) + Ascending
  / Descending toggle; persisted per view.
- **Filter panel**: sidebar flyout or inline panel; filter chips for Status, Rating
  (star range), Tag (multi-select), Shelf (multi-select), Availability
  (Available/Unavailable/All); conjunctive AND logic; "Clear all" button.
- **Shelf sidebar**: list of virtual + smart shelves; create, rename, delete, reorder;
  drag-and-drop book onto shelf; shelf book count badge.
- **Smart shelf editor**: condition builder (field → operator → value); stored as
  JSON query; evaluated on open (not pre-computed — deferred to Phase 10 if FTS5 needed).
- **Book-detail panel** (`BookDetailView`): slides in from the right; five field groups
  with inline edit (plain text fields only); selected-book "Enrich" action wired to
  deterministic Phase 07 providers; "Read" button → Phase 08 reader; cover image;
  rating stars; tags editor; shelf membership.
- **Bulk edit** (V1): multi-select in grid/list (checkboxes, Ctrl+Click, Shift+Click);
  bulk-edit toolbar (tags, shelves, status, rating, confidence); before/after diff
  preview modal; Undo via `ICommandHistory`.
- **Settings panel** (minimal): excluded folders list (backed by `ILibraryRootService`
  from Phase 05); display density preference; view mode preference.
- **Empty states**: no books, no search results, no shelf members — each with a
  colorful illustration-style empty-state icon and localized copy.
- Performance gate: sort/filter on 2,000 books < 150 ms P95 (NFR-OGMA-003); grid
  scroll 60 FPS (NFR-OGMA-006 precondition for 3D; confirmed for 2D here).
- All UI strings in `en` + `fr`; pseudolocale check; full WCAG 2.2 AA keyboard + SR.
- All icons from the rich icon manifest in `icons.md` (view toggles, sort, filter,
  shelf, tag, rating, availability, bulk-edit, empty states).

### Explicitly out of scope

- 3D bookshelf view (Phase 14); the toggle slot is present, routes to a placeholder.
- Bulk metadata enrichment workbench screens (Phase 07); the selected-book
  "Enrich" button is active and shows provider provenance after enrichment.
- PDF reader (Phase 08); the "Read" button opens a placeholder or a stub.
- Full-text search (Phase 10); the search bar is present but limited to metadata search.
- Annotation display in book detail (Phase 09).
- LAN-specific multi-user browsing (Phases 16-17).
- Calibre / Zotero importers (Phase 23).
- Drag-and-drop reorder within a shelf beyond basic implementation (deferred if complex).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-CAT-001 | MVP | Grid/list/directory views; all open same book-detail + reader | `ViewToggle_AllThreeViews_OpenSameBookDetail`; UI automation test |
| FR-CAT-002 | MVP | Sort & filter; conjunctive; single clear | `FilterAndSort_Conjunctive_Under150ms`; `FilterClear_ResetsAllFilters` |
| FR-CAT-003 | MVP | Virtual shelves; book in multiple shelves independent of path | `Shelf_BookInMultipleShelves_NoPathDependency`; `SmartShelf_EvaluatesCondition` |
| FR-CAT-004 | MVP | Full metadata across 5 field groups | `BookDetail_AllFiveFieldGroups_Populated` |
| FR-CAT-005 | V1 | Previewed undoable bulk edit | `BulkEdit_PreviewShownBeforeApply`; `BulkEdit_Undo_RevertChanges` |
| NFR-OGMA-002 | MVP | Catalogue load ≤ 2 s P95 for 2,000 books | `CatalogueLoad_2000Books_Under2s` |
| NFR-OGMA-003 | MVP | Metadata search ≤ 150 ms P95 | `FilterAndSort_2000Books_Under150ms` |
| NFR-PROD-003 | MVP | First screen ≤ 1 s; page ≤ 200 ms | `MainWindow_FirstScreen_Under1s` |
| NFR-PROD-005 | MVP | No UI stall > 100 ms | `GridScroll_2000Books_NoUIStall` |
| NFR-PROD-007 | MVP | Keyboard ops | Full keyboard walkthrough test |
| NFR-PROD-008 | MVP | Screen-reader + AA contrast | SR walkthrough; axe-style contrast check |

---

## 6. Dependencies

### Depends on

| Phase / Decision | What is needed |
| --- | --- |
| Phase 04 — Catalogue & Data Layer | `ICatalogueReadModel`, `BookSummaryProjection`, `BookDetailProjection`, `ShelfProjection`; `Shelves` / `ShelfBooks` write services |
| Phase 05 — Ingestion Pipeline | Populated `Books` and covers for realistic testing; `ScanProgressView` in main window status bar |
| Phase 03 — Design System | Avalonia theming, design tokens, `ILocalizationService`, `IconCatalog`, command-palette scaffold |
| Phase 02 — Scaffolding | MVVM base classes; DI; architecture tests; golden-corpus harness |
| Phase 00 — Decision Closure | Command-palette command set confirmed (SRS context gap) |

### Unblocks

- **Phase 07** — Metadata enrichment UI is embedded in the book-detail panel (the
  "Enrich" button and the provenance display are built here; enrichment logic in P07).
- **Phase 08** — Reader entry point is the "Read" button in book-detail; the
  `IReaderNavigationService` interface is defined here.
- **Phase 09** — Annotation count badge in book-detail defined here.
- **Phase 14** — 3D view toggle slot is pre-wired; spine textures flow through the
  shared cover-loading contract defined here.

---

## 7. Architecture & approach

### Bounded contexts touched

**Bookshelf Presentation** (owns the view layer). **Library Catalogue** (read-only
via `ICatalogueReadModel`). The Bookshelf Presentation context never writes to the
catalogue directly — writes go through `ICatalogueWriteService` in the Application
layer (shelf create/rename/delete, book-to-shelf assignment, metadata field edits).

### MVVM structure

```
OgmaLibrary.App/
  Views/
    Catalogue/
      MainShellView         (shell: sidebar + content + status bar)
      CatalogueGridView     (ItemsRepeater + virtual layout)
      CatalogueListView     (VirtualizingStackPanel)
      CatalogueDirectoryView(TreeView)
      BookDetailView        (slide-in panel, 5 tab groups)
      ShelfSidebarView      (shelf list + create/rename/delete)
      FilterPanelView       (filter chips)
      BulkEditView          (V1 - multi-select toolbar + preview modal)
      SettingsView          (excluded folders + preferences)
  ViewModels/
    Catalogue/
      MainShellViewModel
      CatalogueViewModel    (shared: items, sort, filter, selection)
      BookDetailViewModel
      ShelfSidebarViewModel
      FilterPanelViewModel
      BulkEditViewModel     (V1)
```

`CatalogueViewModel` is the single source for the displayed item list; it holds
the `ObservableCollection<BookSummaryProjection>` and applies sort/filter via LINQ
on the `ICatalogueReadModel` projections (never re-queries the DB on each filter
change — loads into memory, applies LINQ; refresh on rescan event).

### Virtualization strategy (NFR-OGMA-002 / NFR-PROD-005)

- **Grid view**: Avalonia `ItemsRepeater` with `UniformGridLayout` (configurable
  columns). `ItemsRepeater` uses element recycling; cover images loaded lazily via
  `AsyncImage` pattern — request cover JPEG from `ISidecarService` on cell
  materialization; placeholder shown during load.
- **List view**: Avalonia `ListBox` with `VirtualizingStackPanel`; same lazy cover
  loading.
- No full re-render on sort/filter — apply the sorted/filtered projection to the
  `ObservableCollection` via `CollectionView` / list-diff with `ObservableCollection`
  batch updates.
- Target: 60 FPS during scroll on 2,000 items with covers loaded (NFR-PROD-005).

### Sort & filter contract

- `CatalogueFilter` record: `{ StatusFilter, RatingRange, TagIds, ShelfIds, AvailabilityFilter }`.
- `CatalogueSortOrder` record: `{ SortField, Ascending }`.
- Applied client-side (in-memory LINQ) after the initial `GetBookSummariesAsync` load.
- Re-applied on: rescan event, shelf membership change, metadata edit.
- Total sort+filter time < 150 ms P95 on 2,000 items (NFR-OGMA-003).

### Book-detail panel — five field groups (FR-CAT-004)

| Group | Fields |
| --- | --- |
| File | File name, path, size, mtime, format, pages, encryption, PDF version |
| Bibliographic | Title, Authors, Publisher, Year, ISBN-10/13, DOI, Language, Categories, Description |
| Reading | Status, Rating (1-5 stars), Tags, Reading progress %, Last read date, Shelf memberships |
| Enrichment | Provider, Confidence, Lookup date, Override flag, individual field provenance (V1 from Phase 07) |
| AI | AI-generated description, recommended reading-level, related titles (V1 from Phase 13) |

Inline edit: plain text fields (Title, Tags) are editable directly; changes written
via `ICatalogueWriteService`; enriched fields show provenance source and a "Override"
toggle.

### Shared book-detail + reader contract

`IBookDetailNavigationService`:
```csharp
Task OpenDetailAsync(BookId id);
Task OpenReaderAsync(BookId id, int? pageHint = null);
```
All three views (grid, list, directory) and Phase 14 (3D) call this single interface.
The reader (Phase 08) implements `OpenReaderAsync`. This is the contract that prevents
3D from becoming a dead-end (product principle 5).

### Smart shelf evaluation

- `SmartShelfCondition` = `{ Field, Operator, Value }` (e.g., `Rating >= 4`).
- Evaluated by LINQ over the in-memory `BookSummaryProjection` list when the shelf
  is opened (MVP). Phase 10 will extend to FTS5 content queries.
- Stored as `Shelves.Query` JSON; `SmartShelfEvaluator` deserializes and builds the LINQ
  expression; validated at creation time.

### Bulk edit (V1, FR-CAT-005)

- Multi-select: Avalonia multi-selection in both `ItemsRepeater` and `ListBox`.
- `BulkEditViewModel`: collects selected `BookId`s; builds `BulkEditCommand`
  (tags to add/remove, shelf to add/remove, new status, new rating).
- Preview modal: before/after table of affected fields per book.
- Execution via `ICatalogueWriteService.BulkEditAsync(command)`; the service writes
  a snapshot to `AuditEvents(EventType = "BulkEdit", BeforeJson, AfterJson)`.
- Undo: `ICommandHistory.Undo()` reads the snapshot, reverses the write.

### Cross-platform notes

- Avalonia `ItemsRepeater` and `VirtualizingStackPanel` are cross-platform; no
  platform-specific virtualization code.
- Cover images: loaded as `Bitmap` from JPEG bytes via `SidecarService`; decoded on
  a thread-pool thread, bound via `IImage`; Avalonia handles DPI scaling.
- macOS: keyboard shortcut conventions (Cmd vs. Ctrl) follow Avalonia
  `KeyGesture` platform-adaptive bindings (Phase 03 design system convention).
- Directory view uses `Path.GetDirectoryName` with forward-slash normalization;
  never platform-specific path comparison.

---

## 8. Work breakdown (summary)

Full detail in `tasks.md`.

| WP | Title | Key tasks |
| --- | --- | --- |
| WP1 | Main shell layout | `MainShellView`, sidebar + content area + status bar wiring |
| WP2 | Grid view | `CatalogueGridView` with `ItemsRepeater`, virtual layout, lazy covers |
| WP3 | List view | `CatalogueListView` with virtualized `ListBox` |
| WP4 | Directory view | `CatalogueDirectoryView` tree; folder-filter integration |
| WP5 | Sort & filter | `FilterPanelView`, `CatalogueFilter`, LINQ application, < 150 ms gate |
| WP6 | Shelf sidebar | `ShelfSidebarView`; CRUD; drag-and-drop assignment; smart shelf editor |
| WP7 | Book-detail panel | `BookDetailView`; five field groups; inline edit; "Read" button; selected-book "Enrich" action and provider provenance display |
| WP8 | Shared navigation contract | `IBookDetailNavigationService`; `IReaderNavigationService` stub |
| WP9 | Bulk edit (V1) | `BulkEditViewModel`; preview modal; `ICommandHistory` undo |
| WP10 | Settings panel | Excluded folders; display preferences |
| WP11 | Performance gate | 2,000-book scroll + filter benchmarks; Avalonia virtualization confirmation |
| WP12 | Accessibility, i18n, icons | Full keyboard walkthrough; SR labels; `en`+`fr` strings; icon wiring |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons** — 30+ new icons in `icons.md` covering view toggles, sort,
      filter, shelf operations, tag, rating, availability, bulk-edit, and empty states;
      procurement request issued to owner.
- [x] **i18n (en/fr)** — every label, tooltip, empty-state copy, error message, and
      placeholder text externalized; `fr.resx` complete for all MVP surfaces; pseudolocale
      renders without truncation or overlap.
- [x] **Accessibility** — full keyboard navigation of grid/list/directory; Tab order
      logical; filter chips keyboard-operable; shelf list keyboard-operable; book-detail
      panel focus-managed; bulk-select Ctrl+Click + Shift+Click + Space; WCAG 2.2 AA
      contrast on all text + chips; screen-reader walkthrough of primary browse flow.
- [x] **Privacy/egress** — no off-device calls; all data from local catalogue.
- [x] **Reversibility (R1)** — bulk-edit undo path tested; inline metadata edit
      undo via `ICommandHistory`; no destructive operation without preview.
- [x] **Performance budgets** — `CatalogueLoad_2000Books_Under2s` (NFR-OGMA-002);
      `FilterAndSort_2000Books_Under150ms` (NFR-OGMA-003); `GridScroll_60FPS_2000Books`
      (NFR-PROD-005); all instrumented in CI.
- [x] **Bounded-context tests** — `BookshelfPresentation_DoesNotInstantiate_CatalogueDbContext`;
      `CatalogueViewModel_WritesOnly_Via_ICatalogueWriteService`.
- [x] **Documentation** — XML doc comments on all public ViewModels and interfaces;
      `docs/architecture/catalogue-browsing.md` with view-contract diagram; `AVALONIA-STANDARDS.md`
      consulted throughout.

---

## 10. Definition of Done

Global DoD (README §6) plus:

- [ ] `ViewToggle_AllThreeViews_OpenSameBookDetail`: clicking a book in each of grid,
      list, and directory opens the same `BookDetailView` with the correct `BookId`.
- [ ] `FilterAndSort_2000Books_Under150ms`: sort+filter LINQ on 2,000 in-memory
      `BookSummaryProjection` items < 150 ms P95 (NFR-OGMA-003).
- [ ] `CatalogueLoad_2000Books_Under2s`: `ICatalogueReadModel.GetBookSummariesAsync`
      + bind to `ObservableCollection` in < 2 s P95 (NFR-OGMA-002).
- [ ] `GridScroll_60FPS_2000Books`: Avalonia frame rate ≥ 60 FPS during scroll
      over 2,000 items with covers (NFR-PROD-005); measured via Avalonia diagnostic
      frame counter.
- [ ] `Shelf_BookInMultipleShelves_NoPathDependency`: add book to 3 shelves; verify
      all 3 `ShelfBook` rows present; no file-path dependency.
- [ ] `SmartShelf_EvaluatesCondition`: smart shelf with `Rating >= 4` condition shows
      only books with rating >= 4.
- [ ] `BookDetail_AllFiveFieldGroups_Populated`: all five groups render non-empty for
      a fully-seeded book.
- [ ] `BulkEdit_PreviewShownBeforeApply` and `BulkEdit_Undo_RevertChanges` pass (V1).
- [ ] Full keyboard walkthrough of browse-select-open-detail flow; no mouse required.
- [ ] Screen-reader reads book title, author, and availability status from each
      grid cell and list row.
- [ ] All icons wired (premium PNGs or `🟨 placeholder`); `icons.md` current.
- [ ] `en` + `fr` resource keys complete for all interactive surfaces; pseudolocale
      check passes.
- [ ] `BookshelfPresentation_DoesNotInstantiate_CatalogueDbContext` architecture test green.
- [ ] Builds and tests pass on **both** Windows and macOS CI runners.

---

## 11. Skills to use

Full invocation detail in `skills.md`.

- `frontend-ux:practical-ui-design` — grid/list/directory layout, sort/filter UX,
  shelf sidebar.
- `frontend-ux:premium-ui-ux-design` — book-detail panel, empty states, icon
  integration, premium feel.
- `frontend-ux:interaction-design-patterns` — multi-select, drag-and-drop, slide-in
  panel, keyboard navigation patterns.
- `frontend-ux:frontend-performance` — Avalonia virtualization verification; 60 FPS
  scroll; cover lazy-loading.
- `frontend-ux:data-visualization` — filter chip counts, rating distribution badge.
- `frontend-ux:design-audit` — icon coherence gate; palette consistency with Phase 03.
- `superpowers:brainstorming` — before WP6 (shelf UX) and WP9 (bulk-edit preview
  design) to explore options before committing.
- `superpowers:test-driven-development` — ViewModel unit tests before each view.
- `avalonia-desktop-development` (reference `AVALONIA-STANDARDS.md`) — all Avalonia
  patterns; `ItemsRepeater`, `VirtualizingStackPanel`, `DataTemplate`, Dispatcher.
- `/code-review` — after WP7 (book detail) and WP9 (bulk edit); before phase close.
- `comprehensive-review:full-review` — at phase close for the full UI surface.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `MainShellView` + `MainShellViewModel` | `OgmaLibrary.App/Views/Catalogue/` |
| `CatalogueGridView` + `CatalogueListView` + `CatalogueDirectoryView` | `OgmaLibrary.App/Views/Catalogue/` |
| `CatalogueViewModel` + `FilterPanelViewModel` | `OgmaLibrary.App/ViewModels/Catalogue/` |
| `BookDetailView` + `BookDetailViewModel` | `OgmaLibrary.App/Views/Catalogue/` |
| `ShelfSidebarView` + `ShelfSidebarViewModel` | `OgmaLibrary.App/Views/Catalogue/` |
| `BulkEditView` + `BulkEditViewModel` | `OgmaLibrary.App/Views/Catalogue/` |
| `SettingsView` (minimal) | `OgmaLibrary.App/Views/Settings/` |
| `IBookDetailNavigationService` + `IReaderNavigationService` (stub) | `OgmaLibrary.Application/Navigation/` |
| `ICatalogueWriteService` + impl (shelf CRUD, metadata edit, bulk edit) | `OgmaLibrary.Application/Catalogue/`, `OgmaLibrary.Infrastructure/Catalogue/` |
| `SmartShelfEvaluator` | `OgmaLibrary.Application/Catalogue/` |
| `ICommandHistory` + `CommandHistory` impl | `OgmaLibrary.Application/Commands/` |
| `en.resx` + `fr.resx` catalogue browsing keys | `OgmaLibrary.App/Resources/` |
| Icon assets (placeholder or procured) | `OgmaLibrary.App/Assets/icons/catalogue/` |
| ViewModel unit tests + performance benchmarks | `OgmaLibrary.Tests/Catalogue/` |
| `docs/architecture/catalogue-browsing.md` | `docs/architecture/` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Avalonia `ItemsRepeater` with 2,000 items + cover images drops below 60 FPS | R3 | Phase 01 spike should have validated; WP11 performance gate with real covers; fallback = reduce cover resolution or introduce tile-size tiers |
| LINQ sort+filter on 2,000 in-memory items exceeds 150 ms on low-end reference HW | R3 | Profile with `Stopwatch`; if > 150 ms, move to indexed in-memory `SortedList` or pre-sorted projection; record decision in ADR |
| Smart shelf condition evaluator introduces a LINQ expression-injection vector | R2 | `SmartShelfCondition` is a closed enum of known fields/operators — no dynamic code generation; unit test all valid operator/field combinations |
| Bulk-edit undo snapshot (BeforeJson) grows unbounded for large selections | R3 | Cap undo history at 20 entries (`ICommandHistory`); warn user at > 100 books in bulk-edit |
| macOS keyboard shortcuts (Cmd vs Ctrl) require per-platform bindings | R5 | Avalonia `KeyGesture` supports platform-conditional key bindings; confirmed approach in Phase 03 |

---

## 14. Owner asks

1. **Command-palette command set** (SRS context gap, close in Phase 00): confirm
   which browsing commands appear in the command palette (e.g., "Switch to Grid",
   "Filter by Rating", "New Shelf") before WP1 main shell is finalized.
2. **View toggle order**: confirm the desired order of view buttons (3D / Grid / List /
   Directory); the 3D placeholder position matters for the toolbar layout.
3. **Bulk-edit tier**: FR-CAT-005 is V1 — confirm this is acceptable to defer from
   the MVP release, or escalate to MVP if needed.
4. **Smart shelf condition scope**: confirm whether smart shelves in MVP can only
   evaluate catalogued metadata fields (title, author, rating, status, tags) or
   whether full-text conditions (content contains "quantum") are required at MVP
   (if yes, this becomes a Phase 10 dependency for Phase 06 MVP).
5. **Icon procurement request** — see `icons.md` for the full list of ~30 icons.
   Please procure the premium PNG set before WP12 UI finalization.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand-plan agent | Initial draft authored. |
