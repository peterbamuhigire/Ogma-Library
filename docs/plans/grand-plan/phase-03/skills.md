# Phase 03 — Skills & Slash Commands

> Phase-scoped invocation guide. Bird's-eye map: `SKILLS-INDEX.md §Part I Phase 03`.
> Phase 03 is the most skill-intensive phase in Part I. Reference
> `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` for all Avalonia
> decisions.

---

## Always-on

| Skill / command | When | Expected artifact |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP1 (design tokens), before WP6 (command palette UX) | Token design proposal (WP1); command palette interaction model (WP6) — both reviewed by owner before implementation |
| `superpowers:test-driven-development` | WP3 (OGMA0002 analyzer), WP5 (plural rules), WP6 (filter latency), WP7 (contrast test) | Tests written before implementation for every component with a behavioral claim |
| `superpowers:verification-before-completion` | End of each WP; before Phase 03 close | Every DoD item verified; specifically: CI green on both platforms, contrast test passes, SR walkthrough documented |
| `superpowers:requesting-code-review` + `/code-review --effort high` | WP9-T1 (final review) | High-effort review of all Phase 03 code; design audit pass required before merge |
| `frontend-ux:design-audit` | WP8-T1 (audit) | A written audit report for each Phase 03 UI surface; `Error`-severity items resolved before DoD |

---

## WP1 — Design tokens

| Skill | Task | What to produce |
| --- | --- | --- |
| `superpowers:brainstorming` | P03-WP1-T1 (token proposal) | A one-page token proposal in Markdown, covering color families, semantic mappings, and typography/spacing scales; this is the owner-review input for Owner ask #1 |
| `frontend-ux:premium-ui-ux-design` | P03-WP1-T1/T2 | Validate that the proposed token system expresses a premium, calm-control aesthetic: warm palette, generous spacing, refined typography — not flat/generic. The skill answers: "Does this look like a premium desktop app?" |
| `document-skills:brand-guidelines` | P03-WP1-T2 (`DesignTokens.axaml`) | The brand guidelines skill ensures the token names follow a consistent naming convention (semantic, not presentational: `Color.Accent.Oak` not `Color.Orange.500`) and that the token documentation explains intent |
| `document-skills:theme-factory` | P03-WP1-T3 (`DesignTokens.Dark.axaml`) | The theme factory skill generates the dark-theme override structure: same token names, different values; confirms the XAML resource dictionary structure for light/dark switching in Avalonia |
| `frontend-ux:motion-design` | P03-WP1-T4 (motion tokens) | The motion design skill provides the easing curve recommendations (ease-out for opening overlays, linear for color transitions), the duration scale (100/200/300 ms), and the principle "motion communicates state change, not decoration" |

### Key constraint for WP1

Before writing any XAML (P03-WP1-T2), run `superpowers:brainstorming` to
answer: "Are 8 accent color families + 2 surface families sufficient, or does
the calm-control aesthetic require fewer (simpler) or more (more expressive)
families?" The output is a concise decision, not a long exploration — the token
design is already well-specified in ICON-SYSTEM.md §4.

---

## WP2 — Avalonia theming

| Skill | Task | What to produce |
| --- | --- | --- |
| `avalonia-desktop-development` | P03-WP2-T1 (theming approach), all of WP2 | The correct Avalonia FluentTheme extension pattern (confirm whether to copy the source XAML or use `ControlTheme` overrides); the AXAML structure for each control theme; confirm the `MergedDictionaries` loading order so tokens are available when control themes are resolved |
| Reference: `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` | P03-WP2-T1 | Consult this doc for the approved MVVM pattern (CommunityToolkit.Mvvm or ReactiveUI), the control naming conventions, and the theme-loading pattern before writing any AXAML |
| `frontend-ux:practical-ui-design` | P03-WP2-T2..T5 (control themes) | Per-control design decisions: what padding, corner radius, and visual state transitions make each control feel "calm" without losing clarity; the skill answers practical questions like "should the button have a drop shadow on hover?" |
| `frontend-ux:interaction-design-patterns` | P03-WP2-T6 (command palette control theme) | The overlay pattern: backdrop blur vs opacity-dimmed backdrop; entry animation direction (slide down from top bar vs scale from center); focus management after open (cursor lands in the text box immediately) |

