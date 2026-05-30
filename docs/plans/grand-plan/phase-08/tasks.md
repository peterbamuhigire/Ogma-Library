# Phase 08 — Tasks

Work packages and tasks for the PDF Reader Core phase.
Each task carries: ID, description, estimate (hours), dependencies, and the
requirement / NFR / CTRL IDs it satisfies.

Estimate convention: hours of focused engineering effort (not elapsed time).

---

## WP1 — PDFium Adapter (production-quality native interop)

**Goal:** deliver a `PdfiumAdapter : IPdfRenderer` that renders pages correctly
on Windows x64/arm64 and macOS arm64/x86_64, passes all golden-corpus render
fixtures, and stays within the memory and performance budgets.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP1-T1 | Define `IPdfRenderer`, `RenderRequest`, `RenderResult`, `TextLayer`, `ExtractionQuality` interfaces/records in `OgmaLibrary.Application.Reader` | 2 h | Phase 01 spike (ADR-0004) | ADR-0004 |
| P08-WP1-T2 | Write contract tests for `IPdfRenderer` against the Phase 01 mock renderer so all golden-corpus fixture tests exist before production code | 3 h | P08-WP1-T1 | FR-READ-001, ADR-0004 |
| P08-WP1-T3 | Implement `PdfiumAdapter` for Windows (x64 + arm64): NuGet wrapper integration, `SafeHandle`-guarded document/page handles, `RenderPageAsync` on thread pool, `IDisposable` finalizer fallback | 6 h | P08-WP1-T1, P08-WP1-T2 | FR-READ-001, NFR-OGMA-005 |
| P08-WP1-T4 | Implement `PdfiumAdapter` macOS path: universal-binary (`arm64 + x86_64`) dylib load via `NativeLibrary.Load`; document `codesign -s -` ad-hoc signing step for dev CI | 5 h | P08-WP1-T3 | FR-READ-001, NFR-OGMA-005 |
| P08-WP1-T5 | DI registration in `PlatformRendererRegistrar` (Windows / macOS dispatch); `MockPdfRenderer` registered in test host | 2 h | P08-WP1-T3, P08-WP1-T4 | ADR-0004 |
| P08-WP1-T6 | Golden-corpus render tests: `simple-text`, `scanned-image-only`, `very-large-1000pp`, `two-column`, `rotated-pages`, `non-english`, `embedded-toc` — render page 1 and compare to reference bitmap within tolerance | 4 h | P08-WP1-T3, P08-WP1-T4 | FR-READ-001 |
| P08-WP1-T7 | Amend ADR-0004 with production wrapper selection rationale; update developer guide | 1 h | P08-WP1-T6 | ADR-0004 |

**WP1 exit:** `IPdfRenderer` contract tests green on both CI runners; golden-corpus render tests pass.

---

## WP2 — Page-Render Cache

**Goal:** memory-budgeted pre-render cache that hits ≤ 100 ms P95 on cached
page turns and ≤ 100 ms on first uncached page via low-res preview bridging.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP2-T1 | Design `PageRenderCache` API: `GetOrRenderAsync(bookId, pageIndex, size, ct)`, `Prefetch(bookId, pages[])`, `Invalidate(bookId)`, `MemoryUsage` observable | 2 h | P08-WP1-T1 | NFR-OGMA-005, NFR-PROD-005 |
| P08-WP2-T2 | Implement LRU dictionary keyed by `(bookId, pageIndex, renderSize)` with configurable memory-budget cap (default 256 MB); eviction via `MemoryCache` or manual tracking of bitmap byte sizes | 4 h | P08-WP2-T1 | NFR-OGMA-005 |
| P08-WP2-T3 | Implement current ± 1 pre-render on navigation event; cancel pending renders for pages outside the ±1 window via `CancellationTokenSource` per page slot | 3 h | P08-WP2-T1, P08-WP2-T2 | NFR-OGMA-005, NFR-PROD-005 |
| P08-WP2-T4 | Low-resolution preview render (e.g. 1/4 scale) issued first; replaced by full-res when ready; ensures visual feedback within 30 ms even for first page | 2 h | P08-WP2-T3 | NFR-OGMA-005 |
| P08-WP2-T5 | Unit tests: eviction triggers at budget; cancellation stops pending tasks; cache hit returns without re-render | 3 h | P08-WP2-T2, P08-WP2-T3 | NFR-OGMA-005 |
| P08-WP2-T6 | Performance microbenchmark (`ReaderBenchmarks.PageTurn_P95`): sequential page turns through 100 pages in both directions; assert P95 ≤ 100 ms on CI reference hardware | 3 h | P08-WP2-T5 | NFR-OGMA-005 |

