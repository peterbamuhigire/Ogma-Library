# Design system (Lamplit)

The visual identity is decided in
[ADR-0019 Lamplit visual identity](../adrs/0019-lamplit-visual-identity.md) (owner decision
D-14). This page is the working summary for developers. Every value lives in
`src/OgmaLibrary.App/Themes/Tokens.axaml`, and every component style lives in
`src/OgmaLibrary.App/Themes/Controls.axaml`. Views reference tokens with
`{DynamicResource ...}` and never style controls locally.

## Typeface decision

| Role | Typeface | Token | Used for |
| --- | --- | --- | --- |
| Display | Spectral (Regular, SemiBold) | `Type.FontFamily.Display` | Destination headings, card titles, the home greeting, generated cover titles |
| UI and body | Public Sans (Regular, Medium, SemiBold, Bold) | `Type.FontFamily.Body` | All controls and running text (the default for `Window` and `UserControl`) |
| Data | JetBrains Mono | `Type.FontFamily.Mono` | Paths, hashes, identifiers |

**Reason.** Ogma is a reading product. A humanist editorial serif gives headings a book-like
voice, a neutral, legible grotesque keeps dense UI calm, and the mono is clearly distinct
from both. All three are SIL OFL 1.1 and bundled under `src/OgmaLibrary.App/Assets/Fonts`
with their `OFL.txt`, so nothing depends on fonts installed on the machine. Platform fonts
(Segoe UI, Georgia, Consolas, Menlo) are only explicit fallbacks. Public Sans ships as static
weight instances because Avalonia does not apply variable-font axes; the variable file
rendered body text at Thin (100).

## Type scale and line heights

| Token step | Size (px) | Line height (px) | Ratio |
| --- | --- | --- | --- |
| `Caption` (floor) | 12 | 16 | 1.33 |
| `Small` (dense data) | 13 | 18 | 1.38 |
| `Body` | 14 | 21 | 1.50 |
| `BodyLarge` (reading prose) | 16 | 24 | 1.50 |
| `Subtitle` | 17 | 24 | 1.41 |
| `Title` | 21 | 28 | 1.33 |
| `Headline` | 27 | 34 | 1.26 |
| `Display` | 34 | 40 | 1.18 |

Sizes are `Type.Size.*` and line heights are `Type.LineHeight.*`. Line heights are absolute
pixels because Avalonia's `TextBlock.LineHeight` is not a multiplier. They follow the
inverse rule: body text breathes, display text sits tight, and the ratio never grows as the
size climbs. Hierarchy steps are at least 1.2, and at least 1.25 at the display end. No size
is below 12 px. `BodyMedium` is a legacy alias of `BodyLarge`.

Compact density scales sizes, line heights and spacing by 0.9 from the token baselines
(`Themes/DensityTokens.cs`), but font sizes never go below 12 px. Do not copy token values
into code.

## Icons

Lucide outlines (ISC), recoloured as duotones, are the interim set under owner decision D-07.
Every file is listed in the
[asset-licence register](../governance/asset-licence-register.md). Size an `svg:Svg` icon
with one class instead of a literal `Width`/`Height`:

| Class | Token | Size | Use |
| --- | --- | --- | --- |
| `icon-small` | `Icon.Size.Small` | 16 | Inline with text |
| `icon-medium` | `Icon.Size.Medium` | 20 | Toolbars and rails |
| `icon-large` | `Icon.Size.Large` | 24 | Headers and empty states |

## Buttons

Apply one variant class. A plain `Button` is Secondary.

| Variant | Class | Use |
| --- | --- | --- |
| Primary | `primary` | The one main action of a surface (filled `Accent.Primary`, `OnAccent` label) |
| Secondary | none | Outlined neutral control (default) |
| Tool / quiet | `tool` (alias `quiet`) | Transparent until hover: toolbars, rails, inline actions |
| Danger | `danger` | Destructive actions (filled `Status.Danger`) |
| Link | `link` | Text-only, navigation-like actions |

Modifiers: `icon` (square hit target), `compact` (dense toolbars), `busy` (in progress), and
`active` / `selected` (a toggled tool or segment). Every variant styles the rest,
pointer-over, pressed, focus-visible (a 2 px `Focus` ring) and disabled states. Buttons never
set a local `Background` or `Foreground`.

## Colour roles

Tokens have three tiers: primitives (jewel accents and hues), then semantic roles, then
component styles. Use the semantic roles:

- `Surface.*` (canvas, raised card, divider) and `Text.*` (primary, secondary);
- `Control.*` (control background and border);
- `Accent.Primary` / `Accent.Text` / `Accent.OnAccent` / `Accent.Glow`, `Link`, `Selection.*`
  and `Focus`;
- `Status.*` (success, warning, danger, info) and `Brush.Dest.*` (the hue for each destination);
- `Rail.*` (the ink navigation rail) and `Cover.Gilt`.

Light is a warm cream reading room. Dark is the Night Library (indigo `#121A2E` canvas,
parchment text), not a grey inversion. Text pairs must reach 4.5:1 and non-text pairs 3:1
in both themes. A new colour role must be added to the contrast matrix test.

## Component gallery

Press **Ctrl+Shift+Alt+G** in the main window to open the living component gallery
(`Views/Dev/ComponentGallery`). It is a hidden QA command that shows every button variant
and state, tab strip, badge, field, toggle, card and icon in the current theme, with
localised labels.

## Lint and tests

- `scripts/Test-DesignTokens.ps1` runs in CI (`.github/workflows/ci.yml`). It fails on
  literal font sizes or colours in views, buttons with a local fill, colour literals in view
  code-behind outside its allow-list, and literals in the app root. The one exception is
  Fluent `ColorPaletteResources`, whose values are guarded by tests.
- `tests/OgmaLibrary.Tests.Ui/Sept23Phase09DesignSystemTests.cs` guards contrast, the type
  scale and line heights, icon size tokens, the icon licence register, button variants,
  generated covers and the gallery in Light and Dark. The code-behind allow-list appears in
  both the script and this test file; change them together.
