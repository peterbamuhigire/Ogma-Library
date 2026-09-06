# Phase 18 Font Packaging Evidence

Date accessed and verified: 2026-09-06

## Decision

Ogma packages Spectral 2.001 for literary display text, Public Sans 2.001 for
interface/body text, and JetBrains Mono for identifiers/technical data. This
pairing gives the library a recognizably editorial voice while preserving
legibility in dense desktop controls. Inter is removed from runtime composition
and dependency locks.

## Rights and currentness

| Family | Owner/source | Version/status | Licence | Scope and limitation |
| --- | --- | --- | --- | --- |
| Spectral | Production Type via Google Fonts, <https://github.com/google/fonts/tree/main/ofl/spectral> | 2.001; pinned release | SIL OFL 1.1 | Latin Pro coverage includes English and Western European languages; physical rendering remains unassessed |
| Public Sans | USWDS, <https://github.com/uswds/public-sans> | 2.001; upstream states it is not actively developed | SIL OFL 1.1 | Pinned UI/body asset, not an active-support claim; Latin-only upstream scope |
| JetBrains Mono | JetBrains via Google Fonts, <https://github.com/google/fonts/tree/main/ofl/jetbrainsmono> | Variable font pinned by digest | SIL OFL 1.1 | Technical/identifier role only |

The OFL files are redistributed beside each family. Avalonia's official custom
font guidance, <https://docs.avaloniaui.net/docs/styling/custom-fonts>, defines
the `avares://Assembly/Path#Family` resource form used by the application.

## Asset digests

| Asset | SHA-256 |
| --- | --- |
| `JetBrainsMono-VF.ttf` | `48715a42ec242c21e9f02692891e147d022299a52e48d5e413e1a942193ffeda` |
| `PublicSans-VF.ttf` | `d75a7dc1a27eb9e336d5b33f55489d2ecb5621bf694d5c43b2415bce2ca830a8` |
| `Spectral-Regular.ttf` | `fb147ad6ef88dfa39d06e368f08ac84a86274bb0590466af146fe06cd4a287a2` |
| `Spectral-SemiBold.ttf` | `376abb0253fa6e517c8b7d5c83cfde93c4ada07858143927e62c330bc084fd77` |

## Gate boundary

Repository packaging, resource resolution, role selection, and headless font
application are executable gates. Clean-machine Windows/macOS glyph rendering,
fallback behavior, visual hierarchy, truncation, and assistive-technology
inspection remain `NOT ASSESSED` until performed on the signed installed build.

The focused Phase 18 suite passed 5/5 after packaging. Manual inspection of the
headless Light and Dark frames confirmed the Public Sans action label renders
in both themes. The render test now uses per-window theme selection and fails
if the button region lacks a minimum body of light glyph pixels; this prevents
contrast-only math from passing when the intended label did not render.
