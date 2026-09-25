# Phase 09: Design system and visual identity

## 1. Header

| Field | Value |
|---|---|
| Wave | C, Experience |
| Size | 7–10 engineering days (plus icon procurement lead time) |
| Depends on | Phase 07 (navigation shell and toolbar structure settled) |
| Owner decisions | **D-07** icon set (recommended: buy the premium colourful set, with licence recorded) |
| Primary defects | K15 (and supports K11, K14, K33) |
| Requirements | UX-008 (P, themes), UX-002 (P, states), NFR accessibility contrast |

## 2. Why this phase exists

The owner asked for "a beautiful app" with "colorful icons" (recorded in the astra audit,
still undelivered). The running app measured on 25 September shows no coherent visual system
(K15, `evidence/screens/j1-wide.png`, `j3-grid.png`, `j3-detail.png`, `j4-reader.png`):

- **Four unrelated button styles** in one toolbar: flat text links, grey pills, filled oak, filled clay.
- The catalogue **selection highlight is an off-palette salmon** that matches no token.
- The detail-panel and reader side-panel **tab headers render at display size (~28 px)** and wrap
  onto three rows.
- Disabled buttons are grey-on-grey with low contrast. *Rename* overflows the sidebar. Split view
  is a literal `||` glyph. Icons are 12–14 px and inconsistent in style.
- Every book without a cover is an identical flat brown square. A 17-book library looks like 17
  copies of one placeholder.
- Tokens drift: `Type.Size.Caption` is **11 px** (`src/OgmaLibrary.App/Themes/Tokens.axaml:125`),
  under the design engine's 12 px floor. Sizes 12/14/15/16 step far below the 1.25 ratio. Views
  hold **21 hard-coded `FontSize`** values and **77 hard-coded `White`** foregrounds or backgrounds.
  Two views draw QR codes as 3–4 px Consolas text with hard-coded black and white
  (`CatalogueShellView.axaml:590`, `SharingSettingsView.axaml:687`).

The foundations are sound. Spectral, Public Sans and JetBrains Mono are licensed, embedded and
compliant with the anti-slop doctrine, and a Light/Dark token set with 6 accents
(Oak, Ink, Sage, Clay, Plum, Slate) exists. This phase turns those foundations into a system that
is enforced.

## 3. Objectives and exit criteria

1. **Typeface decision recorded** in `docs/developer-guide/` and the Ogma design-system doc:
   Spectral (display: titles, headings, generated cover titles), Public Sans (body, controls, data),
   JetBrains Mono (paths, hashes, code). No banned fonts. Reason: humanist editorial serif for a
   reading product, a neutral legible grotesque for dense UI, and a mono that is distinct from both.
2. **Type scale rebuilt** with ratio ≥ 1.2 for UI steps and ≥ 1.25 at the display end, floor 12 px:
   `Caption 12, Small 13, Body 14, BodyLarge 16, Subtitle 18, Title 22, Headline 28, Display 36`
   (final values verified against rendered screenshots). There are zero hard-coded `FontSize` values
   in `Views/**`.
3. **Button variant system:** exactly five variants (`Primary`, `Secondary`, `Quiet`, `Danger`,
   `Icon`), each with complete states: rest, hover, pressed, focus-visible, disabled, busy. Defined
   once as style classes in `Themes/Controls.axaml` and applied everywhere. No local `Background`
   or `Foreground` on buttons.
4. **Colour roles:** semantic tokens (`Surface.*`, `Text.Primary/Secondary/Disabled`,
   `Accent.Primary`, `Selection.Background/Border`, `Status.Success/Warning/Danger/Info`,
   `OnAccent`) mapped to the existing palette in Light and Dark. Every text/background pair passes
   WCAG 2.2 AA (4.5:1 body, 3:1 large text and UI components), verified by the existing automated
   contrast matrix, extended to the new roles. There are zero hard-coded `White`/`Black` in views.
5. **Selection and focus:** selection uses `Selection.*` (oak tint plus a 2 px border), not salmon.
   The focus ring is visible on every interactive control in both themes.
6. **Tabs and segmented controls:** one compact `TabStrip` style (Body size, single row, overflow
   menu). It is used by the detail inspector (Phase 11) and reader side panel (Phase 12).
7. **Icon system (D-07):** one licensed, colourful icon family covering every navigation item,
   toolbar action, status and file state. Sizes 16/20/24 with a 1.5 px optical stroke or equivalent
   fill style. Every asset is recorded in `docs/governance/asset-licence-register.md` (source,
   licence, purchase reference, date). `IconCatalog` resolves only registered assets.
