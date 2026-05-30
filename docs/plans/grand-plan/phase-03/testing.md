# Phase 03 — Test Plan

> Phase 03 produces the first substantial UI code. This plan covers the test
> layers active in Phase 03, the fixtures, the oracles, and the phase's
> contribution to the golden-corpus and performance baselines.

---

## Applicable test layers

| Layer | Active in Phase 03? | Notes |
| --- | --- | --- |
| Domain | No | No domain model changes in Phase 03 |
| Infrastructure | Partial | `ResxLocalizationService` uses `.resx` files; tested as an infrastructure unit test (no DB) |
| PDF | No | No PDF operations in Phase 03 |
| Search | No | Phase 10 |
| AI | No | Phase 12 |
| UI | Yes | Headless Avalonia tests for command palette, localization, pseudolocale, and focus behavior |
| 3D | No | Phase 14 |
| Performance | Yes | Command palette filter latency; command-palette open animation timing |
| Packaging | No | Phase 22 |
| Architecture | Continues | Architecture tests from Phase 02 continue; Phase 03 adds no new bounded-context violations |

---

## Unit tests

### Design token tests

| Test | Oracle | Deterministic? |
| --- | --- | --- |
| `DesignTokenContrast_AllCombinations_MeetAA` | For each of N foreground/background token pairs used in Phase 03 controls, the WCAG relative-luminance contrast ratio is ≥ 4.5:1 for normal-text pairs and ≥ 3:1 for large-text/UI pairs. Computed with the formula: `(L1 + 0.05) / (L2 + 0.05)` where `L1 = max(Lforeground, Lbackground)`. | Yes (pure function of hex color values) |
| `DesignToken_DarkOverride_AllLightTokensPresent` | Every color token in `DesignTokens.axaml` has either an override in `DesignTokens.Dark.axaml` or is documented as "light-only" (e.g. `Color.Surface.Parchment`). Verified by parsing both AXAML files and comparing key sets. | Yes |
| `MotionToken_ReduceMotion_DurationIsZero` | When `IMotionPreferences.IsReduceMotionEnabled = true` (injected stub), all Avalonia transition durations resolve to `TimeSpan.Zero`. Tested via the `CommandPaletteControl`'s open animation (the `Duration` binding evaluates to 0). | Yes (stub injection) |

### Localization tests

| Test | Oracle | Deterministic? |
| --- | --- | --- |
| `PluralRule_English_Zero_IsOther` | `EnFrPluralRuleProvider.GetCategory("en", 0) == "other"` | Yes |
| `PluralRule_English_One_IsOne` | `GetCategory("en", 1) == "one"` | Yes |
| `PluralRule_French_Zero_IsOne` | `GetCategory("fr", 0) == "one"` (French grammar: 0 uses singular) | Yes |
| `PluralRule_French_Two_IsOther` | `GetCategory("fr", 2) == "other"` | Yes |
| `ResxLocalizationService_EnglishKey_ReturnsCorrectValue` | `Get("MainWindow.Title")` with `en` culture returns "Ogma Library" | Yes |
| `ResxLocalizationService_FrenchKey_ReturnsCorrectValue` | `Get("MainWindow.Title")` with `fr` culture returns "Bibliothèque Ogma" | Yes |
| `ResxLocalizationService_MissingKey_FallsBackToEnglish` | A key present in `en.resx` but absent in `fr.resx` returns the `en` value (not a key name or exception) when `fr` culture is active | Yes |
| `LocalizationService_FrenchCulture_AllPhase03KeysPresent` | Iterates all `en.resx` keys; for each, `Get(key)` in `fr` culture returns a value that is not equal to the key itself (translation exists) | Yes (based on committed resx files) |
| `PseudolocaleService_WrapsAllStrings` | `PseudolocaleLocalizationService.Get(anyKey)` returns a string starting with `[»` and ending with `«]` | Yes |
| `PseudolocaleService_LengthIsAtLeast130Percent` | The returned pseudolocale string length is ≥ 130% of the English source string length (ensures layout overflow testing is meaningful) | Yes |

### Analyzer tests (OGMA0001 + OGMA0002)

