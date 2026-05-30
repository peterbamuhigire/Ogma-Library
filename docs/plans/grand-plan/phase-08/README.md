# Phase 08 — PDF Reader Core

Single-sentence mission: deliver a fast, keyboard-operable, cross-platform PDF
reader that opens any catalogue book, resumes the last page and scroll offset,
navigates fluidly, zooms and changes display modes persistently, enters
full-screen, and supports in-document text search — all within the
≤ 100 ms P95 cached page-turn budget (NFR-OGMA-005).

---

## 1. Title & one-line mission

**Phase 08 — PDF Reader Core**
Realize the Reader bounded context: PDFium-backed page rendering with a
memory-budgeted pre-render cache that hits ≤ 100 ms P95 on cached turns,
plus the full navigate/zoom/display-mode/full-screen/text-search feature set
(FR-READ-001 through FR-READ-006).

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP |
| **Estimate** | 4 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD original Phase 4 (Reader) |
| **Platforms** | Windows 10+ (WebView2 runtime) + macOS 12+ (WKWebView); CI runners for both |
| **Status** | Planned — not started |
| **Depends on** | Phase 04 (Catalogue data layer), Phase 05 (Ingestion, book identity), Phase 03 (Design system, icon system, i18n scaffold) |
| **Unblocks** | Phase 09 (Annotations & Bookmarks), Phase 10 (Search & Indexing — text-layer extraction), Phase 15 (OCR, password PDFs), Phase 16 (LAN Host — reader read-model projection) |

---

## 3. Objectives

1. A book opened from any catalogue view (grid, list, directory, 3D shelf)
   renders its first page within the page-turn budget and resumes at the last
   saved page number and scroll offset (FR-READ-001, NFR-OGMA-005).
2. All six navigation actions (first, previous, next, last, jump-to-page,
   history back/forward) are reachable via keyboard shortcuts **and** toolbar
   buttons; both are fully accessible (FR-READ-002, NFR-PROD-007).
3. Zoom modes fit-width, fit-page, and fixed-percentage are selectable, applied
   immediately, and persisted per book (FR-READ-003).
4. Display modes single-page, two-page, and continuous-scroll are selectable
   and persisted per book (FR-READ-004).
5. Full-screen mode is toggled by a toolbar button and by the platform
   full-screen shortcut; Escape always exits (FR-READ-005).
6. In-document text search highlights all matches on the current page and lists
   matching pages; navigation between matches is keyboard-driven (FR-READ-006).
7. The page-render cache pre-renders current ± 1 pages and a low-resolution
   preview of the next; renders are cancellable and non-blocking; memory
   eviction stays within the configured budget (NFR-OGMA-005, NFR-PROD-005).

---

## 4. Scope

### In scope

- `Reader` bounded context: `ReaderView`, `ReaderViewModel`,
  `ReaderSessionService`, `PdfRenderService`, `TextLayerService`,
  `ReadingProgressService`.
- PDFium native adapter (`IPdfRenderer` + `PdfiumAdapter`) on Windows and
  macOS; native library packaging and signing for both platforms (ADR-0004).
- Page-render cache: current/previous/next pre-render, low-resolution preview,
  cancellable non-blocking renders, configurable memory-budget eviction
  (NFR-OGMA-005).
- All FR-READ-001 through FR-READ-006 features.
- Reader toolbar and keyboard-shortcut map, fully colorfully iconified
  (ICON-SYSTEM.md) and i18n-labelled (en + fr, I18N-STRATEGY.md).
- `ReadingProgress` persistence in the catalogue-of-record (table already
  introduced in Phase 04 schema).
- Text-layer extraction path used by in-document search (PdfPig; separate
  extraction-quality flag per page).
- LAN-projection-ready read model: `IReaderSessionReadModel` emits page/scroll/
  zoom events consumable by the future Host projection layer (Phase 16) without
  building LAN here.
- Performance benchmarks asserting the ≤ 100 ms P95 cached page-turn
  (NFR-OGMA-005) and ≤ 100 ms UI-stall ceiling (NFR-PROD-005) on the
  synthetic 2,000-book perf corpus.
- Golden-corpus fixture passes: simple text, scanned image-only (render path
  only — OCR deferred to Phase 15), very large (1,000+ pp), two-column,
  rotated pages, non-English, embedded outline/TOC.