8. **Generated typographic covers:** books without a cover image get a deterministic cover. The
   background is chosen from the 6 accents by a hash of the title, the title is set in Spectral, the
   author in Public Sans, with a subtle spine band. It replaces the flat brown square.
9. **Dark theme parity:** every screen in the golden journeys renders correctly in Dark, verified
   by screenshots.
10. **Design quality gate** (`design-system-skills/governance/design-quality-gate.md`) and the
    `product-design-audit` score for the shell, catalogue, detail and reader surfaces: no
    slop/WCAG gate failures (a gate failure caps the score at 59).

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\README.md` and `CLAUDE.md` (router; banned fonts; "state the typeface")
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\product-design-audit\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\09-design-systems-tokens-and-theming\design-tokens-and-naming\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\09-design-systems-tokens-and-theming\component-library-architecture\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\01-typography-and-fonts\font-selection-and-pairing\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\01-typography-and-fonts\font-embedding-and-licensing\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\02-color-brand-and-visual-identity\color-system-and-palette\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\02-color-brand-and-visual-identity\accessible-color-and-contrast\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\02-color-brand-and-visual-identity\dark-mode-and-theming\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\11-imagery-illustration-and-art-direction\iconography-system-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\component-states-and-interaction-fidelity\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\premium-ui-ux-design\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`

## 5. Scope

**In scope:** `Themes/Tokens.axaml` and `Themes/Controls.axaml`, the removal of local styling across
`Views/**`, the icon system and licence register, generated covers, QR rendering as a real image,
Dark parity, and the design-quality gate checks in CI (a static check that forbids hard-coded
`FontSize`, `Foreground="White"` and hex colours in `Views/**`).

**Out of scope:** layout redesign of individual surfaces (Phases 10–12 consume this system), the 3D
scene's own typography (`src/shelf3d/src/scene.ts:522`, Georgia at 6–7 px, which is Phase 18), and
motion design beyond state transitions.

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 9.1 | Baseline capture. Run the `product-design-audit` 11-dimension score on shell, catalogue, detail and reader in Light and Dark from the Phase 01 harness screenshots. Record the raw score with evidence classes. | `docs/implementation/execution/evidence/sept-23-kaizen/phase-09/baseline/` | Score block recorded (raw, capped, confidence, gates). |
| 9.2 | Rebuild the type scale tokens, then migrate every `FontSize` literal in `Views/**` to tokens. | `Themes/Tokens.axaml:125-131`; the 9 view files with literals | Static check: 0 literal `FontSize` in `Views/**`. |
| 9.3 | Add semantic colour roles for Light and Dark, map them to the palette, and extend the contrast matrix test to every role pair. | `Themes/Tokens.axaml`, contrast test in `OgmaLibrary.Tests.Ui` (`Phase18DesignSystemTests`) | Every pair meets AA; the test fails on regression. |
| 9.4 | Define the button variants and `TabStrip`, `Chip`/`Badge`, `TextField`, `Toggle`, `ListRow` and `Card` styles with a full state matrix in `Controls.axaml`. Add a hidden developer "component gallery" route that renders every variant and state for screenshot review. | `Themes/Controls.axaml`, `Views/Dev/ComponentGallery.axaml` (debug builds only) | Gallery screenshots in Light and Dark; each state is visually distinct. |
| 9.5 | Replace local button styling across all views with variant classes; remove the 77 hard-coded `White` usages. | `CatalogueShellView.axaml`, `CatalogueGridView.axaml` (badges), `BookDetailView.axaml`, `ReaderView.axaml`, `SharingSettingsView.axaml`, `Views/Ai/*` | Static check: 0 `White`/`Black`/`#hex` in `Views/**`. |
| 9.6 | Selection and focus visuals for `ListBoxItem`, `TreeViewItem` and `DataGrid` rows use the `Selection.*` roles. | `Controls.axaml` | Screenshot: selected card uses oak tint and border in both themes. |
| 9.7 | Icon procurement (D-07): shortlist 2–3 licensed colourful families covering the ~75 current glyphs plus new ones, get the owner's choice, buy, and record the licence. Convert to SVG/`DrawingImage`, or PNG at 1×/2×, per licence terms. | `src/OgmaLibrary.App/Assets/icons/**`, `Icons/IconCatalog.cs`, `docs/governance/asset-licence-register.md` | 100 % of `IconCatalog` keys resolve to registered assets; the icon catalog tests updated. |
| 9.8 | Generated typographic cover renderer: deterministic, cached as a vector `DrawingGroup` (no file write), used by `CoverImageView` when no image exists or it fails to decode. | `Views/Catalogue/CoverImageView.axaml(.cs)` | Screenshot: 17-book synthetic library shows varied, legible covers; the same title always yields the same cover. |
| 9.9 | QR codes rendered as a real bitmap (QRCoder is already a dependency) with a quiet zone and minimum module size; remove the text QR. | `CatalogueShellView.axaml:585-600`, `SharingSettingsView.axaml:687` | A phone camera scans the code from the screen (manual check, record the result or NOT ASSESSED). |
| 9.10 | Static design lint in CI: a script failing on hard-coded `FontSize`, colours and `Foreground` literals under `Views/**`, and on new unregistered icon files. | `scripts/Test-DesignTokens.ps1`, CI workflow | The CI job is red on a seeded violation and green after removal. |
| 9.11 | Re-run the `product-design-audit` score and the design quality gate; write the before/after table. | Evidence folder | Gates pass; the score delta is recorded. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Inconsistent buttons | No variant system; views style locally | 5 variants in `Controls.axaml` | Consistency raises perceived quality and scannability | Distinct button styles in shell: 4 ad-hoc → 5 defined variants used 100 % | Static lint plus screenshots | Visual regressions in rarely-seen views | Revert per-view commits |
| Caption below 12 px, flat scale | Tokens grew organically | New scale, ratio-driven | Better legibility and hierarchy | Smallest text 11 → 12 px; literals 21 → 0 | Token diff plus lint | Layout overflow at larger sizes | Density token absorbs |
| Hard-coded white | Quick fixes (Sept-14 tranche) | `OnAccent` role | Dark theme parity | Literals 77 → 0 | Lint | Missed contrast pairs | Contrast test gate |
| Salmon selection | Fluent default accent leaking | `Selection.*` role | Palette coherence | Off-palette colours visible: ≥1 → 0 | Screenshot | None | Revert style |
| Identical brown covers | Placeholder never designed | Generated typographic covers | Books become recognisable at a glance | Distinct placeholder appearances: 1 → up to 6 × title | Screenshot plus owner walkthrough | Garish combinations | Tune palette mapping |
| No colourful icons | Asset decision pending | D-07 purchase plus register | Meets owner request | Owner acceptance: no → yes | Owner verdict in wave record | Licence terms forbid redistribution | Record terms first |