| Test | Oracle | Tool |
| --- | --- | --- |
| `OGMA0001_FiresOnHardCodedString` | A code snippet `myButton.Content = "Submit";` in a `Window` subclass produces `OGMA0001` at the string literal position | Roslyn `CSharpAnalyzerVerifier` |
| `OGMA0001_DoesNotFireOnLocalizedString` | A code snippet `myButton.Content = _loc.Get("Button.Submit");` produces zero diagnostics | Roslyn `CSharpAnalyzerVerifier` |
| `OGMA0002_FiresOnIconButtonWithoutLabel` | XAML `<IconButton Icon="IconKey.Scan" />` (no `AccessibleLabel`) produces `OGMA0002` | Roslyn `XamlAnalyzerVerifier` (or custom AXAML test) |
| `OGMA0002_DoesNotFireOnCorrectIconButton` | XAML `<IconButton Icon="IconKey.Scan" AccessibleLabel="{loc:Loc Icons.Scan.Label}" />` produces zero diagnostics | Roslyn `XamlAnalyzerVerifier` |

---

## UI layer tests (headless Avalonia)

### Command palette tests

| Test | Oracle | Approach |
| --- | --- | --- |
| `CommandPalette_OpenOnCtrlK_Windows` | Simulating `Ctrl+K` keyboard event on the MainWindow sets `CommandPaletteViewModel.IsOpen = true` | Headless Avalonia keyboard simulation |
| `CommandPalette_OpenOnCmdK_macOS` | Same with `Cmd+K` on macOS (platform-detected binding) | Headless Avalonia; platform-specific KeyModifiers |
| `CommandPalette_DismissOnEscape` | When `IsOpen = true`, simulating `Escape` sets `IsOpen = false` | Headless Avalonia |
| `CommandPalette_Filter_ReturnsMatchingCommands` | Setting `Query = "scan"` on the VM when the "Scan Library" command is registered returns exactly 1 item in `FilteredCommands` | ViewModel unit test (no UI needed) |
| `CommandPalette_Filter_CaseInsensitive` | Setting `Query = "SCAN"` returns the same results as `"scan"` | ViewModel unit test |
| `CommandPalette_Filter_EmptyQuery_ReturnsAllCommands` | Setting `Query = ""` returns all registered commands | ViewModel unit test |
| `CommandPalette_ExecuteOnEnter_InvokesAction` | When a command is selected and `Enter` is simulated, the command's `Execute` action is called exactly once | Headless Avalonia |
| `CommandPalette_KeyboardNavigation_ArrowDown` | When `IsOpen = true` and the list has 3 items, pressing `ArrowDown` three times wraps `SelectedIndex` from 2 to 0 | Headless Avalonia |

### Pseudolocale UI test

| Test | Oracle | Approach |
| --- | --- | --- |
| `Pseudolocale_MainWindowAndCommandPalette_NoMissingKeys` | The headless app with `OGMA_PSEUDOLOCALE=1` opens `MainWindow`, opens the command palette, and navigates all 30 commands without throwing `MissingManifestResourceException` or rendering any `[» [» ... «] «]` double-wrapped strings (which would indicate a key that is looked up but not in the pseudolocale service) | Headless Avalonia; `PseudolocaleLocalizationService` injected |
| `Pseudolocale_NoTruncation_In_CommandPaletteItems` | The pseudolocale command labels (130% length) render without text truncation (ellipsis `…`) in the command palette list at 600 px width (standard command palette minimum width) | Headless Avalonia screenshot comparison: no `TextTrimming.CharacterEllipsis` clip detected |

---

## Accessibility tests

### Contrast tests

All contrast tests are pure-function unit tests; they run in < 1 ms each and
require no UI infrastructure.

| Token pair | Foreground | Background | Expected ratio | WCAG level |
| --- | --- | --- | --- | --- |
| Normal text on parchment (light) | `Color.Foreground.Primary` (`#1A1A1A`) | `Color.Surface.Parchment` (`#FAF7F2`) | ≥ 4.5:1 (expected ~15:1) | AA normal |
| Button label on oak (light) | `#FFFFFF` | `Color.Accent.Oak` (`#D4922A`) | ≥ 4.5:1 | AA normal |
| Command palette placeholder on parchment | `Color.Foreground.Tertiary` (60% of primary) | `Color.Surface.Parchment` | ≥ 4.5:1 | AA normal |
| Focus ring on parchment | `Color.Accent.Ink` (`#2B4A7A`) | `Color.Surface.Parchment` | ≥ 3:1 (focus indicator is a UI component) | AA UI |
| Normal text on walnut (dark) | `Color.Foreground.Primary.Dark` | `Color.Surface.Walnut` (`#1E1A17`) | ≥ 4.5:1 | AA normal |

The test class iterates these 5 pairs plus all additional pairs from
`DesignTokens.axaml` color combinations that appear in Phase 03 control themes
(enumerated programmatically from the token dictionary).

