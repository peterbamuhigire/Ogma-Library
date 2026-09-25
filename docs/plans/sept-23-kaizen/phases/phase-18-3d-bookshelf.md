# Phase 18: 3D bookshelf

## 1. Header

| Field | Value |
|---|---|
| Wave | E, Advanced surfaces |
| Size | 6–9 engineering days |
| Depends on | Phase 05 (correct asset root for covers and spines, K20), Phase 09 (tokens, motion and colour rules), Phase 08 (the 3D toggle lives in Settings) |
| Owner decisions | None new. D-04 (cache location) decides where the scheme handler serves assets from. |
| Primary defects | K60, K20 |
| Requirements | CAT-001 (W for grid, list and directory; the 3D view is effectively unavailable) and the Phase 31–33 Aug-39 3D gates |

## 2. Why this phase exists

On a Windows 11 machine with Edge WebView2 installed, the *3D shelf view* button showed "3D view is
not available on this device - showing the accessible bookshelf list" (`sheet1.png`, K60). The
fallback list works, but the headline visual feature never appears. The code explains why:

1. **Hidden-WebView deadlock.** The WebView lives in `ContentControl x:Name="WebViewHost"` inside a
   `Border` whose `IsVisible` is bound to `IsInteractive3DVisible`
   (`src/OgmaLibrary.App/Views/Shelf3D/Bookshelf3DView.axaml:44-51`). That property is
   `IsWebGl2Supported && !IsPerformanceDegraded`
   (`src/OgmaLibrary.App/ViewModels/Shelf3D/Bookshelf3DViewModel.cs:94`). `IsWebGl2Supported` becomes
   true only after the page posts `WebGl2Status` (`Bookshelf3DViewModel.cs:349`). A WebView that is
   never made visible may never create its surface or send that message, so the fallback text
   (`Shelf3D.Fallback.Message`) appears by default and stays.
2. **Lost scene.** The host posts `SetScene` around navigation
   (`src/OgmaLibrary.App/Views/Shelf3D/NativeWebViewHostAdapter.cs:92-105`), but the page installs
   `window.ogmaShelf3D` only in `boot()` on `DOMContentLoaded` (`src/shelf3d/src/main.ts:28-52`).
   Messages sent before that are dropped, there is no ready handshake, and nothing re-sends the scene
   after a reload.
3. **Wrong asset root.** `Shelf3DHostCoordinator.InitializeAsync` serves assets from
   `Path.Combine(_options.LibraryRoot, ".ogma")`
   (`src/OgmaLibrary.App/ViewModels/Shelf3D/Shelf3DHostCoordinator.cs:26`). That is the startup root
   (the app-data folder by default), while the audit found covers written under the user's chosen
   folder (K20). Even a working scene would show blank covers.
4. **Off-thread fallback.** The fallback list is filled off the UI thread
   (`Bookshelf3DViewModel.cs:168-172`, K72).
5. **The environment flag does nothing.** `OGMA_ENABLE_3D_SHELF` changes only diagnostic text (K17).

## 3. Objectives and exit criteria

1. On Windows 11 with WebView2 and a WebGL2-capable GPU, the 3D view renders the user's library with
   real covers and spines within 2 s of opening (2k books, reference machine) or 1 s (the 17-book audit
   corpus, local).
2. A deterministic, versioned **ready handshake**: the page posts `Ready {protocolVersion, webgl2,
   maxTextureSize}`. The host sends `SetScene` only after `Ready` and re-sends it on every reload or
   navigation completion. There are unit tests on both sides of the bridge.
3. Capability detection is truthful: *Unavailable* is shown only when WebView2 is missing (with an
   install link and the reason), WebGL2 is unsupported, or the handshake times out after 5 s. The
   reason appears in Diagnostics.
4. The fallback is a first-class view: keyboard-navigable, with covers, the same sort and filter as the
   catalogue, and a clear explanation. It never says "not available" for a reason the user cannot act on
   without saying what to do.
5. Keyboard alternative inside 3D: arrow keys move focus between books, Enter opens Detail, and a Focus
   Book command from search or Advisor (the existing `FocusBook` bridge) works. Every interaction has a
   non-drag equivalent.
