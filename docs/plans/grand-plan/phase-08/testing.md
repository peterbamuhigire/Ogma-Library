# Phase 08 — Test Plan

This document covers all nine test layers for Phase 08 (PDF Reader Core).
Every table row maps a test to a requirement/NFR ID, a golden-corpus fixture
(where applicable), and the deterministic oracle.

---

## 1. Test layers in scope

| Layer | In scope this phase | Notes |
| --- | --- | --- |
| 1 — Domain unit | Yes — `ZoomCalculator`, `NavigationHistory`, `PageRenderCache` eviction logic | Pure logic; no I/O |
| 2 — Infrastructure unit | Yes — `PdfiumAdapter`, `TextLayerService`, `ReadingProgressService` | Requires native library; mocked in pure tests |
| 3 — PDF fixture | Yes — golden-corpus render regression; text-layer extraction accuracy | Reference bitmap oracle |
| 4 — Search unit | Partial — `InDocumentSearchService` against extracted text fixtures | No FTS5 yet |
| 5 — AI unit | No — Reader has no AI dependency | N/A |
| 6 — UI / component | Yes — `ReaderView`, toolbar, search panel, navigation, zoom, full-screen | Avalonia headless/UI test harness |
| 7 — 3D | No | N/A |
| 8 — Performance | Yes — page-turn P95 benchmark; continuous-scroll frame time; memory leak | BenchmarkDotNet + custom perf runner |
| 9 — Packaging | No — native lib packaging tested in Phase 22 | Note: dev ad-hoc signing tested in WP1 |
| Manual | Yes — keyboard walkthrough; screen-reader pass; macOS full-screen | Documented below |

---

## 2. Golden-corpus fixtures

All fixtures are version-pinned and hash-verified. Fixture names match the
golden-corpus catalogue defined in SOURCE-SUMMARY.md §J.

| Fixture | What is tested | Oracle |
| --- | --- | --- |
| `simple-text` | Render + text extraction; basic navigation | Reference bitmap page 1; extracted text matches known content |
| `scanned-image-only` | Render succeeds; `ExtractionQuality = Scanned`; search returns OCR notice | Bitmap within tolerance; quality flag = `Scanned` |
| `very-large-1000pp` | Render, navigation, and cache eviction on a 1,000+ page document | Page turn P95 ≤ 100 ms; no OOM; cache stays within budget |
| `two-column` | Text extraction correctly orders columns left-to-right | Extracted word order matches reference JSON |
| `rotated-pages` | Pages rendered at correct rotation; bounding boxes correct for highlights | Bitmap matches rotation; highlight position correct |
| `non-english` | Unicode text extracted correctly (e.g. French accented characters, CJK) | Extracted text round-trips through search without corruption |
| `embedded-toc` | TOC structure available; jump-to-page via TOC works | Navigation lands on correct page |
| `bad-metadata` | Document opens despite absent or malformed PDF metadata | No crash; page count correct |

---

## 3. Unit tests

### 3.1 Domain — `PageRenderCache`

| Test | Oracle |
| --- | --- |
| `PageRenderCache_EvictsLRU_WhenBudgetExceeded` | After inserting entries totalling > 256 MB, oldest entry is absent |
| `PageRenderCache_CancelsRender_WhenPageAbandoned` | Navigate away before render completes; `CancellationToken` is cancelled; no result stored |
| `PageRenderCache_ServesLowResThenHighRes_InOrder` | Low-res result arrives before high-res; high-res replaces it |
| `PageRenderCache_HitDoesNotRerender` | Second access to same key returns cached result without calling `IPdfRenderer` |

### 3.2 Domain — `NavigationHistory`

| Test | Oracle |
| --- | --- |
| `History_PushesOnExplicitNavigation` | `GoToPage(n)` appends to stack |
| `History_GoBack_PopsStack` | `GoBack()` moves to previous entry; does not push a new entry |
| `History_CapAt50Entries` | 51st entry evicts oldest |
| `History_GoForward_ReinstatesForwardStack` | After `GoBack`, `GoForward` returns to the page navigated away from |

### 3.3 Domain — `ZoomCalculator`

