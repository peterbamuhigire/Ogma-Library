# Spike 4 — 3D macOS WKWebView WebGL2 FPS — RESULT

**Status:** ⏳ Scene built; **FPS measurement deferred to macOS reference hardware
(M-REF-01)**. This is an environment block, not a failure — see Owner ask #1 in
`phase-01/README.md`.

## What was built

- `scene.html` — a self-contained Three.js scene rendering **500 textured
  Plane/Box "spine" meshes** (the NFR-OGMA-006 target scene of 500 books), with:
  - a **WebGL2 capability check** (`renderer.capabilities.isWebGL2`) — the same
    check the host performs before enabling the 3D shelf (DEP-3, HLD §6.4),
  - a **Stats.js** FPS overlay,
  - a 10-second mean-FPS sampler with an on-screen PASS/MARGINAL/FAIL readout
    against the ≥ 60 FPS gate,
  - a `postMessage` callback that reports `{type:"fpsResult", data:{meanFps,…}}`
    back over the **Spike 3 bridge contract** when hosted in a WebView.

## How to run the measurement (when macOS hardware is available)

1. On **M-REF-01** (M1 MacBook Air, 8 GB, macOS 13+), host `scene.html` in a
   WKWebView (or open in Safari as a first proxy) and read the mean FPS after the
   10 s window.
2. On Windows, host in WebView2 / open in Edge as a secondary reference.
3. Record the mean FPS in this file and in `spikes/RESULTS.md`, then amend
   **ADR-0003** with the measured result.

## Pass / fail criteria (from the plan)

- **Pass:** mean FPS ≥ 60 on M-REF-01 → ADR-0003 confirmed.
- **Marginal:** 45–59 FPS → ADR-0003 amendment documenting a macOS-specific
  target or a mitigation (reduce spine-texture resolution / initial polygon
  count / instanced meshes).
- **Fail:** < 45 FPS → ADR-0003 amendment; evaluate fallback (grid/list remains
  first-class regardless — UI-3).

## Notes

- GitHub-hosted macOS runners have **no GPU**, so CI can only confirm the scene
  initializes without error; the real FPS number must come from physical
  reference hardware.
- The scene uses `BoxGeometry` (cheap) and `MeshBasicMaterial` (unlit) — the
  realistic worst case (lit materials, real WebP textures, hover picking) is a
  Phase 14 measurement; this spike establishes the baseline feasibility number.
- **Tracked item:** `TRACK-P01-S4-MACOS-FPS` — run the FPS measurement on
  M-REF-01 and amend ADR-0003.
