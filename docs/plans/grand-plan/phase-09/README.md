# Phase 09 — Annotations, Bookmarks & Reading Memory

Single-sentence mission: make every highlight, note, bookmark, annotation
layer, citation card, and reading-memory entry durable in the catalogue-of-record
before reporting saved, and reload accurately — including on rotated pages —
with fault-injection proof that no R1 data-loss path exists (FR-READ-007,
FR-READ-008, FR-READ-011; NFR-OGMA-008).

---

## 1. Title & one-line mission

**Phase 09 — Annotations, Bookmarks & Reading Memory**
Persist reader annotations (highlights + notes), bookmarks with labels,
named annotation layers, citation capture cards, and a reading-memory journal
— all durably in the SQLite catalogue-of-record, DB-first with no PDF
write-back (ADR-0008), surviving abnormal termination and reload accurately on
any PDF including rotated pages.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP (FR-READ-007, FR-READ-008) · V1 (FR-READ-011, annotation layers, reading memory) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD original Phase 4 (Reader — annotations) |
| **Platforms** | Windows 10+ + macOS 12+; CI workflow targets both; latest remote run result unavailable from this environment |
| **Status** | Locally implementation-complete; local automated gates green; CI workflow configured with remote run result unavailable; premium SVG icons delivered and rendered on reader surfaces; owner confirmations and manual accessibility/visual signoff pending |
| **Depends on** | Phase 08 (Reader Core — `ReaderView`, `IPdfRenderer`, text layer) |
| **Unblocks** | Phase 10 (FTS5 index — annotation text is indexed), Phase 11 (annotation text in embeddings), Phase 13 (reading memory feeds AI advisor) |

---

## 3. Objectives

1. A highlight or note created during reading is persisted to the
   catalogue-of-record **within 500 ms** and before any "Saved" indicator is
   shown; on reload it appears at exactly the correct position — including
   on rotated-page documents (FR-READ-008, NFR-OGMA-008).
2. Bookmarks with user-supplied labels are created, renamed, deleted, and
   navigated to by page jump; they survive app restart and abnormal termination
   (FR-READ-007).
3. Named annotation layers (world-class addition) allow a reader to organize
   highlights into distinct overlays (e.g. "Key arguments", "Counterpoints"),
   toggle layer visibility, and merge or delete layers.
4. A citation capture card (FR-READ-011, V1) freezes title/author/page/
   selected-text into a shareable, exportable card with one keyboard shortcut.
5. A reading-memory journal records why the book was opened, notable highlights,
   open questions, and the reader's disposition — retrievable in the book-detail
   view and searchable in Phase 10/11.
6. All annotation data is stored DB-first (ADR-0008, OQ-03); no bytes are
   written back to the PDF in this phase (out of scope: Phase 15 / V2).
7. Fault-injection tests prove no R1 data-loss path: abnormal termination
   during annotation write, disk-full simulation, and transaction rollback all
   leave the catalogue consistent.

---

## 4. Scope

### In scope

- `Annotations` bounded context (sub-context of Reader): `AnnotationService`,
  `BookmarkService`, `AnnotationLayerService`, `CitationService`,
  `ReadingMemoryService`.
- Persistence: Phase 09 `AnnotationsV2`, `AnnotationLayers`, and `ReadingMemory`
  tables plus the Phase 04 durable `Bookmarks` table.
- Highlight rendering overlay: colored highlight rectangles over text selection;
  correct for zoom, scroll, and page rotation (NFR-OGMA-008 rotated-page golden
  fixture).
- Note pop-over: inline note icon on annotated text; expandable note panel.
- Annotation layer UI: layer sidebar, visibility toggle, rename/delete, active
  layer selector.
- Bookmark panel: list of bookmarks with labels; click to navigate; right-click
  to rename/delete.
- Citation card: Ctrl+Shift+C (configurable); card shows title, author, page,
  selected text; copy-to-clipboard and export-to-file actions.
- Reading memory: journal panel with structured fields (motivation, key insight,
  open questions, disposition 1–5); auto-saves on focus-out.
- Durable write pattern: EF Core transactions; `DbContext.SaveChangesAsync`
  called before any UI confirmation; WAL mode on SQLite.
