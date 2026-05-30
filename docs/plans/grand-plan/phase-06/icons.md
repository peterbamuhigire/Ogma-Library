# Phase 06 — Icon Manifest

Phase 06 is the richest UI phase to date, covering every primary browsing surface.
It introduces the largest icon set of any single phase. All icons follow the colorful
premium style from `ICON-SYSTEM.md`: warm library palette (oak-amber for primary
actions, ink for navigation/reading, sage for success, clay for warnings, plum for AI,
slate for secondary actions).

---

## Icon manifest

### View toggle icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_grid_view` | View toggle button: Grid | Switch to grid/cover view | 2×2 grid of squares; oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_list_view` | View toggle button: List | Switch to list view | Horizontal lines (list); oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_directory_view` | View toggle button: Directory | Switch to folder-tree view | Folder with tree lines; oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_3d_view` | View toggle button: 3D (placeholder) | 3D bookshelf (coming in Phase 14) | 3D cube or shelf; oak-amber; muted/disabled style for placeholder | 16/24/32 @1x-3x | ⬜ to procure |

### Sort & filter icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_sort_asc` | Sort bar: ascending toggle | Sort ascending | Up-arrow with lines; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_sort_desc` | Sort bar: descending toggle | Sort descending | Down-arrow with lines; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_filter` | Filter panel toggle button | Open/close filter panel | Funnel; oak-amber when active, slate when inactive | 16/24 @1x-3x | ⬜ to procure |
| `ic_filter_clear` | Filter "Clear all" button | Clear all active filters | Funnel with X; clay | 16/24 @1x-3x | ⬜ to procure |
| `ic_rating_star` | Rating filter; book-detail rating; list row | Star rating (1-5) | Filled star; warm gold/amber | 12/16/24 @1x-3x | ⬜ to procure |
| `ic_rating_star_empty` | Rating star (unselected) | Empty star slot | Outlined star; slate | 12/16/24 @1x-3x | ⬜ to procure |
| `ic_available` | Availability filter chip; status chip | Book is available on disk | Circle-check; sage green | 12/16/24 @1x-3x | ⬜ to procure |
| `ic_unavailable` | Availability filter chip; status chip | Book file missing from disk | Circle-X or broken link; clay | 12/16/24 @1x-3x | ⬜ to procure |

### Shelf icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_shelf` | Shelf sidebar list items; shelf chip | A virtual bookshelf | Shelf/bookcase outline; oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_shelf_smart` | Smart shelf list items | Smart (dynamic) shelf with conditions | Shelf with lightning bolt or gear; plum | 16/24 @1x-3x | ⬜ to procure |
| `ic_shelf_add` | "New Shelf" button | Create a new shelf | Shelf with + sign; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_shelf_rename` | Context menu: Rename | Rename a shelf | Pencil on shelf; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_shelf_delete` | Context menu: Delete | Delete a shelf | Shelf with X or trash; clay | 16/24 @1x-3x | ⬜ to procure |
| `ic_shelf_drag` | Shelf drag handle | Drag to reorder shelves | Grip dots; slate | 16/24 @1x-3x | ⬜ to procure |

### Tag icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_tag` | Tag chip; book-detail tags editor; filter | A tag/label applied to a book | Tag shape; oak-amber | 12/16/24 @1x-3x | ⬜ to procure |
| `ic_tag_add` | Tags editor "Add tag" action | Add a new tag to a book | Tag with + sign; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_tag_remove` | Tag chip remove button | Remove a tag from a book | Tag with X; clay | 12/16 @1x-3x | ⬜ to procure |

### Book-detail panel icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_open_reader` | "Read" button in book-detail | Open the PDF reader | Open book; ink blue | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_enrich` | "Enrich" button (disabled placeholder) | Enrich metadata from online providers | Sparkle or globe+book; plum; muted when disabled | 16/24 @1x-3x | ⬜ to procure |
| `ic_book_no_cover` | Cover placeholder in book-detail + grid | Book has no cover yet | Book outline with question mark; slate | 48/96 @1x-3x | ⬜ to procure |
| `ic_provenance` | Provenance indicator on enriched fields | Indicates field was enriched from a provider | Small badge: source icon; oak-amber | 12/16 @1x-3x | ⬜ to procure |
| `ic_edit_inline` | Inline field edit affordance | Click to edit this field | Pencil; slate | 12/16 @1x-3x | ⬜ to procure |
| `ic_field_group_file` | File field group tab | File & format metadata | Document/page; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_field_group_biblio` | Bibliographic field group tab | Bibliographic metadata | Open book; ink | 16/24 @1x-3x | ⬜ to procure |
| `ic_field_group_reading` | Reading field group tab | Reading state & progress | Bookmark; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_field_group_enrichment` | Enrichment field group tab | Provider-enriched fields | Globe/cloud with check; plum | 16/24 @1x-3x | ⬜ to procure |
| `ic_field_group_ai` | AI field group tab | AI-generated insights | Sparkle or brain; plum | 16/24 @1x-3x | ⬜ to procure |

### Bulk edit icons (V1)

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_bulk_edit` | Bulk-edit toolbar | Edit multiple books | Stacked pages with pencil; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_bulk_select_all` | Select-all button | Select all books | Multiple pages with checkmark; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_bulk_deselect` | Deselect button | Clear selection | Multiple pages with X; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_undo` | Undo button (Ctrl+Z) | Undo last edit | Curved arrow CCW; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_preview` | Preview button in bulk-edit | Show before/after preview | Eye over document; oak-amber | 16/24 @1x-3x | ⬜ to procure |

