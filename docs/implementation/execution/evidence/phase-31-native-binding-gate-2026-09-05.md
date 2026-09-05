# Phase 31 native binding gate — 2026-09-05

## Disposition

The native host implementation slice is now present and buildable. The
remaining WebView2/WKWebView rendering, capability, performance, crash/reload,
keyboard-focus, and physical cross-platform gates are **NOT ASSESSED** until
the application is run on the reference Windows and macOS environments.

## Implemented

- Added the MIT-licensed `Avalonia.Controls.WebView` 11.4.0 dependency for the
  .NET 10 Avalonia desktop application.
- Replaced the 3D `NativeControlHost` placeholder with Avalonia's
  `NativeWebView` control.
- Added `NativeWebViewHostAdapter`, which maps native script execution and
  `WebMessageReceived` events onto `IWebViewHostAdapter`.
- Added loopback-only, random-token asset serving. Every request is converted
  back to an `ogma://assets/...` URI and remains authorized by
  `OgmaSchemeHandler`; no filesystem path is exposed to the browser.
- Rewrites outbound asset references to the token-scoped loopback origin and
  blocks navigation outside that origin. New-window requests are handled and
  suppressed.
- Added `Shelf3DHostCoordinator` and composition wiring so view attachment
  initializes the bridge, publishes the verified bundle, registers the asset
  boundary, and navigates the WebView.
- Disposes the host/server when the view leaves the visual tree, preserving the
  existing 2D fallback on initialization failure.
- Converts non-cancellation script/host failures during scene load, layout, or
  focus operations into the same accessible fallback rather than failing the
  catalogue shell.

## Verification

```text
dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release --no-restore -m:1
```

Result: **0 warnings, 0 errors**.

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BridgeMessageTests|FullyQualifiedName~Bookshelf3DViewModelTests|FullyQualifiedName~OgmaSchemeHandlerTests|FullyQualifiedName~ApplicationStartupTests|FullyQualifiedName~Phase02CompositionTests" --logger "console;verbosity=minimal" -m:1
```

Result: **37 passed, 0 failed, 0 skipped**.

```text
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~Bookshelf3DViewRenderTests" --logger "console;verbosity=minimal" -m:1
```

Result: **1 passed, 0 failed, 0 skipped**. This remains headless fallback
evidence, not proof of a native WebView render.

## Remaining evidence

Physical Windows WebView2 and macOS WKWebView runs must still prove packaged
scene rendering, WebGL2 capability reporting, bidirectional messages, local
asset loading, external-navigation rejection, reload/crash fallback, keyboard
focus, and startup/performance behavior. The package and source compile are
not substitutes for those runs.

## Source record

- Avalonia official embedding documentation:
  <https://docs.avaloniaui.net/docs/app-development/embedding-web-content>
- Avalonia official WebView repository:
  <https://github.com/AvaloniaUI/Avalonia.Controls.WebView>
- Package evaluated and selected: `Avalonia.Controls.WebView` 11.4.0, MIT,
  evaluated 2026-09-05.
