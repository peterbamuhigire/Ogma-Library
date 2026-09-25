# Phase 07: Navigation and information architecture

## 1. Header

| Field | Value |
|---|---|
| Wave | B: Core library |
| Size | 6–8 engineering days |
| Depends on | Phase 03 (visible shell, toolbar stopgap) |
| Owner decisions | none; the owner reviews wireframes before T07.3 |
| Primary defects | K11 (unreachable actions), K12 (stacking panels), K17 (dead and misleading entries) |
| Requirement IDs | UX-003 (command palette covers commands), UX-005 (keyboard-only flows), UX-007 (locate and resume in 60 s), CAT-001 (views) |

## 2. Why this phase exists

The shell puts about 20 controls in one fixed row (`CatalogueShellView.axaml:112`, a 14-column
grid): view toggles, Search, AI Smart Search, Index Manager, Split view, Filter, Sharing,
Relocation reviews, Choose folder, Open PDF, the book count, Advisor and Reading plan. They
come in four unrelated visual styles. At the default 1180 px width, five primary actions sit
beyond the window edge. At the 860 px minimum (`DesktopShellWindow.axaml:11`), only 8 controls
are reachable (K11).

Panels are independent booleans (`IsSearchPanelOpen`, `IsIndexManagerOpen`,
`IsFilterPanelOpen`, toggled at `MainShellViewModel.cs:1033-1041`) layered in the same grid.
Opening several leaves them all open at once and squeezes the catalogue into a strip
(`evidence/screens/sheet2.png`). *Library* does not close them, and a value typed into a hidden
filter field keeps filtering (K12).

Some entries lead nowhere:
- *AI Smart Search* shows in standalone mode, but it needs a classroom Host
  (`IsStudentSmartSearchVisible`, line 275, plus a hard-coded label).
- *Sharing* opens a nearly empty page unless an environment variable is set
  (`OpenSharingSettingsAsync`, line 750).
- Three host panels are hard-coded invisible (`CatalogueShellView.axaml:558,658,734`).
- The command palette lists only 7 commands (`AllCommandPaletteItems`, line 1295) (K17).

Phase 03's toolbar stopgap made the library actions reachable. This phase replaces the
structure with a clear, routed information architecture.

## 3. Objectives and exit criteria

1. A **navigation rail** (left) with primary destinations: Library, Search, Reading (last book),
   Advisor, Collections, Activity, Settings, plus Classroom when that mode is enabled. Labels
   and icons; collapsible to icons only; keyboard reachable.
2. **Routed, mutually exclusive destinations** through one `NavigationState` (a current route
   and a history stack). Transient tools (filter, index manager, relocation review) are
   contextual drawers or dialogs owned by a destination, and they close on navigation.
   Back/forward work (Alt+Left/Right, mouse buttons 4/5).
3. **Contextual toolbars:** each destination has its own toolbar with ≤ 7 visible controls; the
   rest go into an overflow menu. The library toolbar has: Add folder, Rescan, view switcher,
   sort, filter, and *Open PDF* in overflow.
4. **Responsive:** every primary action is reachable, without horizontal scrolling, from 860×560
   to 2560×1440. The rail collapses below 1100 px. The inspector becomes an overlay below 1280 px.
5. **No dead entries:** a destination appears only when its capability is available, or it shows
   an explanatory state with a route to Settings (Phase 08). *AI Smart Search* and *Sharing* are
   hidden in standalone mode unless the Classroom capability is enabled.
6. **Command palette:** every destination and every command reachable from menus is in the
   palette, with shortcut hints and fuzzy matching. The palette closes on Escape, on clicking
   outside and on execution, and keeps its F01 fixes.
7. **Keyboard map:** a documented shortcut set (Ctrl/Cmd+1..7 for destinations, Ctrl/Cmd+K and
   Ctrl/Cmd+Shift+P for the palette, Ctrl/Cmd+F for search in context, Ctrl/Cmd+O to open PDF,
   F5 to rescan, Esc to close the drawer). G1–G4 are completable with the keyboard only.
8. The Phase 03 stopgap toolbar is removed.

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\navigation-and-information-architecture\SKILL.md`: IA, labelling, wayfinding.
- `C:\wamp64\www\design-system-skills\skills\03-layout-grid-and-composition\responsive-and-adaptive-layout\SKILL.md`: breakpoints for desktop windows.
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\webapp-gui-design\SKILL.md`: application shell patterns.
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\interaction-design-patterns\SKILL.md`: drawers, overflow menus, palettes.
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\component-states-and-interaction-fidelity\SKILL.md`: states for rail items and toolbars.
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\ux-remediation-and-redesign\SKILL.md`: triage and re-validation loop.
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md`: focus order, keyboard access, target size.
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`: the state matrix and gate.
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`: routing without a web router; compiled bindings.

