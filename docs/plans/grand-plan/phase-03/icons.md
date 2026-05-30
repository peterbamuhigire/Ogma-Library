# Phase 03 — Icon Manifest

Phase 03 establishes the icon system. This is the first phase that procures
and wires colorful premium icons. All icons below are initially status
`🟨 placeholder in use`; the import script moves them to `🟨 placeholder in use`
once placeholder PNGs are generated, and to `✅ premium PNG wired` once
the owner-purchased premium PNGs are imported via `scripts/Import-Icons.ps1`.

---

## Icon manifest

### Category: `app` — Application chrome and global controls

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_app_logo` | Window title bar, About screen, Taskbar | Ogma Library application identity mark | Warm oak amber; stylized open book or owl motif; colorful | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_settings` | Menu → Settings; toolbar settings button | Open the application settings | Slate gear; duotone slate/light-slate | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_keyboard_shortcut` | Command palette item → shortcut chip | Indicates a keyboard shortcut is available | Neutral slate; minimal keycap style | 12/16 @1x-3x | 🟨 placeholder in use |
| `ic_close` | Command palette dismiss button; dialog close | Dismiss or close a panel | Slate × glyph; clean, no background | 16/24 @1x-3x | 🟨 placeholder in use |
| `ic_search` | Command palette text box leading icon; search bar | Initiate a search | Ink-blue magnifying glass; duotone | 16/24/32 @1x-3x | 🟨 placeholder in use |

### Category: `library` — Library setup and management

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_lib_scan` | Command: Scan Library; toolbar Scan button | Scan/rescan the library root folder | Oak-amber radar/scan wave; colorful | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_lib_folder_open` | Command: Open Library Folder; file picker | Open or change the library root folder | Oak-amber open folder; colorful | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_lib_health` | Command: Library Health; health dashboard link | View the library health report | Sage-green heart-rate or pulse; colorful | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_lib_preferences` | Command: Library Preferences | Open per-library preferences | Slate gear with a small book overlay | 16/24/32/48 @1x-3x | 🟨 placeholder in use |

### Category: `catalogue` — Catalogue browsing views and navigation

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_cat_view_grid` | View toggle: Grid view | Switch to grid (cover) view | Ink-blue grid of 4 squares; duotone | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_view_list` | View toggle: List view | Switch to list view | Ink-blue horizontal lines; clean | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_view_shelf3d` | View toggle: 3D Shelf view | Switch to 3D bookshelf view | Oak-amber tilted book stack; colorful | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_view_directory` | View toggle: Directory view | Switch to directory/folder tree view | Ink-blue folder tree | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_shelf` | Shelf items in navigation tree; shelf management | A virtual shelf holding books | Oak-amber bookshelf side-view; warm | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_cat_shelf_new` | Command: New Shelf | Create a new virtual shelf | Oak-amber shelf with a `+` badge | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_filter` | Filter panel toggle; sort & filter control | Open the filter/sort panel | Ink-blue funnel; clean | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_cat_sort` | Sort menu | Change the sort order | Ink-blue up/down sort arrows | 16/24 @1x-3x | 🟨 placeholder in use |

### Category: `reader` — PDF reader controls

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_read_open` | Command: Open Book; book-detail Open button | Open a book in the reader | Ink-blue open book with page-turn curl; colorful | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_read_bookmark` | Command: Add Bookmark; bookmark button in reader toolbar | Add a bookmark at the current page | Oak-amber ribbon bookmark; colorful | 16/24/32 @1x-3x | 🟨 placeholder in use |
| `ic_read_zoom_in` | Zoom in button | Increase zoom level | Ink-blue magnifying glass with `+` | 16/24 @1x-3x | 🟨 placeholder in use |
| `ic_read_zoom_out` | Zoom out button | Decrease zoom level | Ink-blue magnifying glass with `−` | 16/24 @1x-3x | 🟨 placeholder in use |
| `ic_read_fullscreen` | Full-screen toggle | Enter/exit full-screen reader mode | Ink-blue expand arrows | 16/24 @1x-3x | 🟨 placeholder in use |

### Category: `search` — Search and discovery

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_search_metadata` | Command: Search Books (metadata search) | Run a metadata search | Ink-blue magnifying glass over a card/record | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_search_fulltext` | Command: Full-Text Search (V1) | Run a full-text search (grayed in MVP without FTS) | Ink-blue magnifying glass over text lines | 16/24/32/48 @1x-3x | 🟨 placeholder in use |

### Category: `ai` — AI advisor

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_ai_advisor` | Command: Ask AI Advisor; AI sidebar toggle | Open the AI reading advisor | Plum spark/wand; colorful, warm | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_ai_privacy` | Command: AI Privacy Settings; Privacy Center toggle | Open AI privacy settings and the Privacy Center | Plum shield with an eye | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_ai_disable` | Command: Disable AI; AI status indicator | Indicates AI is disabled / toggle AI off | Plum spark with a slash; desaturated | 16/24 @1x-3x | 🟨 placeholder in use |

