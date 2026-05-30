# Phase 03 — Tasks

> Work packages and tasks for Design System, Icon System & UX Foundation.
> ID format: `P03-WP<n>-T<m>`.

---

## WP1 — Design tokens (color / typography / spacing / motion)

**Goal:** every visual constant is a named token, not a hard-coded value.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP1-T1 | Invoke `superpowers:brainstorming` + `frontend-ux:premium-ui-ux-design` to explore the color token design before writing any XAML. Produce a one-page token proposal covering: (a) the 8 accent + 2 surface families from ICON-SYSTEM.md §4; (b) semantic mappings (what functions use oak vs ink vs sage vs clay vs plum vs slate); (c) light vs dark value candidates. This proposal is sent to the owner for approval (Owner ask #1). | Phase 00 product promise, ICON-SYSTEM.md §4 | 0.5 d | Grand Plan principle 7/8, design tokens |
| P03-WP1-T2 | Write `DesignTokens.axaml`: define all color tokens as `SolidColorBrush` `StaticResource` entries (e.g. `<Color x:Key="Color.Accent.Oak">#D4922A</Color>`); all typography tokens (`FontFamily`, `FontSize`, `FontWeight`); all spacing tokens (as `Thickness` or `Double` resources); all border-radius tokens; all elevation/shadow tokens (as `BoxShadow` strings). | P03-WP1-T1 proposal | 0.5 d | Grand Plan principle 7, calm-control design language |
| P03-WP1-T3 | Write `DesignTokens.Dark.axaml`: override every color token that has a different dark-mode value. The non-color tokens (typography, spacing) are not overridden (same in both themes). | P03-WP1-T2 | 0.25 d | Dark theme, accessibility (contrast) |
| P03-WP1-T4 | Write motion tokens: Avalonia `Transitions` XML items for `Opacity`, `RenderTransform`, and `Background` with the Phase 03 durations (100/200/300 ms) and easing functions (`CubicEaseOut`, `LinearEasing`). Define these as reusable XAML `Transition` `ResourceDictionary` entries, not inline on controls. | P03-WP1-T2 | 0.25 d | Grand Plan principle 7, motion design |
| P03-WP1-T5 | Write a unit test `DesignTokenContrast_AllCombinations_MeetAA` that iterates all foreground/background token pairs used in Phase 03 controls (see WP7-T3) and asserts WCAG 2.2 AA contrast (≥ 4.5:1 for normal text, ≥ 3:1 for large text/UI). Use a pure C# WCAG relative-luminance formula. | P03-WP1-T2/T3 | 0.5 d | NFR-PROD-008, WCAG 2.2 AA, accessibility gate |

---

## WP2 — Avalonia Fluent theming + control themes

**Goal:** all 10 standard Avalonia controls render with Ogma tokens on both
platforms.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP2-T1 | Invoke `avalonia-desktop-development` and `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` to confirm the correct approach for extending Avalonia `FluentTheme` with custom control themes. The standard approach is to copy the Avalonia source control template XAML and replace hard-coded values with token `StaticResource` references. | Phase 02 solution, ADR-0002 | 0.25 d | ADR-0002, Avalonia theming approach |
| P03-WP2-T2 | Write `Button.axaml` control theme: override `Background`, `Foreground`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding`, and `:hover`, `:pressed`, `:disabled` states using Ogma tokens. Apply motion token for hover background-color transition (100 ms). | P03-WP1-T2, P03-WP2-T1 | 0.25 d | Grand Plan principle 7 (calm-control button) |
| P03-WP2-T3 | Write `MenuItem.axaml` control theme: override separator color, selection color, icon area width (24 px to accommodate the icon system), keyboard-shortcut text style. | P03-WP2-T2 | 0.25 d | Grand Plan principle 8 (menu item icons) |
| P03-WP2-T4 | Write `TextBox.axaml`: focus border uses `Color.Accent.Ink` at 2 px; placeholder text uses 60% opacity of `Foreground`. | P03-WP2-T2 | 0.25 d | Calm-control text input |
| P03-WP2-T5 | Write remaining 7 control themes: `TreeView.axaml`, `ListBox.axaml`, `ComboBox.axaml`, `Slider.axaml`, `ToggleSwitch.axaml`, `ProgressBar.axaml`, `ScrollBar.axaml`. Each overrides the visual states using Ogma tokens only (no raw colors). | P03-WP2-T2 | 1 d | Grand Plan principle 7, all 10 control themes |
| P03-WP2-T6 | Write `CommandPalette.axaml` control theme (initially a stub; full implementation in WP6). Theme: overlay panel with `Color.Surface.Parchment` background in light / `Color.Surface.Walnut` in dark; 12 px `CornerRadius`; elevation shadow token; 80% opacity backdrop. | P03-WP2-T2 | 0.25 d | Command palette visual design |
| P03-WP2-T7 | Visual review of all control themes on Windows (reference hardware or CI screenshot test) and macOS (reference hardware). The review checks: (a) correct token application, (b) no raw color values left, (c) hover/focus/disabled states render correctly, (d) dark theme toggle works. Produce a screenshot set for the owner review. | P03-WP2-T2..T6 | 0.5 d | Grand Plan principle 7, cross-platform parity |

---

## WP3 — IconCatalog registry + OGMA0002 + IconButton

**Goal:** the icon system is operational and structurally impossible to misuse.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP3-T1 | Invoke `superpowers:test-driven-development` for OGMA0002. Write the failing analyzer unit test first: a source snippet with `<IconButton Icon="IconKey.Scan" />` but no `AccessibleLabel` should produce `OGMA0002`. Then write the passing test: `<IconButton Icon="IconKey.Scan" AccessibleLabel="{loc:Loc Icons.Scan.Label}" />` produces no diagnostic. | Phase 02 OGMA0001 pattern | 0.25 d | OGMA0002 TDD, ICON-SYSTEM.md §6 |
| P03-WP3-T2 | Write `icons-manifest.json`: a JSON array of icon entries, each with fields: `key` (string enum name), `category` (bounded-context name), `usedOn` (description), `meaning` (accessible meaning), `styleNote` (style/color hint), `sizes` (array of int px), `labelResourceKey` (string). Populate with all Phase 03 icon keys from `icons.md`. | Phase 03 icons.md, P03-WP3-T1 | 0.5 d | ICON-SYSTEM.md §3, MASTER-MANIFEST.md |
| P03-WP3-T3 | Write the `IconCatalog` source generator (T4 template or Roslyn `ISourceGenerator`) that reads `icons-manifest.json` at build time and emits `IconKey.g.cs` and `IconMetadata.g.cs`. Test: build the project; confirm the generated files contain the correct enum members and dictionary entries for all Phase 03 keys. | P03-WP3-T2 | 0.5 d | ICON-SYSTEM.md §2 (IndexCatalog registry) |
| P03-WP3-T4 | Write the `OGMA0002` DiagnosticAnalyzer. Detection logic: any XAML element whose type is `IconButton` (or `IconMenuItem`) and that has a non-empty `Icon` attribute but lacks an `AccessibleLabel` attribute (or `AccessibleLabel` binding) produces `OGMA0002`. | P03-WP3-T1, P03-WP3-T3 | 0.5 d | ICON-SYSTEM.md §6, OGMA0002 |
| P03-WP3-T5 | Write `IconButton.cs` and `IconButton.axaml`: a `Button` subclass with `IconProperty` (`IconKey`) and `AccessibleLabelProperty` (string). The control template renders: (1) an `Image` bound to the active culture's active theme's asset path from `IconMetadata`; (2) a `TextBlock` with `AccessibleLabel` that is visually hidden when `ShowLabel=false` but always present in the accessibility tree (using `IsVisible=false` vs `Opacity=0` — use `Opacity=0; PointerEvents=None` pattern so the element stays in the automation tree). | P03-WP3-T3, P03-WP1-T2 | 0.5 d | ICON-SYSTEM.md §1/2, accessibility |
| P03-WP3-T6 | Write `IconButton.axaml` `AutomationPeer` override (or `IconButtonAutomationPeer.cs`): role = `Button`; name = `AccessibleLabel` resource value; description = `Meaning` from `IconMetadata`. Confirm Narrator (Windows) reads the correct label for an `IconButton` in the headless automation test. | P03-WP3-T5 | 0.25 d | NFR-PROD-008, WCAG 2.2 accessibility |
| P03-WP3-T7 | Confirm the full OGMA0002 build check is wired in `Directory.Build.props` and fires on a deliberate violation in the Phase 03 code base. | P03-WP3-T4 | 0.1 d | Build gate, ICON-SYSTEM.md §6 |

---

## WP4 — PNG asset pipeline + placeholders + MASTER-MANIFEST.md

**Goal:** the icon pipeline is scripted; placeholder assets unblock UI work.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP4-T1 | Write `scripts/Import-Icons.ps1` (Windows/macOS PowerShell Core): takes a source directory of vendor PNGs named `<key>@Nx.png`; validates sizes (@1x=24, @2x=48, @3x=72 px for the toolbar size; @1x=16, @2x=32, @3x=48 for small); copies to `src/OgmaLibrary.App/Assets/icons/<category>/`; updates `icons-manifest.json` status for each icon from `⬜ to procure` to `✅ premium PNG wired`. Write `scripts/import-icons.sh` (bash equivalent). | P03-WP3-T2 | 0.5 d | ICON-SYSTEM.md §2 (PNG pipeline) |
| P03-WP4-T2 | Write a placeholder icon generator in `scripts/Generate-Placeholders.ps1`: uses SkiaSharp (available as a NuGet CLI tool or a dotnet script) to generate a 24x24 px PNG for each icon key in `icons-manifest.json` that has no premium PNG. The placeholder is a gray circle with the icon key's initials (first 2 chars) in white, 12px font. Also generates @2x (48x48) and @3x (72x72) versions by scaling. | P03-WP3-T2 | 0.5 d | ICON-SYSTEM.md §3 (placeholder workflow) |
| P03-WP4-T3 | Run `Generate-Placeholders.ps1` for all Phase 03 icon keys. Confirm all placeholder PNGs are generated in the correct directory structure and pass a SkiaSharp bitmap load without error. | P03-WP4-T2 | 0.25 d | Unblocks WP6 (command palette UI can display icons) |
| P03-WP4-T4 | Create `docs/plans/grand-plan/_icons/MASTER-MANIFEST.md`. This file contains: (a) an intro section explaining the procurement workflow; (b) a table with columns `Icon key | Phase introduced | Category | Used on | Meaning | Style/color note | Sizes | Status | Phase wired`; (c) all Phase 03 icon keys with status `🟨 placeholder in use`; (d) empty placeholder rows for future phases (00-02 stubs with note "no icons"). | P03-WP3-T2, P03-WP4-T3 | 0.5 d | ICON-SYSTEM.md §6 (MASTER-MANIFEST.md) |

---

## WP5 — ILocalizationService implementation + ADR-0011

**Goal:** the full, production-grade localization service replaces the Phase 02
stub; en + fr are 100% complete for Phase 03 surfaces.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP5-T1 | Draft ADR-0011: "Localization resource format — .resx with ResxLocalizationService." Context: `.resx` is the .NET native format; strongly-typed accessor; easy satellite assembly deployment. Alternatives: JSON (translator-friendly, no strong typing), PO/Gettext (professional translator tooling). Decision: `.resx` with a `ILocalizationService` adapter (so format is swappable). Consequences: Phase 21 may switch to PO if a professional translator requires it; the adapter pattern makes this cheap. Status: Proposed → Accepted (owner sign-off per Owner ask #5). | Phase 00 OQ decisions | 0.25 d | ADR-0011, I18N-STRATEGY §2 |
| P03-WP5-T2 | Write `IPluralRuleProvider` interface and `EnFrPluralRuleProvider` implementing ICU-style plural categories: `en` (one/other; 0 → other); `fr` (one/other; 0 → one — French grammar). Write unit tests for: en count=0 (other), en count=1 (one), en count=2 (other); fr count=0 (one), fr count=1 (one), fr count=2 (other). | I18N-STRATEGY §2 | 0.25 d | I18N-STRATEGY §2 (pluralization), FR plural grammar correctness |
| P03-WP5-T3 | Write `ResxLocalizationService : ILocalizationService`. Methods: `Get(key)` → looks up `ResourceManager` for active culture, falls back to `en`, logs warning on missing key; `GetPlural(key, count)` → uses `IPluralRuleProvider` to select the correct plural form key (`key.one` / `key.other`); `SetCulture(culture)` → sets `CultureInfo.CurrentUICulture`, raises `CultureChanged`; `ActiveCulture` property. | P03-WP5-T1/T2 | 0.5 d | I18N-STRATEGY §2, ILocalizationService implementation |
| P03-WP5-T4 | Write `PseudolocaleLocalizationService : ILocalizationService` for CI use: wraps every string in `[» ... «]` and pads to 130% of the original length with `·` characters (simulating longer European translations). Register it in the `App` DI container when an env var `OGMA_PSEUDOLOCALE=1` is set. | P03-WP5-T3 | 0.25 d | I18N-STRATEGY §5 (pseudolocale CI check) |
| P03-WP5-T5 | Add all Phase 03 en strings to `en.resx` (design system error messages, command palette labels, icon accessible labels, motion-preference notification). Add all to `fr.resx` (French translations). Entries must follow the naming convention: `{Control}.{Property}` e.g. `CommandPalette.Placeholder`, `Icons.Scan.Label`. | P03-WP5-T3, Phase 03 command list (CON-4) | 0.5 d | I18N-STRATEGY §3, Global DoD §4 |
| P03-WP5-T6 | Write integration test `LocalizationService_FrenchCulture_AllPhase03KeysPresent`: iterates all resource keys in `en.resx`; for each, calls `ResxLocalizationService.Get(key)` with `fr` culture; asserts the result is not equal to the key itself (i.e., the key is translated, not falling back). This is the per-phase `fr` completeness gate. | P03-WP5-T3/T5 | 0.25 d | I18N-STRATEGY §5 (en/fr completeness gate), Global DoD §4 |
| P03-WP5-T7 | Update the pseudolocale CI test (from Phase 02) to use `PseudolocaleLocalizationService` with `OGMA_PSEUDOLOCALE=1`; confirm it passes with all Phase 03 strings rendered without `MissingManifestResourceException` or overflow/clipping (the headless Avalonia app renders the command palette in pseudolocale mode). | P03-WP5-T4, WP6 command palette complete | 0.25 d | I18N-STRATEGY §5 pseudolocale CI check |

---

## WP6 — Command palette

**Goal:** the command palette is fully functional, localized, iconified, and
keyboard-accessible.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP6-T1 | Write `CommandEntry.cs`: `record CommandEntry(IconKey Icon, string LabelKey, string? MacShortcut, string? WindowsShortcut, Action Execute, string CategoryKey)`. Write `ICommandRegistry.cs`: `void Register(CommandEntry entry)`, `IReadOnlyList<CommandEntry> GetAll()`. Write `InMemoryCommandRegistry : ICommandRegistry` (thread-safe, `ConcurrentBag`). | Phase 02 DI, CON-4 command list | 0.25 d | CON-4, FR-CAT-001 (navigation commands), command palette architecture |
| P03-WP6-T2 | Register the ~30 MVP commands in `App`'s DI composition root (or via an Avalonia `ApplicationLifetime.Startup` handler). Each command must have: a `IconKey` (from Phase 03 icons), a `LabelKey` (from `en.resx`), a keyboard shortcut (Windows and macOS variants), and an `Execute` action (stub implementations for commands whose features are not built yet — e.g. `OpenBook` stubs with `TODO` comment). | P03-WP6-T1, CON-4 approved command list | 0.5 d | CON-4, Global DoD §4 (all commands localized + iconified) |
| P03-WP6-T3 | Write `CommandPaletteViewModel.cs`: `Query` property (string, observable); `FilteredCommands` (computed from `Query` using case-insensitive `Contains` match on `LabelKey` localized value + category); `SelectedIndex` (int); `ExecuteSelectedCommand()`. All observable properties use `CommunityToolkit.Mvvm` (or ReactiveUI, per AVALONIA-STANDARDS.md decision from Phase 02). | P03-WP6-T1/T2 | 0.5 d | Command palette VM, NFR-PROD-005 (filter latency) |
| P03-WP6-T4 | Write `CommandPaletteControl.axaml`: the overlay panel with `CommandPalette.axaml` control theme; a `TextBox` bound to `Query`; a `ListBox` bound to `FilteredCommands` with an `IconButton`-based item template (icon + label + shortcut text). Open/close animations use the WP1 motion tokens (200 ms ease-out opacity + Y-translate of 8 px). | P03-WP2-T6, P03-WP3-T5, P03-WP6-T3 | 0.5 d | Command palette UI, Grand Plan principle 8 (icons), motion design |
| P03-WP6-T5 | Wire the `Ctrl+K` / `Cmd+K` key binding in `MainWindow.axaml.cs` using platform-specific detection. Test: in the headless Avalonia test, simulate the key gesture and assert `CommandPaletteViewModel.IsOpen = true`. Simulate `Escape` and assert `IsOpen = false`. | P03-WP6-T4 | 0.25 d | CON-4, NFR-PROD-007 (keyboard operability) |
| P03-WP6-T6 | Measure command palette filter latency: with the `StopwatchBenchmarkContext` injected into `CommandPaletteViewModel`, measure the time from `Query` property change to `FilteredCommands` update for a 30-command list. Assert P95 < 50 ms in the automated test (simulating 100 rapid filter operations). | P03-WP6-T3, Phase 02 IBenchmarkContext | 0.25 d | NFR-PROD-005 (no stall > 100 ms; filter target ≤ 50 ms) |

---

## WP7 — Accessibility scaffold

**Goal:** all Phase 03 interactive controls are keyboard-navigable, screen-
reader-announced, and AA-contrast-compliant.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP7-T1 | Define the focus ring: write a global `FocusAdorner` style in `DesignTokens.axaml` that applies a 2 px solid `Color.Accent.Ink` ring with 2 px offset to any focused interactive control. Confirm it renders on Windows and macOS (screenshot review). | P03-WP1-T2 | 0.25 d | WCAG 2.2 SC 2.4.11 (Focus Appearance), NFR-PROD-007 |
| P03-WP7-T2 | Set `TabIndex` for all Phase 03 interactive controls: `MainWindow` shell tab order (menu bar → toolbar stub → content area → status bar); `CommandPalette` tab order (text box → result list → close button). Write a keyboard walkthrough test: use Avalonia headless keyboard simulation to Tab through all controls in the correct order. | P03-WP6-T4, P03-WP7-T1 | 0.25 d | NFR-PROD-007, WCAG 2.2 SC 2.1.1 |
| P03-WP7-T3 | Write the WCAG contrast verification test (referenced in WP1-T5 above): define a dictionary of all foreground/background token pairs used in Phase 03 controls (9 pairs: normal text on parchment, button text on oak, etc.); compute relative luminance for each pair; assert contrast ratio. This test runs in CI and fails if any Phase 03 token change creates an AA violation. | P03-WP1-T2, P03-WP1-T5 | 0.25 d | NFR-PROD-008, WCAG 2.2 AA |
| P03-WP7-T4 | Write `CommandPaletteAutomationPeer.cs`: role = `List` (`AutomationControlType.List`); label = `_loc.Get("CommandPalette.AccessibilityLabel")`; child peers = `CommandPaletteItemAutomationPeer` for each item (role = `ListItem`; name = localized label + shortcut). | P03-WP6-T4 | 0.25 d | NFR-PROD-008, automation peer, SR compatibility |
| P03-WP7-T5 | Write `IMotionPreferences.cs` (interface: `bool IsReduceMotionEnabled { get; }`) and platform implementations: `WindowsMotionPreferences` (reads `SystemParameters.MenuAnimation` or user preference key); `MacOsMotionPreferences` (P/Invoke `NSWorkspace.accessibilityDisplayShouldReduceMotion`). Integrate with the Avalonia transition system: when `IsReduceMotionEnabled = true`, set all transition `Duration` to `TimeSpan.Zero`. | P03-WP1-T4 | 0.5 d | WCAG 2.2 SC 2.3.3 (Animation from Interactions), accessibility |
| P03-WP7-T6 | Perform manual SR walkthroughs: (a) Windows Narrator: open the main window, open the command palette (Ctrl+K), navigate results (arrow keys), confirm Narrator announces each item's label and shortcut. (b) macOS VoiceOver: same steps with Cmd+K and VO+arrow. Record the pass/fail for each step in `docs/a11y/WALKTHROUGHS.md §Phase03`. | P03-WP6-T5, P03-WP7-T4, real hardware | 0.5 d | NFR-PROD-008, Grand Plan principle 10 |

---

## WP8 — Design audit + icon procurement request + DoD

**Goal:** Phase 03 meets the visual quality bar before any subsequent phase
builds on it.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP8-T1 | Run `frontend-ux:design-audit` on all Phase 03 UI surfaces (command palette, main window with themed controls). The audit checks: icon-text pairing, spacing consistency (all spacing from tokens, no magic numbers), contrast, motion, and the calm-control aesthetic. Record audit findings and resolve all `Error`-severity items before DoD. | WP1..WP7 complete | 0.5 d | Grand Plan principle 8, design audit gate |
| P03-WP8-T2 | Prepare the owner icon-procurement request (from `icons.md`): a formatted document listing all Phase 03 icon keys, the agreed style tokens (from ICON-SYSTEM.md §4), the target sizes (@1x/2x/3x; 16/24/32/48 px base sizes), the density requirements (Retina/HiDPI), and the license requirement (MSIX + Mac App Store embedding). Send to Peter. | P03-WP4-T4 (MASTER-MANIFEST.md), P03-WP3-T2 | 0.25 d | ICON-SYSTEM.md §3/5, Owner ask #2/#3 |
| P03-WP8-T3 | Owner token review: present `DesignTokens.axaml` color values and the control theme screenshots to Peter; record the approved/adjusted values in `decisions.md §Phase03-tokens`. If any token value changes, update `DesignTokens.axaml` and re-run the contrast test. | P03-WP1-T1, P03-WP2-T7 (screenshots) | 0.25 d | Owner ask #1, Grand Plan principle 7 |
| P03-WP8-T4 | Run the full Phase 03 DoD checklist (README §9). File any open items as GitHub issues. If premium PNGs have been received from the owner, run `Import-Icons.ps1` and update the MASTER-MANIFEST.md status. Otherwise, confirm all placeholders are `🟨 placeholder in use` and tracked as a release blocker. | All WP1..WP7 tasks | 0.25 d | Phase 03 DoD |

---

## WP9 — Code review + merge

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P03-WP9-T1 | Request `/code-review --effort high` on the Phase 03 PR. Review focus: (a) OGMA0002 analyzer correctness and escape-hatch usage; (b) `ResxLocalizationService` thread safety and fallback behavior; (c) `CommandPaletteViewModel` filter logic correctness; (d) contrast test completeness; (e) `IMotionPreferences` P/Invoke safety; (f) `AutomationPeer` implementations. | All WP1..WP8 tasks | 0.25 d | Global DoD §8 |
| P03-WP9-T2 | Resolve all code review findings. Re-run `dotnet test` and CI on both runners. Confirm the design audit and SR walkthrough results are recorded. | P03-WP9-T1 | 0.25 d | Global DoD §8 |
| P03-WP9-T3 | Merge the Phase 03 feature branch to `develop`. Add the Phase 03 entry to `CHANGELOG.md`. Update `CLAUDE.md` with the design system, icon system, and localization service entry points. | P03-WP9-T2 | 0.1 d | Phase 03 close |