- Accessibility: keyboard-only navigation of all reader actions; screen-reader
  labelling via Automation peers; contrast compliance for toolbar chrome
  (WCAG 2.2 AA, NFR-PROD-008).

### Explicitly out of scope

- Annotations, highlights, notes, bookmarks (Phase 09).
- OCR indexing of scanned pages (Phase 15).
- Password-protected PDF unlock flow (Phase 15).
- Split view (Phase 15 / V2, FR-READ-012).
- FTS5 full-text index pipeline (Phase 10).
- LAN streaming / remote render (Phase 16).
- Writing any data back to the PDF file (ADR-0008).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-READ-001 | MVP | Open & resume last page + scroll offset | `ReaderSession_Resume_RestoresPageAndScroll` integration test |
| FR-READ-002 | MVP | Navigate first/prev/next/last/jump/history (keyboard + UI) | `Navigation_AllActions_KeyboardAndToolbar` UI test; keyboard walkthrough |
| FR-READ-003 | MVP | Fit-width/fit-page/fixed-% zoom; persist per book | `Zoom_Modes_PersistedAcrossReopen` integration test |
| FR-READ-004 | MVP | Single/two-page/continuous; persist per book | `DisplayMode_Persisted_AcrossReopen` integration test |
| FR-READ-005 | MVP | Full-screen; Escape exits | `FullScreen_Toggle_EscapeExits` UI test |
| FR-READ-006 | MVP | In-document text-search highlight + page list | `TextSearch_HighlightsMatches_ListsPages` integration test |
| NFR-OGMA-005 | MVP | Page turn ≤ 100 ms P95 cached | `PerfBenchmark_PageTurn_P95_LessThan100ms` benchmark |
| NFR-PROD-005 | MVP | No UI stall > 100 ms | CI stall-monitor; benchmark assertions |
| NFR-PROD-007 | MVP | Keyboard ops on all core flows | Keyboard walkthrough in testing.md |
| NFR-PROD-008 | MVP | Screen-reader + AA contrast | Axe-style automated check + SR pass |

---

## 6. Dependencies

### Depends on

- **Phase 04** — `ReadingProgress`, `Books`, `BookFiles` tables; EF Core
  migrations; sidecar layout.
- **Phase 05** — Stable book identity (content hash); `BookFile` availability
  flag; thumbnail/spine assets (reader can show cover while first page renders).
- **Phase 03** — Design-system tokens, Avalonia theming, icon system scaffold,
  `ILocalizationService` (en/fr), command-palette infrastructure.
- **ADR-0004** — PDFium behind `IPdfRenderer` adapter ratified.
- **ADR-0008** — DB-first annotations/metadata; no PDF write-back in reader.

### Unblocks

- **Phase 09** — Reader view is the canvas for annotations and bookmarks.
- **Phase 10** — `TextLayerService` and the per-page extraction-quality flag
  feed the FTS5 extraction pipeline.
- **Phase 15** — Reader architecture extension points for OCR, password PDFs,
  split view.
- **Phase 16** — `IReaderSessionReadModel` projection interface is ready to
  be wired to the LAN host.

---

## 7. Architecture & approach

### Bounded context: Reader

The Reader bounded context (HLD §5) sits alongside Catalogue, Search, AI, and
Bookshelf3D as one of the nine bounded contexts. It **reads** book identity
from the Catalogue through `IBookFileLocator` — it never owns identity.

```
ReaderView (Avalonia)
  └─ ReaderViewModel
       ├─ ReaderSessionService           — open/close/resume/persist session
       ├─ PdfRenderService               — render single page to bitmap
       │    └─ PdfiumAdapter             — IPdfRenderer; native interop
       ├─ PageRenderCache                — current±1 pre-render, LRU eviction
       ├─ TextLayerService               — extract text layer (PdfPig)
       ├─ ReadingProgressService         — load/save ReadingProgress row
       └─ IReaderSessionReadModel        — projection interface (LAN-ready)
```

#### IPdfRenderer interface (ADR-0004)

```csharp
/// <summary>Renders a single PDF page to a bitmap at a requested size.</summary>
public interface IPdfRenderer : IDisposable
{
    int PageCount { get; }
    Task<RenderResult> RenderPageAsync(
        int pageIndex,
        RenderRequest request,
        CancellationToken ct);
    TextLayer ExtractTextLayer(int pageIndex);
}
```

