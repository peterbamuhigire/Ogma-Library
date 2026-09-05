# Phase 18 Contrast Matrix Evidence

Date: 2026-09-05

## Scope

The shared accent tokens are used for action, navigation, status, AI, and
secondary-action surfaces that retain explicit white labels. The original
palette had white-label failures on Oak and Clay in light mode and on every
lightened dark-theme accent. The accent values were deepened while retaining
the existing semantic hue families.

## Automated matrix

The WCAG relative-luminance calculation uses white foreground text and the
resolved `Color.Accent.*` resource for each Avalonia `Light` and `Dark` theme
variant. The small-text threshold is 4.5:1.

| Accent family | Light | Dark | Result |
| --- | ---: | ---: | --- |
| Oak | 6.31:1 | 6.31:1 | PASS |
| Ink | 15.40:1 | 15.40:1 | PASS |
| Sage | 6.52:1 | 6.52:1 | PASS |
| Clay | 6.66:1 | 6.66:1 | PASS |
| Plum | 14.01:1 | 14.01:1 | PASS |
| Slate | 7.56:1 | 7.56:1 | PASS |

## Proof

- `Phase18DesignSystemTests.AccentPalette_WhiteActionLabelsMeetSmallTextContrastInBothThemes`: passed.
- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`: passed
  with 0 warnings and 0 errors.
- Focused `Phase18DesignSystemTests` slice: 4 passed, 0 failed, 0 skipped.

## Gate disposition

The automated contrast matrix is CLOSED. Rendered contrast snapshots on the
supported desktop platforms and Narrator/VoiceOver journeys remain NOT
ASSESSED; this evidence does not claim those physical gates.