Read `design-system-skills/README.md` first. The design engine's typography rules apply:
Spectral (display) and Public Sans (body) are the approved faces (`Themes/Tokens.axaml:134-136`);
do not introduce another typeface.

## 5. Scope in / scope out

**In:** route model, rail, destination hosts, contextual toolbars, overflow menus, drawers,
responsive rules, capability-based visibility, palette completeness, shortcut map, the owner
wireframe review, and removing dead XAML (the three hidden host panels, the dead `MainWindow`
if Phase 05 has not already removed it).

**Out:** visual tokens and icon artwork (Phase 09; this phase uses existing tokens and icons);
the Settings content (Phase 08; this phase adds the route and an empty host); the reader's
internal chrome (Phase 12).

## 6. Work breakdown

**T07.1: Inventory and IA map.** List every current entry point (toolbar, palette, detail
panel buttons, context menus) with its target and capability dependency. Produce the new IA:
destinations, tools per destination, overflow items and hidden-when conditions. Save it as
`docs/plans/sept-23-kaizen/evidence/phase-07-ia-map.md` with low-fidelity wireframes at 860,
1280 and 1920 px.
*Acceptance:* the owner approves the wireframes (recorded decision).

**T07.2: Route model.** Add `NavigationState` (Application layer contract; App implementation)
with `Route` values `Library`, `Search`, `Reader(bookId, page)`, `Advisor`, `ReadingPlan`,
`Collections`, `Activity`, `Settings(section)`, `Classroom`, `Shelf3D`, plus history. Replace
the `IsXActive`/`IsXOpen` booleans in `MainShellViewModel` (for example lines 637, 1033-1041)
with derived properties from the current route. Existing `INavigationService.OpenReaderAsync`
calls go through it.
*Acceptance:* unit tests: navigating closes the previous destination's drawers; back/forward
restore the previous route and scroll position.

**T07.3: Shell layout.** Restructure `CatalogueShellView.axaml` (or split it into
`ShellView.axaml` plus destination views) into: rail | destination host (toolbar + content) |
optional inspector. Remove the 14-column toolbar grid (line 112) and the Phase 03 stopgap.
Delete the permanently hidden panels at lines 558, 658 and 734, and their dead handlers in
`CatalogueShellView.axaml.cs`, or move their content to the Classroom destination (Phase 19).
*Acceptance:* the Phase 03 overlap test still passes; `AssertReachable` passes for all primary
actions at 860×560, 1280×800 and 1920×1080.

**T07.4: Contextual toolbars and overflow.** A reusable `DestinationToolbar` control with
priority-based overflow (items declare priority; lower-priority items move to the "More" menu
as width shrinks). Library toolbar: Add folder, Rescan, view switcher (segmented), sort, filter
drawer toggle, and overflow items (Open PDF, Relocation reviews, Export).
*Acceptance:* a headless test at widths 860, 1100, 1280 and 1920 asserts which items are visible or in overflow.

**T07.5: Drawers.** Filter, Index Manager and Relocation reviews become drawers owned by
Library or Activity. Opening one closes the others. Escape and navigation close them. Filters
applied from a drawer show as removable chips above the grid, so hidden filters are always
visible (fixes the K12 hidden-filter trap).
*Acceptance:* E2E opens every drawer in sequence; exactly one is open at a time; active filters are visible as chips.

**T07.6: Capability-based visibility (K17).** Add `ICapabilityState` (Application layer): AI
configured, semantic available, classroom Host enabled, classroom client connected, 3D
available, metadata providers enabled. Rail items and toolbar actions bind to it.
Unavailable-but-relevant destinations (Advisor without AI) show a short explanation and a
*Set up in Settings* action instead of failing (G7). *AI Smart Search* and *Sharing* are hidden
unless the Classroom capability is on.
*Acceptance:* E2E in standalone mode shows no Classroom items; G7 shows the explanation state.

**T07.7: Command palette completeness (UX-003).** Generate palette items from a single
`CommandRegistry` that also backs menus and shortcuts, so they cannot drift. Include all
destinations, library actions, view modes, theme and density, rescan, export diagnostics,
settings sections and reader commands (when in the reader). Add fuzzy matching, recent
commands and shortcut hints. Localise labels.
*Acceptance:* a test asserts every registered command appears in the palette and has a label in en and fr.

