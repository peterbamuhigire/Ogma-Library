# ADR-0003 Phase 14 Implementation Notes

Date: 2026-06-01

Phase 14 confirms the ADR-0003 bridge architecture and implements the production
foundation for the WebView-hosted Three.js shelf:

- C# and TypeScript discriminated bridge message unions for scene, layout,
  theme, camera, interaction, WebGL2 status, and performance warnings.
- Validated inbound dispatch that rejects malformed book ids, unknown inbound
  message types, non-finite camera values, and non-finite FPS warnings before
  any application side effect.
- A local-only `ogma://assets/<class>/<file>` scheme handler with traversal
  protection and explicit MIME types.
- A bundled Three.js scene loaded from local app assets without CDN or network
  egress.
- `Bookshelf3DViewModel` and `Bookshelf3DView` with localized toolbar,
  accessible fallback, book-detail parity for click/double-click, Phase 14 icon
  manifest coverage, and a headless fallback render test.
- A deterministic 500-book layout budget (`npm run perf:budget`) with baseline
  recorded in `docs/benchmarks/phase-14/layout-baseline-20260601.json`.

Native WebView package binding remains isolated behind `IWebViewHostAdapter`.
Current Avalonia documentation points to a new `NativeWebView`/XPF WebView path,
while the older community `WebView.Avalonia` NuGet package remains available but
is stale relative to Ogma's Avalonia 11.3.17 baseline. The shared bootstrapper
now performs initialize/register/publish/navigate; concrete platform package
binding and WebView2 runtime packaging stay with Phase 22 packaging.
