# Phase 14 — Tasks

---

## WP1 — Bridge Abstraction

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP1-T1 | Define `IWebViewBridge` interface with XML doc; methods: `InitializeAsync`, `PostMessageAsync`, `ExecuteScriptAsync`, `RegisterSchemeHandlerAsync`; event `MessageReceived` | Phase 02 | 1 h | ADR-0003, HLD §6 |
| P14-WP1-T2 | Define `ISchemeHandler` interface: `CanHandle(Uri) -> bool`, `HandleAsync(Uri) -> Task<SchemeResponse>` | P14-WP1-T1 | 0.5 h | `ogma://` scheme |
| P14-WP1-T3 | Implement `WebView2Bridge` (Windows): wrap `Microsoft.Web.WebView2.Avalonia.WebView2`; `InitializeAsync` calls `EnsureCoreWebView2Async`; register scheme via `AddWebResourceRequestedFilter`; post messages via `PostWebMessageAsJsonAsync`; receive via `WebMessageReceived` event | P14-WP1-T1 | 3 h | Windows platform |
| P14-WP1-T4 | Implement `WKWebViewBridge` (macOS): wrap Avalonia's WebView or a custom NSView/WKWebView interop; register `ogma://` via `WKURLSchemeHandler` subclass; post messages via `evaluateJavaScript:completionHandler:`; receive via `WKScriptMessageHandler` | P14-WP1-T1 | 3.5 h | macOS platform |
| P14-WP1-T5 | Register both bridge implementations in DI composition root via `RuntimeInformation.IsOSPlatform` guard; bind `IWebViewBridge` to the correct impl | P14-WP1-T3..T4 | 0.5 h | architecture |
| P14-WP1-T6 | Unit test `WebView2Bridge_PostMessage_SerializesCorrectly` and `WKWebViewBridge_PostMessage_SerializesCorrectly` using fake/mock WebView2/WKWebView; assert JSON matches expected schema | P14-WP1-T3..T4 | 2 h | bridge correctness |

---

## WP2 — `ogma://` Scheme Handler

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP2-T1 | Implement `OgmaSchemeHandler` implementing `ISchemeHandler`; resolves `ogma://assets/<class>/<encoded-name>` against sidecar root; strict containment check: `resolvedPath.StartsWith(sidecareRoot)` — returns 403 if not | P14-WP1-T2 | 2 h | SI-3, `ogma://` |
| P14-WP2-T2 | Register `OgmaSchemeHandler` with both bridge implementations via `RegisterSchemeHandlerAsync` in `Bookshelf3DView.InitializeAsync` | P14-WP2-T1, P14-WP1-T3..T4 | 0.5 h | SI-3 |
| P14-WP2-T3 | Set correct MIME type in response (`image/png` for PNG spines/covers; `application/javascript` for JS bundle) | P14-WP2-T1 | 0.5 h | WebView loading |
| P14-WP2-T4 | Integration test `SchemeHandlerTest_ValidUri_ReturnsImageBytes`: assert bytes match the file content | P14-WP2-T1 | 1 h | `ogma://` |
| P14-WP2-T5 | Integration test `SchemeHandlerTest_PathTraversal_Returns403`: URI `ogma://assets/covers/../../secrets.db` must return 403 | P14-WP2-T1 | 1 h | SI-3 R2 test |
| P14-WP2-T6 | Integration test `SchemeHandlerTest_UnknownAssetClass_Returns404` | P14-WP2-T1 | 0.5 h | robustness |

---

