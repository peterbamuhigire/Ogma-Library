# Phase 07 — Icon Manifest

Phase 07 is the metadata enrichment and collection health phase. It introduces two
primary UI surfaces (the enrichment review panel and the library health dashboard)
and supporting badges and status indicators. The palette expresses the "intelligence
and care" domain: plum for AI/enrichment, sage for health-OK, clay for warnings,
and oak-amber for primary actions.

---

## Icon manifest

### Enrichment actions & status

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_enrich` | "Enrich" button in book-detail (now active, was disabled in Phase 06) | Open the metadata enrichment panel for a book | Sparkle or globe with upward arrow; plum | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_enrich_batch` | "Enrich all / Batch enrich" toolbar action | Start batch enrichment for selected books | Sparkle over stacked pages; plum | 16/24 @1x-3x | ⬜ to procure |
| `ic_confidence_high` | Confidence indicator (≥ 0.8) | High-confidence proposed field | Filled checkmark or shield; sage green | 12/16 @1x-3x | ⬜ to procure |
| `ic_confidence_medium` | Confidence indicator (0.5–0.8) | Medium-confidence proposed field | Half-filled or dashed circle; warm gold | 12/16 @1x-3x | ⬜ to procure |
| `ic_confidence_low` | Confidence indicator (< 0.5) | Low-confidence proposed field | Outlined or dashed circle; clay | 12/16 @1x-3x | ⬜ to procure |
| `ic_accept_field` | Per-field Accept button | Accept a proposed metadata field | Checkmark in circle; sage | 16/24 @1x-3x | ⬜ to procure |
| `ic_reject_field` | Per-field Reject button | Reject a proposed metadata field | X in circle; clay | 16/24 @1x-3x | ⬜ to procure |
| `ic_accept_all` | "Accept all above threshold" button | Accept all high-confidence proposals | Double-checkmark or checkmark with threshold bar; sage | 16/24 @1x-3x | ⬜ to procure |

### Provider source badges

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_provider_google_books` | Provenance badge on enriched fields | Field sourced from Google Books | Stylized "G" or book with Google colors; keep subtle | 12/16 @1x-3x | ⬜ to procure |
| `ic_provider_open_library` | Provenance badge on enriched fields | Field sourced from Open Library | Stylized open book; oak-amber | 12/16 @1x-3x | ⬜ to procure |
| `ic_provider_user_override` | Provenance badge on manually-edited fields | Field manually overridden by user | Pencil or person icon; slate | 12/16 @1x-3x | ⬜ to procure |

### ISBN icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_isbn` | ISBN display in book-detail; health dashboard "Missing ISBNs" section | The ISBN identifier | Barcode or "ISBN" label icon; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_isbn_missing` | Health dashboard: Missing ISBN list item badge | Book has no ISBN | Barcode with X or question mark; clay | 16/24 @1x-3x | ⬜ to procure |
| `ic_isbn_detected` | ISBN detection success indicator | ISBN was detected from PDF | Barcode with checkmark; sage | 16/24 @1x-3x | ⬜ to procure |

### PDF write-back icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_writeback` | "Write to PDF" button in enrichment panel | Push accepted metadata back to PDF file | Document with upload/write arrow; oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_writeback_diff` | Field diff dialog title bar | Show before/after field changes | Document with two columns; slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_writeback_backup` | Backup indicator (visible in diff dialog) | Backup created before write | Shield with document; sage | 16/24 @1x-3x | ⬜ to procure |
| `ic_writeback_success` | Write-back success toast | PDF metadata written successfully | Document with checkmark; sage | 16/24 @1x-3x | ⬜ to procure |
| `ic_writeback_failed` | Write-back failure toast | PDF write failed; original restored | Document with X; clay | 16/24 @1x-3x | ⬜ to procure |

