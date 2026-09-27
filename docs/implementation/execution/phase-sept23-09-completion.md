# Sept-23 Phase 09 completion: design system and visual identity ("The Lamplit Library")

Status: **IMPLEMENTED; owner walkthrough and post-fix journey re-run pending** (2026-09-27).
Branch: lane worktree `agent-a72db4d63c47d6319` (not pushed, not merged), from `acb9d36`.
Rollback point: `acb9d36`.
Plan: [phase-09](../../plans/sept-23-kaizen/phases/phase-09-design-system-and-visual-identity.md).
Decision record: [ADR-0019](../../adrs/0019-lamplit-visual-identity.md).
Defects: K15 (fixed), supports K14 and K33. Owner feedback applied mid-phase (via the
coordinator): "the UI is a bit dull, too greyish and boring. Amaze me!" The coordinator's
"Lamplit Library" brief replaced the phase document's neutral reading.

## Typeface decision

**Spectral** (display: destination headings, card titles, the home greeting, generated cover
titles and monograms), **Public Sans** (UI, body, controls, badges, cover author small caps),
**JetBrains Mono** (paths, hashes, data). All SIL OFL 1.1, bundled, unchanged. No banned font.
Reason: a humanist editorial serif for a reading product, a neutral legible grotesque for dense
UI, and a mono distinct from both.

## What changed