6. Reduced motion: honour both the OS setting and the Ogma Settings toggle (the page already reads
   `prefers-reduced-motion` in `scene.ts:69`; the host must also send the Ogma preference).
7. Budgets: the `perf:budget` layout p95 stays within `LAYOUT_BUDGET_MS`; frame time p95 ≤ 16.7 ms at 2k
   books on the reference GPU; texture residency is bounded (existing eviction). The performance-degraded
   fallback triggers only on measured sustained slowness, with a *Try 3D again* action.
8. The 3D toggle in Settings (Phase 08) enables or hides the view. The env var becomes an admin override.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (WebView embedding, native fallback rule)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\frontend-performance\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\systematic-bug-diagnosis\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\08-motion-and-interaction\motion-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\15-game-visual-experience\game-feel-feedback-camera-and-haptics\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\inclusive-and-assistive-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\windows-admin-engine-skills\skills\virtualization-containers-and-development\windows-desktop-e2e-testing\SKILL.md` (test the WebView at the HTML layer)
- `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md` (WebView2 runtime distribution and Avalonia WebView package status are time-sensitive)

**Typeface decision:** spine labels currently use Georgia at 6–7 px (`src/shelf3d/src/scene.ts:522`,
engines report). Replace it with Spectral (licensed, embedded as a web font in the bundle) with a
minimum rendered cap height equivalent to 12 px at the default camera distance; hide labels below that
size (LOD) instead of rendering illegible text.

## 5. Scope

**In scope:** the bridge handshake, visibility logic, asset-root resolution, capability probe and
Diagnostics text, fallback view quality, keyboard and focus, reduced motion, spine typography, budgets,
the Settings toggle, and the shelf3d npm gates.

**Out of scope:** new 3D scene features (rooms, animations beyond focus), macOS WKWebView verification
(Phase 27), and GPU-specific tuning beyond the reference machine.

## 6. Work breakdown

1. **Make the WebView live before capability is known.** Keep the WebView host attached and measured
   (visible but covered by a loading overlay) while detection runs. Model the state as
   `Probing → Interactive | Fallback(reason)` instead of a boolean bound to visibility.
   *Acceptance:* the harness shows the loading state, then 3D, on the audit machine.
2. **Ready handshake.** In `src/shelf3d/src/main.ts`, post `Ready` from `boot()` via the host API after
   `initializeShelf3D`, and add `messages.ts` types and a protocol version bump. On the host side, queue
   outbound messages until `Ready`, flush in order, and re-send `SetScene` and preferences after every
   `NavigationCompleted` followed by `Ready`. Add a 5 s timeout that yields `Fallback(HandshakeTimeout)`.
3. **Asset root.** Resolve each book's cover and spine through the same asset resolver the catalogue
   uses after Phase 05 (per-root or app-data per D-04), not `_options.LibraryRoot`. The scheme handler
   serves only resolved, allowlisted asset paths (keep the path-security boundary).
4. **Capability probe and Diagnostics.** Check that the WebView2 runtime is present (version), that
   WebGL2 was reported, and the handshake result. Write the probe to the Phase 02 log and show it in
   Settings → Diagnostics in plain words, with an install link for WebView2.
5. **Threading.** Marshal fallback list population to the UI thread. Make `OnAttachedToVisualTree`
   non-throwing through the Phase 02 guard.
6. **Fallback view.** Reuse the catalogue list item template (covers, title, author, badges), keep the
   catalogue sort and filter, put an explanation banner at the top with the reason and the action, and
   make it keyboard-navigable.
7. **Keyboard and focus in 3D.** Map arrow, Home/End and Enter in the page. Keep the focus ring visible
   (design tokens passed through the bridge). Announce the focused book through a host-side live region
   (the canvas is not accessible to screen readers), so assistive-technology users get the same list.
8. **Reduced motion and preferences.** Send `reducedMotion` from Ogma Settings together with the OS
   value; disable damping, camera tweening and parallax when it is set.
9. **Spine typography and LOD.** Load Spectral from the bundle and apply the minimum-size rule; update
   `perf:budget` for label cost.
10. **Settings toggle.** Bind the Settings 3D switch; hide the toolbar/navigation entry when it is off.
    Remove the no-op env flag path from the capability probe text, or make it a real admin override.
11. **Crash and reload resilience.** When the WebView process crashes or `ProcessFailed` fires, show
    the fallback with *Reload 3D*, re-run the handshake, and never take down the app.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| 3D never appears on capable Windows | WebView hidden until a message it cannot send | Probing state with a live WebView | 3D renders where supported | Win11 audit machine: Fallback → Interactive | Screenshot pair | Flash of empty canvas | Loading overlay |
| Scene lost | No ready handshake; messages sent before boot | `Ready` message, queue, re-send on reload | The scene always arrives | Scene delivery after reload: 0 → 100 % of 20 reloads | Bridge test log | Protocol mismatch | Version negotiation keeps v1 |
| Blank covers in 3D | Startup root used for assets | Shared asset resolver | Covers match the grid | Covers visible 0/12 → 12/12 on the audit corpus | Screenshot | Path-security regression | Scheme-handler allowlist tests |
| Illegible spines | 6–7 px Georgia | Spectral plus LOD rule | Readable spines | Min label size 6 px → ≥ 12 px equivalent | Screenshot at default zoom | Fewer visible labels | LOD threshold setting |
| Unclear fallback | Generic message | Reasoned fallback with actions | Users know what to do | Fallback with reason and action: 0 → 100 % | Screenshot per reason | — | — |

## 8. Test plan

- **TypeScript:** handshake unit tests (message before boot is queued host-side; `Ready` flushes; reload
  re-sends) and keyboard focus traversal. Run the existing `typecheck`, `build` and `perf:budget` gates.
- **.NET bridge:** `BridgeJson` round-trip for `Ready`; host queue ordering; timeout → Fallback(reason).
- **Headless UI:** view-state transitions; fallback list populated on the UI thread; Settings toggle hides
  the entry.
- **Real-window (Windows):** journey J7 "open 3D, see covers, arrow to a book, Enter opens Detail, search
  result *Show on shelf* focuses the book", plus "kill the WebView2 process → fallback with Reload →
  recover". Capture at 1280×800 and 1920×1080.
- **Negative:** WebView2 uninstalled (VM or a policy-disabled user profile), WebGL2 blocked via
  `--disable-webgl2` browser arguments, 0 books, 2k books, missing cover files.

## 9. Acceptance commands

```powershell
Push-Location src/shelf3d; npm ci; npm run typecheck; npm run build; npm run perf:budget; Pop-Location
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Shelf3D|FullyQualifiedName~Bridge"
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~Bookshelf3D"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Shelf3D
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Reference-GPU frame budget at 2k/50k books | Phase 24 | Budgets are local only |
| macOS WKWebView and WebGL2 | Phase 27 | macOS shows the fallback until verified |
| Screen-reader experience of the 3D live region | Phase 21 | 3D is marked "visual enhancement"; the fallback list is the accessible path |
| Integrated-GPU school laptops | Owner (loan a typical school laptop) | The degraded-fallback threshold is unvalidated |

## 11. Risks and mitigations

- **WebView2 missing on managed school machines.** Clear reason and install guidance; Phase 26 decides
  whether the installer bootstraps the Evergreen runtime.
- **GPU driver crashes.** The process-failure handler plus fallback keep the app alive; the probe
  records the reason.
- **Protocol drift between the committed bundle and the host.** Protocol version check at `Ready`; a
  CI gate builds the bundle and compares the committed artefact hash.

## 12. Execution prompt

```
## Prompt 18 - Make the 3D bookshelf render reliably, with an excellent fallback
You are implementing Phase 18 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-18-3d-bookshelf.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K17, K20, K60, K72)
Read the section 4 SKILL.md files. Confirm Phases 05, 08 and 09 are COMPLETE.
Order: tasks 1-2 (visibility + handshake, with failing tests first), 3 (asset root), 4-5, 6-9, 10-11.
Keep the Architecture test boundaries (Bookshelf3D consumes catalogue projections only; no competing book identity).
Run the section 9 commands, including all shelf3d npm gates, and journey J7 at both reference sizes.
Record GPU, macOS and assistive-technology items as NOT ASSESSED with owners.
Write docs/implementation/execution/phase-sept23-18-completion.md and update the README status register.
```
