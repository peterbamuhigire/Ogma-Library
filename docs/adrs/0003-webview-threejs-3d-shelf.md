# ADR-0003: Render the 3D Shelf with WebView-Hosted Three.js Behind a Spike Gate

## Status

Accepted (decision ratified; wrapper/threshold pending Phase 01 spike amendment)

> Ratified in Phase 00 by the project owner, 2026-05-30. The spike-gate outcome
> (macOS WKWebView acceptance thresholds) is recorded as an amendment below when
> the Phase 01 spike concludes.

## Date

2026-05-30

## Context

The immersive 3D shelf is a signature browsing mode for Ogma Library: a hardware-accelerated, navigable arrangement of book spines and covers that aids discovery and memory. The product principle holds that "3D is functional, not gimmick" and that grid and list views remain first-class, which means the 3D shelf must be valuable but never load-bearing. The Avalonia shell (ADR-0002) provides no native 3D scene graph. Two paths exist: build a native 3D renderer bound to the platform graphics APIs, or host a web rendering surface running an established WebGL engine. The web path carries a known platform risk — macOS WebView is WKWebView, whose WebGL performance, memory behaviour, and C#-to-JavaScript bridging must be proven before the team commits. Because the 3D shelf is an enhancement rather than a core promise, its rendering technology must be gated by a Phase 0 spike with a defined fallback.

## Decision Drivers

- **Reuse of a mature, well-documented 3D engine** rather than a hand-built renderer.
- **Acceptable performance and memory behaviour on macOS WKWebView**, the highest-risk platform.
- **A reliable C#-to-JavaScript bridge** for selection, navigation, and catalogue data.
- **A graceful fallback** to first-class grid and list views if the spike fails.
- **Minimal coupling** so the 3D surface can be swapped or removed without disturbing the shell.

## Considered Options

### Option A — WebView-hosted Three.js with a C#-to-JavaScript bridge

- **Pros:** Three.js is a mature, widely supported WebGL engine; the embedded WebView already exists in the Avalonia shell; rapid iteration on the scene; the bridge isolates the 3D surface from the rest of the application.
- **Cons:** macOS WKWebView WebGL performance and memory are unproven for this scene and must be spike-validated; the bridge adds a serialisation boundary; a web surface inside a native shell adds one technology to maintain.

### Option B — Native 3D renderer bound to platform graphics APIs

- **Pros:** maximal control over performance and native integration; no WebView dependency.
- **Cons:** a hand-built cross-platform 3D renderer is high-cost and high-risk for a small team and disproportionate to an enhancement feature; long lead time before the shelf is usable.

### Option C — No 3D shelf; grid and list only

- **Pros:** zero 3D risk; lowest cost.
- **Cons:** drops a signature differentiator that the vision treats as a core way to make the library visible.

## Decision Outcome

Adopt WebView-hosted Three.js with a C#-to-JavaScript bridge for the 3D shelf, hosted inside the Avalonia WebView surface from ADR-0002. The decision is spike-gated in Phase 0: a time-boxed spike must demonstrate acceptable frame rate and memory on macOS WKWebView with a representative shelf, and a working bridge for selection, navigation, and catalogue binding. If the spike fails its acceptance thresholds, the 3D shelf falls back to the first-class grid and list views, and this ADR is amended to record the spike outcome. The grid and list views ship regardless of the spike result, so no core browsing capability depends on the 3D surface.

## Consequences

### Positive

- A mature 3D engine is reused rather than hand-built, keeping the enhancement proportionate to its value.
- The bridge boundary isolates the 3D surface, so it can be swapped or removed without disturbing the shell.

### Negative

- The macOS WKWebView risk must be retired by a Phase 0 spike before any 3D commitment; an unmet threshold triggers the grid/list fallback.
- The C#-to-JavaScript bridge introduces a serialisation boundary that must be kept narrow and well-typed.

### Affects

- ADR-0002 (hosts the WebView surface); the Phase 0 risk-spike backlog (the 3D shelf spike is a named entry); grid and list views, which are unconditionally first-class.

---

## Amendment Log

_This section is completed when the Phase 01 spike concludes. Record: spike date, acceptance thresholds tested (frame rate, memory, bridge round-trip), outcome (pass / fail / conditional), and any constraint imposed on the Three.js scene or bridge API as a result._

| Date | Spike outcome | Thresholds | Notes |
|------|---------------|------------|-------|
| 2026-05-30 | **Bridge contract PASS (7/7); FPS deferred to macOS** | bridge round-trip ✅; ≥60 FPS ⏳ | Phase 01 Spikes 3 & 4 |

### Phase 01 Spikes 3 & 4 result (2026-05-30)

**Spike 3 — bridge contract (PASS).** The typed C#↔JS bridge
(`BridgeCommand`/`BridgeEvent`, closed inbound event set, `ogma://`-only asset
references) was built on .NET 10 and validated headlessly: **7/7 checks pass**,
including **SI-3 rejection** of unknown message types and malformed JSON, and a
well-formed outbound `setScene` envelope. See `spikes/s03-webview-bridge/`. This
satisfies the **contract-definition** portion of beta gate **G1**.

**Spike 4 — 500-spine WebGL2 scene (built; FPS deferred).** A self-contained
Three.js scene rendering 500 textured spine meshes with a WebGL2 capability check
and a 10 s mean-FPS sampler is ready (`spikes/s04-3d-macos/scene.html`). The
**≥ 60 FPS gate (NFR-OGMA-006) measurement requires the macOS reference machine
(M-REF-01)** and a real GPU; GitHub-hosted macOS runners have none. Measurement
is a tracked item: **`TRACK-P01-S4-MACOS-FPS`** (+ the live WebView round-trip on
WebView2/WKWebView, `TRACK-P01-S3-WEBVIEW-RUNTIME`).

**Outcome:** ADR-0003 is **confirmed for the bridge architecture**; the macOS
WebGL2 performance gate remains open pending the deferred FPS run. Grid/list
views are first-class regardless (UI-3, §6.4), so no core capability is blocked.