---

## WP3 — IconCatalog + OGMA0002 + IconButton

| Skill | Task | What to produce |
| --- | --- | --- |
| `avalonia-desktop-development` | P03-WP3-T5/T6 (`IconButton` control + automation peer) | The Avalonia custom control pattern (dependency properties, control template, style); the `AutomationPeer` registration pattern; the `Opacity=0` vs `IsVisible=false` accessibility-tree presence distinction |
| Reference: `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` | P03-WP3-T5 | Consult for the approved `StyledProperty` declaration pattern, the `ControlTheme` AXAML structure, and the binding conventions for custom controls |
| `frontend-ux:image-compression` | P03-WP4-T1/T3 (PNG pipeline + validation) | PNG optimization for the icon assets: confirm that `@1x` PNGs are correctly sized (not upscaled from a larger source); compression settings that preserve color fidelity for the warm colorful icon palette; WebP fallback not needed (PNG is the agreed format per ICON-SYSTEM.md §2) |

### TDD cycle for OGMA0002 (P03-WP3-T1)

Invoke `superpowers:test-driven-development` specifically for the OGMA0002
analyzer. The cycle is:

1. **Write the test** (before any analyzer code): use
   `Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<OGMA0002Analyzer>`
   to specify the expected diagnostic location. Two test cases:
   - Source with `Icon` but no `AccessibleLabel` → expects `OGMA0002` at
     the `Icon` attribute location.
   - Source with both `Icon` and `AccessibleLabel` binding → expects zero
     diagnostics.
2. **Run the test** → it fails (no analyzer implemented yet).
3. **Write the analyzer** to make the test pass.
4. **Refactor** if needed (no behavior change during refactor).

---

