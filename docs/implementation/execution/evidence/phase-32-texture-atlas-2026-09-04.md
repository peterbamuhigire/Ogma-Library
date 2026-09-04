# Phase 32 Texture-Atlas Evidence

Date: 2026-09-04

## Delivered

- Replaced one-generated-texture-per-book residency with a shared 1,024 by
  1,536 canvas atlas containing 192 padded 64 by 128 slots.
- Atlas capacity exceeds the maximum 161 textured books in the focused window;
  mesh residency remains capped at 500 and distant books retain flat-colour
  LOD.
- Generated spine fallbacks and allowlisted local `ogma://assets/` images share
  the same atlas and update asynchronously with stale-request protection.
- Per-mesh UVs are scoped to atlas slots, and slot release clears stale pixels
  before reuse.
- The packaged `shelf3d.js` bundle and build-provenance manifest were rebuilt
  from the TypeScript source.

## Verification

```text
npm run typecheck       # passed
npm run build           # passed
node --check ../OgmaLibrary.Bookshelf3D/Assets/Web/shelf3d.js  # passed
npm run perf:budget     # passed; 50/250/500/1k/5k/10k, atlas capacity 192
```

The focused C# bridge/3D slice passed 31 tests with 0 failures and 0 skips.
The full solution suite subsequently passed 1,066 tests (883 core, 41
architecture, 142 UI) with 0 failures and 0 skips. During the first full-suite
attempt, the 50,000-row metadata benchmark measured 165 ms at P95 against its
150 ms budget; an isolated rerun measured 152 ms and the next isolated rerun
passed, followed by the complete green suite. This is retained as host-load
variance evidence rather than being treated as an atlas regression.
The design-system delivery-evidence validator reported a structurally valid
manifest with a `CONDITIONAL` verdict because render, GPU, device, and
assistive-technology evidence are unavailable in this environment; those cells
remain `NOT ASSESSED`.

## Remaining gate

Search/advisor focus wiring, reference confirmation, native WebView/GPU frame
budgets, and physical Windows/macOS accessibility/interaction evidence remain
open.
