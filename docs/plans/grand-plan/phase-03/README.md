# Phase 03 — Design System, Icon System & UX Foundation

One sentence: Establish the calm-control design language — color/type/spacing/
motion tokens, Avalonia theming, the colorful icon system with its registry and
PNG pipeline, the command palette, full en/fr i18n, and the accessibility
scaffold — so every subsequent UI phase builds on a proven, tested, beautiful
foundation.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (the design system, i18n, and accessibility scaffold gate the MVP) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 0–1 (design/UX foundation, no direct PRD phase number) |
| **Platforms** | All design tokens, icon assets, and accessibility behaviors must be verified on **Windows 10+ (WebView2/WDDM)** and **macOS 13+ (Retina/HiDPI)**; contrast and FPS checks run on both |
| **Depends on** | Phase 02 complete (solution skeleton, i18n analyzer, ILocalizationService stub, DI composition root) |

---

## 2. Objectives

1. The calm-control design language is codified as Avalonia design tokens
   (color, typography, spacing, motion) in a `DesignTokens.axaml` resource
   dictionary and confirmed by the owner; no design decision after this point
   can contradict these tokens without an owner sign-off and a token update.
2. The colorful icon system is operational: the `IconCatalog` (a generated
   C# enum/registry) maps every icon key to its asset paths and to a required
   accessible-label resource key; a missing label fails the build
   (`OGMA0002` diagnostic).
3. The `@1x/2x/3x` PNG asset pipeline is scripted and documented; placeholder
   icons (neutral gray squares with the key name) are in place for every icon
   defined in the master manifest; premium PNGs are procured from the owner and
   wired in where received.
4. The MASTER icon manifest is created at
   `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md`; it aggregates every icon
   defined in Phase 03 and provides empty placeholder rows for all future phases.
5. The command palette is implemented (resolving SRS context gap CON-4): a
   keyboard-invocable overlay (`Ctrl+K` / `Cmd+K`) that searches the Phase 03
   command set (~30 MVP commands); all entries carry colorful icons and are
   localized in en + fr.
6. The `ILocalizationService` is implemented (replacing the Phase 02 stub):
   resx-backed (or JSON-backed, per the ADR chosen in this phase), runtime
   culture switch without restart, pluralization via ICU-style rules,
   pseudolocale (`qps-ploc`) support; MVP delivers **full English + French**
   string coverage.
7. The accessibility scaffold is in place: keyboard focus model (logical tab
   order, visible focus ring), Avalonia `AutomationPeer` registrations for
   all new controls, WCAG 2.2 AA contrast ratios confirmed for all token
   combinations, and a screen-reader walkthrough of the command palette passes
   on Windows (Narrator) and macOS (VoiceOver).
8. All Phase 03 UI controls pass the cross-cutting DoD: colorful icon + label,
   en/fr strings, keyboard nav, automation peer, and AA contrast.

---

## 3. Scope

### In scope

- **Design tokens** (`DesignTokens.axaml`, `DesignTokens.Dark.axaml`):
  the color palette (8 accent families + 2 surface families from ICON-SYSTEM.md
  §4 style tokens), typography scale (font families, sizes, weights), spacing
  scale (4/8/12/16/24/32/48 px), border-radius, elevation/shadow, motion
  easing curves and duration scale (100/200/300/400 ms).
- **Avalonia Fluent theming extension**: apply Ogma tokens over the Avalonia
  `FluentTheme` base; override `Button`, `MenuItem`, `TreeView`, `ListBox`,
  `TextBox`, `ComboBox`, `Slider`, `ToggleSwitch`, `ProgressBar`, and
  `ScrollBar` control themes to match the calm-control aesthetic.
- **`IconCatalog` registry**: a source-generated C# `enum IconKey` (or a
  `static class Icons` with `IconKey` string constants); a generated
  `IconMetadata` record (asset paths @1x/2x/3x for light/dark, accessible-label
  resource key); a build check (`OGMA0002`) that fires if any `IconKey` is
  used in a control without a corresponding localized accessible label.