**T07.8: Keyboard map (UX-005).** Implement the shortcuts from objective 7 through the
registry, on both Ctrl (Windows) and Cmd (macOS; `e7d83ab` already added Meta for the
palette). Define a logical focus order: rail → toolbar → content → inspector. Add a
*Keyboard shortcuts* sheet (Ctrl/Cmd+/).
*Acceptance:* E2E completes G1–G4 without mouse input (UIA keyboard events), and focus is always visible.

**T07.9: Re-validate.** Run the design quality gate state matrix for the shell (default, empty,
loading, error, narrow, wide, keyboard focus). Capture before and after screenshots, and run the
owner walkthrough.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Actions offscreen (K11) | Single fixed toolbar row | Rail + contextual toolbars + overflow | Everything reachable | 8 of ~20 at 860 px → 100 % reachable | AssertReachable at 3 sizes | Discoverability of overflow items | Palette and shortcuts as second paths |
| Panels pile up (K12) | Independent booleans | Route model + exclusive drawers | One thing at a time | 4 panels open at once → max 1 drawer | E2E sequence test | Loss of multitasking | Split view stays as an explicit mode |
| Dead ends (K17) | No capability model | Capability-bound visibility + explanations | No blank pages | 3 dead entries → 0 | G7 and standalone E2E | Hidden features seem missing | Settings lists every capability |
| Palette incomplete (UX-003) | Hand-maintained list | Shared command registry | Palette = menus = shortcuts | 7 commands → all | registry test | Clutter | Grouping and recents |

## 8. Test plan

- **Unit:** route transitions and history; capability-to-visibility mapping; overflow priority
  algorithm; registry completeness.
- **Headless UI:** toolbar overflow at four widths; drawer exclusivity; focus order.
- **E2E:** G1–G4 at three sizes; keyboard-only G1–G4; a standalone-mode dead-entry scan (every
  visible rail item leads to a painted, non-empty destination).
- **Owner review:** wireframes before build; a walkthrough after build.

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests.Ui -c Release --no-build --filter "FullyQualifiedName~Navigation|FullyQualifiedName~Toolbar|FullyQualifiedName~CommandRegistry"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G1|Journey=G2|Journey=G3|Journey=G4|Category=KeyboardOnly|Category=DeadEntries"
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Owner approval of wireframes | Owner | Blocks T07.3 |
| Screen-reader announcement of route changes | Phase 21 | Names are set here; NVDA/Narrator/VoiceOver measured there |
| macOS menu bar integration (NativeMenu) | Phase 27 | NOT ASSESSED |

## 11. Risks and mitigations

- *A large XAML restructuring breaks bindings.* Use compiled bindings (`x:DataType`) everywhere
  touched, so errors fail the build; migrate one destination per commit behind the route model.
- *Hidden features confuse existing users.* The palette and the Settings capability list show
  everything, including items that are disabled and why.
- *Scope creep into visual design.* Keep existing tokens; restyling waits for Phase 09.

## 12. Execution prompt

```
## Prompt 07 - Replace the overloaded toolbar with a routed, responsive navigation model
You are redesigning the Ogma Library shell navigation in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K11, K12, K17)
5. docs/plans/sept-23-kaizen/05-ui-ux-audit.md
6. docs/plans/sept-23-kaizen/phases/phase-07-navigation-and-information-architecture.md
Load skills: C:\wamp64\www\design-system-skills\README.md first, then navigation-and-information-architecture,
responsive-and-adaptive-layout, webapp-gui-design, interaction-design-patterns, component-states-and-interaction-fidelity,
ux-remediation-and-redesign, accessibility-wcag-2-2-compliance, governance/design-quality-gate.md,
and chwezi-dev-engine avalonia-desktop-development.
Work plan: A. T07.1 IA map and wireframes, then STOP for owner approval. B. T07.2 route model. C. T07.3-T07.6.
D. T07.7-T07.8. E. T07.9 re-validation and owner walkthrough.
File scope: src/OgmaLibrary.App/Views/**, src/OgmaLibrary.App/ViewModels/**, the Application-layer navigation and
capability contracts, localization resources, tests. Keep Spectral and Public Sans; no new typefaces; no hard-coded
colours or font sizes.
Acceptance: section 9 commands; screenshots at 860x560, 1280x800 and 1920x1080 in
docs/implementation/execution/evidence/sept-23-kaizen/phase-07/; owner walkthrough verdict recorded.
Recovery point: the last Phase 06 commit.
```