## WP4 — PNG pipeline + MASTER-MANIFEST.md

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-ux:image-compression` | P03-WP4-T1 (Import-Icons.ps1) | The validation rules for the import script: exact pixel dimensions per size class (@1x/2x/3x), color space (sRGB), bit depth (32-bit RGBA), maximum file size per density (e.g. ≤ 4 KB for 24x24 @1x) |

---

## WP5 — ILocalizationService + i18n

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-ux:ux-content-strategy` | P03-WP5-T5 (en source strings) | The source string writing principles: use sentence case for UI labels (not UPPER CASE); write short imperative labels for buttons ("Scan Library", not "Click Here to Scan"); write descriptive labels for icon accessible names ("Scan library — discover new PDF files"); ensure strings are translator-friendly (no abbreviations, no idioms) |
| `content-writing` (general) | P03-WP5-T5 (fr translation) | French translation of all Phase 03 strings using a consistent domain glossary (`bibliothèque` for library, `rayon` for shelf, `annotation` for annotation). Note: the `fr` strings must be reviewed by a native French speaker before the MVP release gate (Owner ask #6). |

### ILocalizationService threading note

Use `avalonia-desktop-development` to confirm the thread-safety pattern:
`ResxLocalizationService.SetCulture()` may be called from the UI thread;
the `CultureChanged` event must be raised on the UI thread (use
`Dispatcher.UIThread.InvokeAsync`) so Avalonia bindings update correctly.
This is a common Avalonia gotcha and must be explicitly handled.

---

## WP6 — Command palette

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-ux:interaction-design-patterns` | P03-WP6-T1/T3/T4 (command palette design) | The interaction model: (1) open → cursor in text box immediately (no manual focus needed); (2) typing filters by prefix/contains on label; (3) ArrowUp/Down navigation wraps around; (4) Enter executes and closes; (5) Escape dismisses without executing; (6) Backspace clears query when empty (does not close). This model is the oracle for the keyboard navigation tests. |
| `avalonia-desktop-development` | P03-WP6-T4/T5 (AXAML + key bindings) | The Avalonia `KeyBinding` / `InputBinding` pattern for `Ctrl+K`; the `MainWindow.OnKeyDown` override vs `InputBinding` in AXAML (use AXAML `InputBinding` for declarative intent, override for platform-specific logic); the `Overlay` or `Popup` control pattern for modal overlays |
| Reference: `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` | P03-WP6-T3 (ViewModel) | Confirm the approved observable property pattern (CommunityToolkit.Mvvm `[ObservableProperty]` vs ReactiveUI `WhenAnyValue`); the command pattern for `ExecuteSelectedCommand` |
| `frontend-ux:motion-design` | P03-WP6-T4 (open/close animation) | Confirm the 200 ms ease-out opacity + 8 px Y-slide is appropriate for a command palette (fast enough to not feel laggy; slow enough to be visible); validate against the reduce-motion behavior (0 ms when enabled) |

---

## WP7 — Accessibility scaffold

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-ux:ux-principles-101` | P03-WP7-T1..T6 (all accessibility work) | The WCAG 2.2 SC mapping: which specific success criteria each control must satisfy (2.1.1 Keyboard, 2.4.3 Focus Order, 2.4.7 Focus Visible, 2.4.11 Focus Appearance, 1.4.3 Contrast Minimum, 2.3.3 Animation from Interactions, 4.1.2 Name/Role/Value) |
| `avalonia-desktop-development` | P03-WP7-T4 (AutomationPeer) | The Avalonia `AutomationPeer` API: `GetAutomationControlTypeCore()` returns `AutomationControlType.List` for the palette; `GetNameCore()` returns the localized accessible label; `GetChildrenCore()` returns `CommandPaletteItemAutomationPeer` instances |

### Screen-reader walkthrough methodology

Before P03-WP7-T6 (the manual SR walkthroughs), invoke
`superpowers:verification-before-completion` to design the walkthrough script:
list the exact user steps, the expected Narrator/VoiceOver announcements for
each step, and the pass/fail criteria. The script is committed to
`docs/a11y/WALKTHROUGHS.md §Phase03-script` before the walkthrough is
performed. This ensures the walkthrough is repeatable (future phases can
re-run the same script).

---

## WP8 — Design audit + procurement

| Skill | Task | What to produce |
| --- | --- | --- |
| `frontend-ux:design-audit` | P03-WP8-T1 | A structured audit report with findings categorized as `Error` (blocks DoD), `Warning` (should fix before next phase), and `Info` (nice to have). Report format: `| Control | Finding | Severity | WCAG/Token/UX reference | Status |` |
| `product-business:premium-product-positioning` | P03-WP8-T3 (owner token review) | Frame the owner review as a premium product positioning question: "Does this design system position Ogma Library as a premium product that users will pay for on the Mac App Store and Windows Store?" The skill helps frame the value proposition that the design language should communicate. |

---

## Slash commands in this phase

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review --effort high` | P03-WP9-T1 | Full review of Phase 03; focus on OGMA0002, localization service, and accessibility |
| `/run` | During WP2 (control theme visual review) and WP6 (command palette) | Drive the running Avalonia app on Windows and macOS to confirm the UI renders correctly; produce screenshots for the owner review |
| `/verify` | After P03-WP8-T4 (DoD checklist) | Run all tests, format check, and build on both platforms; confirm CI green before declaring Phase 03 done |
| `/simplify` | After code review findings are resolved | Apply simplification cleanups to the `ResxLocalizationService`, `CommandPaletteViewModel`, and analyzer code; no behavior changes |
| `/security-review` | Not required for Phase 03 (no security/privacy-touching code) | Skip; no off-device calls, no credential handling, no file system mutation |

---

## Notes on skills NOT used in Phase 03

- `security-scanning:*` — no security-sensitive code in this phase.
- `ai:*` — no AI features in Phase 03.
- `backend-databases:*` — no data layer work.
- `frontend-mobile-development:react-native-architecture` — not applicable
  (Avalonia, not React Native).
- `frontend-mobile-development:tailwind-design-system` — the design token
  system is Avalonia-native (XAML resource dictionaries), not Tailwind. However,
  the `frontend-mobile-development:tailwind-design-system` skill's token naming
  conventions (`{category}.{scale}.{modifier}`) inform the Ogma token naming
  convention; consult it for naming guidance only.
