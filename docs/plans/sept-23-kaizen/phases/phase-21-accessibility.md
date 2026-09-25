# Phase 21: Accessibility

## 1. Header

| Field | Value |
|---|---|
| Wave | F, Quality attributes |
| Size | 5–8 engineering days, plus assistive-technology sessions |
| Depends on | Phases 09–13 (the redesigned shell, catalogue, inspector, reader and search are the surfaces under test); Phase 01 (the harness exposes the UIA tree used for automated checks) |
| Owner decisions | None. The owner should arrange at least one session with a daily screen-reader user (recommended, not blocking). |
| Primary defects | K14, K15 (contrast, sizes), K11/K12 (reachability, already fixed structurally in Phase 07) |
| Requirements | UX-005 (P, keyboard-only flows), UX-002 (P), UX-007 (P), NFR accessibility (WCAG 2.2 AA target in SRS v2.1), CTRL items covering accessible alternatives for 3D |

## 2. Why this phase exists

The audit drove the app through Windows UI Automation, the same channel screen readers use, and found
it largely unusable for assistive technology:

- Every catalogue item's accessible name is a C# record dump, `BookSummaryProjection { BookId =
  01M3…, Title = … }`, and search results read `SearchResultItem { BookId = … }` (K14).
  `AutomationProperties.Name` is set on the inner `Border` in `CatalogueGridView.axaml`, not on the
  `ListBoxItem` that receives focus. Only 18 view files set `AutomationProperties.Name` at all.
- At the default window size, five primary actions were laid out off-screen (K11). A keyboard user
  can tab to controls a sighted mouse user cannot even see.
- Disabled buttons have low contrast; the Caption token is 11 px (`Themes/Tokens.axaml:125`), below the
  design engine's 12 px floor; the selection highlight is an off-palette salmon (K15).
- The command palette is the only route to theme and density (K16), and keyboard coverage beyond the
  reader and palette is unverified (UX-005 P).
- Narrator, NVDA and VoiceOver have never been run on this product (open since 30 August).

Accessibility is a release blocker under the engine doctrine: a failing WCAG gate caps the design score
at 59, whatever else is done.

## 3. Objectives and exit criteria

1. **Keyboard-only.** All golden journeys (Phase 01: first run, add folder, browse, open detail, read and
   annotate, search and jump, Advisor, Settings, Host start, join) complete with the keyboard alone, with
   a documented shortcut map, no keyboard traps and a logical focus order.
2. **Focus visible.** Every focusable control shows a focus indicator of at least 2 px and 3:1 contrast
   against adjacent colours (WCAG 2.2 SC 2.4.11 *Focus Not Obscured* and 2.4.13 *Focus Appearance* as a
   target). Focus is never hidden behind panels.
3. **Names, roles, states.** Every interactive element and list item has a meaningful accessible name
   (for a book: "Title, by Author, status"), the correct control type, and live state (selected,
   expanded, busy). There are zero record-dump names in the UIA tree. This is checked automatically.
4. **Announcements.** Scan progress, search result counts, save confirmations and errors are announced
   through live regions without flooding (at most one announcement per 2 s for progress).
5. **Contrast.** A contrast matrix covers every text and control token pair in Light and Dark: body text
   ≥ 4.5:1, large text and UI components ≥ 3:1. The minimum text size is 12 px.
6. **Reflow and scaling.** At 200 % Windows text scaling and at a 1024×768 window, no content or action
   is lost; the toolbar and navigation reflow.
7. **Reduced motion.** The OS setting and the Ogma toggle disable non-essential animation, including 3D
   camera easing (Phase 18).
8. **Target size.** Pointer targets are ≥ 24×24 px (SC 2.5.8).
9. **Screen-reader passes.** Narrator and NVDA on Windows, and VoiceOver on macOS (in Phase 27), complete
   the golden journeys, and findings are logged. Each pass is **NOT ASSESSED until run** by a named person.
10. **Accessible 3D alternative.** The fallback list is the complete accessible equivalent of the 3D
    shelf and is reachable directly.

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\inclusive-and-assistive-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\02-color-brand-and-visual-identity\accessible-color-and-contrast\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\component-states-and-interaction-fidelity\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\08-motion-and-interaction\motion-design\SKILL.md` (reduced motion)
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\design-qa-and-pre-launch-review\SKILL.md`
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md` (state matrix; logged NVDA/VoiceOver pass)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (AutomationProperties, peers)
- `C:\wamp64\www\windows-admin-engine-skills\skills\virtualization-containers-and-development\windows-desktop-e2e-testing\SKILL.md` (UIA locators; AutomationId-first)
- `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md` (WCAG 2.2 success criteria and current Narrator/NVDA/VoiceOver behaviour are time-sensitive)

**Typeface decision:** no font changes. Enforce the 12 px floor by raising `Type.Size.Caption` to 12
(done in Phase 09; verify here). Public Sans remains the UI face; it has adequate x-height at 12 px.

## 5. Scope

**In scope:** all views in `src/OgmaLibrary.App/Views/{Ai,Catalogue,Classroom,Reader,Search,Settings,Shelf3D}`,
the shell and palette, automation peers for custom controls (`CoverImageView`, reader page surface),
automated accessibility checks in the harness, the contrast matrix, the shortcut map and help page,
and the assistive-technology test protocol.

