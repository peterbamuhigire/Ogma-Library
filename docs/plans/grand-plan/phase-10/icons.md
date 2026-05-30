# Phase 10 — Icon Manifest

New icons for Search, Index Manager, Rebuild, and Filter surfaces.

Color family mapping:
- Search icons — `accent/oak` (warm oak amber): primary search action.
- Index Manager / status icons — `accent/sage` (muted green): health / success.
- Rebuild icon — `accent/clay` (terracotta): action that alters data.
- Failed / warning icons — `accent/clay`.
- Pending-OCR icon — `accent/plum` (soft plum): intelligence / pending AI.
- Filter / source-chip icons — `accent/slate` (neutral slate): secondary actions.

---

## Icon manifest table

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_search_global` | Search bar / toolbar | Open global search | Magnifier; `accent/oak` amber | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_search_clear` | Search bar — clear button | Clear search query | X / clear; `accent/slate` | 16/24 @1x–3x | ⬜ to procure |
| `ic_search_result_book` | Search result list item | Represents a book result | Open book with magnifier; `accent/oak` | 16/24 @1x–3x | ⬜ to procure |
| `ic_search_no_results` | Search — empty state | No results found | Magnifier with empty circle; `accent/slate` | 32/48 @1x–3x | ⬜ to procure |
| `ic_search_filter` | Search — filter toggle | Filter search results by source | Funnel; `accent/slate` | 16/24/32 @1x–3x | ⬜ to procure |
| `ic_filter_chip_page` | Search filter chip | Filter by page-text source | Document page; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_filter_chip_note` | Search filter chip | Filter by annotation-note source | Speech bubble; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_filter_chip_tag` | Search filter chip | Filter by tag source | Label/tag; `accent/oak` | 16/24 @1x–3x | ⬜ to procure |
| `ic_filter_chip_toc` | Search filter chip | Filter by TOC source | List with indent; `accent/ink` | 16/24 @1x–3x | ⬜ to procure |
| `ic_filter_chip_description` | Search filter chip | Filter by description source | Text paragraph; `accent/slate` | 16/24 @1x–3x | ⬜ to procure |
| `ic_index_manager` | Toolbar / sidebar toggle | Open Index Manager | Gauge / speedometer with book; `accent/sage` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_index_status_indexed` | Index Manager — per-book status | Book is fully indexed | Checkmark in circle; `accent/sage` | 16/24 @1x–3x | ⬜ to procure |
| `ic_index_status_indexing` | Index Manager — per-book status | Book is being indexed (in-progress) | Spinning circle / progress; `accent/oak` | 16/24 @1x–3x | ⬜ to procure |
| `ic_index_status_pending_ocr` | Index Manager — per-book status | Book needs OCR (scanned) | Eye with scan lines; `accent/plum` | 16/24 @1x–3x | ⬜ to procure |
| `ic_index_status_failed` | Index Manager — per-book status | Extraction failed | Exclamation in triangle; `accent/clay` | 16/24 @1x–3x | ⬜ to procure |
| `ic_index_rebuild` | Index Manager — rebuild button | Rebuild entire index | Circular arrows (refresh) over index; `accent/clay` | 16/24/32/48 @1x–3x | ⬜ to procure |
| `ic_index_rebuild_cancel` | Index Manager — cancel rebuild | Cancel in-progress rebuild | Stop square over arrows; `accent/slate` | 16/24/32 @1x–3x | ⬜ to procure |
| `ic_index_size` | Index Manager — size stat | Index size on disk | Database with ruler; `accent/slate` | 16/24 @1x–3x | ⬜ to procure |

---

## Accessible label keys (en + fr required)

| Icon key | Label resource key | en label | fr label |
| --- | --- | --- | --- |
| `ic_search_global` | `Search.Open` | "Search" | "Rechercher" |
| `ic_search_clear` | `Search.Clear` | "Clear search" | "Effacer la recherche" |
| `ic_search_result_book` | `Search.Result.Book` | "Book" | "Livre" |
| `ic_search_no_results` | `Search.NoResults` | "No results" | "Aucun résultat" |
| `ic_search_filter` | `Search.Filter` | "Filter results" | "Filtrer les résultats" |
| `ic_filter_chip_page` | `Search.Filter.Page` | "Page text" | "Texte de page" |
| `ic_filter_chip_note` | `Search.Filter.Note` | "Notes" | "Notes" |
| `ic_filter_chip_tag` | `Search.Filter.Tag` | "Tags" | "Étiquettes" |
| `ic_filter_chip_toc` | `Search.Filter.Toc` | "Table of contents" | "Table des matières" |
| `ic_filter_chip_description` | `Search.Filter.Description` | "Description" | "Description" |
| `ic_index_manager` | `IndexManager.Open` | "Index Manager" | "Gestionnaire d'index" |
| `ic_index_status_indexed` | `IndexManager.Status.Indexed` | "Indexed" | "Indexé" |
| `ic_index_status_indexing` | `IndexManager.Status.Indexing` | "Indexing…" | "Indexation…" |
| `ic_index_status_pending_ocr` | `IndexManager.Status.PendingOcr` | "Pending OCR" | "OCR en attente" |
| `ic_index_status_failed` | `IndexManager.Status.Failed` | "Extraction failed" | "Extraction échouée" |
| `ic_index_rebuild` | `IndexManager.Rebuild` | "Rebuild index" | "Reconstruire l'index" |
| `ic_index_rebuild_cancel` | `IndexManager.RebuildCancel` | "Cancel rebuild" | "Annuler la reconstruction" |
| `ic_index_size` | `IndexManager.Size` | "Index size" | "Taille de l'index" |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 10 Search & Indexing — Premium PNG Icon Procurement**

Phase 10 introduces **18 new icons** for the global search interface, source-
filter chips, and the Index Manager dashboard. Please purchase these icons from
the same premium vendor/set used in prior phases.

**Color families required:**
- `accent/oak` (warm oak amber) — primary search, in-progress indexing.
- `accent/sage` (muted green) — indexed status, Index Manager identity.
- `accent/clay` (terracotta) — rebuild action, failed status.
- `accent/plum` (soft plum) — pending OCR (intelligence / pending AI).
- `accent/ink` (deep ink blue) — source-filter chips (page, note, TOC).
- `accent/slate` (neutral slate) — secondary actions, size, clear, cancel.
- Light and dark variants for all icons.

**Size matrix:** 16/24/32/48 px @1x/2x/3x (same as prior phases).

**License:** same redistribution terms (Mac App Store + Windows Store + direct).

**Delivery:** `OgmaLibrary.App/Assets/icons/search/` and
`OgmaLibrary.App/Assets/icons/search/index-manager/`.

Shipping with `🟨` placeholder icons is a release blocker.
