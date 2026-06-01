# Phase 14 Evidence

Date started: 2026-06-01

## Current Status

The first Phase 14 foundation slice is implemented locally. It adds the shared
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
catalogue grid.

The platform-specific native WebView packages are not wired yet; the bridge is
ready for those adapters through `IWebViewHostAdapter`.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~BridgeMessageTests` | Passed: 6 bridge serialization and SI-3 validation tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OgmaSchemeHandlerTests` | Passed: 4 scheme-handler tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SpineTextureGeneratorTests` | Passed: 3 spine texture generator tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3DViewModelTests` | Passed: 4 Bookshelf3D view-model tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ApplicationStartupTests` | Passed: 3 startup/DI tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter FullyQualifiedName~Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity` | Passed: 1 Bookshelf3D bounded-context architecture test |
| `npx --yes -p typescript tsc --noEmit -p src\shelf3d\tsconfig.json` | Passed: strict TypeScript message contract check |
| `npm install` in `src\shelf3d` | Passed: installed TypeScript, Three.js, and type declarations; 0 vulnerabilities |
| `npm run typecheck` in `src\shelf3d` | Passed: strict TypeScript scene and message contract check |

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

## Remaining Phase 14 Work

- WP1 native adapter binding: plug real WebView2/WKWebView controls into `IWebViewHostAdapter` and register platform DI.
- WP2 native bridge registration of the `ogma://` handler remains for the WebView initialization slice.
- WP4 worker integration/cache invalidation remains: generated textures are tested but not yet wired into the ingestion/update pipeline.
- WP5 texture atlas, bundled output, FPS telemetry/baseline, and visual smoke verification remain.
- WP6 reader-open parity for double-click remains pending until the reader navigation command is integrated with the 3D shell.
- WP3 full side-effect integration test against the ViewModel stack after WP6 exists.
- WP4-WP9 spine textures, Three.js scene, view model/view, fallback, performance benchmarks, review, and remote CI evidence.
