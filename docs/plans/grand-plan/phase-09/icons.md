# Phase 09 — Icon Manifest

New icons for Annotations, Bookmarks, Annotation Layers, Citation Cards,
and Reading Memory surfaces.

Procurement status: premium SVG assets delivered and copied into the Phase 09
key-named reader icon paths as of 2026-05-31.

Color family mapping:
- Annotation/highlight icons — `accent/oak` (warm oak amber): library identity,
  primary annotation action.
- Note icons — `accent/ink` (deep ink blue): reading & text.
- Bookmark icons — `accent/oak` amber.
- Layer icons — `accent/plum` (soft plum): organizational / intelligence surface.
- Citation icons — `accent/sage` (muted green): success, export, citation.
- Reading-memory icons — `accent/plum`: reflective / intelligence surface.
- Delete/warning icons — `accent/clay` (terracotta).

---

## Icon manifest table

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_annotation_highlight` | Context menu / toolbar | Create a highlight | Marker / highlighter pen; `accent/oak` amber | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_annotation_highlight_color` | Highlight color picker | Choose highlight color | Color swatch circle with checkmark; multi-color | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_annotation_note` | Context menu / toolbar | Add an inline note | Speech bubble with pencil; `accent/ink` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_annotation_note_anchor` | Overlay — note icon on page | Indicates a note is anchored here | Small speech bubble; `accent/ink` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_annotation_delete` | Context menu / annotation options | Delete annotation | Trash with annotation background; `accent/clay` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_bookmark_add` | Toolbar / bookmark button | Add bookmark to current page | Ribbon bookmark with `+`; `accent/oak` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_bookmark_remove` | Toolbar / bookmark button (active state) | Remove bookmark from current page | Ribbon bookmark with `−`; `accent/clay` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_bookmark_panel` | Toolbar / sidebar toggle | Open bookmark panel | Stack of ribbons; `accent/oak` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_bookmark_item` | Bookmark panel list item | Represents a single bookmark | Single ribbon; `accent/oak` light | 16/24 @1x–3x | Delivered premium SVG |
| `ic_bookmark_rename` | Bookmark context menu | Rename a bookmark | Pencil over ribbon; `accent/ink` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_layer_panel` | Toolbar / sidebar toggle | Open annotation layer panel | Stacked layers icon; `accent/plum` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_layer_add` | Layer panel "+" button | Create a new annotation layer | Layer stack with `+`; `accent/plum` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_layer_visible` | Layer panel visibility toggle | Layer is visible | Open eye; `accent/plum` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_layer_hidden` | Layer panel visibility toggle (off state) | Layer is hidden | Closed / crossed eye; `accent/slate` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_layer_merge` | Layer context menu | Merge this layer into another | Two arrows joining; `accent/plum` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_layer_delete` | Layer context menu | Delete layer | Trash with layer; `accent/clay` | 16/24 @1x–3x | Delivered premium SVG |
| `ic_citation_capture` | Context menu / keyboard shortcut indicator | Capture citation card | Quote marks over document; `accent/sage` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_citation_copy` | Citation card — copy button | Copy citation to clipboard | Clipboard + quotation mark; `accent/sage` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_citation_export` | Citation card — export button | Export citation to file | Arrow-up-from-document; `accent/sage` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_reading_memory` | Sidebar toggle / book-detail card | Open reading memory journal | Open book with lightbulb; `accent/plum` | 16/24/32/48 @1x–3x | Delivered premium SVG |
| `ic_reading_memory_disposition` | Reading memory — disposition field | Disposition/rating widget | Five stars or five dots; `accent/oak` amber | 16/24 @1x–3x | Delivered premium SVG |
| `ic_annotation_panel` | Toolbar / sidebar toggle | Open full annotation list panel | Text lines with highlight bar; `accent/oak` | 16/24/32/48 @1x–3x | Delivered premium SVG |

---

## Accessible label keys (en + fr required)