- Fault-injection test suite (R1, R4): simulate abnormal termination, disk-full,
  transaction abort during annotation write (testing.md §6).
- Golden-corpus rotated-pages fixture: annotations created on a rotated page
  reload with correct bounding boxes (NFR-OGMA-008).
- LAN-projection-ready: `IAnnotationReadModel` exposes an observable of
  `AnnotationEvent` for future Host projection (Phase 16/17) — no LAN built here.
- Icons, en/fr strings, accessibility, keyboard shortcuts for all annotation
  actions (ICON-SYSTEM.md, I18N-STRATEGY.md).

### Explicitly out of scope

- PDF write-back of annotations (ADR-0008, deferred to Phase 15 / V2).
- Annotation export to standard formats (e.g. FDF, XFDF) — post-V2.
- Annotation sharing between users (Phase 17 — multi-user).
- OCR-indexed annotation search at scale (Phase 10 FTS5 pipeline).
- AI-driven annotation summaries (Phase 13).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-READ-007 | MVP | Bookmarks with labels + page jump, durable | `BookmarkService_CreateRenameDelete_RoundTripsAndEmitsBookScopedDelete`, `ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates`, and bookmark fault-injection tests |
| FR-READ-008 | MVP | Highlights & notes persist in catalogue; reload accurately | `AnnotationRepository_CommittedAnnotation_SurvivesFreshContextReopen`, `Annotation_RotatedPage_Reload_KeepsScreenRectWithinOnePixel`, and reader overlay tests |
| FR-READ-011 | V1 | Citation capture card (title, author, page, selection) | `CitationService_CaptureAndExport_UsesCatalogueMetadata`, `ReaderViewModel_SelectionCitation_UsesTextLayerWordsWhenAvailable`, and citation export tests |
| NFR-OGMA-008 | MVP | Annotation durable across abnormal termination | R1 repository rollback, failed-publisher, concurrent-write, and bookmark-abort fault tests |

---

## 6. Dependencies

### Depends on

- **Phase 08** — `ReaderView`, `ReaderViewModel`, `TextLayerService`
  (bounding-box coordinate system), `IPdfRenderer` (page rotation info),
  `PageRenderCache` (overlay must render over the cached bitmap).
- **Phase 04** — catalogue EF Core context, SQLite WAL, and durable
  `Bookmarks` table schema.
- **Phase 03** — Design-system tokens, icon system, `ILocalizationService`.
- **ADR-0008** — DB-first annotations; no PDF write-back confirmed.
- **OQ-03** — Annotation write-back strategy resolved: DB-first, no write-back
  in MVP (confirmed Phase 00).

### Unblocks

- **Phase 10** — Annotation text and notes are FTS5-indexed; `AnnotationBody`
  rows feed the extraction pipeline.
- **Phase 11** — Annotation text is embedded for semantic search.
- **Phase 13** — Reading-memory journal feeds the AI reading advisor.
- **Phase 17** — `IAnnotationReadModel` projection wired to per-student state
  sync in LAN client mode.

---

## 7. Architecture & approach

### Bounded context: Annotations (sub-context of Reader)

The Annotations sub-context owns the write path for all annotation data. It
reads book identity through `IBookFileLocator` (from Catalogue) and the page
geometry through `IPageGeometryProvider` (from the Reader render context). It
**never** owns book identity.

```
ReaderView (Phase 08)
  └─ ReaderView overlay layer     — highlight/note overlay rendered over pages
       └─ AnnotationViewModel
            ├─ AnnotationService         — create/update/delete highlights + notes
            ├─ AnnotationLayerService    — named layers, visibility, merge, delete
            ├─ BookmarkService           — create/rename/delete/navigate bookmarks
            ├─ CitationService           — build + export citation cards
            ├─ ReadingMemoryService      — reading journal; load/save per book
            └─ IAnnotationReadModel      — IObservable<AnnotationEvent> (LAN-ready)
```

#### Coordinate model

A highlight is stored as a list of `AnnotationRegion` records:
`(pageIndex, normalizedLeft, normalizedTop, normalizedWidth, normalizedHeight)`,
coordinates normalized to `[0, 1]` relative to the un-rotated page size.
On render, the overlay panel transforms normalized coordinates by the current
zoom factor **and** the page rotation matrix from `IPdfRenderer.GetPageRotation(i)`.
This is what enables correct reload on rotated pages (NFR-OGMA-008).
The desktop implementation composes this overlay in `ReaderView.axaml` using
`AnnotationOverlays` rather than a separate custom overlay-control class.

