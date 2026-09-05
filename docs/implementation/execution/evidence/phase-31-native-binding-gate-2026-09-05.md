# Phase 31 native binding gate — 2026-09-05

## Disposition

The repository-level 3D bridge contract remains implemented and fail-safe, but
the native WebView runtime gate is **NOT ASSESSED**. A current implementation
attempt was evaluated against the official Avalonia native WebView package and
was deliberately not retained because the repository has no valid commercial
license key for that package.

## Current evidence

- `Avalonia.Controls.WebView` has a public 11.3.16 package compatible with the
  project’s Avalonia 11.x line and documents native WebView2 on Windows and
  WKWebView on macOS.
- The package restore succeeded, but the current project build failed closed
  with `AVLIC0001`: no valid AvaloniaUI license key was found for the required
  commercial product `Avalonia.Controls.WebView`.
- The package was removed before the change was accepted. The application
  therefore remains buildable on the existing dependency set and retains the
  accessible `NativeControlHost` fallback seam.
- The package’s public cross-platform abstraction does not currently provide
  custom-scheme response synthesis. The existing `ogma://` handler therefore
  cannot be claimed as physically integrated merely by adding the control.

## Required next evidence

This gate can close only after one of the following is evidenced and approved:

1. a licensed Avalonia WebView dependency and a tested adapter that preserves
   the `ogma://` authorization boundary; or
2. an independently supportable open-source/native adapter implementation for
   Windows WebView2 and macOS WKWebView.

After that dependency decision, physical Windows and macOS runs must prove
host attachment, local asset loading, bidirectional bridge messages, external
navigation rejection, reload/crash fallback, keyboard focus, and WebGL2
capability reporting. Headless contract tests do not close those gates.

## Source record

- Avalonia official embedding documentation:
  <https://docs.avaloniaui.net/docs/app-development/embedding-web-content>
- Avalonia official WebView repository:
  <https://github.com/AvaloniaUI/Avalonia.Controls.WebView>
- Package evaluated: `Avalonia.Controls.WebView` 11.3.16, evaluated 2026-09-05.
