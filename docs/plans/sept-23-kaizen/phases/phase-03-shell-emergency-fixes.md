# Phase 03: Shell emergency fixes: visible catalogue and truthful status

## 1. Header

| Field | Value |
|---|---|
| Wave | A: Stabilise |
| Size | 2–3 engineering days |
| Depends on | Phase 01 (harness), Phase 02 (error surface and logging) |
| Owner decisions | none |
| Primary defects | K10 (pager overlay hides the catalogue), K11 (unreachable primary actions, stopgap only), K13 (false status text and counters) |
| Requirement IDs | UX-001 (first-run flow), UX-002 (loading, empty and error states), CAT-001 (grid/list/directory views) |

## 2. Why this phase exists

This is the single most visible failure in the product. Since commit `822e760` (4 September
2026, "persist catalogue view state and add paging"), the pager `Border` has been nested inside
the `Grid.Row="4"` content grid of `CatalogueShellView.axaml` instead of sitting beside it. That
grid has no row definitions, so `Grid.Row="5"` collapses into its only cell, and the pager's
opaque `Brush.Surface.Footer` background covers the grid, list and directory views and the
empty state (K10). The measured result: a new user sees a blank canvas with *Previous page /
Page 1 of 1 (0 books) / Next page* floating mid-screen (`evidence/screens/j1-first-run.png`).
After a scan, the user sees "17 books" in the toolbar but an empty page (`j2-scanned.png`).

The fix was verified during the audit: moving the pager block out of the cell
(`evidence/tools/root-cause-pager-overlay.patch`) made the catalogue render (`j3-grid.png`).

Two other defects compound the confusion:
- The status bar says "Ready — choose a library folder to begin" even with 17 books loaded. Its
  counters read "59 / 17 files" and then "Scanned 77 files", because scan progress and
  asset-job progress share one counter (K13).
- At the default 1180×760 window, *Choose library folder* and *Open PDF* sit beyond the right
  edge of the one-row toolbar (K11).

This phase restores a usable first screen quickly. The proper navigation redesign follows in
Phase 07.

## 3. Objectives and exit criteria

1. The catalogue (grid, list, directory) and the empty state are visibly painted and
   unobstructed at 1280×800 and 1920×1080. G1 and G2 pass their visibility assertions.
2. The pager sits in its own row at the bottom of the content area, and hides when there is only
   one page.
3. The status bar never contradicts the catalogue: with books present and no activity it shows
   the book count and last-scan time; during a scan it shows files discovered and processed
   without overflow; after the scan it shows a completion summary.
4. At the minimum window width (860 px, `DesktopShellWindow.axaml:11`), *Choose library folder*
   and *Open PDF* are reachable without horizontal scrolling. This is a stopgap until Phase 07.