The production implementation `PdfiumAdapter` wraps PdfiumCore (Windows) and
the same native library compiled for macOS arm64 + x86_64 (universal binary).
The spike in Phase 01 (ADR-0004 amendment) selected the wrapper; Phase 08
delivers the production quality adapter.

#### Page-render cache

`PageRenderCache` maintains a bounded dictionary keyed by `(bookId, pageIndex,
renderSize)`. Policy:

1. On `NavigateTo(n)`: synchronously serve cached bitmap for page `n`; trigger
   background pre-render for pages `n-1` and `n+1`; trigger low-res preview
   for `n+2`.
2. Memory budget (configurable, default 256 MB): LRU eviction of the oldest
   entries when the budget is exceeded.
3. All render tasks are issued via `CancellationTokenSource`; navigating away
   cancels pending renders for the abandoned page range.
4. Render work is dispatched on `Task.Run` / a dedicated render thread pool;
   results are marshalled back to the UI thread via the Avalonia dispatcher.
   No UI thread stall > 100 ms (NFR-PROD-005).

#### Text layer (PdfPig)

`TextLayerService` calls PdfPig to extract the text layer for in-document
search (FR-READ-006). It stamps each page with an `ExtractionQuality` flag
(`Full`, `Partial`, `Empty`, `Scanned`) that Phase 10 uses to prioritise FTS5
extraction. Text extraction is cached in the sidecar `extracted-text/` folder
(ADR-0005).

#### ReadingProgressService

Persists `ReadingProgress` row (columns: `BookId`, `LastPageIndex`,
`ScrollOffset`, `ZoomMode`, `ZoomPercent`, `DisplayMode`,
`UpdatedAtUtc`) to the SQLite catalogue-of-record. A write is issued within
500 ms of a page-turn or scroll stop. On open, `ReaderSessionService` reads the
row and restores state (FR-READ-001).

#### Cross-platform: PDFium native interop

| Platform | Native library | Distribution |
| --- | --- | --- |
| Windows x64/arm64 | `pdfium.dll` (NuGet: PdfiumCore.Win) | Bundled in MSIX/Velopack package; no separate install. Signing: the DLL is signed with the Chwezi code-signing certificate in the Phase 22 pipeline. |
| macOS arm64 + x86_64 | `libpdfium.dylib` (universal binary) | Bundled in `.app` bundle `/Contents/Frameworks/`; notarized with the DMG in Phase 22 (Apple requires all bundled dylibs to be notarized). The dylib must be **ad-hoc signed** at minimum even in development to pass macOS Gatekeeper in Phase 22. |

**Platform detection:** `IPdfRenderer` is registered in the DI root
(`OgmaLibrary.App` composition root) by a `PlatformRendererRegistrar` that
selects `PdfiumAdapter` on both platforms with the correct native path;
`MockPdfRenderer` is registered in the test host.

#### LAN-projection-ready design (LAN-CLASSROOM-ARCHITECTURE.md §3)

`IReaderSessionReadModel` emits a stream of `ReaderEvent` values
(`PageChanged`, `ZoomChanged`, `DisplayModeChanged`, `Scrolled`) via an
`IObservable<ReaderEvent>`. In Phase 16 the Host wires this observable to the
LAN projection; in Phase 08 only the local subscriber (the ReaderViewModel
itself) exists. The interface is defined now so the architecture is not
retrofitted.

---

## 8. Work breakdown (summary)

