# Phase 14 Evidence

Date started: 2026-06-01

## Current Status

The Phase 14 foundation is implemented locally. It adds the shared
WebView bridge contracts, testable WebView host adapter, WebView2/WKWebView
bridge facades, C# bridge message records, inbound parser, SI-3 inbound
validator, strict TypeScript mirror types for the Three.js scene boundary, and
the first `ogma://` asset scheme handler with traversal protection. The
SkiaSharp spine texture generator now produces 128x512 PNG textures with
adaptive text contrast and title truncation. The first strict TypeScript
Three.js scene controller now handles the bridge message union, creates an
instanced book mesh, posts WebGL2 status and interaction messages, and supports
shelf/grid layouts, theme changes, camera updates, pointer picking, and keyboard
selection.
`Bookshelf3DViewModel` now projects catalogue summaries into scene items, posts
`SetScene` and `SetLayout` bridge messages, reacts to WebGL2 fallback messages,
and navigates through the same book-detail navigation contract used by the
catalogue grid. The first Avalonia shell now hosts the future native WebView
area and renders an accessible fallback bookshelf list when WebGL2 is absent.

The platform-specific native WebView packages are not wired yet; the bridge is
ready for those adapters through `IWebViewHostAdapter`.
The web asset path is now local-only: `src/shelf3d` bundles Three.js into the
Bookshelf3D assembly asset folder, `index.html` uses a restrictive CSP, and
`Shelf3DAssetPublisher` copies the bootstrap and bundle into `ogma://assets/js/`
for runtime serving.
The WebView startup contract now includes navigation and a
`Shelf3DWebViewBootstrapper` that initializes the host adapter, registers the
scheme handler, publishes local web assets, and navigates to the local bootstrap
document.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~BridgeMessageTests` | Passed: 6 bridge serialization and SI-3 validation tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BridgeMessageTests\|FullyQualifiedName~Bookshelf3DViewModelTests"` | Passed: 11 bridge/bootstrap/view-model tests after rerun; earlier parallel attempt hit a transient Windows output-file lock |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OgmaSchemeHandlerTests` | Passed: 4 scheme-handler tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SpineTextureGeneratorTests` | Passed: 3 spine texture generator tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3DViewModelTests` | Passed: 4 Bookshelf3D view-model tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3DViewModelTests` | Passed: 5 Bookshelf3D view-model tests including localization/icon-path coverage |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ApplicationStartupTests` | Passed: 3 startup/DI tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3DViewRenderTests` | Passed: 1 Bookshelf3D fallback render test |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Bookshelf3DViewRenderTests\|FullyQualifiedName~IconCatalogPhase14Tests"` | Passed: 4 UI fallback/icon manifest tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity` | Passed: 1 Bookshelf3D bounded-context architecture test |
| `npx --yes -p typescript tsc --noEmit -p src\shelf3d\tsconfig.json` | Passed: strict TypeScript message contract check |
| `npm install` in `src\shelf3d` | Passed: installed TypeScript, Three.js, and type declarations; 0 vulnerabilities |
| `npm run typecheck` in `src\shelf3d` | Passed: strict TypeScript scene and message contract check |
| `npm run build` in `src\shelf3d` | Passed: bundled local Three.js shelf to `src\OgmaLibrary.Bookshelf3D\Assets\Web\shelf3d.js` |
| `npm run perf:budget` in `src\shelf3d` | Passed: 500-book layout budget; shelf p95 0.079 ms, max 0.571 ms; grid3d p95 0.081 ms, max 0.281 ms |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OgmaSchemeHandlerTests` | Passed: 6 scheme/publisher tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore` | Passed: 388 tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 23 tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore` | Passed: 109 tests on third full run; first two full runs exposed unrelated existing timing flakes in search/reader perf tests |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| WebView bridge contract | `IWebViewBridge` defines initialization, typed outbound post, script execution, scheme registration, and validated inbound messages |
| WebView host seam | `IWebViewHostAdapter` hides WebView2/WKWebView APIs from shared bridge code and enables headless tests |
| Platform facades | `WebView2Bridge` and `WKWebViewBridge` share serialization and validation behavior through `WebViewBridgeBase` |
| Scheme contract | `ISchemeHandler` and `SchemeResponse` define the future `ogma://` handler boundary |
| Outbound messages | `SetScene`, `UpdateBook`, `RemoveBook`, `SetCamera`, `SetTheme`, and `SetLayout` message records serialize to the JS contract |
| Inbound messages | `BookClicked`, `BookDoubleClicked`, `BookHovered`, `CameraChanged`, `WebGl2Status`, and `PerformanceWarning` records parse from JS |
| SI-3 validation | `InboundMessageValidator` rejects invalid local book ids, non-finite/out-of-bounds cameras, non-finite FPS warnings, and unknown message types before dispatch |
| TypeScript contract | `src/shelf3d/src/messages.ts` mirrors the C# bridge contract as strict discriminated unions |
| Architecture gate | `Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity` guards the 3D bounded context from catalogue infrastructure coupling |
| Asset scheme handler | `OgmaSchemeHandler` serves only `ogma://assets/<class>/<filename>` from the sidecar asset root |
| Path traversal guard | Traversal attempts such as `ogma://assets/covers/../../secrets.db` return 403 instead of leaking filesystem paths |
| MIME types | PNG, JPEG, JavaScript, and JSON assets return explicit content types |
| Spine texture generator | `SpineTextureGenerator` renders 128x512 PNG textures from title, author, and dominant background color |
| Spine readability | The generator chooses light/dark text based on background luminance and truncates long title/author text to fit the texture |
| TypeScript package | `src/shelf3d/package.json` defines the local Three.js/TypeScript toolchain and strict typecheck script |
| Three.js scene controller | `src/shelf3d/src/scene.ts` initializes renderer/camera/orbit controls, reports WebGL2 status, and handles typed C# messages |
| Instanced book mesh | `Shelf3DScene` uses `THREE.InstancedMesh` with shared book geometry for the 500-book performance path |
| Interaction bridge | Pointer click, double-click, hover, camera changes, and keyboard Enter post typed inbound messages back to C# |
| Bookshelf3D view model | `Bookshelf3DViewModel` loads up to 500 active catalogue summaries and posts a `SetSceneMessage` to JavaScript |
| Navigation parity | Validated `BookClicked` / `BookDoubleClicked` inbound messages call `IBookDetailNavigationService.OpenDetailAsync` |
| Fallback state | `WebGl2Status(false)` flips `IsWebGl2Supported` and exposes `IsFallbackVisible` for the upcoming Avalonia fallback view |
| Composition root | `IWebViewBridge` is registered per platform facade and `Bookshelf3DViewModel` is registered as transient |
| Bookshelf3D view | `Bookshelf3DView` defines the toolbar, native host placeholder, and accessible fallback list |
| Bookshelf3D render test | `Bookshelf3DViewRenderTests` headless-renders the fallback catalogue path |
| i18n | Bookshelf3D toolbar, fallback banner, host labels, and layout labels now resolve through `ILocalizationService` in English and French |
| Icons | Phase 14 icon manifest keys are registered in `IconCatalog`, backed by SVG assets, and covered by UI tests |
| Local web bootstrap | `Assets/Web/index.html` loads only local `shelf3d.js` under a restrictive CSP |
| Three.js bundle | `npm run build` emits the offline bundle into the Bookshelf3D project for publish-time copy |
| Asset publisher | `Shelf3DAssetPublisher` copies `index.html` and `shelf3d.js` into the `js` asset class served by `ogma://` |
| WebView bootstrapper | `Shelf3DWebViewBootstrapper` initializes the adapter, registers `ogma://`, publishes local web assets, and navigates to `ogma://assets/js/index.html` |
| Performance telemetry | `Shelf3DScene` posts a typed `PerformanceWarning` when sustained FPS drops below the warning threshold |
| CI performance budget | `npm run perf:budget` benchmarks 500-book shelf/grid layout calculations against a 16.67 ms frame budget |
| Benchmark baseline | `docs/benchmarks/phase-14/layout-baseline-20260601.json` records the 500-book layout budget result |
| ADR note | `docs/adrs/0003-phase14-implementation-notes.md` records the Phase 14 ADR-0003 implementation notes |

## Remaining Phase 14 Work

- WP1 native adapter binding: concrete WebView2/WKWebView package binding is deferred to Phase 22 packaging; the shared adapter contract and bootstrap/navigation sequence are implemented and tested.
- WP2 native bridge registration of the `ogma://` handler is implemented at the shared bootstrapper level; concrete platform adapter registration remains behind `IWebViewHostAdapter`.
- WP4 worker integration/cache invalidation remains: generated textures are tested but not yet wired into the ingestion/update pipeline.
- WP5 texture atlas and visual smoke verification remain; layout-budget benchmark and FPS warning telemetry are implemented.
- WP6 reader-open parity for double-click remains pending until the reader navigation command is integrated with the 3D shell.
- WP3 full side-effect integration test against the ViewModel stack after WP6 exists.
- Remote CI evidence remains to be recorded after push.