### Health dashboard icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_health_dashboard` | Health dashboard sidebar nav entry; panel title | Open the library health dashboard | Heart + book or shield; plum/oak-amber | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_duplicate` | Duplicates section tab badge and item | Duplicate books detected | Two overlapping documents; clay | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_missing_cover` | Missing covers section tab badge and item | Book has no cover image | Book with placeholder image icon; clay | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_missing_isbn` | Missing ISBNs section tab badge | No validated ISBN | See `ic_isbn_missing` above (reuse) | — | — |
| `ic_quality_score` | Quality score indicator; quality filter chip; health tooltip | Metadata quality score (0-100%) | Gauge or fill-meter; color varies (sage ≥ 80%, gold 50-80%, clay < 50%) | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_health_all_clear` | Health dashboard empty state (no issues) | All health checks passed | Shield with checkmark; sage | 48/96 @1x-3x | ⬜ to procure |

### Batch enrichment icons

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_batch_pause` | Batch enrichment Pause button | Pause batch enrichment | Pause (two vertical bars); slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_batch_resume` | Batch enrichment Resume button | Resume paused batch | Play triangle; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_batch_cancel` | Batch enrichment Cancel button | Cancel and discard batch | X-circle; clay | 16/24 @1x-3x | ⬜ to procure |

**Total new icons in this phase: ~25.**

---

## Accessible labels (en + fr required)

All 25 icon keys must have localized `AutomationProperties.Name` / `ToolTip` in
both `en.resx` and `fr.resx` before the corresponding control ships. The confidence
indicators must convey their meaning via both color AND a numeric percentage label
(color must not be the sole carrier — WCAG 2.2, NFR-PROD-008).

---

## Owner procurement request

**To: Peter Bamuhigire**

Phase 07 needs approximately **25 premium PNG icons** for the metadata enrichment panel,
provider source badges, PDF write-back flow, quality score indicators, and the library
health dashboard.

**Style specification (from `ICON-SYSTEM.md`):**
- Same cohesive premium family as Phase 06.
- Color assignments:
  - **Plum**: enrichment, AI intelligence surfaces (`ic_enrich`, `ic_enrich_batch`,
    `ic_health_dashboard`, confidence medium)
  - **Sage green**: accept, success, health-OK, backup confirmed, high confidence
  - **Clay/terracotta**: reject, failure, missing fields, low confidence, duplicates
  - **Oak-amber**: primary write/action (`ic_writeback`, `ic_isbn`, `ic_batch_resume`,
    providers)
  - **Slate**: secondary/neutral (diff, cancel, user-override, pause)
  - **Warm gold**: medium confidence indicator
- The quality score icon (`ic_quality_score`) should be a gauge or fill-meter that
  maps naturally to a percentage — works well in three states (high/medium/low) via
  fill amount, not just color.
- The confidence indicators (`ic_confidence_high/medium/low`) must be visually
  distinguishable without relying solely on color (different shapes or fill levels).
- `ic_health_all_clear` should be a welcoming, encouraging empty-state icon
  (48/96 px) — suggests the library is well-cared-for.

**Sizes required:**
- Standard: **16, 24, 32 px** @1x, @2x, @3x.
- Small badge: **12, 16 px** @1x, @2x, @3x (confidence indicators, provider badges).
- Empty-state large: **48, 96 px** @1x, @2x, @3x (`ic_health_all_clear`).

**License requirement:** same as Phase 06 (Mac App Store + Windows Store redistribution).

**Delivery path:**
- `OgmaLibrary.App/Assets/icons/metadata/` — enrichment, ISBN, write-back, confidence.
- `OgmaLibrary.App/Assets/icons/health/` — health dashboard, duplicate, quality score.
- `OgmaLibrary.App/Assets/icons/empty-states/ic_health_all_clear*.png`.

**Timing:** Icons for the enrichment panel (WP4) and health dashboard (WP9) are
needed before those WPs are UI-finalized. Confidence badges and provider badges are
needed for WP5 (provenance display in book-detail). All are release-blockers;
none block coding with placeholders.
