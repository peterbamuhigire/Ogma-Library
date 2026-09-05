# Phase 18 Rendered Contrast Evidence

Date: 2026-09-05

## Scope

This evidence closes the repository-verifiable rendered-contrast subgate for
the shared design-token surface. It exercises the Avalonia headless renderer
with Skia, using both supported theme variants and the production Oak accent
and parchment surface resources.

## Proof

`Phase18DesignSystemTests.RenderedAccentSurface_MeetsSmallTextContrastInBothThemes`
passed for Light and Dark themes. For each theme the test:

- resolves the production `Color.Accent.Oak` and
  `Color.Surface.Parchment` resources;
- renders a centred accent action surface with a white label;
- captures and decodes the actual rendered frame;
- verifies sampled rendered pixels remain within three aggregate RGB channel
  units of the resolved token values; and
- verifies the rendered accent/white pair meets the 4.5:1 WCAG AA small-text
  threshold.

The test also writes reproducible snapshots to:

- `artifacts/screenshots/phase18-contrast-light.png`
- `artifacts/screenshots/phase18-contrast-dark.png`

Focused result: 5 passed, 0 failed, 0 skipped. The earlier automated matrix
continues to cover all six accent families; this test adds actual rendered
surface evidence for the shared action treatment.

The generated Light and Dark snapshots were visually inspected at original
resolution and showed the expected warm surface/accent treatment, centred
label, and no clipping or visible text overlap.

## Gate disposition

The repository-verifiable rendered contrast subgate is CLOSED. Physical
Windows/macOS screenshot review and Narrator/VoiceOver journeys remain NOT
ASSESSED and are still required before Phase 18 can close.
