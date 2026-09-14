# Phase 15: optional 3D library with dependable fallback

Status: planned. Owner:3D/native engineer; reviewer: desktop/GPU/accessibility QA.
Dependencies:06/08/12. Estimate:6-10 person-days. Findings:F27/F29.
Requirements:CAT multi-view and relevant UX/NFR; old phases31-33.
Skills:E1/E4/D1/D4/D9; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

3D is an optional discovery experience over the same catalogue. It can fail or be disabled without reducing the library's basic usefulness. Native appearance/performance, not TypeScript compilation alone, determines acceptance.

## Work slices

1. Inspect `src/shelf3d`, typed bridge, NativeWebViewHostAdapter, host coordinator and catalogue projection. Record protocol/asset versions, host-engine capability and exact supported platforms.
2. Make the entry state explicit: available, initializing, unsupported, disabled or failed. Remove the permanent dim emoji impression. Default to the2D library when3D is not ready.
3. Design shelf scale, cover/spine readability, hover/selection and focus/open interactions with representative titles/covers. Every book maps back to the canonical identity and current permission scope.
4. Prove native WebView2/WKWebView loading, loopback/asset tokens, navigation allowlist, bridge validation and crash/reload recovery. Do not weaken origin/path controls for convenience.
5. Test texture residency, lazy assets, LOD, frame-time and memory across target-scale libraries. Tune from measured reference-GPU evidence, including integrated graphics.
6. Provide keyboard/assistive alternatives and reduced-motion behavior. Search/Advisor focus-in-shelf should be optional and never interrupt an open reading session.

## Acceptance

- Typecheck/build/performance-budget gates pass and deployed assets match their source digest.
- Native Windows/Mac host opens, selects and reads the correct book; disabled WebGL/native failure goes to a useful2D fallback without data loss.
- All supported interactions have keyboard equivalents or equivalent accessible2D tasks; reduced motion avoids camera movement that harms usability.
- No unauthorized asset or book appears through bridge/search/texture paths; malformed messages and external navigation are rejected.
- Real reference-hardware frame-time/memory/startup budgets pass with documented corpus; synthetic/headless tests remain labeled as such.
- Reader tasks stay independent of3D availability and state.

## Recovery

Keep a runtime disable/fallback path and the previous verified asset bundle. Revert scene/texture changes on performance or navigation regression. If native3D cannot meet release gates, use a formal optional-feature deferral with accurate UI/documentation; do not silently claim the requirement complete from a successful build.
