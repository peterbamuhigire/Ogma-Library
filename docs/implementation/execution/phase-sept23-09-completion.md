# Sept-23 Phase 09 completion: design system and visual identity ("The Lamplit Library")

Status: **IMPLEMENTED** (2026-11-19). Not COMPLETE: the open Definition of Done items are listed
in [Definition of Done check](#definition-of-done-check).
Branch: `kaizen/phase-09` (published by the autorun driver; not merged by this lane).
Rollback point: `acb9d36c`.
Plan: [phase-09](../../plans/sept-23-kaizen/phases/phase-09-design-system-and-visual-identity.md).
Decision records: [ADR-0019](../../adrs/0019-lamplit-visual-identity.md); D-14 (the "Lamplit
Library" brief) and D-15 (TabStrip overflow menu) in the
[execution log](../../plans/sept-23-kaizen/EXECUTION-LOG.md).
Defects: K15 (partly resolved, see the [defect register](../../plans/sept-23-kaizen/03-defect-register.md));
K101 and K106 found and fixed here; K102–K105 found here and handed on.

## Typeface decision

**Spectral** for display (destination headings, card titles, the home greeting, generated cover
titles and monograms). **Public Sans** for UI, body text, controls, badges and the small-caps
author on covers. **JetBrains Mono** for paths, hashes and data. All three are SIL OFL 1.1 and
bundled. Public Sans ships as static Regular/Medium/SemiBold/Bold instances cut from the
variable font, because Avalonia ignores variation axes and resolved every weight to Thin.
Reason: a humanist editorial serif suits a reading product, a neutral, legible grotesque suits
dense UI, and the mono is distinct from both. Full text:
[developer guide, typeface decision](../../developer-guide/design-system.md#typeface-decision).

## Tasks

| Task | Result | Commits |
|---|---|---|
| 9.1 Baseline | The real-window baseline (old build, Light, 1280x800) is in `evidence/.../phase-09/before/`. The Dark baseline is NOT ASSESSED: the runner stopped after the Light pass failed on K32 | — |
| 9.2 Type scale | Caption 12 (the floor), Small 13, Body 14, BodyLarge 16, Subtitle 17, Title 21, Headline 27, Display 34. Line-height and icon-size tokens were added. Literal `FontSize` in views went from 16 to 0. Preference changes no longer rewrite the scale (K101) | `c41600a3`, `63161432`, `591f50a5` |
| 9.3 Colour roles | Lamplit jewel primitives and semantic roles (`Surface`, `Text`, `Control`, `Accent`, `Selection`, `Status`, `Rail`, `Focus`, `Dest`, `Cover`), plus the Night Library dark theme | `c41600a3`, `70a0bd68`, `55e4632e` |
| 9.4 Components and gallery | Button variants and states, toggle and drop-down buttons, TabStrip, segmented control, badge, text field (focused fields stay on the raised surface), list and tree rows, cards. The living gallery opens with Ctrl+Shift+Alt+G | `c41600a3`, `d3fc2bce`, `33fcd467`, `b4eebd46` |
| 9.5 View migration | Every view uses variants and tokens. `White`/`Black` literals went from 61 to 0; hex literals in views are 0. The lint was extended to code-behind and `App.axaml` | `63161432`, `56c6aa27` |
| 9.6 Selection and focus | Amber `Selection.*` replaces the salmon OS accent. The 2 px Focus ring is now a layout-neutral focus adorner (K106) | `c41600a3`, `5520c82c` |
| 9.7 Icons (D-07) | 85 Lucide (ISC) duotone icons with a licence register | `921d1c5c` |
| 9.8 Covers and home | Generated cloth-bound covers, a top-anchored extracted cover, a greeting home strip, and in-place grid updates | `d1fdeed0`, `fee04e5b`, `197f0794`, `8d825a5e` (rail) |
| 9.9 QR | A vector QR with a quiet zone and modules of at least 4 px | `63161432` |
| 9.10 Lint | `scripts/Test-DesignTokens.ps1` plus a CI step | `589ffbed`, `56c6aa27` |
| 9.11 Re-score and record | See the [design-quality gate](#design-quality-gate). The TabStrip overflow menu (D-15) was brought forward from Phases 11/12 | `ecc1cbac`, `62824031`, `ce2132ea`, `e09b7f5b`, this record |

`94764452` merged `main` into the lane, and `f7b792c9` is the earlier version of this record.
The focus-ring regression fix is `5520c82c`. Tests are in
`tests/OgmaLibrary.Tests.Ui/Sept23Phase09DesignSystemTests.cs`: the contrast matrix in both
themes, the type floor and ratios, no literal size or colour, icons and licences, cover
determinism, variants, tabs, gallery labels in en and fr, density scaling, and the focus ring
leaving layout unchanged.

## Gates

Windows 11, Release. First run on `135187f3` (the lane head before this slice's fix), logs in
`artifacts/phase09-gates/`. Re-run on the final code commit `5520c82c` (restore, build,
vulnerable scan, analyzers, fast suite), logs in `artifacts/phase09-gates/final/` (local).

| Gate | Result | Evidence class |
|---|---|---|
| `Test-RequirementAccountability.ps1` | **FAIL**: it throws "Requirement-to-phase matrix not found: …docs\plans\aug-39\appendices\01-requirement-phase-matrix.md". Main removed `aug-39` in `3b985c67` (K100). Not caused by Phase 09 | MEASURED |
| `dotnet restore --locked-mode` | PASS (exit 0 on both commits) | MEASURED |
| `dotnet format --verify-no-changes` | PASS (exit 0); re-run on `5520c82c`: PASS | MEASURED |
| Release build | PASS, 0 warnings, 0 errors (both commits) | MEASURED |
| `dotnet list package --vulnerable --include-transitive` | PASS: none of the 11 projects has a vulnerable package (both commits) | MEASURED |
| `dotnet format analyzers --severity warn` | PASS (exit 0, both commits) | MEASURED |
| `dotnet test -m:1` (fast suite) | On `5520c82c`: Architecture 52/52, OgmaLibrary.Tests 1,262/1,262 (16.2 min, K105), Ui 367/367, 0 failures. On `135187f3`: Architecture 50/50 (the first log does not list test names, so the cause of the 50 → 52 difference is not established; no skip or trait exists in that project), 1,262/1,262 (18 min 29 s), 364/364 | MEASURED |
| `scripts/Test-DesignTokens.ps1` | PASS ("Design-token lint: clean."), also on `5520c82c` | MEASURED |
| E2E app build (`-p:OgmaE2EHooks=true`) | PASS, 0 warnings, 0 errors (on `5520c82c`) | MEASURED |
| `shelf3d` npm gates | Not applicable (`src/shelf3d` unchanged) | — |
| Hosted CI run | NOT ASSESSED (the driver pushes after this session) | NOT ASSESSED |

Red and green runs: `FocusRing_DoesNotChangeControlSize` failed 3/3 (focused width 61 → 63 px)
and passes 3/3 after `5520c82c`. Earlier slices: K101 `DensityScaling_*` and `BodyFont_*`.

## Real-window results

### Golden journeys (desktop lock owner `phase-09`, Light and Dark, 1280x800 and 1920x1080)

Final run `phase09-final2` on `5520c82c` (`artifacts/e2e/phase09-final2/results.md`, local).
Every assessed test passes in both themes and at both sizes: **78 PASS, 0 FAIL, 16 NOT
ASSESSED** (39 per theme; the NOT ASSESSED rows are the G9–G12 placeholders). The previous run on `135187f3`
(`artifacts/e2e/phase09-final`) is shown for comparison.

| Journey | 1280x800 L / D | 1920x1080 L / D | Run on `135187f3` | Baseline (audit) K IDs |
|---|---|---|---|---|
| G1 first run | PASS / PASS | PASS / PASS | PASS | — |
| G2 add library, covers | PASS / PASS | PASS / PASS | PASS | was failing at the audit |
| G3 read 50 pages (p95 ≤ 250 ms) | PASS / PASS | PASS / PASS | 3 of 4 FAIL (Dark 1920 p95 257 ms; page did not advance in Dark 1280 and Light 1920) | K32, K94: **cleared in this run, but not yet stable** |
| G4 search, expected book in top 3 | PASS / PASS | PASS / PASS | Light 1920 FAIL (author "Grace Namutebi" → top 3 empty) | K40, K41: **cleared in this run, but not yet stable** |
| G5 resume after relaunch | PASS / PASS | PASS / PASS | Dark 1280 FAIL ("Could not leave page 6") | — (flake, not reproduced) |
| G6 invalid files | PASS / PASS | PASS / PASS | PASS | was failing |
| G7 Advisor unconfigured | PASS / PASS | PASS / PASS | PASS | — |
| G8 worker killed while reading | PASS / PASS | PASS / PASS | PASS | baseline cleared earlier |
| G8 corrupt settings | PASS / PASS | PASS / PASS | PASS | K95, K28 (passing since the stabilisation lane) |
| Navigation (3 tests) | PASS / PASS | PASS / PASS | Drawers FAIL at 1280, both themes | **K106**, a Phase 09 regression, fixed in `5520c82c` |
| Settings (2 tests) | PASS / PASS | PASS / PASS | PASS | — |
| A11yNames catalogue / search | PASS / PASS | PASS / PASS | PASS | — |
| ScanOcrSearch | PASS / PASS | PASS / PASS | PASS | K21, K97 (passing since the stabilisation lane / Phase 17) |
| LaunchCycles (20 launch/close) | PASS / PASS | n/a | PASS | K71 (load-sensitive; passed in both runs) |
| Smoke, Harness (3) | PASS / PASS | n/a | PASS | — |
| G9–G12 placeholders | NOT ASSESSED | NOT ASSESSED | NOT ASSESSED | owned by Phases 10, 11, 18, 19–20 |

The G3, G4 and G5 failures on `135187f3` did not reproduce on the final build. The earlier
failures were load-sensitive (G3's p95 was 257 ms against a 250 ms budget), so this run does not
close K32 or K40; one green run is not a trend. The K106 failure was deterministic: the
Navigation re-check `phase09-nav-recheck` failed again before the fix.

### Screenshots (final build `5520c82c`, synthetic corpus, isolated data dir, desktop lock)

| Surface | Light 1280x800 | Dark 1280x800 | Light 1920x1080 | Dark 1920x1080 |
|---|---|---|---|---|
| Library home | [jpg](evidence/sept-23-kaizen/phase-09/after/library-home-light-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/library-home-dark-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/library-home-light-1920x1080.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/library-home-dark-1920x1080.jpg) |
| Book detail | [jpg](evidence/sept-23-kaizen/phase-09/after/book-detail-light-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/book-detail-dark-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/book-detail-light-1920x1080.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/book-detail-dark-1920x1080.jpg) |
| Reader | [jpg](evidence/sept-23-kaizen/phase-09/after/reader-light-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/reader-dark-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/reader-light-1920x1080.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/reader-dark-1920x1080.jpg) |
| Search | [jpg](evidence/sept-23-kaizen/phase-09/after/search-light-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/search-dark-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/search-light-1920x1080.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/search-dark-1920x1080.jpg) |
| Settings | [jpg](evidence/sept-23-kaizen/phase-09/after/settings-light-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/settings-dark-1280x800.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/settings-light-1920x1080.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/settings-dark-1920x1080.jpg) |
| Component gallery (1280x1000) | [jpg](evidence/sept-23-kaizen/phase-09/after/component-gallery-light.jpg) | [jpg](evidence/sept-23-kaizen/phase-09/after/component-gallery-dark.jpg) | — | — |

All 22 files replace the earlier set; the old book-detail dark capture, taken at the wrong size,
is gone. The before captures are in `evidence/sept-23-kaizen/phase-09/before/` and the owner
concepts in `concept/`. Known visual issues that remain: the reader toolbar clips at 1280 (K104),
and synthetic first-page covers are mostly white pages with small titles, which is content, not
styling.

## Design-quality gate

| Item | Result | Evidence class |
|---|---|---|
| Typeface named and justified; no banned font | PASS | MEASURED (token test) |
| Display and body pairing | PASS | HEURISTIC (screenshots) |
| Real type-scale ratio, 12 px floor | PASS (it also survives preference changes, K101) | MEASURED |
| Line-height inverse rule | PASS on tokens (`Type.LineHeight.*`) | MEASURED (test) |
| Licence permits embedding (fonts, icons) | PASS | MEASURED (licence files and register) |
| Contrast: text ≥ 4.5:1, UI ≥ 3:1, both themes | PASS on token pairs | MEASURED (matrix test) |
| Colour is never the only carrier | PASS | HEURISTIC |
| Spacing on one unit | PARTIAL (literal margins remain in views; Phases 10–12) | HEURISTIC |
| State matrix per component | PASS for buttons, tabs and lists | HEURISTIC (gallery) |
| Focus always visible and never moves layout | PASS (K106 fixed) | MEASURED (test, Navigation journey) |
| Targets ≥ 24×24 | PASS | MEASURED |
| Reduced motion respected | PASS in code; the OS-toggle run is NOT ASSESSED | CODE |
| 200 % text, clipping harness, high contrast, screen reader | NOT ASSESSED (the harness does not support them yet; Phases 21/22) | NOT ASSESSED |
| Final verdict | **CONDITIONAL**: owner walkthrough, accessibility runs and K104 outstanding | — |

`product-design-audit` 11-dimension re-score, **HEURISTIC** (single rater, Windows only, from the
screenshots above): before about 48/100 raw (capped at 59 by the off-palette selection and the
sub-12 px caption); after about 78/100 raw, no gate failure, confidence medium. Remaining
deductions: reader toolbar clipping (K104), *Rename* overflow and the literal `||` split control
(K15 remainder), and spacing tokens not yet enforced.

## Definition of Done check

| DoD item (08-master-plan §5) | Holds? |
|---|---|
| 1 Tasks done or moved with a reason | Yes (deferrals below) |
| 2 Full gate set passes | **No**: the accountability script fails on K100 (from `main`). Every other gate and every assessed journey passes on the final build; hosted CI is NOT ASSESSED |
| 3 Tests failed before and pass after | Yes |
| 4 Before and after screenshots at both sizes, seen by the owner | **Partly**: the "after" set is complete; "before" exists only for Light 1280; the owner walkthrough is pending |
| 5 en/fr strings, tokens, accessible names | Yes (the lint and the A11yNames journeys pass) |
| 6 Completion record | Yes (this file) |
| 7 Status files updated | Yes |

## Premise corrections

- The accountability gate cannot pass on this branch: `aug-39` was archived on `main` (K100).
- D-15 (TabStrip) was already logged by slice 3, so it was not duplicated.
- `Invoke-GoldenJourneys.ps1` has `-TextScale` and `-ClippingCheck` switches, but the 200 % and
  clipping checks are not yet implemented (Phases 21/22), so they are NOT ASSESSED.
- The Avalonia engine route (the design-engine Avalonia skill) was not consulted in this slice:
  NOT ASSESSED.

## NOT ASSESSED and deferrals

| Item | Owner / phase |
|---|---|
| Owner walkthrough of the screenshots and gallery (D-14 "amaze me" acceptance) | Owner |
| Premium icon purchase (drop-in replacement, key for key) | Owner (D-07) |
| QR scan with a physical phone | Owner / tester |
| Windows high-contrast mode and screen reader on the new controls | Phase 21 |
| 200 % text and the clipping harness | Phases 21/22 |
| macOS rendering of fonts, icons and covers | Phase 27 |
| Hosted CI run on the pushed branch | Driver / coordinator |
| Avalonia engine route (design-engine Avalonia guidance) | Next design slice |
| Reader toolbar clipping at 1280 (K104); tab overflow adoption in the inspector and reader; *Rename* overflow; literal `||` split control | Phases 11/12 |
| Fast-suite slowdown (K105) | Phase 24 |
| App icon provenance (`Assets/app*`) | Owner |
| Remaining literal margins and paddings in views | Phases 10–12 |