| Work package | Key tasks | Detail |
| --- | --- | --- |
| WP1 — PDFium adapter | Deliver production-quality `PdfiumAdapter`; Win + macOS native paths; benchmark | `tasks.md` WP1 |
| WP2 — Page-render cache | Memory-budgeted pre-render cache; LRU eviction; cancellation | `tasks.md` WP2 |
| WP3 — Session & progress | `ReaderSessionService`, `ReadingProgressService`, resume (FR-READ-001) | `tasks.md` WP3 |
| WP4 — Navigation | First/prev/next/last/jump/history; keyboard map; toolbar (FR-READ-002) | `tasks.md` WP4 |
| WP5 — Zoom & display modes | Fit-width/fit-page/fixed-%; single/two-page/continuous; persistence (FR-READ-003,004) | `tasks.md` WP5 |
| WP6 — Full-screen | Toggle; Escape; platform full-screen API on Win + macOS (FR-READ-005) | `tasks.md` WP6 |
| WP7 — Text-layer & in-doc search | PdfPig text extraction; ExtractionQuality flag; search highlight + page list (FR-READ-006) | `tasks.md` WP7 |
| WP8 — UI, icons, i18n, a11y | ReaderView XAML; colorful icon wiring; en/fr strings; keyboard + SR pass | `tasks.md` WP8 |
| WP9 — Tests & benchmarks | All test layers; golden-corpus fixtures; perf benchmarks (NFR-OGMA-005) | `tasks.md` WP9 |

---

## 9. Cross-cutting checklist

- [ ] **Colorful icons + manifest**: `icons.md` complete; all reader toolbar,
      zoom, display-mode, full-screen, search, and page-jump icons listed with
      status; owner procurement request issued.
- [ ] **i18n (en/fr)**: all reader strings (toolbar tooltips, search labels,
      navigation labels, error messages, empty states) externalized and present
      in both `en` and `fr`; pseudolocale CI check passes.
- [ ] **Accessibility**: keyboard-only navigation of all reader actions tested;
      Automation peers implemented for ReaderView controls; screen-reader
      walkthrough passes; WCAG 2.2 AA contrast verified on toolbar chrome.
- [ ] **Privacy/egress**: Reader is local-only; no network calls; no AI gateway
      touched — N/A for this phase.
- [ ] **Reversibility**: `ReadingProgress` writes are non-destructive; only the
      last position is stored; no PDF modification (ADR-0008) — no fault
      injection required for reversibility here.
- [ ] **Performance budgets**: NFR-OGMA-005 (≤ 100 ms P95 cached page turn) and
      NFR-PROD-005 (no UI stall > 100 ms) instrumented; CI benchmark asserts
      both on the synthetic perf corpus.
- [ ] **Bounded-context tests**: `Architecture_Reader_DoesNotDependOnSearch`,
      `Architecture_Reader_DoesNotDependOnAI`, `Architecture_Reader_AccessesCatalogueOnlyViaContracts` pass.
- [ ] **Documentation**: `IPdfRenderer`, `IReaderSessionReadModel`,
      `ReaderSessionService`, `PageRenderCache` carry full XML doc comments;
      ADR-0004 amended with production adapter decision; developer guide updated.

---

## 10. Definition of Done

**Global DoD (README §6) fully applied, plus:**

- [ ] Every FR-READ-001 through FR-READ-006 requirement has a passing
      deterministic automated test.
- [ ] NFR-OGMA-005 benchmark: `PerfBenchmark_PageTurn_P95_LessThan100ms` passes
      on **both** Windows and macOS CI runners against the synthetic perf corpus.
- [ ] Golden-corpus fixture `simple-text`, `scanned-image-only`, `very-large-1000pp`,
      `two-column`, `rotated-pages`, `non-english`, `embedded-toc` all render
      without crash; render results match stored reference bitmaps within
      tolerance.
- [ ] Resume test: open book at page 42, close app with abnormal termination
      simulation, reopen — lands on page 42 with correct scroll offset.
- [ ] PDFium native library loads and renders on macOS (arm64 and x86_64 via
      universal binary) and Windows (x64) in CI.
- [ ] All reader toolbar controls reachable via keyboard Tab/arrow/Enter/Escape;
      screen-reader announces page number and zoom level on change.
- [ ] `icons.md` complete; all icons present in en + fr labels; no hard-coded
      strings (lint clean).
- [ ] Architecture tests pass; no dependency from Reader context to Search or AI.
- [ ] `/code-review` completed and findings resolved.

---

## 11. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `superpowers:brainstorming` — before designing the cache eviction policy and
  the text-layer extraction quality strategy.
- `superpowers:test-driven-development` — write `IPdfRenderer` contract tests
  before implementing `PdfiumAdapter`.
- `frontend-ux:frontend-performance` — guide the ≤ 100 ms page-turn cache and
  the non-blocking render dispatch.