| Test | Oracle |
| --- | --- |
| `FitWidth_ReturnsContainerWidthDividedByPageWidth` | Deterministic arithmetic |
| `FitPage_ReturnsMinOfWidthAndHeightRatios` | Deterministic arithmetic |
| `Fixed_ClampedTo25_500Percent` | Values outside range are clamped |

### 3.4 Infrastructure — `PdfiumAdapter`

| Test | Fixture | Oracle |
| --- | --- | --- |
| `PdfiumAdapter_RenderPage_MatchesReference` | `simple-text` fixture page 1 | Pixel diff ≤ 1% of reference bitmap |
| `PdfiumAdapter_RotatedPage_RendersUpright` | `rotated-pages` fixture | Bitmap orientation matches expected rotation |
| `PdfiumAdapter_LargeDocument_DoesNotOOM` | `very-large-1000pp`, render 10 random pages | No `OutOfMemoryException`; process RSS < threshold |
| `PdfiumAdapter_Dispose_ReleasesNativeHandle` | Any fixture | `SafeHandle.IsClosed` after dispose; no crash on double-dispose |
| `PdfiumAdapter_MacOS_LoadsUniversalBinary` | CI: macOS runner | Native library loaded; `PageCount > 0` |

### 3.5 Infrastructure — `TextLayerService`

| Test | Fixture | Oracle |
| --- | --- | --- |
| `TextLayer_SimpleText_ExtractsWords` | `simple-text` | Word list matches reference JSON |
| `TextLayer_TwoColumn_CorrectReadingOrder` | `two-column` | Words ordered left-column-first |
| `TextLayer_ScannedPage_FlagsScanned` | `scanned-image-only` | `ExtractionQuality = Scanned` |
| `TextLayer_NonEnglish_PreservesUnicode` | `non-english` | UTF-8 round-trip intact |
| `TextLayer_CachedResult_LoadsFromSidecar` | `simple-text` (second call) | Sidecar JSON loaded; `IPdfPig` not called again |

### 3.6 Infrastructure — `ReadingProgressService`

| Test | Oracle |
| --- | --- |
| `Progress_SaveAndLoad_RoundTrips` | All fields identical after reload |
| `Progress_AbnormalTermination_DataIntact` | Kill DI scope without `CloseAsync`; reload restores last debounced save |
| `Progress_FirstOpen_UsesDefaults` | Page 0, scroll 0, `FitWidth`, `Single` |

---

## 4. Integration tests

| Test | ID | Oracle |
| --- | --- | --- |
| `ReaderSession_Resume_RestoresPageAndScroll` | FR-READ-001 | Open at page 42 scroll 0.35; close; reopen — page 42, scroll 0.35 |
| `ReaderSession_NavigateAll_KeyboardAndCommands` | FR-READ-002 | All six commands update page index correctly |
| `ZoomMode_FitWidth_PersistedAcrossReopen` | FR-READ-003 | Zoom mode and percent stored in `ReadingProgress`; match on reload |
| `DisplayMode_TwoPage_PersistedAcrossReopen` | FR-READ-004 | DisplayMode stored; match on reload |
| `FullScreen_Escape_Exits` | FR-READ-005 | `IsFullScreen` transitions: false → true → false on Escape |
| `TextSearch_FindsMatches_HighlightsCorrect` | FR-READ-006 | Query "introduct" finds page 1 match; bounding boxes within ±2 px at 100% zoom |
| `TextSearch_ZoomAdjusted_HighlightPositionCorrect` | FR-READ-006 | Highlights at 150% zoom scale proportionally from 100% reference |
| `TextSearch_ScannedPage_ShowsOcrNotice` | FR-READ-006 | `scanned-image-only` fixture returns `ExtractionQuality.Scanned` notice |
| `InDocSearch_LargeDoc_CompletesWithin2s` | FR-READ-006, NFR-PROD-005 | Search across `very-large-1000pp` completes < 2 s (async; no UI stall) |

---

## 5. Performance benchmarks

All benchmarks run with BenchmarkDotNet on both Windows (x64) and macOS
(arm64) CI runners. Results are stored as trend data; the P95 gate is a hard
CI assertion.