**WP2 exit:** benchmark `P95 ≤ 100 ms` passes on both CI runners.

---

## WP3 — Session & Reading Progress

**Goal:** open a book, resume last page + scroll offset, close and persist state.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP3-T1 | `ReaderSessionService.OpenAsync(bookId)`: resolve `BookFile` via `IBookFileLocator`, open PDFium document, load `ReadingProgress` row, return `ReaderSession` | 3 h | Phase 04 migrations, WP1 | FR-READ-001 |
| P08-WP3-T2 | `ReadingProgressService.LoadAsync` / `SaveAsync`: read/write `ReadingProgress` row (PageIndex, ScrollOffset, ZoomMode, ZoomPercent, DisplayMode); debounce save to 500 ms after last change | 3 h | Phase 04 schema | FR-READ-001 |
| P08-WP3-T3 | `ReaderSessionService.CloseAsync`: flush pending progress write synchronously; release PDFium document handle | 2 h | P08-WP3-T1, P08-WP3-T2 | FR-READ-001, NFR-OGMA-008 |
| P08-WP3-T4 | Integration test: open book at page 42; simulate abnormal termination (kill DI scope without `CloseAsync`); reopen — assert page 42 restored | 2 h | P08-WP3-T2 | FR-READ-001, NFR-OGMA-008 |
| P08-WP3-T5 | `IReaderSessionReadModel` interface: `IObservable<ReaderEvent>` emitting `PageChanged`, `ZoomChanged`, `DisplayModeChanged`, `Scrolled`; wire to ViewModel; add "LAN-ready" XML doc note | 2 h | P08-WP3-T1 | LAN-CLASSROOM-ARCHITECTURE.md §3 |

**WP3 exit:** resume integration test passes; `IReaderSessionReadModel` interface published.

---

## WP4 — Navigation

**Goal:** all six navigation actions via keyboard and toolbar; history stack.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP4-T1 | `ReaderViewModel` navigation commands: `GoToFirstPage`, `GoToPrevPage`, `GoToNextPage`, `GoToLastPage`, `GoToPage(n)`, `GoBack`, `GoForward` — all as `ICommand` | 3 h | WP3 | FR-READ-002 |
| P08-WP4-T2 | Navigation history stack (`NavigationHistory`): push on every explicit `GoToPage`; `GoBack`/`GoForward` pop without pushing; max depth 50 | 2 h | P08-WP4-T1 | FR-READ-002 |
| P08-WP4-T3 | Jump-to-page input: text box accepts page number; validates range; commits on Enter; clears on Escape | 2 h | P08-WP4-T1 | FR-READ-002 |
| P08-WP4-T4 | Keyboard shortcut map: Left/Right arrow = prev/next; Home/End = first/last; Ctrl+G or `g` = jump prompt; Alt+Left/Right = history back/forward; Space = next page in single/two-page mode, scroll in continuous | 2 h | P08-WP4-T1 | FR-READ-002, NFR-PROD-007 |
| P08-WP4-T5 | Toolbar buttons for all navigation actions wired to `icons.md` icons; Automation peers announcing current page / total pages | 2 h | P08-WP4-T4 | FR-READ-002, NFR-PROD-008 |
| P08-WP4-T6 | UI tests: keyboard navigation traverses all actions; toolbar buttons trigger correct commands; history back/forward; jump to out-of-range page shows validation error | 3 h | P08-WP4-T5 | FR-READ-002 |

**WP4 exit:** all six navigation actions reachable via keyboard and UI; UI tests pass.

---

## WP5 — Zoom & Display Modes

