# Phase 18 catalogue-shell localization evidence

Date: 2026-09-04

Catalogue shell filter headings, title/author watermarks, clear-filter action,
and the filtered result count now bind to localized English/French resources.
The existing culture-switch mechanism therefore updates this copy with the
rest of the shell; no user-entered filter value is persisted as translated
text.

Verification:

- Isolated Release application build: passed, 0 warnings, 0 errors.
- `SkeletonRenderTests.MainWindow_CultureSwitch_UpdatesTitle_WithoutMissingResources`:
  passed.

The catalogue-shell copy-extraction sub-gate is CLOSED locally. Full app-wide
copy inventory, pseudo-locale coverage, theme/density persistence, command
palette execution, contrast snapshots, and physical assistive-technology
evidence remain open.
