# ADR-0019: "The Lamplit Library" visual identity and enforced token system

- Status: Accepted (owner-proxy, Sept-23 Kaizen Phase 09; queued for the owner walkthrough)
- Date: 2026-09-25
- Deciders: Phase 09 lane under delegated owner authority (coordinator brief relaying the owner's
  feedback "the UI is a bit dull, too greyish and boring. Amaze me!")
- Related: K15 (incoherent visual system), K14, K33; decisions D-07 (icons) and D-13 (wireframes)

## Context

The measured app (25 September) had four unrelated button styles, an off-palette salmon
selection (the Windows accent leaking through Fluent), tab headers at display size wrapping to
three rows, an 11 px caption, 21 hard-coded font sizes and 77 hard-coded whites, identical flat
brown cover placeholders and a mixed, unlicensed icon set. The owner asked for a beautiful,
colourful app, then explicitly for something vivid and memorable rather than subtle.

## Decision

1. **Concept.** A warm, jewel-toned reading room. Chrome and content are separated: a deep
   midnight-ink navigation rail (both themes) with luminous hued icons; a warm cream canvas with a
   faint deterministic paper grain; cards that float on soft warm shadows instead of grey borders.
2. **Typefaces (unchanged, stated per the design engine).** Spectral for display (destination
   headings, card titles, the home greeting, generated cover titles); Public Sans for UI and body;
   JetBrains Mono for paths, hashes and data. All SIL OFL 1.1, bundled. No banned font. Reason:
   a humanist editorial serif for a reading product, a neutral legible grotesque for dense UI,
   and a mono distinct from both.
3. **Palette.** Jewel accents as functional colours: amber (primary, Library), lapis (Search,
   info), malachite (Reading, success), plum (Advisor/AI), saffron (Collections, warning),
   garnet (Activity, danger), teal (Classroom), slate (Settings, neutral). Fill tones are darkened
   until white labels reach 4.5:1; icon hues are tuned to hold 3:1 against the cream canvas, the
   raised card, the ink rail and the night canvas. Each destination's hue drives its rail icon,
   active indicator and page-header accent bar (`Brush.Dest.*`).
4. **Night Library (Dark).** Deep indigo canvas (#121A2E), parchment text, gold accent text, amber
   glow selection; not a grey inversion. The reader page canvas stays paper-coloured.
5. **Token tiers.** Primitives (accents, hues) → semantic roles (`Surface.*`, `Text.*`,
   `Control.*`, `Accent.Primary/Text/OnAccent/Glow`, `Link`, `Selection.*`, `Status.*`, `Focus`,
   `Rail.*`, `Cover.Gilt`) → component styles in `Themes/Controls.axaml`. Legacy primitive keys
   (`Oak`, `Ink`, `Sage`, `Clay`, `Parchment`…) remain as aliases so no view breaks.
6. **Type scale.** Caption 12 (floor), Small 13 and BodyLarge 16 (density helpers), Body 14 →
   Subtitle 17 → Title 21 → Headline 27 → Display 34 (steps 1.21, 1.24, 1.29, 1.26).
7. **Components.** Five button variants (primary, secondary default, tool/quiet, danger, link)
   plus icon/compact/busy modifiers; one TabStrip (Body size, one scrollable row); segmented
   control; badge/chip with status modifiers; book card with hover lift and amber selection glow;
   list/tree selection on `Selection.*`; a 2 px focus ring on every interactive control.
8. **Icons (D-07 interim).** Lucide outlines (ISC, verified from the vendored npm package
   metadata) recoloured as duotones in the destination/category hues by
   `scripts/icons/New-DuotoneIcons.py`; recorded in `docs/governance/asset-licence-register.md`.
   The earlier Flaticon/Freepik files were removed because no licence record existed.
9. **Generated covers.** Deterministic cloth-bound covers: FNV-1a hash of the title picks one of
   eight jewel cloths and one of three weaves; gradient, spine band with edge highlight, gilt frame,
   Spectral title, small-caps author; monogram on thumbnails. Real extracted covers still win.
10. **Library home.** A greeting header ("Good evening") with the library size and a strip of books
    to continue (progress rings) or, when none are in progress, the first shelved books.
11. **Motion.** Card lift, shadow and colour transitions only under a window carrying the `motion`
    class, which is withheld when Windows "Show animations" is off or `OGMA_REDUCED_MOTION=1`.
12. **Enforcement.** `scripts/Test-DesignTokens.ps1` (CI lint) and `Sept23Phase09DesignSystemTests`
    fail on literal font sizes or colours in views, unregistered or unlicensed icons, contrast
    regressions in either theme and type-scale regressions.

## Consequences

- Every surface inherits the identity through tokens; Phases 10–12 restyle layouts on top of it.
- Contrast is a gate, not a review: new colour roles must be added to the matrix test.
- The icon set is a replaceable interim; a premium purchase drops in key for key.
- Risk: a vivid palette can turn garish. Mitigation: hues are confined to icons, indicators,
  covers and a few fills; canvas and text stay calm; owner walkthrough pending.
- Not done here (recorded as deferrals): route cross-fade and drawer slide animations, an
  overflow menu on the TabStrip, macOS rendering evidence, Windows high-contrast mode.