### Category: `settings` — Application settings

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_set_appearance` | Settings: Appearance section | Appearance settings (theme, font, density) | Slate paintbrush or palette | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_set_language` | Settings: Language / Localization section | Language and localization settings | Slate globe with text lines | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_set_updates` | Settings: Updates; Command: Check for Updates | Software update settings and status | Slate cloud with download arrow | 16/24/32/48 @1x-3x | 🟨 placeholder in use |
| `ic_set_about` | Menu: About Ogma Library; Settings: About | About screen and version info | App logo variant; small | 16/24 @1x-3x | 🟨 placeholder in use |

### Category: `status` — Status and feedback icons

| Icon key | Used on | Meaning | Style/color note | Sizes (px base) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_status_available` | Book availability chip: Available | The PDF file is available at its path | Sage green circle checkmark | 12/16/24 @1x-3x | 🟨 placeholder in use |
| `ic_status_unavailable` | Book availability chip: Unavailable | The PDF file is missing or unavailable | Clay terracotta circle X | 12/16/24 @1x-3x | 🟨 placeholder in use |
| `ic_status_loading` | Spinner / progress indicator | An operation is in progress | Ink-blue animated arc (spinner); use the animated variant if the vendor supplies it | 16/24 @1x-3x | 🟨 placeholder in use |

---

## Summary count

| Category | Icon count | Status |
| --- | --- | --- |
| `app` | 5 | All 🟨 placeholder in use |
| `library` | 4 | All 🟨 placeholder in use |
| `catalogue` | 8 | All 🟨 placeholder in use |
| `reader` | 5 | All 🟨 placeholder in use |
| `search` | 2 | All 🟨 placeholder in use |
| `ai` | 3 | All 🟨 placeholder in use |
| `settings` | 4 | All 🟨 placeholder in use |
| `status` | 3 | All 🟨 placeholder in use |
| **Total Phase 03** | **34** | **34 🟨 placeholder in use** |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 03 premium icon procurement — 34 icons**

Ogma Library's design system requires one cohesive premium icon family for all
34 icons listed above, plus future phases (estimated total: ~120 icons across
all 24 phases). The Phase 03 batch is the first procurement.

### Vendor selection criteria (from ICON-SYSTEM.md §5)

Please select **one vendor/family** that meets all of the following:

1. **Style:** colorful / duotone flat-color or outlined-with-fill; warm palette
   compatible with the oak amber / ink blue / sage / clay / plum / slate token
   families.
2. **Coverage:** full coverage of books, shelves, scanning, search, AI, reader,
   settings, status, and classroom/admin (for future phases). Ideally a vendor
   who can supply custom additions for the rare keys they don't have standard
   icons for.
3. **Format:** PNG at @1x / @2x / @3x for base sizes 16, 24, 32, and 48 px
   (i.e. each icon is supplied as 16/32/48, 24/48/72, 32/64/96, 48/96/144 px
   files). SVG also welcome if the vendor supplies it.
4. **License:** permits redistribution inside a **signed desktop app** sold on
   the **Mac App Store** and the **Windows (Microsoft) Store**. The license
   must explicitly cover app-store resale embedding.
5. **Light + dark variants:** the vendor should supply both light-background
   and dark-background versions, or the icons should be designed so they read
   well on both `Color.Surface.Parchment` (`#FAF7F2`) and
   `Color.Surface.Walnut` (`#1E1A17`).

### Recommended vendors (shortlist for owner review)

The engineering team will prepare a 2–3 vendor shortlist with sample icon
previews from each vendor covering 5–6 of the icon keys above, so Peter can
compare visual quality and style fit before purchasing.

### Delivery format

Supply the purchased PNGs in a ZIP archive with the naming convention:
`<icon_key>@Nx.png` (e.g. `ic_lib_scan@1x.png`, `ic_lib_scan@2x.png`,
`ic_lib_scan@3x.png`). The engineering team will run
`scripts/Import-Icons.ps1 -SourceDir <path-to-zip-contents>` to wire them in
and update the MASTER-MANIFEST.md status to `✅ premium PNG wired`.

### Blocking note

Premium PNGs are a **release blocker** for any phase that ships a UI surface.
Placeholder icons (programmatically generated gray circles) are used during
development so no work is blocked. However, Phase 03 must have premium PNGs
wired before any alpha/beta release. We request the vendor selection and
purchase to happen within **2 weeks** of Phase 03 starting.