**Goal:** fit-width/fit-page/fixed-% zoom and single/two-page/continuous modes,
each persisted per book across sessions.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP5-T1 | `ZoomMode` enum (`FitWidth`, `FitPage`, `Fixed`); `DisplayMode` enum (`Single`, `TwoPage`, `Continuous`); add columns to `ReadingProgress` migration | 1 h | Phase 04 | FR-READ-003, FR-READ-004 |
| P08-WP5-T2 | Zoom calculation: `FitWidth` = container width / page width; `FitPage` = min(w ratio, h ratio); `Fixed` = stored % (25–500 range); recalculate on container resize | 3 h | P08-WP5-T1 | FR-READ-003 |
| P08-WP5-T3 | Zoom toolbar: dropdown or button group for three modes; ±10% increment/decrement buttons; current-% label | 2 h | P08-WP5-T2 | FR-READ-003 |
| P08-WP5-T4 | Display-mode selector: single/two-page/continuous toggle buttons; two-page aligns even/odd spreads correctly | 2 h | P08-WP5-T1 | FR-READ-004 |
| P08-WP5-T5 | Continuous-scroll panel: `VirtualizingStackPanel`-equivalent that renders only visible pages ± 1 buffer; ties to `PageRenderCache` prefetch | 4 h | WP2, P08-WP5-T4 | FR-READ-004, NFR-PROD-005 |
| P08-WP5-T6 | Persistence: `ReadingProgressService.SaveAsync` includes zoom + display mode; `LoadAsync` restores them | 1 h | WP3, P08-WP5-T1 | FR-READ-003, FR-READ-004 |
| P08-WP5-T7 | Tests: zoom modes produce correct page dimensions; continuous panel renders only visible pages; persistence round-trip | 3 h | P08-WP5-T5, P08-WP5-T6 | FR-READ-003, FR-READ-004 |

**WP5 exit:** all three zoom modes and three display modes work; persistence tests pass.

---

## WP6 — Full-Screen

**Goal:** full-screen toggle via toolbar and keyboard; Escape always exits.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP6-T1 | `IWindowFullScreenService` interface: `EnterFullScreen()`, `ExitFullScreen()`, `IsFullScreen` observable | 1 h | Phase 03 | FR-READ-005 |
| P08-WP6-T2 | Windows implementation: `WindowState = WindowState.FullScreen` (Avalonia); hide title bar chrome | 1 h | P08-WP6-T1 | FR-READ-005 |
| P08-WP6-T3 | macOS implementation: `NSWindow.ToggleFullScreen()` via Avalonia macOS native interop or `WindowState.FullScreen`; handle `NSWindowWillEnterFullScreenNotification` | 2 h | P08-WP6-T1 | FR-READ-005 |
| P08-WP6-T4 | Escape key handler always exits full-screen (and dismisses any open overlay like text-search panel) before propagating | 1 h | P08-WP6-T2, P08-WP6-T3 | FR-READ-005 |
| P08-WP6-T5 | Toolbar full-screen button; icon switches between enter/exit states; tooltip updates | 1 h | P08-WP6-T4 | FR-READ-005 |
| P08-WP6-T6 | UI tests: enter full-screen hides chrome; Escape exits; toolbar button toggles correctly on both platforms | 2 h | P08-WP6-T5 | FR-READ-005 |

**WP6 exit:** full-screen works on Windows and macOS; Escape exits in all UI states.

---

## WP7 — Text Layer & In-Document Search

**Goal:** extract text layer via PdfPig; provide in-document search with
highlights and a page-list panel.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP7-T1 | `TextLayerService.ExtractAsync(bookId, pageIndex)`: call PdfPig; return `TextLayer` (list of `TextWord` with bounding box, text, confidence); stamp `ExtractionQuality` | 3 h | P08-WP1-T1 | FR-READ-006 |
| P08-WP7-T2 | Page-level text cache: store extracted text in sidecar `extracted-text/<bookId>/<pageIndex>.json`; invalidate on content hash change (Phase 05 identity) | 2 h | P08-WP7-T1, Phase 05 | FR-READ-006 |
| P08-WP7-T3 | `InDocumentSearchService.SearchAsync(bookId, query)`: search extracted text across all pages; return `SearchMatch[]` (page, word positions, context snippet) | 3 h | P08-WP7-T2 | FR-READ-006 |
| P08-WP7-T4 | Highlight overlay: render colored highlight rectangles over matched word bounding boxes on the current page; correct for current zoom and scroll | 3 h | P08-WP7-T3, WP5 | FR-READ-006 |
| P08-WP7-T5 | Search panel UI: search input field; match-count label; "Page N: context…" list; clicking a match navigates to that page | 2 h | P08-WP7-T4 | FR-READ-006 |
| P08-WP7-T6 | Keyboard: Ctrl+F opens search panel; F3 / Shift+F3 cycle through matches; Escape closes search panel and removes highlights | 2 h | P08-WP7-T5, WP6 | FR-READ-006, NFR-PROD-007 |
| P08-WP7-T7 | `ExtractionQuality.Scanned` pages show "No text layer — OCR available in V1" notice in search results | 1 h | P08-WP7-T1 | FR-READ-006 |
| P08-WP7-T8 | Tests: search finds exact phrase across pages; highlight positions match at 100%, 150%, 200% zoom; `Scanned` pages suppressed; keyboard shortcuts work | 4 h | P08-WP7-T6 | FR-READ-006 |