- **PNG asset pipeline**: a PowerShell + bash script (`scripts/Import-Icons.ps1`,
  `scripts/import-icons.sh`) that takes vendor-supplied PNGs and places them
  at `OgmaLibrary.App/Assets/icons/<category>/<key>@Nx.png`; validates sizes
  (16/24/32/48 @1x, 2x, 3x variants); updates the `IconCatalog`.
- **Placeholder icons**: for every icon key defined in Phase 03's `icons.md`,
  a 24x24 px neutral placeholder PNG is generated programmatically (SkiaSharp;
  gray circle with the key's initials in white) so UI work is not blocked
  while premium PNGs are procured.
- **MASTER icon manifest**: `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md`
  aggregating all Phase 03 icons and providing a template for future phases.
- **Command palette** (`CommandPalette.axaml`, `CommandPaletteViewModel.cs`):
  `Ctrl+K` / `Cmd+K` opens a search overlay; typing filters the ~30 MVP
  commands (from CON-4 answer in Phase 00); results show an icon + label +
  keyboard shortcut; `Enter` / click executes; `Escape` dismisses.
  All entries are localized in en + fr; all icons have accessible labels.
- **`ILocalizationService` implementation** (`ResxLocalizationService` or
  `JsonLocalizationService`, per ADR in this phase): runtime culture switch;
  pluralization; pseudolocale (`qps-ploc`) mode for the CI runner.
  All strings added in Phase 03 are present in both `en` and `fr` resource
  files.
- **Accessibility scaffold**:
  - Logical tab order for all Phase 03 controls (command palette, main toolbar
    stub, window chrome).
  - Visible focus ring (≥ 2 px, `accent/ink` color token).
  - `AutomationPeer` implementations for `CommandPaletteControl`,
    `CommandPaletteItem`, and `IconButton` (a reusable wrapper that pairs
    an `IconCatalog` icon with a localized accessible label).
  - Contrast check: all token combinations must meet WCAG 2.2 AA (4.5:1 for
    normal text, 3:1 for large text and UI components); verified with a
    programmatic contrast calculator test.
  - Screen-reader manual walkthroughs recorded in `docs/a11y/WALKTHROUGHS.md`.
- **Localization resource-format ADR** (new ADR in this phase): choose between
  `.resx` (strongly typed, native .NET) and JSON/PO (easier for translators).
  Record the choice and rationale. Recommendation: `.resx` with a
  `ResxLocalizationService` adapter that converts to `ILocalizationService`
  so the format is swappable.
- **Motion tokens**: Avalonia transition definitions for the command palette
  open/close (200 ms ease-out opacity + vertical slide), button hover (100 ms
  background-color transition), and focus ring appearance (100 ms opacity).
  All motion respects the `prefers-reduced-motion` equivalent: a
  `IMotionPreferences` service checks the OS accessibility setting and
  substitutes a 0 ms duration when reduce-motion is active.

### Explicitly out of scope

- Any catalogue, reader, search, or AI UI (Phases 06-13).
- The 3D bookshelf (Phase 14).
- The full Spanish/Italian/German localization (Phase 21).
- Icon procurement for Phases 04-23 (those phases file their own owner asks;
  Phase 03 does the first batch procurement for the command palette + chrome
  icons).
- The full accessibility audit (Phase 21); Phase 03 is the scaffold, not the
  audit.
- Any data-layer work (Phases 04-05).

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| Grand Plan principle 7 | MVP | Premium means calm control (calm-control design language) | Design token review (owner sign-off on tokens); `/design-audit` pass |
| Grand Plan principle 8 | MVP | Beautiful and iconified; colorful icons everywhere | IconCatalog compiles; all Phase 03 controls have icons; MASTER-MANIFEST.md created |
| Grand Plan principle 9 | MVP | Multilingual by construction; en+fr at MVP | `ILocalizationService` implementation; en/fr coverage 100% for Phase 03 surfaces; pseudolocale CI check passes |
| Grand Plan principle 10 | MVP | Accessible as a gate; AA contrast; keyboard + SR | Contrast test passes; keyboard walkthrough passes; SR walkthrough recorded |
| CON-4 | MVP | Command-palette command set (context gap resolved) | Command palette implemented with the Phase 00-approved ~30 MVP command list |
| FR-CAT-001 | MVP | All views open the same book-detail + reader (command palette routes to them) | Command palette `open-book` command registered; routing verified in headless test |
| NFR-PROD-005 | MVP | No UI stall > 100 ms | Command palette filter latency test: P95 < 50 ms for a 30-command list on reference hardware |
| NFR-PROD-007 | MVP | Keyboard operability of all core flows | Keyboard nav test: command palette reachable and operable by keyboard only |
| NFR-PROD-008 | MVP | Screen-reader + AA contrast | Contrast ratio test; automation peer tests; SR walkthrough |
| I18N-STRATEGY §2 | MVP | No hard-coded UI strings (OGMA0001 + OGMA0002) | Build fails on violation; all Phase 03 strings in en + fr |
| I18N-STRATEGY §5 | MVP | en/fr completeness gate | CI check: every MVP-surface resource key present in fr.resx |
| ICON-SYSTEM.md §6 | MVP | IconCatalog build check: no icon without accessible label | OGMA0002 fires on violation; all Phase 03 icons have labels |
| ICON-SYSTEM.md §3 | MVP | MASTER-MANIFEST.md created | File exists at `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md` |
| ADR-0002 | MVP | Avalonia shell with Fluent theming extended | Themed controls render correctly on Windows + macOS |

---

## 5. Dependencies

### Depends on

- Phase 02:
  - `ILocalizationService` interface stub (Phase 03 implements it).
  - `IBenchmarkContext` (used to measure command-palette filter latency).
  - i18n analyzer (`OGMA0001`) operational; Phase 03 adds `OGMA0002`.
  - Avalonia `App` and `MainWindow` shells exist.
  - `.editorconfig` and `Directory.Build.props` enforced.
- Phase 00:
  - CON-4 (command-palette command set) answered; the approved command list is
    the Phase 03 implementation scope.
  - Owner has confirmed the calm-control aesthetic direction (Phase 00 §Product
    promise "premium means calm control").

### Unblocks

- All subsequent UI phases (04, 05, 06, 07, 08, 09, 14, 15, 16, 17, 18, 21):
  all must use the `IconCatalog`, the `ILocalizationService`, and the
  `DesignTokens` established here.
- The icon procurement loop: the first batch of premium PNGs is requested in
  this phase; subsequent phases add to the master manifest.
- Phase 21 (full a11y + i18n): the scaffold Phase 03 creates is the base.

---

## 6. Architecture & approach

### Design token architecture

Design tokens live in XAML resource dictionaries in `OgmaLibrary.App/Themes/`:

```
Themes/
  DesignTokens.axaml           # Color, typography, spacing, motion tokens (light)
  DesignTokens.Dark.axaml      # Dark-theme token overrides
  ControlThemes/
    Button.axaml               # Calm-control Button override
    MenuItem.axaml             # ...
    CommandPalette.axaml       # Command palette control theme
  Icons/
    IconButton.axaml           # IconButton control theme (icon + label wrapper)
```

Tokens are Avalonia `SolidColorBrush` and `StaticResource` entries, not
hard-coded values. Every control theme references only token names (never a
raw hex color) so the dark theme override works by swapping the token values
without touching the control themes.

### Color token design (from ICON-SYSTEM.md §4)

| Token name | Light value (proposed) | Dark value (proposed) | Use |
| --- | --- | --- | --- |
| `Color.Accent.Oak` | `#D4922A` | `#E8B05A` | Primary actions, library identity |
| `Color.Accent.Ink` | `#2B4A7A` | `#6B9BE8` | Reading & navigation |
| `Color.Accent.Sage` | `#4A7A5A` | `#7EC89A` | Success, available, health-OK |
| `Color.Accent.Clay` | `#C05A3A` | `#E88A6A` | Warnings, needs attention |
| `Color.Accent.Plum` | `#6A3A7A` | `#B87ACC` | AI / intelligence surfaces |
| `Color.Accent.Slate` | `#5A6A7A` | `#9AAABB` | Settings, secondary actions |
| `Color.Surface.Parchment` | `#FAF7F2` | N/A (dark base) | Light theme base |
| `Color.Surface.Walnut` | N/A (light base) | `#1E1A17` | Dark theme base |

> These values are proposed; the owner confirms or adjusts them in the token
> review (Owner ask #1). The token names are stable; only values change.

### Typography scale

Based on the system font (San Francisco on macOS, Segoe UI on Windows) with a
fallback chain. Sizes: 11/12/13/14/16/20/24/32 px. The scale is defined as
Avalonia `FontSize` resources keyed `Type.Size.Caption` (11px) through
`Type.Size.Display` (32px). Line heights follow a 1.4x multiplier. The
`Type.Weight.Normal` / `.Medium` / `.SemiBold` / `.Bold` resources map to the
corresponding `FontWeight` values.

### IconCatalog architecture

The `IconCatalog` is a build-time source generator (or a T4 template) that
reads `icons-manifest.json` (the machine-readable icon registry) and generates:

1. `IconKey.g.cs` — a C# `public enum IconKey { ... }` with every icon key as
   a member; used by `IconButton` binding.
2. `IconMetadata.g.cs` — a dictionary from `IconKey` to `IconAssets` record
   (light/dark paths @1x/2x/3x; accessible-label resource key).
3. The build check `OGMA0002`: a Roslyn analyzer that fires if any code assigns
   an `IconKey` value to an `IconButton.Icon` property without a corresponding
   `IconButton.AccessibleLabel` binding or hardcoded string.

`icons-manifest.json` lives in `OgmaLibrary.App/Assets/icons/manifest.json`
and is the machine-readable source of truth for the MASTER-MANIFEST.md.

### IconButton control

`IconButton` is a custom Avalonia `Button` subclass that wraps the
`IconCatalog` usage pattern:

```csharp
public class IconButton : Button
{
    public static readonly StyledProperty<IconKey> IconProperty;
    public static readonly StyledProperty<string> AccessibleLabelProperty;
    // The control template renders: [icon image] [label text (visually hidden
    //   by default if ShowLabel=false, but always present for screen readers)]
}
```

Every toolbar button, menu item with an icon, command-palette result row, and
action button is an `IconButton` (or an `IconMenuItem`). This ensures that
colorful icons and accessible labels are always paired — it is structurally
impossible to have one without the other.

### Command palette architecture

`CommandPaletteViewModel` holds a `IReadOnlyList<CommandEntry>` (all registered
commands); on query change, it filters to a `FilteredCommands` collection using
a case-insensitive prefix/fuzzy match (no external library; simple `Contains`
for MVP, upgraded to fuzzy in Phase 15 if needed).

`CommandEntry` record: `IconKey`, `LabelKey` (string resource key), `KeyboardShortcut`
(string, e.g. "Ctrl+O"), `Execute` (`Action`), `Category` (string resource key).

Commands are registered via `ICommandRegistry.Register(CommandEntry)` in the
DI composition root (`App`). Each bounded context registers its own commands in
Phase 03 (the ~30 MVP commands from CON-4). Subsequent phases add commands via
the same registry.

`CommandPaletteControl` is keyboard-driven:
- `Ctrl+K` / `Cmd+K`: open.
- `ArrowUp` / `ArrowDown`: navigate results.
- `Enter`: execute the selected command.
- `Escape`: dismiss.

The control has a `CommandPaletteAutomationPeer` that exposes its role as
`ListBox` (for screen readers), with each item exposing its label and keyboard
shortcut as the automation name.

### ILocalizationService implementation

ADR for resource format: use `.resx` (Phase 03 creates the ADR in
`docs/adrs/ADR-0011.md` — "Localization resource format: .resx with
ResxLocalizationService adapter"). The implementation is
`ResxLocalizationService : ILocalizationService`:

- `Get(string key)`: looks up `key` in the active culture's `ResourceManager`.
  Falls back to `en` if the key is missing in the active culture (degraded
  gracefully, never throws to the UI; logs a warning for missing keys).
- `SetCulture(CultureInfo culture)`: sets `CultureInfo.CurrentUICulture` and
  raises `CultureChanged` event that all bindings subscribe to via an
  Avalonia binding adapter.
- Pluralization: `GetPlural(string key, int count)` uses a
  `IPluralRuleProvider` (an ICU-style rule engine for en and fr plural
  categories: `one`/`other` for en; `one`/`other` for fr — but fr has special
  rules for 0 that must be correct).
- Pseudolocale: `PseudolocaleLocalizationService` wraps every string in
  `[» ... «]` and pads to 130% length (simulating longer European translations).

### Accessibility scaffold

- **Focus ring**: defined as a custom `FocusAdorner` in the Avalonia theme:
  2 px solid `Color.Accent.Ink`, 2 px offset from the control bounds.
- **Tab order**: all interactive controls in Phase 03 (command palette, main
  toolbar stub) have explicit `TabIndex` set; the logical order is document-
  flow order (top-to-bottom, left-to-right on LTR layouts).
- **Automation peers**: `CommandPaletteAutomationPeer` (role: `ListBox`),
  `CommandPaletteItemAutomationPeer` (role: `ListItem`),
  `IconButtonAutomationPeer` (role: `Button`; name from `AccessibleLabel`).
- **Contrast verification test**: a unit test in `OgmaLibrary.Tests` iterates
  every foreground/background token combination used in Phase 03 controls and
  asserts the WCAG relative luminance contrast ratio ≥ 4.5:1 (normal text)
  or ≥ 3:1 (large text / UI components). The contrast formula is implemented
  per WCAG 2.1 §1.4.3.
- **`IMotionPreferences`**: detects the OS reduce-motion setting
  (`SystemParameters.HighContrast` on Windows; `NSWorkspace.accessibilityDisplayShouldReduceMotion`
  on macOS via P/Invoke); if true, all motion durations are set to 0 ms.

### Cross-platform approach (Windows + macOS)

- **Icons**: PNG @1x/2x/3x cover both standard and Retina/HiDPI displays on
  macOS; `WritableBitmap` scaling on Windows is handled by Avalonia's image
  scaling pipeline. The import script validates that each @2x icon is exactly
  2× the @1x pixel dimensions.
- **Theming**: Avalonia FluentTheme is the base on both platforms;
  the Ogma token overrides are platform-independent (no `#if`). The only
  platform-specific adaptation is the `IMotionPreferences` P/Invoke call.
- **Command palette shortcut**: `Ctrl+K` on Windows, `Cmd+K` on macOS.
  Avalonia's `KeyGesture` handles this via
  `new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Meta)`? No —
  Avalonia uses `KeyModifiers.Control` on both platforms by default for Ctrl,
  but macOS users expect Cmd. Use platform detection:
  `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)` to bind `Cmd+K` on macOS.
  The command registration abstracts this so each `CommandEntry` specifies
  `WindowsShortcut` and `MacShortcut` separately if they differ.
- **Screen-reader compatibility**: Narrator (Windows) and VoiceOver (macOS)
  both consume Avalonia automation peers. Manual walkthrough tests are performed
  on both platforms and results recorded in `docs/a11y/WALKTHROUGHS.md`.

---

## 7. Work breakdown (summary)

| WP | Work package | Est. |
| --- | --- | --- |
| WP1 | Design tokens (color/type/spacing/motion) + owner token review | 2 d |
| WP2 | Avalonia Fluent theming extensions + control themes | 2 d |
| WP3 | IconCatalog registry + OGMA0002 build check + IconButton control | 2 d |
| WP4 | PNG asset pipeline + placeholder icon generation + MASTER-MANIFEST.md | 1 d |
| WP5 | ILocalizationService implementation + ADR-0011 + en/fr resource files | 2 d |
| WP6 | Command palette (UI + VM + ICommandRegistry + 30 MVP commands) | 2 d |
| WP7 | Accessibility scaffold (focus ring, tab order, automation peers, contrast test, IMotionPreferences) | 2 d |
| WP8 | Design audit, SR walkthroughs, icon procurement request, DoD checklist | 1.5 d |
| WP9 | Code review + merge | 0.5 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** Phase 03 creates the icon system. The
  first batch of ~30 command-palette + chrome icons is requested from the owner
  (see `icons.md`). Placeholders are generated programmatically. The
  MASTER-MANIFEST.md is created. `OGMA0002` enforces icon-label pairing.
- [x] **i18n (en/fr):** `ILocalizationService` is implemented; 100% en + fr
  coverage for all Phase 03 strings; pseudolocale (`qps-ploc`) CI check passes;
  the resource-format ADR (ADR-0011) is recorded. The `OGMA0001` build check
  from Phase 02 continues to enforce no hard-coded strings.
- [x] **Accessibility (keyboard + SR):** Focus ring, tab order, automation
  peers, and the contrast test are all in place. Manual SR walkthroughs on
  Narrator + VoiceOver are documented. `IMotionPreferences` respects the OS
  reduce-motion setting.
- [x] **Privacy/egress:** No off-device calls in Phase 03. The architecture
  test `Architecture_OnlyInfrastructureUsesHttpClient` continues to pass.
- [x] **Reversibility:** No user data operations in Phase 03.
- [x] **Performance budgets:** Command palette filter P95 < 50 ms (NFR-PROD-005
  proxy) on reference hardware. Motion durations are within the 100–300 ms
  design token range. IBenchmarkContext used to measure command palette open
  time; result recorded in `BenchmarkBaseline.md`.
- [x] **Bounded-context tests:** Architecture tests from Phase 02 continue
  to pass. Phase 03 adds no new bounded contexts; it adds the Theming and
  Localization cross-cutting services, which live in `App` and `Application`
  respectively.
- [x] **Documentation:** ADR-0011 committed; MASTER-MANIFEST.md created;
  `docs/a11y/WALKTHROUGHS.md` started; developer guide updated with design
  system usage; `CLAUDE.md` updated.

---

## 9. Definition of Done

- [ ] `DesignTokens.axaml` and `DesignTokens.Dark.axaml` are committed and
  reviewed by the owner; every token has a name, a light value, a dark value,
  and a comment explaining its use.
- [ ] All 10 control themes (Button, MenuItem, TreeView, ListBox, TextBox,
  ComboBox, Slider, ToggleSwitch, ProgressBar, ScrollBar) are overridden with
  Ogma tokens; a visual review on Windows + macOS confirms the calm-control
  aesthetic.
- [ ] `IconCatalog` compiles; `OGMA0002` fires on a deliberate violation (an
  `IconButton` with `Icon` but no `AccessibleLabel`) and does not fire on a
  correct `IconButton` usage.
- [ ] MASTER-MANIFEST.md exists at
  `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md` with all Phase 03 icon
  entries; all entries have status `⬜ to procure` or `🟨 placeholder in use`.
- [ ] Placeholder PNGs exist for all Phase 03 icon keys (24x24 @1x minimum;
  @2x and @3x generated by the import script at 2× and 3× dimensions).
- [ ] `ILocalizationService` implementation passes all localization tests;
  pseudolocale runner passes (`qps-ploc` mode); 100% key coverage in `fr.resx`
  for all Phase 03 strings.
- [ ] ADR-0011 (resource format choice) is committed as `Accepted`.
- [ ] Command palette opens on `Ctrl+K` (Windows) and `Cmd+K` (macOS);
  filters commands correctly; executes on Enter; dismisses on Escape.
  P95 filter latency < 50 ms on reference hardware.
- [ ] All Phase 03 commands are localized in en + fr and carry a colorful
  icon key.
- [ ] Accessibility: contrast ratio test passes (all token combinations ≥ 4.5:1
  for normal text, ≥ 3:1 for large/UI); keyboard walkthrough of command palette
  passes; Narrator (Windows) and VoiceOver (macOS) SR walkthroughs are
  documented in `docs/a11y/WALKTHROUGHS.md`.
- [ ] `IMotionPreferences` reduce-motion check works: with the OS setting
  enabled, all Avalonia transitions have 0 ms duration.
- [ ] `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`, and
  architecture tests all pass on both CI runners.
- [ ] `/code-review --effort high` and `frontend-ux:design-audit` skill pass;
  all findings resolved.
- [ ] No open R1 or R2 defect.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `frontend-design:frontend-design` — design token system architecture;
  Avalonia theming patterns.
- `frontend-ux:premium-ui-ux-design` — calm-control aesthetic; colorful icon
  system; command palette UX.
- `frontend-ux:practical-ui-design` — control theme details; spacing and
  typography decisions.
- `frontend-ux:motion-design` — motion token values; command palette
  open/close transitions.
- `frontend-ux:design-audit` — final audit of all Phase 03 controls before DoD.
- `frontend-ux:ux-principles-101` — WCAG 2.2 AA compliance strategy;
  accessible icon pairing.
- `frontend-ux:interaction-design-patterns` — command palette keyboard
  interaction model; focus management.
- `frontend-ux:image-compression` — PNG optimization for the icon asset
  pipeline (@1x/2x/3x).
- `document-skills:theme-factory` / `document-skills:brand-guidelines` —
  design token codification and the icon style guide.
- `ux-content-strategy` + `content-writing` — en source strings; fr translation
  quality for MVP.
- `avalonia-desktop-development` — Avalonia-specific theming, custom controls,
  `AutomationPeer`, headless test patterns.
- Reference: `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md`.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| `DesignTokens.axaml`, `DesignTokens.Dark.axaml` | `src/OgmaLibrary.App/Themes/` |
| 10 control theme AXAML files | `src/OgmaLibrary.App/Themes/ControlThemes/` |
| `IconButton.axaml`, `IconButton.cs` | `src/OgmaLibrary.App/Controls/` |
| `icons-manifest.json` | `src/OgmaLibrary.App/Assets/icons/` |
| `IconKey.g.cs`, `IconMetadata.g.cs` (generated) | build output |
| `OGMA0002` analyzer | `src/OgmaLibrary.App/Analyzers/` |
| `Import-Icons.ps1`, `import-icons.sh` | `scripts/` |
| Placeholder PNGs (@1x/2x/3x) for all Phase 03 keys | `src/OgmaLibrary.App/Assets/icons/<category>/` |
| `MASTER-MANIFEST.md` | `docs/plans/grand-plan/_icons/` |
| `ResxLocalizationService.cs` (or `JsonLocalizationService.cs`) | `src/OgmaLibrary.Application/Localization/` |
| `en.resx`, `fr.resx` (Phase 03 additions) | `src/OgmaLibrary.App/Localization/` |
| `IPluralRuleProvider.cs`, `EnFrPluralRuleProvider.cs` | `src/OgmaLibrary.Application/Localization/` |
| `PseudolocaleLocalizationService.cs` | `src/OgmaLibrary.Application/Localization/` |
| `ADR-0011.md` (localization resource format) | `docs/adrs/` |
| `CommandPaletteControl.axaml`, `CommandPaletteViewModel.cs` | `src/OgmaLibrary.App/Controls/CommandPalette/` |
| `ICommandRegistry.cs`, `CommandEntry.cs` | `src/OgmaLibrary.Application/Commands/` |
| `CommandPaletteAutomationPeer.cs` | `src/OgmaLibrary.App/Accessibility/` |
| `IMotionPreferences.cs`, platform impls | `src/OgmaLibrary.Application/` + `src/OgmaLibrary.Infrastructure/` |
| `docs/a11y/WALKTHROUGHS.md` (SR walkthroughs) | `docs/a11y/` |
| `BenchmarkBaseline.md` (updated with command palette latency) | `docs/performance/` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Owner token review delays theming work | R5 | Start WP2 with the proposed token values; mark controls as "pending token confirmation"; the owner review is gated at WP8, not WP1, so design work is not blocked on the exact color values — only the final approval is. |
| Premium PNG procurement takes longer than 2 weeks | R5 | Placeholder PNGs (programmatically generated) unblock all UI work. Premium PNGs are a release gate (not a phase gate); Phase 03 can close with placeholders provided they are flagged `🟨 placeholder in use` in the MASTER-MANIFEST.md and tracked as a release blocker. |
| `OGMA0002` false positives on valid IconButton usages | R5 | The analyzer has an `[SuppressMessage]` escape hatch; any false positive is documented with a justification comment. The target is < 3 false positives on Phase 03 code. |
| Avalonia headless test infrastructure incompatible with OGMA0001/0002 analyzers | R5 | Analyzers run at build time, not at test time; no conflict expected. If there is a namespace collision between the headless test assembly and the analyzer target, use an `AnalyzerReference` exclusion in the test project's `.csproj`. |
| macOS VoiceOver screen-reader walkthrough requires physical macOS hardware | R5 | Use the reference macOS machine (M1 MacBook Air); if access is limited, the walkthrough can be performed once on the owner's machine and results recorded by the engineer. A video recording supplements the written walkthrough notes. |
| French plural rules for count = 0 are frequently wrong in hand-written resource files | R5 | The `IPluralRuleProvider` unit tests include count = 0 test cases for both en and fr; the `fr` rule must return `"one"` category for 0 (French grammar rule) while en returns `"other"` for 0. Test is written before the implementation (TDD). |

---

## 13. Owner asks

1. **Design token values approval:** Review and approve (or adjust) the proposed
   color token values in `DesignTokens.axaml` before Phase 03 WP8. The token
   names are stable; only the color values are subject to owner preference.
   Peter should confirm the warm library aesthetic (oak amber, parchment,
   walnut dark) matches his vision for the product.
2. **Icon vendor / style selection (ICON-SYSTEM.md §5):** Choose one cohesive
   premium icon family from the vendor shortlist prepared in WP4. The vendor
   must supply colorful/duotone PNGs at @1x/2x/3x with a license that permits
   Mac App Store + Windows Store embedding. Peter confirms the vendor choice;
   the team provides a shortlist of 2–3 candidates with sample icon previews.
3. **First icon procurement batch:** Once the vendor is chosen, Peter purchases
   the icon set for all keys listed in `icons.md §Phase 03 icon manifest`.
   The team needs the purchased PNGs no later than Phase 06 (catalogue browsing)
   to meet the DoD for shipped UI. (Phase 03 closes with placeholders.)
4. **Command-palette command list approval (CON-4 refinement):** The Phase 00
   decision (CON-4) provided a first-pass list; Phase 03 implements exactly that
   list. If Peter has additions or removals before WP6 starts, they should be
   communicated before Day 6 of the phase.
5. **Language preference for localization tool:** The team recommends `.resx`
   (ADR-0011 recommendation). If Peter prefers PO/Gettext format (which many
   professional translators prefer), the ADR should be updated before WP5
   begins. The `ILocalizationService` abstraction makes the format swappable
   later, but the initial implementation choice affects the tooling.
6. **French translation review:** The `fr.resx` strings added in Phase 03 must
   be reviewed by a native French speaker before the MVP release gate. Peter
   should identify the reviewer. The review can be deferred to Phase 21's i18n
   completeness gate, but the reviewer must be identified in Phase 03.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