## 8. Test plan

- **Unit and headless UI:** the extended contrast matrix; the icon catalog resolves every key; the
  generated cover is deterministic; the variant styles apply (style-class tests on sample controls).
- **Static:** `scripts/Test-DesignTokens.ps1` in CI.
- **Real-window:** a screenshot set of the golden journeys in Light and Dark at 1280×800 and
  1920×1080; the component gallery; the 860 px minimum width.
- **Human review:** the owner walkthrough of the gallery and the four main surfaces (recorded).
- **Negative:** high-contrast OS mode on Windows (record the result or NOT ASSESSED); 200 % display scaling.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build
./scripts/Test-DesignTokens.ps1
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Visual -Themes Light,Dark -Sizes 1280x800,1920x1080
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Icon licence purchase (D-07) | Owner | Tasks 9.7 blocked; interim open-licence set only with register entry |
| Screen-reader verification of new controls | Phase 21 | Accessible names checked by UIA only |
| macOS rendering of fonts and icons | Phase 27 | Windows evidence only |
| QR scan on a physical phone | Owner or tester | Recorded as NOT ASSESSED if not done |

## 11. Risks and mitigations

- **Broad visual churn** touching every view. Mitigation: migrate one surface per commit with
  before/after screenshots; the component gallery catches state gaps early.
- **Licence ambiguity** for bought icons (embedding in an open-source repo). Mitigation: confirm
  redistribution rights before committing assets; otherwise store them outside the repo and fetch
  them at build from a private feed.
- **Contrast failures in Dark** for accent chips. Mitigation: `OnAccent` computed per accent and
  tested in the matrix.

## 12. Execution prompt

```text
You are implementing Phase 09 (Design system and visual identity) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-09-design-system-and-visual-identity.md
4. docs/plans/sept-23-kaizen/03-defect-register.md row K15 and screenshots in evidence/screens/
Then load every skill in section 4, starting with design-system-skills README.md and CLAUDE.md.
State the typeface decision (Spectral / Public Sans / JetBrains Mono) in your first commit message
body. Do not introduce any banned font. Work order: 9.1 baseline → 9.2–9.4 tokens and styles
with the component gallery → 9.5–9.6 migrate views one surface per commit → 9.8–9.9 covers and QR
→ 9.10 lint → 9.7 icons when D-07 is decided → 9.11 re-score. After each surface, run the
section 9 commands and store Light/Dark screenshots under
docs/implementation/execution/evidence/sept-23-kaizen/phase-09/. Never mark a gate passed without
a rendered screenshot. Record NOT ASSESSED items in docs/implementation/execution/phase-sept23-09-completion.md.
```