| Benchmark | ID | Gate | Method |
| --- | --- | --- | --- |
| `PageTurn_P95_Cached` | NFR-OGMA-005 | ≤ 100 ms P95 | 100 sequential page turns through `simple-text` fixture, cache warm |
| `PageTurn_P95_TwoPage` | NFR-OGMA-005 | ≤ 100 ms P95 | 50 two-page spread turns; both pages cached |
| `PageTurn_FirstUncached_P95` | NFR-PROD-005 | ≤ 500 ms P95 | First turn of 20 random pages, cache cold |
| `ContinuousScroll_FrameTime` | NFR-PROD-005 | No stall > 100 ms | Scroll 500 virtual pixels; measure UI-thread event loop time |
| `MemoryLeak_50Books` | NFR-PROD-006 | RSS delta < 50 MB after GC | Open, render 5 pages, close 50 books sequentially |

---

## 6. Fault-injection tests

The Reader is not a destructive-write context (ADR-0008 — no PDF write-back).
Fault injection focuses on progress persistence and native handle safety.

| Scenario | Simulated fault | Oracle |
| --- | --- | --- |
| Abnormal termination mid-read | Kill DI scope after page-3 navigation; do not flush | On reopen, last debounced progress save is restored (within 500 ms window) |
| PDFium handle leak | Allocate 100 documents; do not call `Dispose`; force GC | Finalizer reclaims native handles; no `AccessViolation` |
| Sidecar text-cache write failure | Mock `IFileSystem` to throw on sidecar write | `TextLayerService` degrades to in-memory extraction; no crash |

---

## 7. UI / accessibility tests

| Test | Tooling | Oracle |
| --- | --- | --- |
| `ReaderToolbar_KeyboardTab_TraverseAll` | Avalonia headless test | Tab key visits all toolbar buttons in order; no focus trap |
| `Navigation_KeyboardShortcuts_AllWork` | Avalonia headless test | Left/Right/Home/End/Ctrl+G/Alt+Left/Alt+Right all trigger correct commands |
| `FullScreen_KeyboardEscape_Exits` | Avalonia headless test | Escape key sets `IsFullScreen = false` |
| `Search_CtrlF_OpensPanel_F3_Cycles` | Avalonia headless test | Ctrl+F opens search panel; F3 cycles; Shift+F3 reverses |
| Screen-reader pass (manual) | Narrator (Windows) + VoiceOver (macOS) | Page number announced on navigation; zoom level announced on change; search match count announced |
| Contrast audit (manual / automated) | axe-style check | All icon+text combos ≥ 4.5:1 (normal) or ≥ 3:1 (large); highlight overlay ≥ 3:1 against page background |

---

## 8. Architecture tests

| Test | Oracle |
| --- | --- |
| `Architecture_Reader_DoesNotDependOnSearch` | No type in `OgmaLibrary.Reader` references `OgmaLibrary.Search.*` |
| `Architecture_Reader_DoesNotDependOnAI` | No type in `OgmaLibrary.Reader` references `OgmaLibrary.AI.*` |
| `Architecture_Reader_AccessesCatalogueOnlyViaContracts` | No EF Core `DbContext` usage in `OgmaLibrary.Reader`; only `IBookFileLocator`, `IReadingProgressRepository` contracts |
| `Architecture_IPdfRenderer_NotLeakedOutsideInfrastructure` | `PdfiumAdapter` is `internal` to `OgmaLibrary.Infrastructure`; only `IPdfRenderer` crosses the boundary |

---

## 9. Manual test checklist (gate before phase close)

- [ ] Open the `very-large-1000pp` fixture; navigate to page 750; close; reopen —
      confirm page 750 restored.
- [ ] On macOS: toggle full-screen using the green traffic-light button; confirm
      title bar hides; press Escape; confirm return to windowed mode.
- [ ] On Windows: toggle full-screen via toolbar button; confirm Escape exits.
- [ ] Enable two-page mode; confirm pages 2–3 are a spread; odd/even pairing
      correct.
- [ ] Run under VoiceOver (macOS): navigate three pages; confirm page number
      announced each time.
- [ ] Run under Narrator (Windows): open search panel; enter "the"; confirm
      "N matches found" announced.
- [ ] Change UI language to French; confirm all toolbar labels and tooltips
      are in French; confirm no English string is visible in the reader UI.
