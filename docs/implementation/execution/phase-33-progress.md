# Phase 33 Progress - 3D Scale, Accessibility and Performance

Date: 2026-09-04

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
- Added bounded texture residency around the focused index; distant resident
  books use a flat-colour LOD and are promoted to generated/local textures only
  as focus moves.

## Verification

- `npm run typecheck` passed.
- `npm run build` passed from `src/shelf3d`.
- `node --check src/OgmaLibrary.Bookshelf3D/Assets/Web/shelf3d.js` passed.
- `npm run perf:budget` passed across 50/250/500/1k/5k/10k arithmetic layout
  cases in both shelf and grid modes.
- C# bridge/3D and view-model tests: 31 passed after the final build; physical
  WebView/GPU evidence remains an environment gate.
- Residency checks passed with at most 500 resident meshes and at most 161
  textured books across 50, 250, 500, 1k, 5k, and 10k catalogue inputs; the
  shared Phase 32 atlas provides 192 bounded slots for that focus band.
- Current-head rerun from `src/shelf3d` passed `npm run typecheck`, `npm run
  build`, `npm run perf:budget`, and `node --check` for the packaged bundle;
  see `evidence/phase-33-renderer-gate-reconciliation-2026-09-04.md`.
- The complete solution suite passed 1,066 tests (883 core, 41 architecture,
  142 UI), with 0 failures and 0 skips, after the atlas rebuild.

## Remaining phase gate

The headless environment cannot prove GPU frame budgets, native WebView2 or
WKWebView attachment, context-loss behavior on reference hardware, or
cross-platform accessibility screenshots. Those remain release evidence gates;
the renderer now emits the measurements required for that evidence and fails
closed to the accessible 2D/list path.

The current TypeScript build, deterministic performance budget, and bounded
texture-residency checks passed through 10,000 items. Evidence:
`evidence/phase-31-33-shelf-build-2026-09-06.md`.

The Aug-39 Definition of Done now records bounded 1,000/5,000/10,000-item
strategy and accessible 2D/keyboard action parity as closed by local executable
evidence. Real GPU/WebView metrics, the reference-hardware 500-book frame gate,
and the independently accepted 3D contract freeze remain unchecked.
