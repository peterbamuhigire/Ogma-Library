# Phase 17 real-window evidence (Phase CO slice 5, 2026-11-19)

Journey `ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText`, inside `Use-DesktopLock.ps1`, with a
fresh temporary data folder and the synthetic corpus. Source: the `ocr.badges` and `ocr.searches`
records in each run's timings JSON.

| Run | Theme | 1280x800 | 1920x1080 |
|---|---|---|---|
| `co-all-2` (slice 2) | Light | PASS (baseline cleared) | PASS (baseline cleared) |
| `co-all-3` (slice 2) | Light | PASS (baseline cleared) | PASS (baseline cleared) |
| `co-nav-1` | Light, Dark | PASS (baseline cleared) | PASS (baseline cleared) |
| `co-nav-2` (baseline entry removed) | Light, Dark | PASS | PASS |

Every `co-nav-1` and `co-nav-2` row (MEASURED):

- *Scanned Pamphlet* badges went from `ImageOnly` to `OcrInProgress` to `OcrText`. *Scanned Handout*
  reached `OcrText`.
- `amber library lantern` listed *Scanned Pamphlet* in the top three, and `rareocrkeyword` listed
  *Scanned Handout*. Both hits carried the "From OCR text" label under the catalogue title, never
  "Untitled" (K97 and K99 resolved).

The 1280x800 size has 6 consecutive passes, and 1920x1080 has 7. That is more than the
3-consecutive rule needs, so the `baseline.json` entry was removed in `912977c6`.

NOT ASSESSED: OCR language packs other than the bundled one, and timing on the declared
reference hardware.
