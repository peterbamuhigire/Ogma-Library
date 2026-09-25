# Phase 10: Catalogue experience

## 1. Header

| Field | Value |
|---|---|
| Wave | C, Experience |
| Size | 7–10 engineering days |
| Depends on | Phase 06 (clean data: validity, duplicates, identity promotion), Phase 09 (variants, tokens, generated covers) |
| Owner decisions | D-03 (multi-root affects grouping by folder) |
| Primary defects | K14, K20 (UI verification), K23, K21 (presentation) |
| Requirements | CAT-001 (W), CAT-002 (P), CAT-003 (P), CAT-004 (W), CAT-005 (B), CAT-006 (B), CAT-007 (B), META-007 (P), META-008 (B), LIB-004 (W) |

## 2. Why this phase exists

With the K10 overlay removed (Phase 03) and the cover-root bug fixed (Phase 05), the catalogue
renders (`evidence/screens/j3-grid.png`). It is still a thin shell over a much richer backend:

- **Every card is a placeholder** because covers resolve against the wrong root (K20). Phase 05
  fixes the root; this phase proves covers appear progressively and handles failures visibly.
- **Screen readers hear record dumps.** Items announce `BookSummaryProjection { BookId = … }`
  because `AutomationProperties.Name` sits on the inner `Border`, not the `ListBoxItem` (K14).
- **Filters are partial (CAT-002 P).** Only title, author and sort are exposed
  (`CatalogueShellView.axaml:357-375`). `StatusFilter` and `AvailabilityFilter` exist in
  `CatalogueFilterViewModel.cs:55,80` with no UI. There are no rating, missing-field or
  quality filters (META-007 P). A persisted filter that matches nothing shows a blank grid
  (journeys J3).
- **Shelves cannot hold books.** `ICatalogueWriteService.AddBookToShelfAsync`
  (`ICatalogueWriteService.cs:42`) has no UI caller (CAT-003 P). Smart shelves are always
  created with `isSmart: false` (`ShelfSidebarViewModel.cs:183`, CAT-006 B).
- **Bulk edit with preview and undo** (`BulkEditAsync`, `ICatalogueWriteService.cs:74`, CAT-005 B),
  **work/edition merge and split** (`IdentityGroupingContracts.cs:38-52`, CAT-007 B) and the
  **library health dashboard** (`ILibraryHealthService.GetHealthSnapshotAsync`, META-008 B) are
  backend-only.
- **Duplicates are invisible.** The byte-identical *The Lantern Keeper (copy)* is listed as a
  separate book with no hint (K23).
- The grid is a non-virtualised `ListBox` with `WrapPanel` (`CatalogueGridView.axaml:15-25`),
  relying on 50-item paging instead of scrolling.

## 3. Objectives and exit criteria

1. Real covers appear for every book whose asset exists, **progressively** during processing (no
   wait for the whole queue to drain). Failed covers show the Phase 09 generated cover plus a
   "cover unavailable" badge with the reason in the tooltip.
2. The grid and list are virtualised (`ItemsRepeater` with a uniform-grid layout, or a virtualising
   panel) and scroll smoothly through 2,000 books. The pager becomes optional ("Show all" versus pages).
3. Every item's accessible name is "Title, by Author" plus its status badges. There are zero record
   `ToString` names in the UIA tree (K14).
4. Filter bar: text, author, status (reading state), rating, availability (available, missing),
   quality (below threshold), missing fields (no author, no ISBN, no cover, no year), file problems
   (Phase 05 "needs attention"), and shelf. There is an active-filter chip row with *Clear all*. A
   no-results state explains the active filters and offers *Clear filters*.
5. Sort: title, author, date added, last opened, rating, year, file size, quality.
6. Shelves: add or remove books via context menu, drag and drop onto the sidebar, and multi-select.
   Smart shelves are created from the current filter ("Save as smart shelf"), with live counts.
7. Bulk edit: multi-select → *Edit…* → field form → **preview of changes** → apply → *Undo* in a
   toast and in the Activity Centre (CAT-005).
8. Duplicates and editions: a "Possible duplicates" filter plus a grouped card ("2 copies") with
   *Compare*, *Merge into one work*, *Keep separate* and *Split*, all with undo (CAT-007).
9. Library health dashboard: counts of missing metadata, failed processing, unavailable files,
   duplicates and low quality, each a one-click filter; batch enrichment start, pause and resume
   (META-006/008) when providers are enabled.
