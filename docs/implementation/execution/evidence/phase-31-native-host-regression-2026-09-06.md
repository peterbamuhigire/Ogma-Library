# Phase 31 native-host regression evidence — 2026-09-06

## Finding

The first full-solution run after adding `Avalonia.Controls.WebView` failed one
of 159 UI tests. The headless `MainWindow` constructed the native WebView even
though the bookshelf view was not visible, and Avalonia's Windows WebView2
adapter raised `RPC_E_CHANGED_MODE` while attaching.

## Correction

- Replaced eager XAML construction with an empty `ContentControl` host.
- Native `NativeWebView` creation now occurs only when the bookshelf view is
  effectively visible and the platform composition supplied a host
  coordinator.
- Ancestor visibility changes trigger lazy activation when the user opens the
  3D bookshelf.
- Detachment cancels initialization, unsubscribes host-loss events, disposes
  the adapter and clears the host content.
- View-model-only and headless fallback tests do not construct a native control.

## Verification

```text
dotnet build src/OgmaLibrary.App/OgmaLibrary.App.csproj --configuration Release --no-restore -m:1
```

Result: **0 warnings, 0 errors**.

```text
dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal" -m:1
```

Result: **1,125 passed, 0 failed, 0 skipped** — 41 architecture, 925 core,
and 159 UI tests.

This closes the repository-level headless regression gate. It does not close
the physical WebView2/WKWebView, WebGL2, performance, crash/reload, keyboard,
or cross-platform acceptance gates recorded in the Phase 31 native-binding
evidence.