## WP3 — Message Types & Validation

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP3-T1 | Define C# inbound/outbound message record hierarchy (abstract base + concrete records per README §7.3); add `System.Text.Json` attributes for polymorphic serialization via `type` discriminator | Phase 02 | 1.5 h | HLD §6 |
| P14-WP3-T2 | Implement `InboundMessageValidator`: validates `BookId` (GUID regex), `CameraState` (finite floats, scene-bounds range); returns `ValidationResult`; unknown `Type` → log + discard (no exception) | P14-WP3-T1 | 1.5 h | SI-3 |
| P14-WP3-T3 | Define TypeScript message types in `src/shelf3d/src/messages.ts` (manually authored to match C# records; in Phase 23 a code-gen tool may replace this); include discriminant `type` field on all inbound messages | P14-WP3-T1 | 1 h | typed bridge |
| P14-WP3-T4 | Unit test `InboundMessageValidator_BookId_InvalidGuid_Rejected` | P14-WP3-T2 | 0.5 h | SI-3 |
| P14-WP3-T5 | Unit test `InboundMessageValidator_CameraState_NonFiniteFloat_Rejected` | P14-WP3-T2 | 0.5 h | SI-3 |
| P14-WP3-T6 | Unit test `InboundMessageValidator_UnknownType_ReturnsDiscardResult_NoException` | P14-WP3-T2 | 0.5 h | SI-3 |
| P14-WP3-T7 | Integration test `InboundMessage_Invalid_IsRejectedWithNoSideEffect`: mock bridge fires invalid message; assert `IBookSelectionService.SelectBook` is never called | P14-WP3-T2 | 1 h | SI-3 |

---

## WP4 — Spine Texture Generation

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP4-T1 | Implement `SpineTextureGenerator` (SkiaSharp): draws 128×512 px PNG; dominant-color background (from Phase 05 cover thumbnail); title bold max 2 lines; author 1 line; contrast-adaptive text color (WCAG 4.5:1 for the typography, even though this is a texture, for readability) | Phase 05 | 3 h | HLD §6, visual quality |
| P14-WP4-T2 | Integrate `SpineTextureGenerator` into Phase 05 background job: generate spine on ingest and on metadata update; cache to `sidecar/spines/<bookId>.png`; invalidate on title/author/dominant-color change | Phase 05 job infra | 1.5 h | HLD §6 |
| P14-WP4-T3 | Unit test `SpineTextureGenerator_ProducesValidPng`: assert output is a valid 128×512 PNG; SkiaSharp image properties check | P14-WP4-T1 | 1 h | spine generation |
| P14-WP4-T4 | Unit test `SpineTextureGenerator_LongTitle_Truncated`: title > 2 lines rendered with ellipsis; no overflow clipping outside canvas | P14-WP4-T1 | 0.5 h | visual robustness |
| P14-WP4-T5 | Unit test `SpineTextureGenerator_ContrastColor_IsAdaptive`: dark background → white text; light background → dark text | P14-WP4-T1 | 0.5 h | readability |

---

## WP5 — Three.js Scene

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP5-T1 | Initialize Three.js scene (`WebGLRenderer`, `Scene`, `PerspectiveCamera`); check `webgl2` context at init; post `WebGl2StatusMessage(supported: bool)` back to C# | P14-WP3-T3 | 2 h | DEP-3, HLD §6 |
| P14-WP5-T2 | `InstancedMesh` book objects: shared `BoxGeometry(0.025, 0.18, 0.13)` (spine proportions); per-instance transform (position, rotation); per-instance spine texture via texture atlas or dynamic texture slot | P14-WP5-T1 | 4 h | NFR-OGMA-006 |
| P14-WP5-T3 | Shelf layout (`SetLayout("shelf")`): arrange books in rows of configurable shelf-width; row Y offset; slight random rotation for realism; animated transition from current positions using `GSAP` or `Tween.js` | P14-WP5-T2 | 2 h | FR-CAT-001 |
| P14-WP5-T4 | Grid3D layout (`SetLayout("grid3d")`): isometric-style grid; all books face forward; clean mathematical arrangement | P14-WP5-T2 | 1.5 h | FR-CAT-001 |
| P14-WP5-T5 | `OrbitControls`: mouse drag (orbit), scroll (zoom), right-drag (pan); fire `CameraChanged` on significant change (position delta > 0.05 world units), throttled to 1 Hz | P14-WP5-T1 | 1.5 h | camera |
| P14-WP5-T6 | Raycaster interaction: `pointerdown` → `BookClicked`; `dblclick` → `BookDoubleClicked`; `pointermove` (throttled 30 Hz) → `BookHovered` if hit changes | P14-WP5-T2 | 1.5 h | FR-CAT-001, SI-3 |
| P14-WP5-T7 | Keyboard navigation: `keydown` handler on document; Arrow keys move focus cursor between books (wrap at edges); Enter fires `BookDoubleClicked`; visible focus ring on focused book object | P14-WP5-T6 | 2 h | WCAG 2.2, keyboard nav |
| P14-WP5-T8 | `SetTheme(themeKey)` handler: `"light"` → warm parchment background, warm ambient; `"dark"` → walnut background, dim ambient | P14-WP5-T1 | 1 h | design tokens |
| P14-WP5-T9 | Handle `SetScene`, `UpdateBook`, `RemoveBook` messages: full scene rebuild on `SetScene`; incremental update on `UpdateBook` (update instance transform + texture); remove instance on `RemoveBook` | P14-WP5-T2 | 2 h | bridge correctness |
| P14-WP5-T10 | `requestAnimationFrame` FPS counter: track rolling 60-frame average; if < 30 FPS for 5 s, post `PerformanceWarning` back to C# | P14-WP5-T1 | 1 h | NFR-OGMA-006 monitoring |
| P14-WP5-T11 | TypeScript build: `tsconfig.json`; `esbuild` or `rollup` bundle to `dist/shelf3d.js`; committed to `src/OgmaLibrary.Bookshelf3D/Assets/`; loaded via `ogma://assets/js/shelf3d.js` | P14-WP5-T1..T10 | 1 h | build pipeline |

---

## WP6 — Bookshelf3DViewModel

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP6-T1 | `Bookshelf3DViewModel`: exposes `IsWebGl2Supported` (bool, default true), `IsLoading`, `CurrentLayout` (enum), `Books` (ObservableCollection of `BookSceneItem`) | Phase 06 | 2 h | FR-CAT-001 |
| P14-WP6-T2 | `LoadAsync()`: queries catalogue for all books; builds `BookSceneItem[]`; sends `SetScene` message via bridge | P14-WP6-T1, P14-WP1-T1 | 1.5 h | FR-CAT-001 |
| P14-WP6-T3 | Handle `BookClicked` from bridge: navigate to book-detail route using the same `INavigationService.NavigateToBookDetail(BookId)` used by grid/list views | P14-WP6-T1 | 1 h | FR-CAT-001 parity |
| P14-WP6-T4 | Handle `BookDoubleClicked`: open the book in the reader (same action as double-click in grid view) | P14-WP6-T3 | 0.5 h | FR-CAT-001 parity |
| P14-WP6-T5 | `SetLayoutCommand`: sends `SetLayout` message; persists preference in settings | P14-WP6-T1 | 0.5 h | layout |
| P14-WP6-T6 | Handle `WebGl2StatusMessage` inbound: set `IsWebGl2Supported = false` → triggers fallback in View | P14-WP6-T1 | 0.5 h | DEP-3, UI-3 |
| P14-WP6-T7 | Unit test `Bookshelf3DViewModel_BookClicked_NavigatesToCorrectBook`: assert `INavigationService.NavigateToBookDetail` called with correct BookId | P14-WP6-T3 | 1 h | FR-CAT-001 |

---

## WP7 — Bookshelf3DView Avalonia

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP7-T1 | `Bookshelf3DView.axaml`: `NativeControlHost` hosting the WebView; `IsVisible` bound to `IsWebGl2Supported`; fallback `GridView` shown when `!IsWebGl2Supported` | P14-WP6-T1 | 2 h | HLD §6.4, UI-3 |
| P14-WP7-T2 | Toolbar row above WebView: `ic_shelf3d_layout_shelf` / `ic_shelf3d_layout_grid3d` toggle buttons; `ic_shelf3d_camera_reset` button; `ic_shelf3d_toggle` (enable/disable 3D); all with accessible labels | P14-WP7-T1 | 1.5 h | i18n, a11y |
| P14-WP7-T3 | WebView initialization sequence in code-behind: call `IWebViewBridge.InitializeAsync`; register `OgmaSchemeHandler`; load `ogma://assets/js/shelf3d.js`; await WebGL2 capability check result | P14-WP1, P14-WP2 | 1.5 h | bridge init |
| P14-WP7-T4 | Keyboard boundary: `NativeControlHost` gets `TabIndex`; `KeyDown` with Tab/Shift-Tab triggers `FocusHelper.MoveToNextAvaloniaControl()` / `MoveToPreviousAvaloniaControl()` | P14-WP7-T1 | 1.5 h | keyboard nav |
| P14-WP7-T5 | Theme sync: subscribe to Avalonia `ActualThemeVariant`; send `SetTheme` on change | P14-WP7-T1 | 0.5 h | design tokens |
| P14-WP7-T6 | Fallback banner: shown over grid view when `!IsWebGl2Supported`; text key `shelf3d.fallback.message`; `ic_shelf3d_unavailable` icon; dismissable | P14-WP7-T1 | 0.5 h | UI-3 |
| P14-WP7-T7 | Externalize all strings en + fr; wire all icons from `icons.md` | P14-WP7-T2..T6 | 1 h | i18n, a11y |

---

## WP8 — Architecture & Fallback Tests

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP8-T1 | Architecture test `Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity`: assert `Bookshelf3D` project types do not reference `Domain.Catalogue.BookIdentity` or `Infrastructure.Catalogue` directly | All WPs | 1 h | bounded context |
| P14-WP8-T2 | Integration test `BookShelf3D_Click_NavigatesToSameBookDetail_As_GridView`: mock bridge; fire `BookClicked(bookId)`; assert navigation service called with same route as grid view click | P14-WP6-T3..T4 | 1.5 h | FR-CAT-001 |
| P14-WP8-T3 | Integration test `WebGL2Absent_FallbackView_ShowsFullCatalogueCapability`: mock bridge returns WebGL2 unsupported; assert grid view is visible; assert filter/sort/book-detail all reachable | P14-WP7-T1 | 1 h | UI-3, DEP-3 |
| P14-WP8-T4 | Integration test `InboundMessage_Invalid_IsRejectedWithNoSideEffect` (from WP3; wired here to full ViewModel stack) | P14-WP3-T2, P14-WP6 | 1 h | SI-3 |

---

## WP9 — Performance Benchmark

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P14-WP9-T1 | Build FPS measurement harness: headless Three.js scene instantiated in a Node.js environment (or a Playwright-driven Chromium headless run); load `SetScene` with 500 `BookSceneItem` objects; measure `requestAnimationFrame` rate for 5 s | P14-WP5-T11 | 2 h | NFR-OGMA-006 |
| P14-WP9-T2 | CI fixture `ThreeJs_FPS_500Books_MeetsTarget`: run harness; assert mean FPS ≥ 60 (or record as trend if headless measurement differs from real hardware; annotate with reference hardware note) | P14-WP9-T1 | 1 h | NFR-OGMA-006 |
| P14-WP9-T3 | Bridge round-trip latency benchmark: measure time from `PostMessageAsync(BookClicked)` to ViewModel handler invocation; assert P95 < 50 ms | P14-WP6-T3 | 1 h | interaction responsiveness |
| P14-WP9-T4 | Record benchmark baseline in `docs/benchmarks/phase-14/fps-baseline-<date>.json` | P14-WP9-T1..T3 | 0.5 h | NFR-OGMA-006 trend |
