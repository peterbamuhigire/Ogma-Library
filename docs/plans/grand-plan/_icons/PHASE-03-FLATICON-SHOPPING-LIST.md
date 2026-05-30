# Phase 03 — Flaticon Shopping List (34 icons)

The first icon procurement batch (`ogma-phase03-core`). **Style: flat full-color**
(D-001), **source: Flaticon** (D-005, your account), master **SVG** + exported
PNG @1x/2x/3x (D-004). Use **Flaticon Premium** (no attribution) for store
distribution, or add a Flaticon credit screen if on free tier.

## How to buy efficiently

1. On Flaticon, create a **collection** named `ogma-phase03-core`.
2. For each row below, run the **search query**, filter style to **"Color"**,
   pick the icon that best fits the **color family**, and add it to the
   collection. Prefer icons from a **single author/pack** so the set is cohesive
   (Flaticon shows "More icons from this pack" — lean on one pack).
3. Download each as **SVG** (and PNG if offered) and place at the **destination**
   path. Or download the whole collection pack and we sort with `Import-Icons`.
4. Record the chosen Flaticon asset URL/ID in the last column for auditability.

Destination root: `src/OgmaLibrary.App/Assets/icons/<category>/<icon_key>.svg`

## The list

| # | icon_key | Color family | Flaticon search query | Destination (`Assets/icons/…`) | Flaticon URL (fill in) |
| --- | --- | --- | --- | --- | --- |
| 1 | `ic_app_logo` | oak | `open book library logo` | `app/ic_app_logo.svg` | |
| 2 | `ic_settings` | slate | `settings gear` | `app/ic_settings.svg` | |
| 3 | `ic_keyboard_shortcut` | slate | `keyboard key` | `app/ic_keyboard_shortcut.svg` | |
| 4 | `ic_close` | slate | `close cross button` | `app/ic_close.svg` | |
| 5 | `ic_search` | ink | `search magnifier` | `app/ic_search.svg` | |
| 6 | `ic_lib_scan` | oak | `scan document` | `library/ic_lib_scan.svg` | |
| 7 | `ic_lib_folder_open` | oak | `open folder` | `library/ic_lib_folder_open.svg` | |
| 8 | `ic_lib_health` | sage | `health heartbeat pulse` | `library/ic_lib_health.svg` | |
| 9 | `ic_lib_preferences` | slate | `book settings gear` | `library/ic_lib_preferences.svg` | |
| 10 | `ic_cat_view_grid` | ink | `grid view` | `catalogue/ic_cat_view_grid.svg` | |
| 11 | `ic_cat_view_list` | ink | `list view` | `catalogue/ic_cat_view_list.svg` | |
| 12 | `ic_cat_view_shelf3d` | oak | `stack of books` | `catalogue/ic_cat_view_shelf3d.svg` | |
| 13 | `ic_cat_view_directory` | ink | `folder tree directory` | `catalogue/ic_cat_view_directory.svg` | |
| 14 | `ic_cat_shelf` | oak | `bookshelf` | `catalogue/ic_cat_shelf.svg` | |
| 15 | `ic_cat_shelf_new` | oak | `add bookshelf` | `catalogue/ic_cat_shelf_new.svg` | |
| 16 | `ic_cat_filter` | ink | `filter funnel` | `catalogue/ic_cat_filter.svg` | |
| 17 | `ic_cat_sort` | ink | `sort arrows` | `catalogue/ic_cat_sort.svg` | |
| 18 | `ic_read_open` | ink | `open book reading` | `reader/ic_read_open.svg` | |
| 19 | `ic_read_bookmark` | oak | `bookmark ribbon` | `reader/ic_read_bookmark.svg` | |
| 20 | `ic_read_zoom_in` | ink | `zoom in` | `reader/ic_read_zoom_in.svg` | |
| 21 | `ic_read_zoom_out` | ink | `zoom out` | `reader/ic_read_zoom_out.svg` | |
| 22 | `ic_read_fullscreen` | ink | `fullscreen expand` | `reader/ic_read_fullscreen.svg` | |
| 23 | `ic_search_metadata` | ink | `search file card` | `search/ic_search_metadata.svg` | |
| 24 | `ic_search_fulltext` | ink | `search text document` | `search/ic_search_fulltext.svg` | |
| 25 | `ic_ai_advisor` | plum | `ai magic wand sparkle` | `ai/ic_ai_advisor.svg` | |
| 26 | `ic_ai_privacy` | plum | `privacy shield eye` | `ai/ic_ai_privacy.svg` | |
| 27 | `ic_ai_disable` | plum | `magic wand off disable` | `ai/ic_ai_disable.svg` | |
| 28 | `ic_set_appearance` | slate | `paint palette` | `settings/ic_set_appearance.svg` | |
| 29 | `ic_set_language` | slate | `language globe translate` | `settings/ic_set_language.svg` | |
| 30 | `ic_set_updates` | slate | `cloud download update` | `settings/ic_set_updates.svg` | |
| 31 | `ic_set_about` | slate | `information info` | `settings/ic_set_about.svg` | |
| 32 | `ic_status_available` | sage | `check mark circle` | `status/ic_status_available.svg` | |
| 33 | `ic_status_unavailable` | clay | `cross error circle` | `status/ic_status_unavailable.svg` | |
| 34 | `ic_status_loading` | ink | `loading spinner` | `status/ic_status_loading.svg` | |

## Color families (for picking among search results)

| Family | Hue | Used for |
| --- | --- | --- |
| oak | warm amber/brown | library identity, shelves, scanning, bookmarks |
| ink | deep blue | reading, navigation, search, views |
| sage | muted green | health-OK, available, success |
| clay | terracotta | warnings, unavailable, errors |
| plum | soft purple | AI / intelligence |
| slate | neutral grey-blue | settings, secondary chrome |

Pick the result whose dominant color is closest to the family; exact hue is
re-tinted in code where needed. Prefer one cohesive pack over perfect per-icon hue.