**Out of scope:** visual redesign (Phase 09 owns tokens; this phase only corrects failing pairs), the
French screen-reader pass beyond a smoke check (Phase 22), and macOS VoiceOver execution (Phase 27, which
uses this phase's protocol).

## 6. Work breakdown

1. **Automated UIA audit in the harness.** Add a check to the Phase 01 harness that walks the UIA tree on
   every journey step and fails on: names matching `^\w+ \{ ` (record dumps), empty names on focusable
   elements, off-screen focusable primary actions, duplicate names within one container, and missing
   `AutomationId` on journey targets. Baseline the current count, then drive it to zero.
2. **List item names.** Move `AutomationProperties.Name` and `HelpText` to the `ListBoxItem` via an item
   container style (or override `ToString`-independent peers) for the catalogue grid, list and directory,
   search results, shelves, annotations, bookmarks, Activity Centre rows and Host sessions.
3. **Custom-control peers.** Give `CoverImageView` an image role with its title. The reader page surface
   exposes the page number and a text alternative when the text layer exists ("Page 12 of 120").
4. **Focus order and traps.** Audit tab order per view. Add explicit `TabIndex` only where the visual order
   differs. Ensure Escape closes transient panels and returns focus to the invoking control. Verify the
   palette, dialogs and the folder picker return focus correctly.
5. **Focus appearance.** Add a focus-ring style from design tokens (2 px, accent colour meeting 3:1 in
   both themes) to every control template, including list items and reader toolbar buttons.
6. **Live regions.** Use Avalonia's `AutomationProperties.LiveSetting` on the status bar, scan progress,
   search status and toast area. Throttle progress announcements.
7. **Contrast matrix.** Generate a table from `Themes/Tokens.axaml` (every foreground and background token
   pair used in views) with computed ratios, in Light and Dark. Fix failing pairs in tokens only. Add it
   as a unit test so regressions fail CI.
8. **Scaling and reflow.** Run the journeys at 200 % text scale and 1024×768. Fix clipped text,
   fixed-width controls and overflow (for example the sidebar *Rename* overflow).
9. **Target sizes.** Enforce `MinHeight`/`MinWidth` ≥ 24 on icon buttons (highlight colour swatches,
   reader navigation, close buttons).
10. **Shortcut map and help.** Document shortcuts in an in-app *Keyboard shortcuts* page (Phase 28 help
    will link it), including macOS Command equivalents (commit `e7d83ab`).
11. **Assistive-technology protocol and sessions.** Write
    `docs/qa/assistive-technology-protocol.md` (tasks, expected announcements, severity scale). Run
    Narrator and NVDA passes on Windows and log the findings with the date, versions and tester. Fix P0/P1
    findings in this phase.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Record-dump names | Name set on inner Border; the item uses `ToString()` | Name on the item container | Screen readers read titles | Record-dump names: 17+ per page → 0 | Harness UIA report | Verbose names | Short name plus HelpText |
| Invisible focus | No token-based focus style | Focus-ring style | Keyboard users can track focus | Controls without visible focus: unknown → 0 | Screenshot set | Visual noise | Focus-visible only on keyboard |
| Unannounced progress | No live regions | LiveSetting on status areas | Blind users know scan state | Announced events: 0 → scan, search, errors | NVDA log | Chatty output | Throttling |
| Low-contrast tokens | No matrix gate | Contrast unit test | No regressions | Failing pairs: unknown → 0 | Matrix CSV | Palette shift | Adjust tokens only |
| AT never tested | No protocol or owner | Protocol plus named sessions | Real barriers found | AT passes: 0 → Narrator + NVDA | Session logs | Tester availability | Owner arranges |

## 8. Test plan

- **Automated:** the harness UIA audit on every golden journey step; the contrast-matrix unit test;
  headless tests asserting item-container names and live settings; a keyboard-only journey script (Tab,
  Arrow, Enter, Escape only) through the harness.
- **Manual:** 200 % scaling pass; 1024×768 pass; reduced-motion pass; High Contrast (Windows contrast
  themes) smoke pass.
- **Assistive technology:** Narrator and NVDA full protocol on Windows (NOT ASSESSED until run and
  logged); VoiceOver in Phase 27.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~Accessibility|FullyQualifiedName~Contrast|FullyQualifiedName~AutomationName"
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -KeyboardOnly -UiaAudit -Sizes 1024x768,1280x800,1920x1080
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -UiaAudit -TextScale 200
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Narrator pass | Engineer running Phase 21, logged | WCAG claim stays "partially conformant" |
| NVDA pass | Engineer or external tester | Same |
| VoiceOver pass | Phase 27 on Mac hardware | macOS accessibility claim withheld |
| Session with a daily screen-reader user | Owner to arrange | Findings may remain undiscovered |
| High Contrast themes fidelity | Engineer | Documented limitations |

## 11. Risks and mitigations

- **Avalonia automation gaps for custom controls.** Implement custom `AutomationPeer`s; record any
  framework limitation with a workaround and an upstream issue link.
- **Fixing names makes UI tests brittle.** Journey targets use `AutomationId`, not names.
- **Scope creep into redesign.** Contrast fixes go through tokens; layout issues found here go back to
  Phase 09/07 owners if they need redesign.

## 12. Execution prompt

```
## Prompt 21 - Make Ogma usable with keyboard and screen readers (WCAG 2.2 AA target)
You are implementing Phase 21 in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/phases/phase-21-accessibility.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K11, K12, K14, K15, K16)
Read the section 4 SKILL.md files and the design quality gate. Confirm Phases 01 and 09-13 are COMPLETE.
Start with task 1 so every later fix is measured by the automated UIA audit; record the baseline counts.
Then tasks 2-3, 4-6, 7-9, 10, 11.
Do not claim WCAG conformance from automated checks alone: assistive-technology passes are NOT ASSESSED until run, logged and dated.
Run section 9 commands. Write docs/implementation/execution/phase-sept23-21-completion.md and update the README status register.
```