| Icon key | Label resource key | en label | fr label |
| --- | --- | --- | --- |
| `ic_annotation_highlight` | `Annotation.Highlight.Create` | "Highlight" | "Surligner" |
| `ic_annotation_highlight_color` | `Annotation.Highlight.Color` | "Choose highlight color" | "Choisir la couleur de surlignage" |
| `ic_annotation_note` | `Annotation.Note.Create` | "Add note" | "Ajouter une note" |
| `ic_annotation_note_anchor` | `Annotation.Note.Anchor` | "Note" | "Note" |
| `ic_annotation_delete` | `Annotation.Delete` | "Delete annotation" | "Supprimer l'annotation" |
| `ic_bookmark_add` | `Bookmark.Add` | "Add bookmark" | "Ajouter un signet" |
| `ic_bookmark_remove` | `Bookmark.Remove` | "Remove bookmark" | "Supprimer le signet" |
| `ic_bookmark_panel` | `Bookmark.Panel` | "Bookmarks" | "Signets" |
| `ic_bookmark_item` | `Bookmark.Item` | "Bookmark" | "Signet" |
| `ic_bookmark_rename` | `Bookmark.Rename` | "Rename bookmark" | "Renommer le signet" |
| `ic_layer_panel` | `Layer.Panel` | "Annotation layers" | "Couches d'annotation" |
| `ic_layer_add` | `Layer.Add` | "New layer" | "Nouvelle couche" |
| `ic_layer_visible` | `Layer.Visible` | "Layer visible" | "Couche visible" |
| `ic_layer_hidden` | `Layer.Hidden` | "Layer hidden" | "Couche masquée" |
| `ic_layer_merge` | `Layer.Merge` | "Merge layer" | "Fusionner la couche" |
| `ic_layer_delete` | `Layer.Delete` | "Delete layer" | "Supprimer la couche" |
| `ic_citation_capture` | `Citation.Capture` | "Capture citation" | "Capturer la citation" |
| `ic_citation_copy` | `Citation.Copy` | "Copy citation" | "Copier la citation" |
| `ic_citation_export` | `Citation.Export` | "Export citation" | "Exporter la citation" |
| `ic_reading_memory` | `ReadingMemory.Open` | "Reading memory" | "Mémoire de lecture" |
| `ic_reading_memory_disposition` | `ReadingMemory.Disposition` | "Disposition" | "Appréciation" |
| `ic_annotation_panel` | `Annotation.Panel` | "Annotations" | "Annotations" |

---

## Delivered icon assets

**To: Peter Bamuhigire**
**Re: Phase 09 Annotations & Reading Memory — Premium SVG Icon Delivery**

Phase 09 introduces **22 new icons** for the annotation, bookmark, layer,
citation, and reading-memory surfaces. Premium SVG assets have been supplied
and copied into the key-named runtime paths. If PNG variants are later required
for packaging, generate them from these delivered SVG sources using the same
size matrix.

**Color families required:**
- `accent/oak` (warm oak amber) — highlight, bookmark, annotation actions.
- `accent/ink` (deep ink blue) — note, ink-related annotation actions.
- `accent/plum` (soft plum) — layer panel, reading memory (intelligence surface).
- `accent/sage` (muted green) — citation capture, copy, export (success/export).
- `accent/clay` (terracotta) — delete/warning variants.
- `accent/slate` (neutral slate) — hidden/inactive state variants.
- Light and dark variants for all icons.

**Size matrix:** same as Phase 08 — 16/24/32/48 px @1x/2x/3x.

**License:** same redistribution terms as Phase 08 (Mac App Store + Windows
Store + direct distribution permitted).

**Delivery:** SVG files named `<icon_key>.svg` in
`OgmaLibrary.App/Assets/icons/reader/annotations/` and
`OgmaLibrary.App/Assets/icons/reader/memory/`.

Shipping with placeholder icons is no longer a Phase 09 blocker.
