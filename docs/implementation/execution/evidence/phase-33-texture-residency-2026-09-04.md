# Phase 33 texture-residency evidence — 2026-09-04

## Change

The 3D shelf now associates every asynchronous spine-image request with a
per-mesh token. When a resident window is rebuilt or a mesh is otherwise evicted,
a late image response is rejected and its newly-created Three.js texture is
disposed immediately. Valid responses still replace the fallback texture and
dispose the previous map. This makes the existing 500-book resident window
bounded under rapid focus/window changes. Only the 161-book focus band (the
focused index plus 80 neighbours on each side) attempts local image loading;
all other resident meshes keep the generated spine until they enter that band.

The shipped `shelf3d.js` bundle was rebuilt from `src/shelf3d/src/scene.ts`.

## Verification

```text
npm run typecheck
npm run build
node --check ..\OgmaLibrary.Bookshelf3D\Assets\Web\shelf3d.js
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~Shelf3D" --logger "console;verbosity=minimal"
```

Results: **PASS** — TypeScript typecheck, bundle build, Node syntax check, and
28/28 Shelf3D tests passed. `npm run perf:budget` also passed for both layouts
at 50, 250, 500, 1,000, 5,000, and 10,000 arithmetic layout inputs.

## Remaining gates

- WebView/GPU frame-time, draw-call, memory, and texture-atlas measurements at
  reference counts remain `NOT ASSESSED`.
- Physical keyboard, reduced-motion, context-loss recovery, and cross-platform
  accessibility acceptance remain open.
- The 3D contract freeze and independent release approval remain open.