**WP7 exit:** in-document search highlights correct positions at all zoom levels;
keyboard Ctrl+F/F3 cycle works; `ExtractionQuality` flag populated for Phase 10.

---

## WP8 — UI, Icons, i18n, Accessibility

**Goal:** wire all premium icons; externalize all strings en + fr;
keyboard + screen-reader walkthrough passes.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP8-T1 | Create `reader.en.resx` and `reader.fr.resx`; externalize all reader strings (toolbar labels, tooltips, error/empty-state messages); run pseudolocale check | 3 h | Phase 03 i18n scaffold | I18N-STRATEGY.md |
| P08-WP8-T2 | Wire premium icon PNGs (or placeholders flagged `🟨`) for every reader toolbar button, zoom control, display-mode toggle, search panel button; register in `IconCatalog` | 3 h | Phase 03 icon system, icons.md | ICON-SYSTEM.md |
| P08-WP8-T3 | Implement Automation peers for `ReaderView`: page-number announcement on navigation; zoom-level announcement on change; search-match count announcement | 3 h | WP4, WP5, WP7 | NFR-PROD-008 |
| P08-WP8-T4 | Keyboard-only walkthrough: Tab order traverses toolbar, search panel, page-jump input; no focus trap; all actions operable without mouse | 2 h | WP4–WP7 | NFR-PROD-007 |
| P08-WP8-T5 | Contrast audit: toolbar chrome, highlight overlay, search panel — all text/icon combos meet WCAG 2.2 AA 4.5:1 (normal) or 3:1 (large) | 1 h | P08-WP8-T2 | NFR-PROD-008 |
| P08-WP8-T6 | Pseudolocale render: run reader view under `qps-ploc`; verify no truncation/overlap of toolbar labels or search panel text | 1 h | P08-WP8-T1 | I18N-STRATEGY.md |

**WP8 exit:** pseudolocale check clean; no hard-coded strings; all icons registered;
keyboard + SR walkthrough documented in testing.md passes.

---

## WP9 — Tests & Benchmarks

**Goal:** all nine test layers applied; golden-corpus suite passes;
performance benchmarks within budget.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P08-WP9-T1 | Architecture tests: `Architecture_Reader_DoesNotDependOnSearch`, `Architecture_Reader_DoesNotDependOnAI`, `Architecture_Reader_AccessesCatalogueOnlyViaContracts` | 2 h | all WPs | CONVENTIONS.md bounded-context discipline |
| P08-WP9-T2 | Performance benchmark suite (`ReaderBenchmarks.cs`): page-turn P95 over 100 turns, two-page-mode render, continuous-scroll virtualization frame time | 3 h | WP2, WP5 | NFR-OGMA-005, NFR-PROD-005 |
| P08-WP9-T3 | Memory-leak test: open/render/close 50 books in sequence; assert `GC.GetTotalMemory` remains below threshold after collection | 2 h | WP1, WP2 | NFR-PROD-006 |
| P08-WP9-T4 | Golden-corpus render regression: store reference bitmaps for page 1 of each fixture; CI compares pixel-by-pixel with 1% tolerance | 2 h | WP1 | FR-READ-001 |
| P08-WP9-T5 | End-to-end UI smoke test on both platforms: open → navigate → zoom → display-mode → full-screen → search → close; assert no crash | 3 h | all WPs | NFR-PROD-006 |
| P08-WP9-T6 | `dotnet format --verify-no-changes` + `dotnet build` (warnings-as-errors) + `dotnet test` in CI matrix (Windows + macOS) | 1 h | all WPs | Global DoD §3 |

**WP9 exit:** all tests green on both CI runners; benchmark P95 ≤ 100 ms.
