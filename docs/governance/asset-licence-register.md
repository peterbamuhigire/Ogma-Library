# Asset licence register

Every bundled visual asset (fonts, icons, images) is listed here with its source, licence,
version and date. An asset without a row here must not ship (`scripts/Test-DesignTokens.ps1`
and `Sept23Phase09DesignSystemTests` enforce the icon side). Owner decision D-07 (Sept-23
Kaizen): an open-licence, consistent, colourful icon set with a recorded licence is the interim;
the owner's premium purchase is a later drop-in that replaces the SVGs key for key.

| Asset | Path | Source | Version | Licence | Redistribution in the app | Recorded |
|---|---|---|---|---|---|---|
| Icon set "Lamplit" duotone (85 SVGs, one per `IconCatalog` key) | `src/OgmaLibrary.App/Assets/icons/**` | Lucide outlines (<https://github.com/lucide-icons/lucide>) taken from the npm package `@iconify-json/lucide` (package.json `"license": "ISC"`; info.json licence URL <https://github.com/lucide-icons/lucide/blob/main/LICENSE>); recoloured as duotones by `scripts/icons/New-DuotoneIcons.py`; glyph-per-key map in `scripts/icons/icon-map.txt`; source bodies vendored in `scripts/icons/lucide-subset.json` | 1.2.110 | ISC (portions MIT, Feather) | Permitted, notice retained in `Assets/icons/LICENSE-ICONS.txt` | 2026-09-25, Phase 09 |
| Spectral (display) | `src/OgmaLibrary.App/Assets/Fonts/Spectral/` | Production Type / Google Fonts | as bundled | SIL OFL 1.1 (`OFL.txt`) | Permitted with licence file | Phase 18 (grand plan), re-confirmed Phase 09 |
| Public Sans (body, UI) | `src/OgmaLibrary.App/Assets/Fonts/PublicSans/` | USWDS / Google Fonts | Static Regular/Medium/SemiBold/Bold instances (wght 400/500/600/700) cut from the bundled variable font with fontTools 4.63 `varLib.instancer`; the variable file was removed because Avalonia ignores its axes and rendered its Thin default | SIL OFL 1.1 (`OFL.txt`); no Reserved Font Name, so the modified files keep the family name | Permitted with licence file | Phase 18, re-confirmed and instanced Phase 09 |
| JetBrains Mono (data) | `src/OgmaLibrary.App/Assets/Fonts/JetBrainsMono/` | JetBrains | as bundled | SIL OFL 1.1 (`OFL.txt`) | Permitted with licence file | Phase 18, re-confirmed Phase 09 |
| Generated covers, progress rings, paper grain | code (`Views/Catalogue/GeneratedCover.cs`, `ProgressRing.cs`, `Themes/Tokens.axaml`) | Original work in this repository | n/a | Repository licence | Permitted | 2026-09-25, Phase 09 |
| App icon | `src/OgmaLibrary.App/Assets/app*.png`, `app.ico`, `app.icns` | Pre-existing | n/a | NOT ASSESSED (provenance not recorded before Phase 09) | NOT ASSESSED | Owner to confirm |

## Superseded

| Asset | Reason | Date |
|---|---|---|
| Previous per-key SVGs (Flaticon downloads with `fi_*` ids and a Freepik "Capa_1" export) | No purchase record or licence file in the repository, so their redistribution right could not be verified; mixed styles. Replaced key for key by the Lamplit set. | 2026-09-25, Phase 09 |

## Replacing the icon set (premium drop-in)

1. Record the purchase (vendor, licence, order reference, redistribution terms) in this table first.
2. Drop the new files at the same `Assets/icons/<category>/<key>.svg` paths (or extend
   `IconCatalog` for PNG), then update `LICENSE-ICONS.txt`.
3. Run `./scripts/Test-DesignTokens.ps1` and the UI tests.