5. A layout regression test fails if any sibling covers the catalogue region.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`: Grid layout and visual-tree testing.
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`: empty-state content and call to action.
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\onboarding-and-first-run-design\SKILL.md`: the first-run experience.
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`: status and progress wording.
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\click-path-audit\SKILL.md`: verifying the Choose-folder handler chain.

## 5. Scope in / scope out

**In:** the pager relocation, a layout regression test, empty-state polish (heading, body,
two actions, a supported-formats hint), status text rules, a minimal progress-counter split in
the view model (the pipeline split is in Phase 06), and a toolbar stopgap that moves library
actions into a visible primary group.

**Out:** the navigation rail and routed panels (Phase 07); covers (Phase 05); visual restyling
(Phase 09).

## 6. Work breakdown

**T03.1: Relocate the pager (K10).** In `src/OgmaLibrary.App/Views/Catalogue/CatalogueShellView.axaml`,
move the block that starts at the comment `Bounded catalogue paging keeps large result sets
responsive` (line ~450, `<Border Grid.Row="5" IsVisible="{Binding IsCatalogueActive}" ...>`)
out of `<Grid Grid.Row="4">` (line 392) so that it is a direct child of the content grid
declared with `RowDefinitions="Auto,Auto,Auto,Auto,*,Auto"`. Apply the audit patch
`evidence/tools/root-cause-pager-overlay.patch` as the starting point. Also check the
reconciliation panel `Border Grid.Row="3" Grid.RowSpan="3"`, which currently sits inside the
same cell. Place it deliberately (as an overlay with explicit alignment) and document why.
Add `IsVisible` = `IsCatalogueActive && Catalogue.PageCount > 1`.
*Acceptance:* `j3-grid.png` layout at both sizes; G1 and G2 visibility assertions pass.

**T03.2: Layout regression test.** In `tests/OgmaLibrary.Tests.Ui`, add a headless test that
renders `CatalogueShellView` with 17 fake books and asserts:
- the grid `ListBox` bounds intersect no other visible element with a non-transparent
  background that comes later in z-order (walk `GetVisualDescendants()` and compare
  `TransformedBounds`);
- the pager's bounds lie below the catalogue's bounds.

Add the same check for the empty state. The test must fail when the patch is reverted.
*Acceptance:* the test fails on `0ad3c0c` and passes after T03.1 (record both runs).

**T03.3: Empty state (UX-001).** Keep the existing heading and body
(`MainWindow.EmptyState.*`). Ensure the two buttons have AutomationIds
(`Shell.Empty.ChooseFolder`, `Shell.Empty.OpenPdf`) and a clear primary/secondary hierarchy.
Add one line of help: "Ogma reads PDF files. Your files stay where they are." (en/fr). Use the
second sentence only once D-04 (Phase 05) stops writing derived files into the library folder;
until then, use "Ogma reads PDF files and never moves or renames them."
Show the empty state only when the library truly has no books. When a filter hides every book,
show a separate "No books match these filters" state with *Clear filters*. `IsEmpty` is
currently `_allItems.Count == 0 && !_isLoading` (`CatalogueViewModel.cs:253`); add
`IsFilteredEmpty`.
*Acceptance:* E2E with a persisted filter that matches nothing shows the no-match state.

**T03.4: Truthful status (K13).** Rewrite the idle branch of `MainShellViewModel.StatusText`
(`ViewModels/Catalogue/MainShellViewModel.cs:441-473`). The fallback currently always returns
`MainWindow.Status.Ready` ("Ready — choose a library folder to begin",
`InMemoryLocalizationService.cs:412`). New rules:
- no root and no books: the existing Ready text;
- books present and idle: "{count} books · last scanned {relative time}";
- scanning: "Scanning: {processed} of {discovered} files", with `processed` clamped to
  `discovered`;
- generating covers and indexes: "Preparing books: {done} of {total} tasks" (read from the job
  counts, not the file counters);
- completed with failures: "{n} files need attention" with an action that opens the
  needs-attention filter (Phase 05 provides the filter; until then, the Activity Centre).

In `OnProgressChanged` (line 1157), stop showing file counters during `GeneratingAssets` until
Phase 06 separates the counters at the source.
*Acceptance:* unit tests for each state; E2E G2 asserts no "N / M" text with N > M at any
sample point.

**T03.5: Toolbar stopgap (K11).** In the toolbar grid
(`CatalogueShellView.axaml:112`, 14 auto columns), move *Choose library folder* (column 9) and
*Open PDF* (column 10) into the first columns. Group the view toggles (grid, list, directory,
3D) into one segmented control. Move the rarely used actions (*Relocation reviews*, *Sharing*,
*Split view*) into a "More" overflow menu. Label this change `stopgap:` in the commit message.
Phase 07 replaces the toolbar.
*Acceptance:* `AssertReachable` on both library actions at 860×560, 1280×800 and 1920×1080.

**T03.6: Owner check.** Record before and after screenshots of G1 and G2 at both sizes, and
show the owner the first screen and the post-scan catalogue.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Catalogue invisible (K10) | Pager nested in content cell | Move pager to its own row | Content visible | 0 painted cards → all cards painted | G1/G2 screenshots | Reconciliation overlay misplaced | Revert single commit |
| No regression guard (K05, K10) | Tests check VM only | Headless overlap test | Same bug cannot return silently | undetected → test fails on revert | test log both ways | False positives on transparent overlays | Ignore elements with transparent backgrounds |
| False status (K13) | Idle text ignores books; shared counters | State rules + clamp | Status matches reality | "59 / 17" → never N > M | E2E sampling | Hides real job progress | Show tasks separately |
| Actions offscreen (K11) | One fixed row | Stopgap regrouping | Actions reachable | 5 offscreen → 0 at 860 px | AssertReachable | Owner dislikes interim look | Phase 07 replaces |

## 8. Test plan

- **Headless UI:** the overlap test (T03.2); empty vs filtered-empty states; the status text matrix.
- **E2E:** G1 and G2 at 860×560, 1280×800 and 1920×1080; a status sampling loop every 500 ms during a scan.
- **Negative:** a library with 1 book (pager hidden); 60 books (pager visible with 2 pages);
  the filter-matches-nothing state; a scan cancelled halfway (status must leave "Scanning";
  where the orchestrator never reaches a terminal phase, record for Phase 05).

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests.Ui -c Release --no-build --filter "FullyQualifiedName~CatalogueShellLayout"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G1|Journey=G2"
git stash; <revert T03.1 locally>; dotnet test ... --filter CatalogueShellLayout   # must FAIL; then restore
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Dark theme rendering of the empty state | Phase 09 | Checked there; this phase checks Light only |
| macOS rendering | Phase 27 | NOT ASSESSED |

## 11. Risks and mitigations

- *Other elements depend on the accidental nesting* (for example the reader panels in the same
  cell). Run all E2E journeys, not just G1 and G2, before merging.
- *The stopgap toolbar becomes permanent.* Phase 07's exit criteria require its removal.

## 12. Execution prompt

```
## Prompt 03 - Make the catalogue and first-run screen visible and the status truthful
You are fixing the Ogma Library shell layout in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K10, K11, K13)
5. docs/plans/sept-23-kaizen/phases/phase-03-shell-emergency-fixes.md
6. docs/plans/sept-23-kaizen/evidence/tools/root-cause-pager-overlay.patch
Load skills (read SKILL.md): avalonia-desktop-development, empty-error-and-loading-states,
onboarding-and-first-run-design, error-empty-and-system-messaging, click-path-audit.
Work plan: T03.2 first (write the failing test), then T03.1, then T03.3-T03.5, then T03.6.
File scope: src/OgmaLibrary.App/Views/Catalogue/CatalogueShellView.axaml, CatalogueViewModel.cs,
MainShellViewModel.cs (status only), localization resources, tests.
Acceptance: section 9 commands; before/after screenshots at three sizes in
docs/implementation/execution/evidence/sept-23-kaizen/phase-03/; the completion record lists the reverted-test failure.
Recovery point: the last Phase 02 commit.
```
