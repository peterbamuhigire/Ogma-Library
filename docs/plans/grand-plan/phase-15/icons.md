# Phase 15 — Icon Manifest

Icons for the OCR pipeline, password-protected PDF, split view, and batch
enrichment surfaces. Category paths: `OgmaLibrary.App/Assets/icons/reader/`
and `workers/`.

---

## Icon table

| Icon key | Used on | Meaning | Style / color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_ocr_run` | Book-detail settings; Health Dashboard "Run OCR" button | Trigger OCR for a scanned PDF | Document with scan lines and a spark; `accent/oak` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_ocr_active` | OCR job status chip (running) | OCR in progress | Document with a rotating arrow; `accent/plum` animated | 16/24 @1x-3x | ⬜ to procure |
| `ic_ocr_paused` | OCR job status chip (paused) | OCR paused | Document with a pause symbol; `accent/clay` | 16/24 @1x-3x | ⬜ to procure |
| `ic_ocr_completed` | OCR job status chip (done) | OCR completed | Document with a check; `accent/sage` | 16/24 @1x-3x | ⬜ to procure |
| `ic_ocr_failed` | OCR job status chip (failed) | OCR failed / error | Document with an X; `accent/clay` | 16/24 @1x-3x | ⬜ to procure |
| `ic_ocr_derived` | Book-detail header badge | Book text was OCR-derived (not native) | Eye/scanner badge on a document; `accent/ink` | 16/24 @1x-3x | ⬜ to procure |
| `ic_reader_locked` | Book-detail header; catalogue badge | PDF is password-protected | Padlock (closed); `accent/clay` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_reader_lock_open` | Unlock action; "Forget password" button | PDF unlocked / clear password | Padlock (open); `accent/sage` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_reader_split` | Reader toolbar; split-view menu item | Split view (V2 scaffold) | Two side-by-side document panels; `accent/ink` | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_batch_enrichment` | Health Dashboard batch enrichment section header | Batch metadata enrichment | Stacked books with arrows cycling; `accent/oak` | 24/32/48 @1x-3x | ⬜ to procure |
| `ic_batch_pause` | Health Dashboard batch enrichment pause button | Pause batch enrichment | Pause symbol; `accent/clay` | 16/24 @1x-3x | ⬜ to procure |
| `ic_batch_resume` | Health Dashboard batch enrichment resume button | Resume batch enrichment | Play/resume symbol; `accent/sage` | 16/24 @1x-3x | ⬜ to procure |
| `ic_batch_export_failed` | Failed-books CSV export button | Export list of failed enrichments | Download arrow + warning; `accent/clay` | 16/24 @1x-3x | ⬜ to procure |
| `ic_smartshelf_perf` | Index Manager "Smart Shelf Query Stats" panel | Smart-shelf query performance | Funnel with a lightning bolt; `accent/ink` | 16/24 @1x-3x | ⬜ to procure |

---

## Accessible label resource keys

| Icon key | `en` label | `fr` label |
| --- | --- | --- |
| `ic_ocr_run` | Run OCR | Lancer l'OCR |
| `ic_ocr_active` | OCR in progress | OCR en cours |
| `ic_ocr_paused` | OCR paused | OCR en pause |
| `ic_ocr_completed` | OCR complete | OCR terminé |
| `ic_ocr_failed` | OCR failed | OCR échoué |
| `ic_ocr_derived` | OCR-derived text | Texte issu de l'OCR |
| `ic_reader_locked` | Password protected | Protégé par mot de passe |
| `ic_reader_lock_open` | Unlocked | Déverrouillé |
| `ic_reader_split` | Split view | Vue divisée |
| `ic_batch_enrichment` | Batch enrichment | Enrichissement par lot |
| `ic_batch_pause` | Pause enrichment | Mettre en pause |
| `ic_batch_resume` | Resume enrichment | Reprendre |
| `ic_batch_export_failed` | Export failed books | Exporter les livres en échec |
| `ic_smartshelf_perf` | Smart shelf performance | Performance des étagères |

---

## Owner procurement request

**To: Peter Bamuhigire**
**Re: Phase 15 icon set — OCR, Advanced Reader & Power Tools**

Please procure a **14-icon set** for the OCR pipeline, password-protected PDF,
split-view scaffold, batch enrichment, and smart-shelf surfaces.

**Style requirements:** same Phase 03 icon family (single vendor, consistent grid).

Key colors for this phase:
- `accent/oak` — OCR run trigger; batch enrichment (primary workflow actions).
- `accent/plum` — OCR active/in-progress state.
- `accent/sage` — OCR completed; lock-open; batch resume.
- `accent/clay` — OCR paused/failed; locked PDF; batch pause; export failed.
- `accent/ink` — OCR-derived badge; split view; smart-shelf perf.

**Sizes and density:** 16/24/32/48 @1x/2x/3x; macOS Retina.

**License:** same redistribution rights (Windows Store, Mac App Store, GitHub).

**Icon keys (14):**
`ic_ocr_run`, `ic_ocr_active`, `ic_ocr_paused`, `ic_ocr_completed`,
`ic_ocr_failed`, `ic_ocr_derived`, `ic_reader_locked`, `ic_reader_lock_open`,
`ic_reader_split`, `ic_batch_enrichment`, `ic_batch_pause`, `ic_batch_resume`,
`ic_batch_export_failed`, `ic_smartshelf_perf`.

**Note on `ic_ocr_active`:** If the premium icon vendor provides an animated
variant (e.g., a Lottie JSON or CSS animation), that is preferred for the "in
progress" state. A static PNG is also acceptable. Confirm with the owner at
procurement time.

Placeholders in use during build (`🟨`). Premium PNGs required before shipping.
