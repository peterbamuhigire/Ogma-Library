# Sept-23 Phase 07 completion: navigation and information architecture

Status: **IMPLEMENTED, not COMPLETE: the owner walkthrough (D-13) is NOT ASSESSED** (close-out
2026-11-19; G1, G7 and Navigation meet D-17 on `ebb7b718`, run `co-final`; see
[Close-out 2026-11-19](#close-out-2026-11-19)). First recorded 2026-09-25. Routed shell, rail, contextual toolbars, drawers, filter
chips, capability-gated entries, command registry and shortcuts are in place, with headless and
real-window proof below. Owner walkthrough: pending (see NOT ASSESSED).
Branch: `worktree-agent-afcaaea65aa4b9fe6` (lane worktree, not pushed), from `6e84a18`.
Rollback point: `6e84a18` (the last commit before any Phase 07 change).
Plan: [phase-07](../../plans/sept-23-kaizen/phases/phase-07-navigation-and-information-architecture.md).
Defects: K11, K12, K17 (K16 prepared: the Settings route and host exist for Phase 08).
IA map and wireframes: [phase-07-ia-map.md](../../plans/sept-23-kaizen/evidence/phase-07-ia-map.md).

## Typeface decision

Unchanged and compliant with the design engine: **Spectral** (display: destination headings,
sheet titles), **Public Sans** (body and all controls, the theme default) and **JetBrains Mono**
(shortcut hints in the palette and the shortcuts sheet). No new face; no banned font. All new
colours, sizes and radii are tokens (`Brush.*`, `Type.Size.*`, `Radius.*`); the shell's last
hard-coded `Foreground="White"` (startup Retry button) now uses `Brush.Accent.OnAccent`.

## Owner-proxy decision

| ID | Decision | Basis |
|---|---|---|
| D-13 | Wireframes (T07.1) approved under the owner's delegated authority: rail of 7 destinations (+ Classroom when available), Reading plan as an Advisor tab, shelves as the Collections destination, Library folders as a Library drawer until Phase 08 moves it to Settings, 3D shelf hidden unless its capability is on | Plan §3 objectives 1–5; design engine `navigation-and-information-architecture` (breadth over depth, ≤ 7 peer items, no "More" dumping ground for destinations) |

## Tasks

| Task | Result | Commit |
|---|---|---|
| T07.1 IA map | Inventory of 22 former entry points, destination table, drawers, responsive rules, wireframes at 860/1280/1920 px, keyboard map | `015dda8` |
| T07.2 Route model | `NavigationRoute`/`RouteKind`/`ShellDestination`, `INavigationState` (Application) and `NavigationState` (App): one current route, 50-entry back/forward history, scroll offset recorded and restored on Back. `IsSearchPanelOpen`, `IsIndexManagerOpen`, `ActiveView` etc. are now derived from the route; `OpenReaderAsync` navigates through it; Back into a closed reader route reopens the book | `834dd57`, `11ac8c6` |
| T07.3 Shell layout | Rail \| destination host (toolbar, chips, content, drawer, pager) \| inspector; status bar below. Removed: the Phase 03 wrap-panel stopgap and its More menu, the 14-control row, the always-open shelf sidebar, the three `IsVisible="False"` Host panels and their 10 handlers, and the dead `MainWindow`. Inspector overlays the content below 1280 px; rail collapses to icons below 1100 px (user can pin it) | `11ac8c6`, `4a12315` |
| T07.4 Toolbars and overflow | `PriorityToolbarPanel` + `ToolbarOverflow.Fit`: pinned items stay, lower-priority items move to More as width shrinks; overflowed items are parked outside the clipped panel and removed from the tab order. Library: Add folder (pinned), Filter, view switcher, Rescan, Sort, Folders, count; More: overflowed items + Open PDF, Relocation reviews, Export diagnostics. Reading, Advisor, Collections and Classroom have their own toolbars | `834dd57`, `11ac8c6`, `4a12315` |
| T07.5 Drawers and chips | Filter, Folders and Relocation reviews are exclusive Library drawers; Escape, their close button and any navigation close them. Active filters (title, author, collection, status, rating, availability) show as removable chips with "Clear all filters" (K12 hidden-filter trap). Collections now really filters the catalogue (the shelf list previously wrote to a second, unused filter instance) | `11ac8c6` |
| T07.6 Capabilities (K17) | `ICapabilityState` (Application) + `RuntimeCapabilityState` (App, composed in `ShellModule`): AI configured (live `IAiAdvisorService.IsEnabled`), classroom Host, classroom client connected, 3D shelf, metadata providers. Classroom rail item (Sharing, Smart search) appears only with the capability; a stale Classroom route redirects to Settings; *AI Smart Search* no longer shows in standalone (label now localised); the 3D view appears only when enabled. The Advisor shows "The Advisor needs an AI provider" with **Set up in Settings** (`Advisor.SettingsRoute`); Settings lists every capability with its state | `11ac8c6` |
| T07.7 Command registry | One `CommandRegistry` (30 commands: 9 destinations, back/forward, 11 library, 2 reader, 3 view, palette, shortcuts, export diagnostics) backs the palette, the keyboard map, the More menu labels and the shortcuts sheet. Fuzzy matching (substring, word prefixes, subsequence, accent-insensitive), recents first, shortcut hints, unavailable commands hidden | `834dd57`, `11ac8c6` |
| T07.8 Keyboard map | Ctrl/Cmd+1..7 (+8 Classroom), Ctrl/Cmd+K and Ctrl/Cmd+Shift+P palette, Ctrl/Cmd+F search, Ctrl/Cmd+O open PDF, F5 rescan, Alt+Left/Right and mouse buttons 4/5 back/forward, Esc closes drawer/palette/sheet, Ctrl/Cmd+/ shortcuts sheet, Up/Down/Home/End in the rail. Palette: Enter runs the top match, Down enters the list, a click outside (scrim), Escape or running a command closes it, and focus returns to where it was | `11ac8c6` |
| T07.9 Re-validate | Headless and real-window runs below; state matrix: default, empty, filtered-empty, drawer open, narrow (860), wide (2560), keyboard focus (focus-order test). Loading/error states are unchanged from Phase 02/03 | — |

## Tests

- Headless (`OgmaLibrary.Tests.Ui`, 20 new, `NavigationShellTests.cs`): history and scroll
  restore; bounded history; one active surface at a time and drawers closing on navigation;
  exclusive drawers and Escape; hidden filter shown as a chip; standalone hides Classroom and 3D
  and explains the Advisor; overflow algorithm; toolbar visible/overflow sets at **860, 1100,
  1280, 1920 px** (860/1100: Add folder, Filter, view switcher, Rescan; 1280: + Sort, Folders;
  1920: + count) and every overflowed item offered by More; reachability of every rail item,
  its painted heading and *Add folder* at **860×560, 1280×800, 1920×1080, 2560×1440**; focus order
  rail → toolbar → content; Ctrl+1..7, Alt+Left, Ctrl+/; every available command in the palette
  with en and fr labels. Existing tests updated for the new model (Ctrl+F opens search, Ctrl+K
  the palette; folders panel is a drawer; rail toggle replaces the sidebar toggle; the
  `MainWindow` screenshot tests render the shell). The Phase 03 layout-cover guard passes.
- Real window (`tests/OgmaLibrary.Tests.E2E/Journeys/NavigationTests.cs`, journey `Navigation`):
  destinations and primary actions reachable or in More; drawers exclusive and standalone dead
  entries absent (`Category=DeadEntries`); palette via keyboard (`Category=KeyboardOnly`).

## Measured results (Windows 11, Release E2E build, isolated data, 2560×1440 @ 96 DPI)

All runs under `Use-DesktopLock.ps1 -Owner 'phase-07'`.

| Run | Journeys × sizes | Result |
|---|---|---|
| `p07-final` | G1, G2, G4, G7, Navigation × 1280×800, 1920×1080 | G1 PASS ×2; **G7 PASS ×2 (baseline cleared, entry removed)**; Navigation 6/6 PASS; G2 BASELINE-FAIL ×2 (14 cards vs 12, K21/K20, unchanged from `main`); G4 BASELINE-FAIL ×2 (K40/K41) |
| `p07-final-sweep` | Navigation, G1 × 860×600, 2560×1440 | 8/8 PASS |

| Check | Before (`6e84a18`, Phase 03 stopgap) | After |
|---|---|---|
| Controls in the top row | ~20 in four styles, wrapping to 2–3 rows at 860 px | Rail of 7 + Library toolbar of ≤ 7 + More |
| Destinations/primary actions reachable at 860 px | reachable only by wrapping; Sharing, Split view, Relocation hidden in a stopgap menu | 100 % on screen or in More (MEASURED, UIA, 860×600 and 2560×1440) |
| Panels open at once | up to 4 (K12) | 1 destination + at most 1 drawer (MEASURED) |
| Hidden filter applying silently | possible | always shown as a chip (headless) |
| Dead entries in standalone | AI Smart Search, blank Sharing, 3 hidden Host panels | 0 (MEASURED, DeadEntries journey) |
| Palette commands | 7, hand-kept, English close label | 30 from the registry, localised, closes on Esc/outside/run (MEASURED) |
| Fast suite | 1,281 | **1,301 passed, 0 failed** (Architecture 52, core 1,049, UI 200) |

Screenshots — before: [phase-03 g1-860](evidence/sept-23-kaizen/phase-03/g1-860.png),
[phase-03 g2-after-scan-1920](evidence/sept-23-kaizen/phase-03/g2-after-scan-1920.png); after:
[library 1280](evidence/sept-23-kaizen/phase-07/after-library-1280.png),
[Advisor set-up state 1280](evidence/sept-23-kaizen/phase-07/after-advisor-setup-1280.png),
[folders drawer 860](evidence/sept-23-kaizen/phase-07/after-folders-drawer-860.png),
[palette 860](evidence/sept-23-kaizen/phase-07/after-palette-860.png).

## Gates

`restore --locked-mode` pass; `format --verify-no-changes` 0 changes (CRLF); Release build
0 warnings 0 errors; `Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls);
`Test-Fast.ps1` 1,301/1,301 on the final commit.

## Coordination with Phase 06

`StatusText`, `OnProgressChanged`, `OnScanCompleted` and `RaiseAllChanged` are untouched; the
navigation code lives in a new partial file `MainShellViewModel.Navigation.cs`. New strings were
inserted after `Navigation.ReadingPlan` in both dictionaries (not at the end) to avoid merge
conflicts. `SidebarToggleLabel` is now unused but kept because `RaiseAllChanged` references it.

## Deviations and NOT ASSESSED

| Item | Owner | Note |
|---|---|---|
| Owner walkthrough of the built shell | Owner | Wireframes approved by proxy (D-13); a live walkthrough is still owed |
| Keyboard-only G1–G4 (`-KeyboardOnly`) | Phase 21 | Palette and shortcuts are proven by the keyboard journey; the full keyboard-only G1–G4 runner mode remains NOT ASSESSED |
| Screen-reader announcement of route changes | Phase 21 | Rail items expose `ItemStatus` "Current page"; NVDA/Narrator not measured |
| Avalonia `MenuItem` exposes no UIA Invoke pattern | Phase 21 | The E2E clicks More-menu items; noted for the accessibility phase |
| Settings content (AI provider set-up, folders, language) | Phase 08 | This phase adds the route, host and capability list only |
| Semantic-search capability flag | Phase 14 | Not modelled until the provider exposes availability |
| Sort option names and "Ascending/Descending" are not localised (pre-existing) | Phase 22 | Out of scope; shown unchanged |
| Catalogue scroll restore on Back | — | CODE + unit test of the history; not measured in the real window |
| macOS menu bar (NativeMenu), Cmd shortcuts on macOS | Phase 27 | Cmd is accepted by the same gestures; NOT ASSESSED on macOS |

## Phase CO slice 5 (2026-11-19): journeys on the close-out head

- Navigation (all 3 tests), G1, G7 and A11yNames passed at 1280x800 and 1920x1080 in Light and Dark in `co-nav-1` (52/52 for the whole run). With slice 2's runs, and counting each theme as one run, Navigation now has 4 consecutive passes at 1280x800 (`co-all-2`, `co-all-3`, and `co-nav-1` Light and Dark) and 5 at 1920x1080. K107 did not recur.
- The owner walkthrough stays NOT ASSESSED (Owner), under D-02 and D-13.

## Close-out 2026-11-19

Full record: [phase-sept23-CO-completion.md](phase-sept23-CO-completion.md#slice-6-record-and-evidence-final-head).
Gates on `ebb7b718`, all PASS (MEASURED): accountability (162 IDs), locked restore, format, Release
build (0 warnings), vulnerable-package scan (11 of 11 clean), analyzers, and `scripts/Test-Fast.ps1`
1,677 of 1,677. Journey runs `co-final` (`-All`, both sizes, Light: 44 PASS, 2 BASELINE-FAIL for
G3, 0 FAIL, 8 NOT ASSESSED placeholders) and `co-final-g2`. Stability rule D-17: 3 consecutive passes
at both sizes. Typeface unchanged: Spectral, Public Sans and JetBrains Mono.
| Journey | co-final 1280x800 / 1920x1080 | D-17 streak | Result |
|---|---|---|---|
| G1 first run | PASS / PASS | 5 / 5 | met |
| G7 Advisor without AI | PASS / PASS | 5 / 5 | met |
| Navigation (3 tests: destinations, drawers, command palette) | PASS / PASS | 4 / 5 | met |

Evidence: [`evidence/sept-23-kaizen/phase-07/closeout/`](evidence/sept-23-kaizen/phase-07/closeout/).

**Status: IMPLEMENTED, not COMPLETE.** The owner walkthrough of the IA (D-13) is NOT ASSESSED;
owner: Owner. Screen reader and keyboard-only runs: Phase 21. macOS: Phase 27.
Rollback point: `ebc9bf6e`.