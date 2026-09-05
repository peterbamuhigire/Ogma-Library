# Phase 31 Progress - Native 3D Host and Catalogue Contract

Date: 2026-09-05

## Delivered in this increment

- Added the versioned `shelf3d-v1` outbound/inbound bridge contract and rejected
  unsupported inbound protocol versions before dispatch.
- Integrated the 3D bookshelf view model and route into the desktop shell using
  the shared catalogue projection; no duplicate catalogue data path was added.
- Replaced the optimistic default WebGL state with capability-safe fallback
  behavior. Missing/uninitialized native host adapters preserve the local book
  list and show the accessible 2D fallback instead of failing the shell.
- Added load/layout recovery handling and automatic first-load behavior when the
  3D view is attached to the visual tree.
- Kept local CSP, `ogma://` scheme containment, typed message validation and
  existing WebView bridge tests intact.
- Added the typed `FocusBook` command used to synchronize search/advisor
  selections with the visual shelf without trusting arbitrary book IDs.
- Recorded the code-level host-contract and accessible-fallback evidence in
  `evidence/phase-31-3d-host-contract-2026-09-04.md`; native adapters and
  physical platform evidence remain explicitly unassessed.
- Reconciled the current phase record: the repository-only host-contract and
  native-binding implementation gates are delivered; physical platform gates
  remain **NOT ASSESSED**. See
  `evidence/phase-31-host-contract-reconciliation-2026-09-04.md`.
- Added the current MIT-licensed Avalonia native WebView package, wired
  `NativeWebView` into the shell, and added a loopback/token host adapter that
  preserves the existing `ogma://` authorization boundary. Physical WebView2,
  WKWebView, WebGL2 and cross-platform evidence remain **NOT ASSESSED**. See
  `evidence/phase-31-native-binding-gate-2026-09-05.md`.

## Verification

- Bridge/message and 3D view-model slice: 28 passed.
- Headless rendered 3D fallback slice: 1 passed.
- Architecture suite: 41 passed on the current worktree.
- Shelf3D TypeScript typecheck and bundle build: passed; the build provenance
  manifest was regenerated from the checked-in source and lockfile.
- Application Release build: 0 warnings, 0 errors with the native binding
  enabled.
- Native host binding regression: 37 passed, 0 failed, 0 skipped; native
  control rendering remains a physical-platform gate.
- Full solution regression after lazy native-host activation: 1,125 passed,
  0 failed, 0 skipped (41 architecture, 925 core, 159 UI). Evidence:
  `evidence/phase-31-native-host-regression-2026-09-06.md`.

## Remaining phase gate

Physical Windows WebView2 and macOS WKWebView rendering, crash/reload recovery,
platform capability probes, WebGL2 behavior, and physical Windows/macOS
integration evidence remain. The native binding is present, but the runtime
must remain fail-safe until those platform gates are evidenced.