```csharp
public record AnnotationRegion(
    int PageIndex,
    double NormLeft,
    double NormTop,
    double NormWidth,
    double NormHeight);
```

#### Durable write pattern

All writes use an EF Core transaction with SQLite in WAL mode:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
_db.AnnotationsV2.Add(annotation);
await _db.SaveChangesAsync(ct);   // throws on failure; never returns partial state
await tx.CommitAsync(ct);
// Only now: notify IAnnotationReadModel observers
```

The UI confirms "Saved" only after the await returns. This satisfies
NFR-OGMA-008 and makes the R1 fault-injection tests deterministic.

#### Named annotation layers (world-class addition)

`AnnotationLayer` rows: `(Id, BookId, Name, Color, IsVisible, SortOrder)`.
Each `Annotation` belongs to exactly one layer (nullable → default layer).
The layer sidebar lists all layers; a layer visibility toggle hides/shows its
highlights without deleting them. A "Merge layers" action moves all annotations
from source to target layer. All mutations go through `AnnotationLayerService`
which validates that at least one layer always remains.

#### Reading memory (world-class addition)

`ReadingMemory` row: `(BookId, OpenedBecause, KeyInsight, OpenQuestions,
Disposition [1–5], CreatedAtUtc, UpdatedAtUtc)`. The memory journal is
presented as a structured panel in the book-detail view (Phase 06) and in
a summary card in the reader sidebar. Phase 13 (AI advisor) reads this as
first-party evidence for recommendations.

#### LAN-projection-ready design

`IAnnotationReadModel` emits `AnnotationEvent` values
(`AnnotationCreated`, `AnnotationUpdated`, `AnnotationDeleted`,
`BookmarkCreated`, `BookmarkDeleted`, `LayerChanged`). In Phase 08 the
reader only subscribes locally. In Phase 17 the Host wires it to per-student
state sync. Interface defined now; no LAN built here.

---

## 8. Work breakdown (summary)

| Work package | Key tasks | Detail |
| --- | --- | --- |
| WP1 — DB schema & durable write | Migrations for Annotations/Bookmarks/Layers/Memory; EF Core context; WAL; R1 fault tests | `tasks.md` WP1 |
| WP2 — Highlight & note engine | Text-selection → `AnnotationRegion`; normalized coordinates; rotation-correct reload | `tasks.md` WP2 |
| WP3 — Annotation overlay UI | Render highlight rectangles; note icon; note pop-over | `tasks.md` WP3 |
| WP4 — Annotation layers | Layer CRUD; visibility toggle; layer sidebar; merge/delete | `tasks.md` WP4 |
| WP5 — Bookmarks | Create/rename/delete/jump; bookmark panel | `tasks.md` WP5 |
| WP6 — Citation cards | Ctrl+Shift+C capture; card UI; copy + export | `tasks.md` WP6 |
| WP7 — Reading memory | Journal panel; structured fields; auto-save; book-detail summary | `tasks.md` WP7 |
| WP8 — UI, icons, i18n, a11y | All icon wiring; en/fr strings; keyboard + SR pass | `tasks.md` WP8 |
| WP9 — Tests & fault injection | All test layers; rotated-page golden fixture; R1 fault injection | `tasks.md` WP9 |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest**: key-named premium SVG assets are wired
      and tested for every annotation, bookmark, layer, citation, and
      reading-memory icon.
- [x] **i18n (en/fr)**: Phase 09 reader strings are externalized in both
      `annotations.en.resx` and `annotations.fr.resx`; automated pseudolocale
      rendering passes.
- [ ] **Accessibility**: highlight color is paired with accessible text labels,
      annotation controls are keyboard-operable in automated coverage, and the
      overlay contrast gate passes; manual Narrator/VoiceOver signoff remains
      pending.
- [x] **Privacy/egress**: annotation data stays local; source audit of the
      Phase 09 reader/annotation path found no network client APIs.
- [x] **Reversibility (R1)**: durable write pattern enforced with repository
      rollback, disk-full simulation, failed-publisher, invalid-FK,
      partial-region repair, concurrent-write, bookmark-abort,
      bookmark-reopen, and failed layer-delete tests.
- [x] **Performance**: annotation overlay render adds <= 10 ms P95 in the
      100-annotation transform test; page turn with 100 annotations remains
      <= 100 ms P95; annotation writes remain <= 200 ms P95.
- [x] **Bounded-context tests**: Annotations does not depend on Search or AI;
      Catalogue persistence is accessed only through contracts/repositories.
- [x] **Documentation**: `IAnnotationReadModel`, `AnnotationRegion` normalized
      coordinate model, and durable-write/read-model contracts carry XML doc
      comments.

---

## 10. Definition of Done

**Global DoD (README §6) fully applied, plus:**

- [x] FR-READ-007 (bookmarks) and FR-READ-008 (highlights/notes) have passing
      deterministic automated tests including reload-accuracy test.
- [x] Rotated-page golden fixture: annotation created on a 90°-rotated page;
      app restarted; annotation reloads at the correct screen position.
- [x] R1 fault-injection coverage: repository failures, invalid FK rollback,
      disk-full simulation, failed read-model publication, partial-region
      repair, bookmark abort, bookmark reopen-after-save, concurrent writes, and
      failed/cross-book layer deletion leave the catalogue consistent.
- [x] FR-READ-011 citation card captures correct title/author/page/selection
      and produces a well-formed export file.
- [x] Annotation layer UI: create, rename, delete, toggle visibility, merge
      all work; direct delete moves annotations to the default remaining layer;
      at-least-one-layer constraint enforced.
- [x] Reading-memory journal persists across restart; fields editable in
      book-detail view.
- [ ] All annotation controls reachable via keyboard; highlight color has
      accessible label; screen-reader announces bookmark count. Automated
      coverage is present; manual screen-reader pass remains pending.
- [x] `icons.md` complete; all icons have en + fr labels; no hard-coded Phase 09
      reader strings; premium SVG assets are committed.
- [x] Architecture tests pass; no automated R1/R2 defects open.
- [x] `/code-review` completed via sub-agent review; High/Medium/Low findings
      resolved in code and tests.

---

## 11. Skills to use

See `skills.md` for full guidance. Summary:

- `superpowers:test-driven-development` — write fault-injection tests before
  the durable write implementation.
- `backend-databases:database-reliability` — durable write pattern; WAL;
  transaction discipline.
- `superpowers:brainstorming` — before designing the normalized-coordinate
  model for annotations on rotated pages.
- `frontend-ux:interaction-design-patterns` — text-selection UX; note pop-over;
  layer sidebar; citation card interaction.
- `avalonia-desktop-development` — overlay panel rendering atop page bitmaps;
  custom Automation peers for annotations; keyboard shortcuts.
- `sdlc-meta:advanced-testing-strategy` — R1 fault-injection test design.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `AnnotationService`, `BookmarkService`, `AnnotationLayerService`, `CitationService`, `ReadingMemoryService` | `src/OgmaLibrary.Reader/Annotations/` |
| `IAnnotationReadModel` interface | `src/OgmaLibrary.Application/Reader/IAnnotationReadModel.cs` |
| `AnnotationRegion` normalized-coordinate model | `src/OgmaLibrary.Domain/Annotations.cs` |
| Annotation overlay and note anchors | `src/OgmaLibrary.App/Views/Reader/ReaderView.axaml`, `src/OgmaLibrary.App/ViewModels/Reader/ReaderViewModel.cs` |
| Text-selection mapping helper | `src/OgmaLibrary.App/ViewModels/Reader/TextSelectionService.cs` |
| Annotation-related DB migrations | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/20260531120000_Phase09Annotations.cs` |
| Annotation en/fr resource files | `src/OgmaLibrary.App/Assets/Strings/annotations.en.resx`, `annotations.fr.resx` |
| Fault-injection test suite | `tests/OgmaLibrary.Tests/Reader/Phase09AnnotationTests.cs` |
| Golden-corpus rotated-page annotation fixture | `tests/GoldenCorpus/annotations/rotated-page-annotation.json` |
| `icons.md` icon manifest | `docs/plans/grand-plan/phase-09/icons.md` |
| Phase 09 verification evidence | `docs/plans/grand-plan/phase-09/evidence.md` |
| Phase 09 closeout audit | `docs/implementation/review-31-May-2026/phase-09-closeout-audit.md` |
| Phase 09 manual signoff packet | `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Bounding-box drift on rotated pages after zoom change | R1/R5 | Normalized-coordinate model; rotation matrix applied at render time; golden-corpus test with oracle bounding boxes |
| Partial annotation write on abnormal termination (e.g. power loss mid-transaction) | R1 | SQLite WAL; EF Core transaction; `SaveChangesAsync` completes before UI confirms; fault-injection proves this |
| Performance regression: overlay panel re-renders on every scroll tick | R3 | Overlay redraws only on annotation change or navigation; dirty-flag pattern; benchmark asserts ≤ 10 ms overhead |
| Layer merge/delete leaves orphaned `AnnotationBody` rows | R5 | Cascade delete FK in schema; integration test checks row counts after merge |
| Reading-memory journal conflicts with future AI advisor data model | R5 | `IReadingMemoryRepository` contract; AI advisor accesses only through this interface |

---

## 14. Implementation progress and next steps

Current implementation progress:

- Phase 09 core implementation is locally complete for annotations, notes,
  bookmarks, layers, citations, reading memory, persistence, and reader UI.
- Automated Debug and Release verification is green; see
  `docs/plans/grand-plan/phase-09/evidence.md`.
- Premium SVG icons are wired and covered by icon/catalog tests.
- Runtime catalogue access has been hardened so app-lived reader, ingestion,
  metadata enrichment, scan health, catalogue write, audit, and legacy
  repository paths lease factory-created EF contexts per operation instead of
  sharing a singleton unit-of-work.
- Direct PDF open now preserves the existing library root for external files
  while still queueing metadata/thumbnail work for rematched books and allowing
  PDF metadata write-back for the exact registered writable external file.
- Direct PDF open now retries catalogue schema repair if SQLite reports a
  missing model table such as `BookFiles`, and weak/fuzzy direct-open matches
  register the selected PDF as a new book instead of overwriting an existing
  catalogue entry.
- Direct PDF open missing-table repair now uses the production DI path with
  fresh factory-created migration contexts, so damaged catalogues can be
  repaired before a user-selected PDF is registered and opened.
- Reader file location now also retries catalogue schema repair when SQLite
  reports a missing `BookFiles` table, preventing the post-registration reader
  open from surfacing the raw SQLite error and preserving the selected-PDF
  add-then-open flow.
- Catalogue summary/detail/progress/shelf projections now also retry catalogue
  schema repair when SQLite reports a missing model table, covering the
  immediate post-open refresh that runs after a selected PDF is added.
- The Choose Library Folder scan path has production-DI regression coverage for
  the same damaged-catalogue `BookFiles` repair path before discovered PDFs are
  registered.
- The annotation layer sidebar now marks the active writable layer and exposes
  an `Active annotation layer: <name>` automation label that follows the first
  visible layer.
- Direct PDF metadata completion now refreshes the running shell catalogue,
  shelf sidebar, and loaded detail projection so extracted title/author data
  appears without restarting or forcing a new query.
- Startup catalogue migration now verifies that model tables still exist even
  when EF migration history reports the database current; missing tables such
  as `BookFiles` are backed up and repaired before direct-PDF registration or
  shell catalogue queries run.
- The book-detail panel now exposes selected-book deterministic metadata
  enrichment using the existing no-AI provider pipeline, refreshes provider
  results into the detail projection, and displays provider provenance rows.
- The book-detail Reading tab now exposes editable reading-memory fields backed
  by `IReadingMemoryService`; saving opened-because, key-insight,
  open-questions, or disposition refreshes the compact detail summary.
- Direct PDF open now registers any selected PDF path that does not already
  have a present `BookFiles` row, even if hash or fuzzy identity matches an
  existing catalogue item.
- Choose Library Folder scans now register coexisting same-hash PDFs at new
  unregistered paths as new books while preserving true move/rename rematches
  when the previous file path is no longer present.
- Delivered Phase 09 premium SVGs now render across reader toolbar actions,
  selection actions, citation cards, sidebar tab headers, bookmark/layer
  panels, note anchors, highlight color picker, bookmark rename, and
  reading-memory disposition surfaces.
- Note-anchor icons on page overlays are now rendered as actionable buttons;
  clicking an anchor opens the inline note editor for that annotation, matching
  the planned note pop-over route.
- Annotation delete confirmation now has rendered-view coverage for both the
  Confirm and Cancel buttons, proving the UI button route deletes only after
  confirmation and leaves annotations intact on cancel.
- Phase 09 reader shortcut wiring has been rechecked against the Avalonia reader
  view: Ctrl+B toggles the current-page bookmark, Ctrl+Shift+B opens the
  bookmark panel, and Ctrl+Shift+C captures a citation from selected text.
- Citation copy now has rendered-view regression coverage proving the card's
  copy action writes the formatted plain-text citation to the Avalonia clipboard
  and reports the copied status.
- Citation export now has rendered-view regression coverage proving the export
  button keeps the documented clipboard side effect while exporting the captured
  domain citation card to sidecar storage.
- Note editor focus-out now has rendered-view regression coverage proving the
  planned blur-save route persists edited note text and closes the editor.
- Phase 09 French resource and runtime labels are cleaned of mojibake in the
  annotation/bookmark/layer/citation/reading-memory surfaces and guarded by
  automated resource/runtime tests that also catch drift between the committed
  `.resx` values and in-memory runtime dictionaries.
- Text-selection mapping is now an explicit `TextSelectionService` helper, so
  the planned screen-space selection to normalized unrotated `AnnotationRegion`
  path is traceable outside the reader view-model and covered directly.
- Reader text selection now accepts touch and pen drags without requiring the
  mouse-left-button flag, while preserving primary-button gating for mouse
  selection.
- Selection action menu buttons now have rendered-view coverage for the
  Highlight, Add note, and Capture citation routes, proving the XAML click
  handlers execute the planned selection workflows.
- Layer panel add, filter, rename, merge, and delete controls now have
  rendered-view coverage; inline layer rename also compares against the
  persisted layer name so two-way text binding cannot bypass the save route.
- Bookmark toolbar add plus direct row navigate, rename-on-blur, and delete
  controls now have rendered-view coverage; inline bookmark rename also
  compares against the persisted bookmark label so two-way text binding cannot
  bypass the save route.
- The latest local Release full-suite gate after the rendered bookmark pass is
  green: Architecture 15, Core 236, and UI 93 tests.
- The Release desktop app was rebuilt and relaunched after the direct-PDF
  missing-`BookFiles` repair and selected-PDF registration tests were rerun, so
  the running app is using the current direct-open path instead of a stale
  `--no-build` binary.

Next steps before final Phase 09 closure:

1. Record the owner decisions for palette, citation export V1 scope, and
   final reading-memory disposition wording.
2. Complete the manual Narrator/VoiceOver walkthrough and visual accessibility
   review in `docs/qa/PHASE-09-MANUAL-SIGNOFF-PACKET.md`.
3. Re-run Release build/test after manual signoff updates and update
   `evidence.md`.

---

## 15. Owner asks

1. **Premium icon procurement (Annotations set):** Delivered as premium SVGs
   and copied into key-named runtime paths. See `icons.md` for source mapping.

2. **Annotation layer color palette:** The system pre-creates four default
   layer colors; please confirm or adjust the warm-library color palette for
   annotation highlights (proposed: amber, sage, clay, plum — matching
   ICON-SYSTEM.md accent tokens).

3. **Citation export format:** For FR-READ-011, the default export is plain
   text with a structured template. Please confirm whether a secondary export
   to a structured format (BibTeX, RIS, or Markdown) is desired for V1.

4. **Reading-memory disposition scale:** The disposition field currently uses
   the label `Disposition (1-5)` and a 1–5 integer scale (1 = "did not
   finish / not useful", 5 = "transformative"). Please confirm the scale and
   any final label wording before release signoff.

---

## 16. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-05-30 | Initial v1.0 baseline authored | Grand-plan agent |
| 2026-05-31 | Updated implementation status, deliverable paths, and evidence-backed closeout checklist | Codex |
| 2026-05-31 | Recorded owner icon procurement in progress and added next-step closeout path | Codex |
| 2026-05-31 | Replaced Phase 09 placeholder icons with delivered premium SVG assets | Codex |
| 2026-05-31 | Recorded live shell refresh after direct-PDF metadata completion and startup repair for missing catalogue model tables | Codex |
| 2026-06-01 | Reconciled Phase 09 evidence dates/counts and replaced placeholder verification test names with current automated test names | Codex |
| 2026-06-01 | Added bookmark page/date sorting and made the reading-memory disposition range visible in the reader UI and accessibility evidence | Codex |
| 2026-06-01 | Added active writable layer marker and hardened direct PDF open against missing `BookFiles` schema errors and weak fuzzy rematches | Codex |
| 2026-06-01 | Tightened direct PDF registration so untracked selected paths become new books even on same-hash matches | Codex |
| 2026-06-01 | Rendered delivered Phase 09 reader icons across note anchors, panel tabs, highlight color, bookmark rename, and reading-memory disposition surfaces | Codex |
| 2026-06-01 | Hardened production-DI direct PDF repair so missing `BookFiles` is rebuilt through fresh migrator contexts before selected PDFs are registered | Codex |
| 2026-06-01 | Clarified the manual signoff packet as the authoritative Phase 09 manual-evidence source and refreshed CI-audit wording | Codex |
| 2026-06-01 | Rechecked reader shortcut wiring and recorded Ctrl+B, Ctrl+Shift+B, and Ctrl+Shift+C coverage in the closeout evidence | Codex |
| 2026-06-01 | Added rendered note editor blur-save coverage and refreshed Phase 09 evidence counts | Codex |
| 2026-06-01 | Cleaned Phase 09 French localization mojibake and added resource/runtime guard tests for mojibake and resource drift | Codex |
| 2026-06-01 | Extracted explicit text-selection mapping service and added rotation-aware coverage | Codex |
| 2026-06-01 | Added production-DI folder-scan coverage for missing `BookFiles` repair and refreshed Phase 09 evidence | Codex |
| 2026-06-01 | Added editable book-detail reading-memory fields and refreshed Phase 09 UI/full-suite evidence counts | Codex |
| 2026-06-01 | Added touch/pen-capable reader text-selection eligibility and refreshed Phase 09 evidence counts | Codex |
| 2026-06-01 | Tightened folder scans so coexisting same-hash unregistered paths register as new books while true moves/renames still rematch | Codex |
| 2026-06-01 | Rebuilt and relaunched the Release desktop app after rerunning direct-PDF/schema repair evidence, and refreshed stale startup/direct-PDF counts | Codex |
| 2026-06-01 | Refreshed broader Phase 09 evidence counts and aligned the test plan with current implemented test names | Codex |
| 2026-06-01 | Removed a stale metadata-enrichment filter token from Phase 09 evidence after rerunning the current metadata/direct-PDF/write-back slice | Codex |
| 2026-06-01 | Aligned Phase 09 task-exit wording with current green fault tests and pending manual screen-reader signoff | Codex |
| 2026-06-01 | Hardened reader file location against missing `BookFiles` errors after direct PDF open and verified selected PDFs are immediately locatable | Codex |
| 2026-06-01 | Hardened catalogue projections against missing `BookFiles` errors during the post-open shell refresh | Codex |
| 2026-06-01 | Added rendered citation clipboard regression coverage for the copy action | Codex |
| 2026-06-01 | Added rendered citation export coverage for the clipboard side effect plus sidecar export | Codex |
| 2026-06-01 | Made rendered note-anchor icons actionable and covered click-to-open note editing | Codex |
| 2026-06-01 | Added rendered annotation delete confirmation coverage for Confirm and Cancel button routes | Codex |
| 2026-06-01 | Added rendered selection action button coverage for highlight, note, and citation routes | Codex |
| 2026-06-01 | Added rendered layer lifecycle coverage and fixed inline layer rename after two-way binding | Codex |
| 2026-06-01 | Added rendered bookmark direct-control coverage and fixed inline bookmark rename after two-way binding | Codex |
| 2026-06-01 | Refreshed the local Release full-suite gate after bookmark direct-control coverage: Architecture 15, Core 236, UI 93 | Codex |
