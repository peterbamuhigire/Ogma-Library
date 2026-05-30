# Phase 05 — Icon Manifest

Phase 05 introduces a modest set of UI icons for the scan progress panel and the
V1 scan health report. All icons follow the colorful premium style defined in
`ICON-SYSTEM.md` (warm library palette: oak-amber for primary actions, clay for
warnings, sage for success).

---

## Icon manifest

| Icon key | Used on | Meaning | Style / color note | Sizes (px) | Status |
| --- | --- | --- | --- | --- | --- |
| `ic_scan_library` | Library toolbar button "Select / Rescan Library" | Initiate a library scan or select a root folder | Outlined book+magnifying-glass duotone; oak-amber accent | 16/24/32/48 @1x-3x | ⬜ to procure |
| `ic_scan_progress` | Scan progress status-bar chip (active state) | Scanning is in progress | Animated-ready spinner or pulsing circle; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_scan_complete` | Scan progress chip (complete state) | Scan finished successfully | Checkmark-in-circle; sage green | 16/24 @1x-3x | ⬜ to procure |
| `ic_scan_error` | Scan progress failure-count chip; failure badge | One or more files failed to import | Warning triangle or exclamation-circle; clay/terracotta | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_scan_cancel` | Cancel button in scan progress panel | Stop the current scan | X-circle or stop-square; neutral slate | 16/24 @1x-3x | ⬜ to procure |
| `ic_health_warning` | Health report panel tab badge; individual item | File has a health issue requiring attention | Triangle with exclamation; clay/terracotta | 16/24/32 @1x-3x | ⬜ to procure |
| `ic_health_ok` | Health report panel (all-clear state) | No health issues | Shield or checkmark; sage green | 24/48 @1x-3x | ⬜ to procure |
| `ic_retry` | Retry button in health report; per-item and retry-all | Re-queue a failed job for reprocessing | Circular arrows; oak-amber | 16/24 @1x-3x | ⬜ to procure |
| `ic_file_missing` | Health report "unavailable files" list item | File is not found on disk | File with X mark; clay/terracotta | 16/24 @1x-3x | ⬜ to procure |

**Total new icons in this phase: 9.**

---

## Accessible labels (en + fr required before any icon ships)

| Icon key | `en` label key | `fr` label key |
| --- | --- | --- |
| `ic_scan_library` | `Scan.Action.ScanLibrary` | `Scan.Action.ScanLibrary` |
| `ic_scan_progress` | `Scan.Status.Scanning` | `Scan.Status.Scanning` |
| `ic_scan_complete` | `Scan.Status.Complete` | `Scan.Status.Complete` |
| `ic_scan_error` | `Scan.Status.Error` | `Scan.Status.Error` |
| `ic_scan_cancel` | `Scan.Action.Cancel` | `Scan.Action.Cancel` |
| `ic_health_warning` | `Health.Status.Warning` | `Health.Status.Warning` |
| `ic_health_ok` | `Health.Status.OK` | `Health.Status.OK` |
| `ic_retry` | `Health.Action.Retry` | `Health.Action.Retry` |
| `ic_file_missing` | `Health.Status.FileMissing` | `Health.Status.FileMissing` |

All label resource keys must exist in both `en.resx` and `fr.resx` before the
corresponding control ships. Missing keys fail the `IconCatalog` build check
(Phase 03).

---

## Owner procurement request

**To: Peter Bamuhigire**

Phase 05 needs **9 premium PNG icons** for the scan progress panel and health
report. Please procure icons matching the following specification:

**Style specification (from `ICON-SYSTEM.md`):**
- Colorful duotone or flat-color style; warm library aesthetic.
- Primary accent: **oak-amber** (primary actions: scan, retry).
- Warning accent: **clay/terracotta** (errors, missing files, health warnings).
- Success accent: **sage green** (scan complete, health OK).
- Secondary: **slate** (cancel, neutral actions).
- Corner radius and stroke weight consistent with the Phase 03 chosen icon family.
- Light **and** dark variants if the vendor provides them.

**Sizes required:**
- Base: **16, 24, 32, 48 px**.
- Density: **@1x, @2x, @3x** for both Windows HiDPI and macOS Retina.
- Total files per icon: up to 12 PNG files (4 sizes × 3 densities).

**License requirement:**
- Must permit redistribution inside a signed desktop app sold on the
  **Mac App Store** and **Microsoft (Windows) Store**.

**Delivery path:**
- Drop PNG files into `OgmaLibrary.App/Assets/icons/library/` following the naming
  convention `<icon_key>@<N>x_<size>.png` (e.g., `ic_scan_library@2x_24.png`).
- Update this `icons.md` status column from `⬜ to procure` → `🟨 placeholder in use`
  when placeholders are wired, and → `✅ premium PNG wired` when final assets are in.

**Blocking:** the scan progress panel (WP9) and health report (WP10) will use
neutral placeholder icons during build. Final premium icons are required before the
MVP release milestone. Procurement is not on the critical path for coding, but it
**is** on the critical path for the release.