- `frontend-ux:interaction-design-patterns` — inform navigation history, zoom
  UX, and full-screen transitions.
- `frontend-ux:motion-design` — page-turn and zoom transitions within the calm
  design language.
- `frontend-ux:practical-ui-design` — reader toolbar layout, icon placement,
  and two-page spread layout.
- `avalonia-desktop-development` — Avalonia-specific virtualized scroll,
  custom render panel, Automation peers; see `_reference/AVALONIA-STANDARDS.md`.
- `language-standards` (C# / .NET 10) — native interop safety (P/Invoke),
  `unsafe` blocks, span-based bitmap handling.
- `superpowers:verification-before-completion` — run benchmarks and golden-corpus
  suite before marking any WP done.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.Reader` project | `src/OgmaLibrary.Reader/` |
| `IPdfRenderer` interface + `PdfiumAdapter` | `src/OgmaLibrary.Infrastructure/Pdf/` |
| `IReaderSessionReadModel` interface | `src/OgmaLibrary.Application/Reader/` |
| `PageRenderCache` | `src/OgmaLibrary.Reader/Cache/` |
| `TextLayerService` + `ExtractionQuality` | `src/OgmaLibrary.Reader/TextLayer/` |
| `ReadingProgressService` | `src/OgmaLibrary.Reader/Progress/` |
| `ReaderView` + `ReaderViewModel` | `src/OgmaLibrary.App/Views/Reader/` |
| `icons.md` icon manifest | `docs/plans/grand-plan/phase-08/icons.md` |
| Reader en/fr resource files | `src/OgmaLibrary.App/Assets/Strings/reader.en.resx`, `reader.fr.resx` |
| Benchmark project additions | `src/OgmaLibrary.Benchmarks/ReaderBenchmarks.cs` |
| Golden-corpus render reference bitmaps | `tests/GoldenCorpus/reader/` |
| ADR-0004 amendment (production adapter) | `docs/architecture/adr/ADR-0004-pdfium-adapter.md` (amendment entry) |
| Phase-08 tasks, skills, testing, icons | `docs/plans/grand-plan/phase-08/` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| PDFium macOS dylib signing/notarization blocks CI | R4 | Spike signing in WP1; document the codesign + notarytool commands; use ad-hoc signing in dev CI, real cert in Phase 22 pipeline. |
| Cache eviction miscalibrated on large documents (1,000+ pp) | R3 | Benchmark with `very-large-1000pp` corpus fixture; tune budget default; expose config knob. |
| PdfPig text extraction fails on scanned-image-only pages | R5 | `ExtractionQuality.Scanned` flag suppresses search on those pages; UI shows "No text layer — OCR available in V1". |
| Render thread contention on two-page mode (two concurrent renders) | R3 | Dedicated render thread pool with bounded parallelism; cancel stale renders on navigation. |
| Memory leak in PDFium document handle on abnormal close | R1 | `PdfiumAdapter` implements `IDisposable` with `SafeHandle`; finalizer fallback; leak test in golden-corpus suite. |
| macOS arm64 universal binary size | R5 | Acceptable (~30 MB); noted in packaging budget for Phase 22. |

---

## 14. Owner asks

1. **Premium icon procurement (Reader Core set):** Please review `icons.md`
   and purchase the named premium PNG icons at the style, sizes, and densities
   specified (oak-ink color family, 16/24/32/48 px @1x/2x/3x, light + dark
   variants). Full request in `icons.md` Owner Procurement block.

2. **Reference hardware confirmation (Phase 00 gap):** The NFR-OGMA-005
   ≤ 100 ms P95 benchmark is stated relative to reference hardware. Please
   confirm the Windows and macOS reference machine specs so the CI benchmark
   asserts against the right baseline.

3. **PDFium wrapper selection sign-off (ADR-0004 amendment):** The Phase 01
   spike evaluated two wrappers. Please confirm the chosen wrapper is still
   satisfactory before WP1 begins production implementation.

4. **macOS code-signing certificate availability:** A valid Apple Developer
   certificate is required to ad-hoc sign the `libpdfium.dylib` for macOS CI
   even before Phase 22 full notarization. Please confirm the certificate is
   accessible in the macOS CI secrets store.

---

## 15. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-05-30 | Initial v1.0 baseline authored | Grand-plan agent |