| Task | Result | Commit |
|---|---|---|
| 9.1 Baseline | Real-window baseline (old build, Light, 1280×800) under the desktop lock; three "before" captures kept in `evidence/.../phase-09/before/`. Dark baseline: NOT ASSESSED (the runner stops after the Light pass fails on the known K32 page-turn budget) | — |
| 9.2 Type scale | Caption 12 (floor), Small 13, Body 14, BodyLarge 16, Subtitle 17, Title 21, Headline 27, Display 34; `Type.Size.Glyph` for glyph icons. Literal `FontSize` in `Views/**`: **16 → 0** | `c41600a`, `6316143` |
| 9.3 Colour roles | Lamplit palette: jewel primitives (amber, lapis, malachite, garnet, plum, slate, saffron, teal) and roles `Surface.*`, `Text.*`, `Control.*`, `Accent.Primary/Hover/Pressed/Text/OnAccent/Glow`, `Link`, `Selection.*`, `Status.*`, `Rail.*`, `Focus`, `Hue.*`, `Dest.*`, `Cover.Gilt`; paper-grain canvas brush; hero gradient; warm elevation shadows. Night Library Dark theme (indigo #121A2E, parchment text, gold accents). Fluent accent pinned to amber | `c41600a` |
| 9.4 Components + gallery | Button variants `primary`, secondary (default), `tool`/`quiet`, `danger`, `link` + `icon`, `compact`, `busy`, `active`; states rest/hover/pressed/focus-visible/disabled. TabStrip (Body size, one scrollable row), segmented control, badge/chip, text field, toggles, list/tree rows, card, book card. Living gallery `Views/Dev/ComponentGallery.axaml` (hidden QA command **Ctrl+Shift+Alt+G**), rendered Light/Dark by a headless test | `c41600a`, `d3fc2bc` |
| 9.5 View migration | Every accent-filled button uses a variant; `White`/`Black` literals **61 → 0**; hex **0 → 0**; buttons with local fills **43 → 1** (the Search panel, owned by the concurrent Phase 13 lane; token substitution only) | `6316143` |
| 9.6 Selection/focus | `ListBoxItem`/`TreeViewItem`/`ToggleButton` selection use `Selection.*` (amber tint + 2 px amber border; amber glow on book cards); the salmon OS accent no longer appears. 2 px `Focus` ring on buttons, tabs, list rows, text fields, combo; rail has its own `Rail.Focus` | `c41600a` |
| 9.7 Icons (D-07) | 85 `IconCatalog` keys → Lucide outlines (ISC, `@iconify-json/lucide` 1.2.110, licence verified from the vendored package metadata) recoloured as duotones in destination/category hues by `scripts/icons/New-DuotoneIcons.py`. The previous Flaticon/Freepik files had no licence record and were superseded. `docs/governance/asset-licence-register.md` and `Assets/icons/LICENSE-ICONS.txt` added | `921d1c5` |
| 9.8 Generated covers | `GeneratedCover`: FNV-1a title hash → 8 jewel cloths × 3 weaves, gradient, spine band + edge highlight, gilt frame, Spectral title, small-caps author (omitted when unknown), monogram below 100 px. Pure vector, no file write. Extracted covers still win | `d1fdeed` |
| 9.8+ Library home | Greeting header (morning/afternoon/evening), library size, strip of books to continue with progress rings, or the first shelved books when none is in progress. Keys `Catalogue.Home.*` (en, fr) | `d1fdeed` |
| 9.9 QR | `QrCodeView` draws QRCoder modules as vectors with a 4-module quiet zone and ≥ 4 px modules; the 4 px Consolas text QR is gone | `6316143` |
| 9.10 Lint | `scripts/Test-DesignTokens.ps1` + CI step. MEASURED red on a seeded `FontSize="11" Foreground="White"` (2 violations, exit 1) and green after removal (exit 0) | `589ffbe` |
| Motion | Card lift, shadow and button colour transitions only under the window `motion` class, withheld when Windows "Show animations" is off or `OGMA_REDUCED_MOTION=1` (`MotionPreference`) | `c41600a` |
| Rail | Deep midnight-ink rail in both themes; per-destination hue for icon, active indicator and the header accent bar | `8d825a5` |
| 9.11 Re-score | See the design-quality gate and score below | `ecc1cba`, this record |

## Tests added

`tests/OgmaLibrary.Tests.Ui/Sept23Phase09DesignSystemTests.cs` (88 cases with theories):
contrast matrix — 46 text pairs ≥ 4.5:1 and 33 non-text pairs ≥ 3:1, each in Light and Dark;
Night canvas is indigo and dark; type floor and ratios; no literal font size or colour in any
view; every SVG registered, duotone and licensed; cover determinism and variety (17 titles →
≥ 5 cloths); cover render in both themes; variant fills and focus ring; tabs at 14 px on one
row; gallery renders Light/Dark with no missing en/fr label.

## Gates

Windows 11, Release, lane branch after `git merge main --no-ff` (`778314a`, no conflicts: `main` only
brought docs since `acb9d36`, so no view from `main` needed re-tokening).

| Gate | Result | Evidence class |
|---|---|---|
| `Test-RequirementAccountability.ps1` | PASS (101 FRs, 29 NFRs, 32 controls, 162 IDs) | MEASURED |
| `dotnet restore --locked-mode` | PASS | MEASURED |
| `dotnet format --verify-no-changes` | PASS (0 changes; re-run after the final fixes) | MEASURED |
| Release build | PASS, 0 warnings, 0 errors (re-run after the final fixes) | MEASURED |
| `dotnet list package --vulnerable --include-transitive` | PASS, no vulnerable package in 11 projects | MEASURED |
| `dotnet format analyzers --severity warn` | PASS | MEASURED |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean"; re-run after the final fixes) | MEASURED |
| `dotnet test OgmaLibrary.sln -m:1 --filter "Category!=Performance"` on `778314a` | Architecture 52/52, core 1,152/1,152, UI 303/303 (1,507, 0 failures). The same command also ran the E2E project (18/43 pass) **outside the desktop lock** while other lanes ran real-window work, so those E2E numbers are not evidence; the locked run below is | MEASURED |
| UI project after the final fixes (`BodyFont_*`, cover brush) | 307/307 | MEASURED |
| Core/architecture after the final fixes | Pending: re-run `dotnet test ... --filter "Category!=Performance"` (only App views/theme and UI tests changed) | NOT ASSESSED |

Red/green: `BodyFont_ResolvesToTheRequestedWeightNotTheThinDefault` failed 4/4 with the old variable
file (`Expected "Public Sans", Actual "Public Sans Thin"`) and passes 4/4 with the static instances.

## Real-window results

### Golden journeys (desktop lock, run id `phase09-all`, 1280x800 and 1920x1080)

Build: lane at `778314a` (after the merge, **before** the three fixes below). Summary:
`artifacts/e2e/phase09-all/results.md` (local, not committed). 43 tests: 28 pass, 15 fail.

| Journey | 1280x800 | 1920x1080 | On `main` | Regression? |
|---|---|---|---|---|
| G1 first run | PASS | PASS | PASS | no |
| G2 add library, covers | PASS | PASS | PASS | no |
| G3 read 50 pages | BASELINE-FAIL (K32, K94) | BASELINE-FAIL | failing | no |
| G4 search | BASELINE-FAIL (K40, K41) | BASELINE-FAIL | failing | no |
| G5 resume | PASS | PASS | PASS | no |
| G6 invalid files | PASS | PASS | PASS | no |
| G7 Advisor unconfigured | PASS | PASS | PASS | no |
| G8 worker killed while reading | PASS (baseline cleared) | PASS (baseline cleared) | PASS | no |
| G8 corrupt settings | BASELINE-FAIL (K95, K28) | BASELINE-FAIL | failing | no |
| Navigation (3 tests) | PASS | PASS | PASS | no |
| Settings (2 tests) | PASS | PASS | PASS | no |
| A11yNames catalogue / search | PASS / BASELINE-FAIL (K40, K41) | PASS / BASELINE-FAIL | — | no |
| Smoke, Harness (3) | PASS | n/a | PASS | no |
| ScanOcrSearch | FAIL (K21: no OCR badge, OCR search misses) | FAIL | failing | no (owned by stabilisation lane / Phase 13) |
| LaunchCycles | FAIL (5/20 closes needed a kill; K71) | n/a | failing (3/20) | no Phase 09 cause; load-sensitive (K71, Phase 24) |
| G9–G12 placeholders | NOT ASSESSED | NOT ASSESSED | — | — |

**No journey that passes on `main` regressed.** Post-fix journey re-run: **pending** (next step:
`& ./scripts/Use-DesktopLock.ps1 -Owner 'phase-09' -ScriptBlock { ./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080 }`
on the final commit; G2 is the one most likely affected, by the cover brush change).

### Visual defects found in the real window and fixed

| Defect (seen in the first "after" captures) | Root cause | Fix | Evidence |
|---|---|---|---|
| All Public Sans text rendered as faint grey hairlines in Light ("greyish" even though the text tokens pass 4.5:1): darkest rendered ink on "Appearance" was `#746D59` for a `#1F1A14` token | The bundled `PublicSans-VF.ttf` has default instance **Thin (100)**; Avalonia ignores variation axes, so every weight resolved to "Public Sans Thin" | Cut static Regular/Medium/SemiBold/Bold instances (fontTools 4.63 `varLib.instancer`, OFL, no Reserved Font Name) and removed the variable file; register updated; headless guard added | MEASURED (red/green test; screenshots) |
| Grid covers lost their title band: a centred UniformToFill crop showed only the middle of first-page covers | `Image Stretch="UniformToFill"` always crops around the centre | Cover drawn as a top-anchored `ImageBrush` (`AlignmentY=Top`); Phase 19 cover test asserts it | MEASURED (test) + HEURISTIC (screenshots) |
| "More" (DropDownButton) and the Settings "Needs attention" toggle kept Fluent's off-palette grey fill | The `Button` selector does not match `ToggleButton`/`DropDownButton` | Rest/hover/pressed/focus rules on Lamplit `Control.*` tokens | HEURISTIC (screenshots) |
| Component gallery opened Dark over a Light window; Ctrl+Shift+Alt+G sometimes swallowed | Gallery copied the owner's `ActualThemeVariant`; gesture handled on bubble after focused controls | Gallery follows the application theme; gesture moved to a tunnel handler | HEURISTIC (screenshots) |

### Screenshots (final build, synthetic corpus, isolated data dir, desktop lock)

Light and Dark ("Night Library"), 1280x800 except where noted:
[library home light](evidence/sept-23-kaizen/phase-09/after/library-home-light.jpg),
[library home dark](evidence/sept-23-kaizen/phase-09/after/library-home-dark.jpg),
[book detail light](evidence/sept-23-kaizen/phase-09/after/book-detail-light.jpg),
[book detail dark](evidence/sept-23-kaizen/phase-09/after/book-detail-dark.jpg) (window came up maximised at 2576x1456; the resize did not apply),
[reader light](evidence/sept-23-kaizen/phase-09/after/reader-light.jpg),
[reader dark](evidence/sept-23-kaizen/phase-09/after/reader-dark.jpg),
[settings light](evidence/sept-23-kaizen/phase-09/after/settings-light.jpg),
[settings dark](evidence/sept-23-kaizen/phase-09/after/settings-dark.jpg),
[component gallery light](evidence/sept-23-kaizen/phase-09/after/component-gallery-light.jpg),
[component gallery dark](evidence/sept-23-kaizen/phase-09/after/component-gallery-dark.jpg) (1280x1000).
The rail appears in every shell capture.

Observed and not fixed (outside Phase 09 scope): the reader toolbar and annotation tabs clip at
1280 px (Phase 12, TabStrip/toolbar overflow); synthetic first-page covers are mostly white pages
with small titles (content, not styling).

## Design-quality gate (design-system-skills/governance/design-quality-gate.md)

| Item | Result | Evidence class |
|---|---|---|
| Typeface named and justified before output; no banned font | PASS | MEASURED (token test asserts Spectral/Public Sans/JetBrains Mono; no other family in views) |
| Display + body pairing | PASS | HEURISTIC (screenshots) |
| Type scale real ratio, ≥ 12 px floor | PASS (UI steps 1.21–1.24, display 1.26–1.29; Caption→Body 1.17 is floor-clamped and recorded) | MEASURED (test) |
| Line-height inverse rule | NOT ASSESSED (no line-height tokens yet) | NOT ASSESSED |
| Licence permits embedding (fonts, icons) | PASS | MEASURED (licence files present; register) |
| Contrast text ≥ 4.5:1, large/UI ≥ 3:1 in every theme | PASS on token pairs | MEASURED (matrix test). Rendered-pixel check only for Oak/parchment (existing Phase 18 test) |
| Colour never the only carrier | PASS (badges carry text; selection adds border/glow) | HEURISTIC |
| Consistent spacing on one unit | PARTIAL (4-pt tokens exist; many views still use literal margins) | HEURISTIC |
| State matrix per component | PASS for buttons/tabs/lists (gallery); loading/empty/error states unchanged | HEURISTIC (gallery screenshots) |
| Disabled controls explain why | NOT ASSESSED (Phase 21) | NOT ASSESSED |
| Keyboard operable, focus always visible | Focus ring styled on all interactive controls | HEURISTIC (gallery); screen reader NOT ASSESSED (Phase 21) |
| Targets ≥ 24×24 | PASS (buttons ≥ 36, link ≥ 24, segments 30) | MEASURED (existing 36 px test + styles) |
| Reduced motion respected | PASS (motion class gated on OS setting) | CODE; OS toggle run NOT ASSESSED |
| 200 % zoom / 320 px reflow | NOT ASSESSED (desktop min 860 px; Phase 21) | NOT ASSESSED |
| Windows high-contrast mode | NOT ASSESSED | NOT ASSESSED |
| Premium authorship: one signature choice | "Lamplit" rail + cloth-bound covers | HEURISTIC; owner verdict pending |
| Final verdict | **CONDITIONAL** (owner walkthrough, high-contrast, zoom and screen-reader checks outstanding) | — |

`product-design-audit` 11-dimension score (HEURISTIC, from the real-window screenshots): before
≈ 48/100 raw (capped at 59 by the salmon off-palette selection and sub-12 px caption gates);
after ≈ 76/100 raw, no gate failure, confidence medium (single rater, Windows only).

## NOT ASSESSED and deferrals

| Item | Owner / phase |
|---|---|
| Owner walkthrough of the concept screenshots and gallery (D-07 / "amaze me" acceptance) | Owner |
| Golden-journey re-run on the final commit (after the font, cover and toggle fixes) and the full `dotnet test` gate | Coordinator / next lane session |
| Premium icon purchase (drop-in replacement, key for key) | Owner (D-07) |
| QR scan with a physical phone | Owner / tester |
| Windows high-contrast mode, 200 % scaling, screen reader on new controls | Phase 21 |
| macOS rendering of fonts, icons and covers | Phase 27 |
| Route cross-fade and drawer slide animations (styles prepared: `Border.route-host`) | Phase 10/12 |
| TabStrip overflow menu (tabs scroll in one row today) | Phases 11/12 |
| App icon provenance (`Assets/app*`) | Owner |
| Pre-existing French mojibake in `InMemoryLocalizationService` (e.g. `IndexÃ©`) — observed, not changed | Phase 22 |
| Remaining literal margins/paddings in views (spacing tokens not yet enforced) | Phases 10–12 |
