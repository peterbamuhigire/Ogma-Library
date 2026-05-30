# Phase 14 — Test Plan

---

## 1. Test layers active

| Layer | Active | Notes |
| --- | --- | --- |
| Domain unit | No | No new domain types; `BookSceneItem` is a DTO |
| Infrastructure unit | Yes | `SpineTextureGenerator`, `OgmaSchemeHandler`, `InboundMessageValidator` |
| Integration | Yes | Bridge round-trip, fallback, book-detail parity |
| Architecture | Yes | Bounded-context chokepoint test |
| Performance | Yes | FPS benchmark (WP9); bridge latency benchmark |
| Accessibility | Yes | Keyboard boundary tests; fallback SR reachability |
| 3D fidelity | Manual | Visual inspection on reference hardware per Phase 21 design-audit gate |
| Security/Privacy | Yes | SI-3 tests; `ogma://` path-traversal test (R2 adjacent) |
| E2E | No | Phase 21; Phase 14 uses integration tests |

---

## 2. Security tests (highest priority)

These must pass with zero failures before the phase gate; path-traversal is R2-adjacent.

| Test | Oracle |
| --- | --- |
| `SchemeHandlerTest_PathTraversal_Returns403` | HTTP status 403; zero bytes of file content returned |
| `InboundMessage_Invalid_IsRejectedWithNoSideEffect` (BookId non-GUID) | `INavigationService` never called; no exception propagated |
| `InboundMessage_Unknown_Type_IsDiscarded` | No exception; warning logged; no domain action |
| `OgmaSchemeHandler_NeverServesOutsideSidecarRoot` | Parametric test over 10 traversal variants; all return 403 |

---

## 3. Functional tests

| Test | Oracle |
| --- | --- |
| `BookShelf3D_Click_NavigatesToSameBookDetail_As_GridView` | `INavigationService.NavigateToBookDetail(bookId)` called with matching BookId |
| `WebGL2Absent_FallbackView_ShowsFullCatalogueCapability` | `IsWebGl2Supported == false` → GridView visible; filter command reachable |
| `SpineTextureGenerator_ProducesValidPng` | Output is valid 128×512 PNG; SkiaSharp decode succeeds |
| `SpineTextureGenerator_LongTitle_Truncated` | Image has no pixel row entirely outside canvas bounds |
| `Bookshelf3DViewModel_BookClicked_NavigatesToCorrectBook` | Mock bridge fires `BookClicked("abc123")`; navigation asserted |

---

## 4. Performance gates

| Budget | Threshold | Test |
| --- | --- | --- |
| 3D render: 500 books | ≥ 60 FPS (mean over 5 s) | `ThreeJs_FPS_500Books_MeetsTarget` |
| Bridge round-trip latency | P95 < 50 ms | `Bridge_RoundTrip_Latency_P95_Under50ms` |
| Spine texture generation | P95 < 200 ms per texture | `SpineTextureGenerator_PerformanceTest` |

---

## 5. Fixtures

| Fixture | Source | Used by |
| --- | --- | --- |
| 500-book synthetic metadata corpus | Phase 02 seed | WP9 FPS benchmark (`SetScene` with 500 items) |
| `scanned-image-only` golden corpus | Phase 05 | Spine generation with no cover art (dominant color fallback) |
| `simple-text` golden corpus | Phase 05 | Spine generation with text metadata |
| Path-traversal URI variants (10 cases) | Handcrafted fixture JSON | `OgmaSchemeHandler_NeverServesOutsideSidecarRoot` |

---

## 6. Accessibility tests

| Surface | Test |
| --- | --- |
| 3D view keyboard boundary | Tab enters WebView; arrow keys move book focus; Enter opens book; Shift-Tab returns to native Avalonia control |
| Fallback grid view | SR: fallback banner announces "3D view not available"; full grid capability reachable via keyboard |
| Toolbar buttons | All icons have accessible labels; keyboard reachable; SR announces layout and camera actions |
| Reduced motion | Three.js layout transitions respect `prefers-reduced-motion: reduce` (animation disabled or instant) |

---

## 7. Manual tests (Phase 14)

These require real hardware and are performed before the phase DoD is cleared;
results are recorded in `docs/benchmarks/phase-14/manual-test-<date>.md`:

| Manual test | Platform | What to verify |
| --- | --- | --- |
| 3D spine texture visual quality | Windows + macOS | Spines are legible at normal zoom; title/author readable |
| 60 FPS on reference hardware with 500 books | Windows + macOS | OS-level frame rate monitor or `PerformanceOverlay` in DevTools |
| WKWebView WebGL2 on macOS 12 | macOS 12+ | 3D view loads without fallback |
| WebView2 runtime detection on clean Windows | Windows | Startup shows actionable install prompt if WebView2 missing |
| Keyboard navigation end-to-end | Windows + macOS | Tab in, arrow keys, Enter to open book, Shift-Tab out |

---

## 8. CI integration

- `dotnet test` covers all unit and integration tests.
- TypeScript build (`npx tsc --noEmit`) runs in CI before `dotnet test`.
- FPS benchmark runs via a Playwright-driven headless Chromium script in nightly
  CI; results compared against baseline; a regression > 10 FPS triggers a CI
  warning (not a hard failure, due to CI hardware variability).
- Architecture test `Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity`
  runs in `OgmaLibrary.ArchitectureTests` project on every PR.
- SI-3 and path-traversal tests tagged `[Category("Security")]`; run in a
  dedicated CI step with zero-tolerance.
