# Phase 08 — Icon Manifest

All new icons introduced in the PDF Reader Core phase.
Style tokens are defined in `ICON-SYSTEM.md §4`; the agreed base sizes are
16, 24, 32, 48 px at @1x/2x/3x for Windows and macOS HiDPI/Retina.

Color family mapping for this phase:
- Navigation icons — `accent/ink` (deep ink blue): reading & navigation.
- Zoom icons — `accent/ink` secondary shade.
- Display-mode icons — `accent/ink`.
- Full-screen icons — `accent/slate` (neutral slate): secondary actions.
- Search icons — `accent/oak` (warm oak amber): primary / identity.
- Page-jump / input icons — `accent/slate`.

---

## Icon manifest table

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_reader_first_page` | Reader toolbar | Jump to first page | Outlined arrow to left edge; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_prev_page` | Reader toolbar | Go to previous page | Single left chevron; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_next_page` | Reader toolbar | Go to next page | Single right chevron; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_last_page` | Reader toolbar | Jump to last page | Outlined arrow to right edge; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_nav_back` | Reader toolbar / history | Navigate back in history | Counter-clockwise curved arrow; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_nav_forward` | Reader toolbar / history | Navigate forward in history | Clockwise curved arrow; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_jump_page` | Reader toolbar / page-jump | Jump to specific page | Bookmark + page number indicator; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_zoom_fit_width` | Zoom toolbar | Fit page to container width | Page with horizontal arrows; `accent/ink` light | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_zoom_fit_page` | Zoom toolbar | Fit full page in view | Page with all-corner arrows; `accent/ink` light | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_zoom_fixed` | Zoom toolbar | Fixed percentage zoom | Magnifier with percentage symbol; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_zoom_in` | Zoom toolbar | Increase zoom | Magnifier with `+`; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_zoom_out` | Zoom toolbar | Decrease zoom | Magnifier with `-`; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_display_single` | Display-mode selector | Single-page mode | One page rectangle; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_display_two_page` | Display-mode selector | Two-page spread mode | Two page rectangles side by side; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_display_continuous` | Display-mode selector | Continuous-scroll mode | Stacked pages with scroll indicator; `accent/ink` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_fullscreen_enter` | Toolbar / full-screen toggle | Enter full-screen | Four outward corner arrows; `accent/slate` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_fullscreen_exit` | Toolbar / full-screen toggle | Exit full-screen | Four inward corner arrows; `accent/slate` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_search` | Search panel trigger button | Open in-document text search | Magnifier over text lines; `accent/oak` amber | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_search_next` | Search panel navigation | Next search match | Downward chevron in search context; `accent/oak` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_search_prev` | Search panel navigation | Previous search match | Upward chevron in search context; `accent/oak` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_search_close` | Search panel dismiss button | Close search panel | X / close in `accent/slate` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_no_text_layer` | Search panel — scanned-page notice | Indicates no text layer (OCR required) | Magnifier with warning dot; `accent/clay` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_reader_page_count` | Status bar | Total page count indicator | Stacked pages mini icon; `accent/slate` | 16/24 @1x–3x | ⬜ to procure |

---

## Accessible label keys (en + fr required)

Each icon must be registered in `IconCatalog` with a localized label resource key.
The build fails if the active locale is missing a label for any registered icon.

| Icon key | Label resource key | en label | fr label |
| --- | --- | --- | --- |
| `ic_reader_first_page` | `Reader.Nav.FirstPage` | "First page" | "Première page" |
| `ic_reader_prev_page` | `Reader.Nav.PrevPage` | "Previous page" | "Page précédente" |
| `ic_reader_next_page` | `Reader.Nav.NextPage` | "Next page" | "Page suivante" |
| `ic_reader_last_page` | `Reader.Nav.LastPage` | "Last page" | "Dernière page" |
| `ic_reader_nav_back` | `Reader.Nav.Back` | "Back" | "Retour" |
| `ic_reader_nav_forward` | `Reader.Nav.Forward` | "Forward" | "Suivant" |
| `ic_reader_jump_page` | `Reader.Nav.JumpPage` | "Go to page" | "Aller à la page" |
| `ic_zoom_fit_width` | `Reader.Zoom.FitWidth` | "Fit width" | "Ajuster à la largeur" |
| `ic_zoom_fit_page` | `Reader.Zoom.FitPage` | "Fit page" | "Ajuster à la page" |
| `ic_zoom_fixed` | `Reader.Zoom.Fixed` | "Fixed zoom" | "Zoom fixe" |
| `ic_zoom_in` | `Reader.Zoom.In` | "Zoom in" | "Agrandir" |
| `ic_zoom_out` | `Reader.Zoom.Out` | "Zoom out" | "Réduire" |
| `ic_display_single` | `Reader.Display.Single` | "Single page" | "Page unique" |
| `ic_display_two_page` | `Reader.Display.TwoPage` | "Two-page spread" | "Double page" |
| `ic_display_continuous` | `Reader.Display.Continuous` | "Continuous scroll" | "Défilement continu" |
| `ic_reader_fullscreen_enter` | `Reader.FullScreen.Enter` | "Enter full screen" | "Plein écran" |
| `ic_reader_fullscreen_exit` | `Reader.FullScreen.Exit` | "Exit full screen" | "Quitter le plein écran" |
| `ic_reader_search` | `Reader.Search.Open` | "Search in document" | "Rechercher dans le document" |
| `ic_reader_search_next` | `Reader.Search.Next` | "Next match" | "Correspondance suivante" |
| `ic_reader_search_prev` | `Reader.Search.Prev` | "Previous match" | "Correspondance précédente" |
| `ic_reader_search_close` | `Reader.Search.Close` | "Close search" | "Fermer la recherche" |
| `ic_reader_no_text_layer` | `Reader.Search.NoTextLayer` | "No text layer" | "Aucune couche de texte" |
| `ic_reader_page_count` | `Reader.Status.PageCount` | "Page {0} of {1}" | "Page {0} sur {1}" |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 08 Reader Core — Premium PNG Icon Procurement**

Phase 08 introduces **23 new icons** for the PDF reader UI. Please purchase
(or supply from an existing premium set) the icons listed in the manifest table
above. The procurement specification is:

**Style:** colorful, duotone or flat-color; warm library aesthetic consistent
with the Phase 03 design-system tokens. Icons must feel cohesive with icons
purchased in prior phases (same vendor / same grid / same stroke weight and
corner radius).

**Color families required:**
- `accent/ink` (deep ink blue) for all navigation, zoom, and display-mode icons.
- `accent/oak` (warm oak amber) for search icons.
- `accent/slate` (neutral slate) for full-screen and secondary action icons.
- `accent/clay` (terracotta) for the no-text-layer warning icon.
- Light and dark variants for all icons.

**Size matrix (mandatory):**

| Base size | @1x | @2x | @3x |
| --- | --- | --- | --- |
| 16 px | 16×16 | 32×32 | 48×48 |
| 24 px | 24×24 | 48×48 | 72×72 |
| 32 px | 32×32 | 64×64 | 96×96 |
| 48 px | 48×48 | 96×96 | 144×144 |

**License requirement:** the license must permit redistribution inside a signed
desktop application distributed via the **Mac App Store** and the **Microsoft
(Windows) Store**, as well as direct (Velopack/DMG) distribution. Please
confirm the license terms before purchase.

**Delivery format:** PNG files named `<icon_key>@Nx.png` (e.g.
`ic_reader_next_page@2x.png`), placed in
`OgmaLibrary.App/Assets/icons/reader/` per the `ICON-SYSTEM.md` storage
convention.

Until premium PNGs arrive, placeholder icons are used and flagged `🟨` in this
manifest. Shipping a release with `🟨` placeholders is a **release blocker**.