10. The golden journeys "find a book by browsing" and "organise into a shelf" pass in the real
    window at 1280×800 and 1920×1080.

## 4. Skills to load before starting

- `C:\wamp64\www\design-system-skills\README.md`, `CLAUDE.md`
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\click-path-audit\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\14-conversion-and-web-page-patterns\empty-error-and-loading-states\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\interaction-design-patterns\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\form-ux-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\12-data-viz-and-dashboards\dashboard-and-data-product-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\accessibility-wcag-2-2-compliance\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (virtualisation, compiled bindings, UI-thread rules)
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-design-engineering\SKILL.md` (filter queries server-side)

Typeface: card titles in Public Sans SemiBold at Body, authors in Public Sans at Small in
`Text.Secondary`, and generated cover titles in Spectral. No new faces.

## 5. Scope

**In scope:** grid, list and directory views; filter/sort model and bar; the shelf sidebar
interactions; bulk edit UI; duplicate and edition UI; the health dashboard view; accessible names;
progressive cover refresh.

**Out of scope:** the detail inspector (Phase 11), metadata enrichment internals (Phase 06/08),
3D (Phase 18), and search (Phase 13; the catalogue text filter stays a local filter).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 10.1 | Move `AutomationProperties.Name`/`HelpText` to `ListBoxItem` via `ItemContainerTheme`; compose "Title, by Author, status". Apply the same to list and directory views. | `CatalogueGridView.axaml`, `CatalogueListView.axaml`, `CatalogueDirectoryView.axaml` | UIA dump: 0 names matching `Projection {`. |
| 10.2 | Virtualised grid (`ItemsRepeater` + `UniformGridLayout`, or a virtualising wrap panel) with stable card size tokens; keep keyboard navigation (arrows, Home/End, PageUp/Down). | `CatalogueGridView.axaml(.cs)` | 2,000-book synthetic corpus: scroll with no dropped frames above 100 ms (measured with the harness frame timer); memory bounded. |
| 10.3 | Progressive covers: subscribe to per-book asset-ready events from the pipeline (Phase 06) instead of refreshing only on queue `Complete` (`MainShellViewModel.cs:1168-1171`); update single items in place. | `CatalogueViewModel.cs`, `CoverImageView.axaml.cs` | Real-window: covers appear during a scan of the synthetic library. |
| 10.4 | Server-side filter model: extend `CatalogueFilter` and the read-model query with status, rating, availability, quality, missing-field and shelf predicates. Do not re-apply the persisted state on every refresh (journeys J3 defect). | `CatalogueFilterViewModel.cs`, `CatalogueViewModel.cs:446-523`, catalogue read model in Infrastructure | Unit: each predicate; a refresh preserves the user's live changes. |
| 10.5 | Filter bar and chips, the no-results state, and *Save as smart shelf* (calls `CreateShelfAsync(isSmart: true, condition)` with `SmartShelfCondition`). | `CatalogueShellView.axaml` filter row (moved into the Phase 07 layout), `ShelfSidebarViewModel.cs:183` | Headless UI: a smart shelf created from a filter shows the same count. |
| 10.6 | Shelf membership: context menu *Add to shelf ▸*, drag cards onto sidebar shelves, and *Remove from shelf* when viewing a shelf. | `ICatalogueWriteService.cs:42,48`, `ShelfSidebarView` | Real-window journey J-CAT-2. |
| 10.7 | Multi-select (Ctrl/Shift) and a selection toolbar: *Add to shelf*, *Edit…*, *Mark as read*, *Enrich*. | Grid and list views | Keyboard-only multi-select works. |
| 10.8 | Bulk edit dialog: field picker, value, preview table (before → after per book), apply via `BulkEditAsync`, undo via the command history. | New `Views/Catalogue/BulkEditDialog.axaml`, `ICatalogueWriteService.cs:74` | Unit plus headless: preview equals applied; undo restores. |
| 10.9 | Duplicates and editions: show the identity group on the card ("2 copies · 1 work"); *Compare* shows file facts side by side; merge and split via `IIdentityGroupingService` with `UndoLastAsync`. | `IdentityGroupingContracts.cs:31-63`, new view | Synthetic duplicate pair: merge → one card; undo → two. |
| 10.10 | Library health dashboard view bound to `ILibraryHealthService` (retry job, pause and resume batch), with a link from Settings → Library and from the status bar. | `ILibraryHealthService.cs:98-116`, new `Views/Catalogue/LibraryHealthView.axaml` | Counts match DB queries on the synthetic corpus. |
| 10.11 | "Needs attention" presentation for invalid files from Phase 05 (quarantined, not shown as books by default; a filter shows them with the reason). | Filter plus card badge | The 0-byte, HTML and truncated fixtures are not listed as normal books. |
| 10.12 | Localise every new string; add card badge tooltips. | Localization | Key parity test. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Record-dump names (K14) | Name on inner element; no container theme | Container-level names | Screen-reader users can browse | Bad names: 17/17 → 0 | UIA dump | None | Revert template |
| Backend-only catalogue features | UI never built after backend phases closed | Build UI for CAT-003/005/006/007, META-008 | FR usable count rises | CAT/META W count: 6 → 13 | Inventory re-run | Scope creep | Ship per feature slice |
| Invisible duplicates (K23) | Grouping not surfaced | Group badge plus actions | Users trust the count | Unflagged duplicates: 1 → 0 | Screenshot | Wrong merges | Undo path mandatory |
| Blank grid on stale filter | Persisted state re-applied every refresh | Restore only at start-up; no-results state | No "missing books" scares | Blank-without-explanation states: ≥1 → 0 | Harness journey | None | Revert |
| Non-virtualised grid | Paging used instead | Virtualising layout | Smooth at 2k | Frame time p95 at 2k books: unmeasured → ≤ 16 ms render, no hitch > 100 ms | Harness perf | Layout bugs | Keep pager fallback |

## 8. Test plan

- **Unit:** filter predicates; smart-shelf condition round-trip; bulk-edit preview versus apply;
  merge/split/undo.
- **Headless UI:** accessible names on items; the filter chip row; the no-results state;
  selection toolbar enablement; keyboard multi-select.
- **Real-window journeys:** J-CAT-1 browse and open detail by keyboard only; J-CAT-2 create a
  shelf, add three books by drag and by menu, filter by shelf; J-CAT-3 filter "missing author" and
  bulk-set the author with preview and undo; J-CAT-4 resolve the duplicate pair; J-CAT-5 open the
  health dashboard and jump to failed items.
- **Negative:** a filter matching nothing; drag onto a deleted shelf; bulk edit cancelled
  mid-preview; covers missing on disk.
- **Performance:** the 2,000-book generated corpus (extend `New-SyntheticCorpus.py` with a
  `--count` option).

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
python docs/plans/sept-23-kaizen/evidence/tools/New-SyntheticCorpus.py   # 17-file audit corpus
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Catalogue -Sizes 1280x800,1920x1080
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| Screen-reader listening test of the catalogue | Phase 21 | UIA name evidence only |
| 50k-book scroll on reference hardware | Phase 24 | 2k measured locally |
| Provider-backed batch enrichment | Owner (provider terms) | Dashboard shows enrichment only when enabled |

## 11. Risks and mitigations

- **Virtualisation changes keyboard focus behaviour.** Mitigation: keyboard journeys run before
  and after; keep the `ListBox` fallback behind a debug switch until verified.
- **Merges are destructive in the user's mind.** Mitigation: never delete files; undo is always
  shown; merge only affects catalogue identity.
- **Drag and drop on macOS.** Mitigation: the context menu is the primary path; drag is additive.

## 12. Execution prompt

```text
You are implementing Phase 10 (Catalogue experience) of the Sept-23 Kaizen plan in
C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-10-catalogue-experience.md
4. docs/plans/sept-23-kaizen/03-defect-register.md rows K14, K20, K21, K23 and 04-inventory-and-requirements.md (CAT/META rows)
Load the skills in section 4. Confirm Phases 06 and 09 are COMPLETE in
docs/implementation/execution/00-execution-status.md; if not, stop and report.
Slices: A) 10.1 names + 10.2 virtualisation + 10.3 progressive covers; B) 10.4–10.5 filters and
smart shelves; C) 10.6–10.8 shelves, multi-select and bulk edit; D) 10.9–10.11 duplicates, health
dashboard, needs-attention; E) 10.12 localisation. Each slice: tests first, then implementation,
then the section 9 commands and the real-window journeys with screenshots under
docs/implementation/execution/evidence/sept-23-kaizen/phase-10/. Use only Phase 09 variants and
tokens. Record results and NOT ASSESSED items in docs/implementation/execution/phase-sept23-10-completion.md.
```