### Empty state icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_empty_library` | Empty library state | No books yet; prompt to select a folder | Large bookshelf outline; oak-amber; friendly/welcoming | 96/128 @1x-3x | ⬜ to procure |
| `ic_empty_filter` | No search/filter results | No books match the current filters | Funnel with 0; slate | 48/96 @1x-3x | ⬜ to procure |
| `ic_empty_shelf` | Empty shelf state | This shelf has no books yet | Shelf outline; oak-amber | 48/96 @1x-3x | ⬜ to procure |

### Miscellaneous

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_folder` | Directory tree node | A folder in the library tree | Folder; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_settings` | Settings navigation entry | Open settings panel | Gear; slate | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_close_panel` | Book-detail panel close button | Close the slide-in panel | X or chevron-right; slate | 16/24 @1x-3x | ⬜ to procure |

**Total new icons in this phase: ~32.**

---

## Accessible labels (en + fr required)

All 32 icon keys must have localized `AutomationProperties.Name` / `ToolTip` in
both `en.resx` and `fr.resx` before their corresponding control ships. A missing
label key fails the `IconCatalog` build check (Phase 03).

---

## Owner procurement request

**To: Peter Bamuhigire**

Phase 06 needs approximately **32 premium PNG icons** — the largest single procurement
request in the project. These cover the core browsing, sorting, filtering, shelf,
tag, rating, bulk-edit, and empty-state surfaces that users will see constantly.

**Style specification (from `ICON-SYSTEM.md`):**
- Colorful duotone or flat-color; warm library aesthetic. A single cohesive family
  (same vendor as Phase 03 selection, consistent grid/stroke/corner radius).
- Color assignments:
  - **Oak-amber**: primary actions (scan, grid view, list view, shelf, add, enrich, open reader)
  - **Ink blue**: reading/navigation (open reader, file-group tab, list, directory)
  - **Sage green**: success/available states
  - **Clay/terracotta**: warnings, unavailable, remove/delete, filter-clear
  - **Plum**: AI and enrichment surfaces (AI field group, enrich, smart shelf, provenance)
  - **Slate**: secondary/neutral (sort, settings, inline-edit, deselect, undo, close)
  - **Gold/warm amber**: star ratings
- Light **and** dark variants where the vendor provides them.
- Empty-state icons (library, filter, shelf) should be larger-format, "illustration-style"
  icons suitable for a 96-128 px display area — friendly and inviting, not alarming.

**Sizes required:**
- Standard: **16, 24, 32 px** @1x, @2x, @3x.
- Empty-state large: **48, 96 px** @1x, @2x, @3x.
- Rating star: **12, 16, 24 px** @1x, @2x, @3x (both filled and empty variants).

**License requirement:**
- Redistribution in a signed app sold on the **Mac App Store** and **Microsoft
  (Windows) Store**.

**Delivery path:**
- `OgmaLibrary.App/Assets/icons/catalogue/` for browsing, sort/filter, shelf, tag,
  bulk-edit, and miscellaneous icons.
- `OgmaLibrary.App/Assets/icons/reader/` for `ic_open_reader`.
- `OgmaLibrary.App/Assets/icons/empty-states/` for the three empty-state icons.

**Timing:** Icons are needed before WP12 (final UI pass). All prior WPs can use
neutral placeholders. Premium assets are a **release blocker**, not a build blocker.
