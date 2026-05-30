# Spike 3 — WebView ↔ C# bridge — RESULT

**Status:** ✅ **Bridge contract built and validated headlessly on .NET 10.**
WebView GUI round-trip (WebView2 / WKWebView) deferred to a desktop session.

## What was built

- `Bridge.cs` — the typed bridge contract feeding Phase 14:
  - `BridgeCommand` (C# → JS) and `BridgeEvent` (JS → C#) envelopes.
  - A **closed set** of inbound event types (`bookClicked`, `bookDoubleClicked`,
    `bookHovered`, `cameraChanged`) — anything else is rejected (**SI-3**).
  - `SceneModel`/`SceneBook` projection with textures referenced **only via the
    `ogma://` scheme**, never a raw file path (HLD §6.2).
  - `BridgeDispatcher` that validates and dispatches inbound JSON, rejecting
    malformed JSON and unknown types without throwing.
- `Program.cs` — a headless test exercising the dispatcher.
- `index.html` — the JS side showing both transports (WebView2
  `chrome.webview.postMessage`, WKWebView `webkit.messageHandlers`).

## Measured result (this dev box, .NET 10.0.101)

```
Spike 3 — bridge contract validation
  [PASS] bookClicked accepted
  [PASS] bookDoubleClicked accepted
  [PASS] bookHovered accepted
  [PASS] cameraChanged accepted
  [PASS] unknown type rejected          <- SI-3: arbitrary "evalArbitraryJs" refused
  [PASS] malformed json rejected
  [PASS] outbound setScene envelope well-formed   <- payload textures are ogma://
Result: 7 passed, 0 failed.   (exit 0)
```

Run with: `dotnet run -c Release` in `spikes/s03-webview-bridge/`.

## Conclusion (G1 evidence)

The **typed bridge contract is proven**: the validation logic that protects the
native side from untrusted WebView messages (SI-3) works, and the outbound scene
model serialises with safe `ogma://` asset references only. This satisfies the
*contract-definition* portion of beta gate **G1**.

The remaining G1 evidence — an actual message round-trip across the native
WebView boundary on **both** WebView2 (Windows) and WKWebView (macOS), incl.
keyboard-focus crossing the boundary — requires a desktop GUI session and is a
tracked item: **`TRACK-P01-S3-WEBVIEW-RUNTIME`** (Windows desktop session) and is
combined with the macOS run in Spike 4 / `TRACK-P01-S4-MACOS-FPS`.

## Risk note

`Avalonia.WebView` backends differ across platforms; the bridge keeps the
serialisation boundary narrow and typed precisely so the platform-specific
transport (the two `postMessage` shapes shown in `index.html`) is the only thing
that varies. No production code depends on this spike (throwaway).