### Keyboard navigation test

| Test | Oracle | Tool |
| --- | --- | --- |
| `FocusOrder_MainWindow_FollowsLogicalOrder` | Tab through the main window shell: focus moves to (1) menu bar, (2) toolbar stub, (3) content area placeholder. No focus trap; Tab from last item cycles back to first. | Headless Avalonia keyboard simulation; assert `FocusManager.Instance.Current` at each step |
| `FocusOrder_CommandPalette_TextBoxFirst` | When the command palette opens, focus is on the search text box (`CommandPaletteControl.SearchBox`) without requiring an additional Tab press. | Headless Avalonia |

### Automation peer tests

| Test | Oracle | Tool |
| --- | --- | --- |
| `CommandPaletteAutomationPeer_Role_IsList` | `CommandPaletteAutomationPeer.GetAutomationControlTypeCore()` returns `AutomationControlType.List` | Headless Avalonia automation peer API |
| `CommandPaletteItemAutomationPeer_Name_IsLocalizedLabel` | For a `CommandEntry` with `LabelKey = "Commands.ScanLibrary"`, the peer's `GetNameCore()` returns the localized value (`"Scan Library"` in `en`) | Headless Avalonia with mocked `ILocalizationService` |
| `IconButtonAutomationPeer_Name_IsAccessibleLabel` | For an `IconButton` with `AccessibleLabel = "Scan library"`, the peer's `GetNameCore()` returns `"Scan library"` | Headless Avalonia |

---

## Performance tests

### Command palette filter latency

| Test | Oracle | Method |
| --- | --- | --- |
| `CommandPaletteFilter_P95Latency_Under50ms` | 100 iterations of setting `CommandPaletteViewModel.Query` to a random 3-char string; P95 elapsed time ≤ 50 ms on the reference Windows hardware | `StopwatchBenchmarkContext` injected; sorted latency array; P95 computed as `sorted[94]`; asserted ≤ 50 ms |

### Application startup (updated baseline)

The Phase 03 startup measurement updates the Phase 02 `BenchmarkBaseline.md`:
the main window now includes the full themed control stack + `ILocalizationService`
initialization. If the cold-start time increases by > 500 ms vs the Phase 02
baseline, a Phase 20 issue is filed (but the phase is not blocked — cold-start
budget is NFR-OGMA-001 ≤ 3 s P95; the baseline should be well under this at
Phase 03).

---

## Manual tests (not automated)

| Test | Description | When | Documented in |
| --- | --- | --- | --- |
| SR-WIN-P03 | Narrator (Windows) command palette walkthrough | WP7-T6 | `docs/a11y/WALKTHROUGHS.md §Phase03-Windows` |
| SR-MAC-P03 | VoiceOver (macOS) command palette walkthrough | WP7-T6 | `docs/a11y/WALKTHROUGHS.md §Phase03-macOS` |
| VIS-WIN-P03 | Visual review of all 10 control themes on Windows reference hardware | WP2-T7 | Screenshots in PR |
| VIS-MAC-P03 | Visual review of all 10 control themes on macOS reference hardware (Retina) | WP2-T7 | Screenshots in PR |
| REDUCE-P03 | Enable OS reduce-motion setting; confirm all Phase 03 animations are 0 ms | WP7-T5 | Noted in WP7-T5 completion comment |

---

## Beta gate coverage

Phase 03 does not directly cover any G1-G8 beta gate. However:

- The design system establishes the visual baseline that the design-audit gate
  at Phase 06 checks against.
- The `IBenchmarkContext` latency measurement for the command palette is the
  first real-time performance measurement in the production codebase (all prior
  measurements were spikes).
- The accessibility scaffold (automation peers + keyboard model) is the
  foundation that Phase 21's full WCAG audit validates against.

---

## Defect classification

| Tier | Phase 03 examples |
| --- | --- |
| R1 (data loss) | Not expected; no user data operations in Phase 03 |
| R2 (privacy breach) | Not expected; no off-device calls |
| R3 (performance budget) | Command palette filter P95 > 50 ms; startup > 3 s P95 |
| R4 (recoverability) | Not expected in Phase 03 |
| R5 (functional) | OGMA0002 false positive; contrast test failure; `fr.resx` missing key; keyboard navigation regression |

No R1 or R2 defects may be open when Phase 03 closes. R3 (command palette
latency > 50 ms) triggers a Phase 20 issue but does not block Phase 03 close
if the root cause is identified and a mitigation is documented.
