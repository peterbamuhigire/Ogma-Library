# Phase 33 Progress - 3D Scale, Accessibility and Performance

Date: 2026-08-30

## Delivered in this increment

- Restored the checked-in TypeScript scene as the source of truth and rebuilt
  the packaged bundle with the repository's `npm run build` command.
- Added bounded resident scene virtualization: the full catalogue can remain
  addressable while the renderer keeps at most 500 book meshes resident.
- Added focus-window movement, keyboard traversal, camera synchronization,
  reduced-motion detection, and WebGL context-loss/recovery handling.
- Added runtime renderer metrics for average FPS, p95 frame time, draw calls,
  total scene books, resident books, and reduced-motion state.
- Added bounded performance-matrix coverage for 50, 250, 500, 1k, 5k, and 10k
  layout inputs; the existing accessible catalogue/list path is selected when
  WebGL is unavailable or sustained performance degradation is reported.
- Added protocol parsing and validation for performance metrics, with tests for
  accepted bounds and resident-window overflow rejection.

## Verification

- `npm run typecheck` passed.
- `npm run build` passed from `src/shelf3d`.
- `node --check src/OgmaLibrary.Bookshelf3D/Assets/Web/shelf3d.js` passed.
- `npm run perf:budget` passed across 50/250/500/1k/5k/10k arithmetic layout
  cases in both shelf and grid modes.
- C# bridge/3D and view-model tests passed after the final build; physical
  WebView/GPU evidence remains an environment gate.

## Remaining phase gate

The headless environment cannot prove GPU frame budgets, native WebView2 or
WKWebView attachment, context-loss behavior on reference hardware, or
cross-platform accessibility screenshots. Those remain release evidence gates;
the renderer now emits the measurements required for that evidence and fails
closed to the accessible 2D/list path.
