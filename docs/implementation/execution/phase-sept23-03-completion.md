# Sept-23 Phase 03 completion: shell emergency fixes

Status: **COMPLETE, with one hand-off** (2026-09-25). Rollback point: `31ab521`.
Plan: [phase-03](../../plans/sept-23-kaizen/phases/phase-03-shell-emergency-fixes.md).
Commit: `5c3b9b2`. The real-window proof used the prototype UIA driver because the Phase 01
harness is not built yet.

## Tasks

| Task | Result |
|---|---|
| T03.1 Relocate the pager (K10) | The pager is its own row (`Grid.Row="5"` of the content grid), shown only when there is more than one page (`IsPagerVisible`). The reconciliation panel stays an explicit top-right overlay. |
| T03.2 Layout regression test | `CatalogueShellLayout_*` checks that nothing painted later with an opaque background covers the empty-state heading or the book cards, at 1280×800 and 1920×1080. **All 4 fail on the original nesting** ("ListBoxItem … is covered by: Border") and pass after the fix. |
| T03.3 Empty and filtered-empty states | "No books match these filters" with *Clear filters* (`IsFilteredEmpty`); en and fr. |
| T03.4 Truthful status (K13) | Idle shows "{n} books in your library". Scanning shows "Scanning: {p} of {d} files", clamped. Asset generation shows "Preparing covers and search index…" with no counters. Unit tests cover each state, including the 59-of-17 overflow. |
| T03.5 Toolbar stopgap (K11) | The toolbar wraps instead of scrolling. Library actions come first. Split view, Sharing and Relocation reviews move to a *More* menu. Marked as a stopgap; Phase 07 replaces it. |
| Extra | New `Brush.Accent.OnAccent` token; 16 hard-coded `Foreground="White"` in the shell replaced. |
| T03.6 Owner check | Screenshots below. The owner walkthrough happens at the end of Wave A. |

## Measured results (real window, Windows 11, isolated data dir, synthetic 17-file corpus)

| Check | Before (`0ad3c0c`) | After |
|---|---|---|
| First-run heading and call to action visible | No (covered) | Yes, at 860, 1280 and 1920 px ([g1-860](evidence/sept-23-kaizen/phase-03/g1-860.png), [g1-1280](evidence/sept-23-kaizen/phase-03/g1-1280.png), [g1-1920](evidence/sept-23-kaizen/phase-03/g1-1920.png)) |
| *Choose library folder* / *Open PDF* reachable at the default size | No (x≈1320–1455 in a 1180 px window) | Yes (first in the toolbar; wraps at narrow widths) |
| Catalogue visible after the scan | No ([before](evidence/sept-23-kaizen/phase-03/g2-before-1600.png)) | 17 cards painted ([after](evidence/sept-23-kaizen/phase-03/g2-after-scan-1920.png)) |
| Status text during a scan | "59 / 17 files", "Scanned 77 files" | Never exceeds discovered files; sampled every ~4 s for 90 s |
| Idle status with books | "Ready — choose a library folder to begin" | "17 books in your library" (unit-tested) |
| Fast suite | 1,149 passed | 1,159 passed (UI 163 → 173) |
| Format / analyzers / build | green | green, 0 warnings |

The full folder-picker journey (empty-state button → native dialog → path → Select Folder) now
runs from `Invoke-OgmaUia.ps1 -Action pick`.

## Hand-offs and NOT ASSESSED

| Item | Owner |
|---|---|
| The status stays on "Preparing covers and search index…" for more than 90 s, because embedding jobs retry against an absent provider (K25) | Phase 06 (retry policy, "waiting for provider" state) |
| Covers still show placeholders (K20) and first-refresh cards show filename titles with "Unknown author" until metadata jobs finish | Phase 05 (cover root), Phase 06 (progressive refresh) |
| Dark theme rendering of the new states | Phase 09 |
| macOS rendering | Phase 27 |
