# Sept-23 Phase 10 completion: catalogue experience

Status: **IMPLEMENTED** (2026-11-19). All 8 slices are done; this is **not COMPLETE**. See
[Phase status](#phase-status-slice-8) for the Definition of Done items that do not hold.
Branch: `kaizen/phase-10`. The autorun driver publishes it; this lane does not merge it.
Rollback point: `d7e75952`.
Plan: [phase-10](../../plans/sept-23-kaizen/phases/phase-10-catalogue-experience.md).
Decisions: D-18 to D-48 in the [execution log](../../plans/sept-23-kaizen/EXECUTION-LOG.md).
Defects:
- K14 is resolved for the catalogue: grid, list, directory and home strip.
- K23 is partly resolved.
- K114 to K121 were found here. K116, K117 and K119 are resolved; K114, K115, K118, K120 and K121 are open.

See the [defect register](../../plans/sept-23-kaizen/03-defect-register.md).

## Typeface decision

No new typefaces. Cards use **Public Sans** for author, badges and captions. Card titles keep the
Phase 09 `ogma-display` class. Generated covers keep **Spectral**. Sizes and colours come only
from Phase 09 tokens; `Test-DesignTokens.ps1` is clean. The new tokens are the card and slot
sizes `Size.Catalogue.*` in `Themes/Tokens.axaml`.

## Slice 1: accessible names, virtualised grid and cover-failure badge

| Task | Result |
|---|---|
| Composed accessible names (K14) | `Converters/BookAccessibleNameConverter` builds the name "Title, by Author" and adds badges in this order: Locked, Unavailable, Favourite, text status, "N copies". It falls back to a localised "Unknown author" and "Untitled". English and French strings are in place. The name, HelpText and `Catalogue.Item.{id}` id are set on the `ListBoxItem` in the grid, list and directory views. The home strip uses `Catalogue.Home.Item.{id}`. The directory view gains the id `Catalogue.Directory`. The duplicate inner `Border` names are removed. Displayed authors use the same fallback instead of `PrimaryAuthor`. |
| Virtualised grid | `Views/Catalogue/VirtualizingUniformGridPanel` is an in-repo `VirtualizingPanel` that lays out fixed token-sized slots. It realises only the visible rows plus one row of buffer each side, and recycles the rest. It keeps the focused container. `ScrollIntoView` and `GetControl` support arrows, Home/End and PageUp/PageDown. PageUp/PageDown are handled on tunnel, because otherwise the ScrollViewer consumes them. Setting `OGMA_CATALOGUE_VIRTUALIZE=0` restores `WrapPanel`. |
| Recycling correctness | A container released during a layout pass can be rebound in the same pass without rebuilding its template, which made scroll steps much faster. A container still unused at the end of the pass is cleared, including its `DataContext`. Reuse prefers the container that last showed the same item, then the one at the same index. Without this, real-window G2 selected the wrong book three times, because refreshes moved books between containers that UIA already held. |
| Show all | `CatalogueViewModel.ShowAll` is on by default and is saved in `CatalogueViewState.ShowAll`. The pager shows only when Show all is off and there is more than one page. The footer with the "Show all books" check box shows when the results exceed one page. |
| Cover-unavailable badge | `BookSummaryProjection` gains `CoverSource`, `HasFailedCover` and `CopyCount`, all filled by `CatalogueReadModel`. The badge `Catalogue.Item.Badge.CoverUnavailable` appears when there is no extracted cover and processing has finished. Its tooltip gives the reason: password-protected, file missing, extraction failed or no cover found. Reasons are inferred from the processing state; see K114. |
| Corpus `--count N` | Added to `tests/fixtures/corpus/New-SyntheticCorpus.py` and the evidence-tools prototype. It adds N small original books under `Bulk/`. The default output was regenerated and is identical to `MANIFEST.sha256.json` (17 files). |
| E2E harness | `Uia.FindListItem` scrolls a virtualised list a page at a time until the item is realised, then scrolls it into view. `Shell.FindCatalogueItem` uses it. |

### Gates (slice 1)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings |
| `dotnet format --verify-no-changes` | PASS |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean") |
| `scripts/Test-Fast.ps1` filter (`Category!=Benchmark&Category!=E2E`) on the final code | **PASS**: 1,684 of 1,684 (Tests 1,248, Ui 384, Architecture 52). One earlier failure, `CatalogueShellLayout_PagerShownOnlyWhenThereIsMoreThanOnePage(101)`, was an intended change, since Show all now defaults on; the test now asserts the new behaviour. |
| `Sept23Phase10Grid` tests | PASS, 8 of 8, including the benchmark (`Category=Benchmark`) |
| G2 at 1280x800, under the desktop lock | **PASS** twice (`20261119-101326`, `20261119-101527`). Three earlier runs failed on the recycling defect described above. |
| Frame figures | Headless proxy only: median scroll step 3-5 ms with 42 containers realised. Real-window frame times are **NOT_ASSESSED**. See [measurements](evidence/sept-23-kaizen/phase-10/slice-1/headless-grid-measurements.md). |
| `Test-RequirementAccountability.ps1`, Arabic RTL, 200% text, screen reader | NOT_ASSESSED in this slice |

Evidence: `evidence/sept-23-kaizen/phase-10/slice-1/`.

### Open for later slices

- ~~`LoadAsync` still re-applies the saved view state on every refresh (task 10.4).~~ Fixed in slice 2.
- Real-window grid frame timing with `--count 2000`, and the keyboard and screen-reader walkthrough.
- Exact-hash copies show "N copies" in the name only. The Compare action and merge/split on work groups follow D-21.

## Slice 2: criteria model, read-model predicates, sorts and restore-once

| Area | Result |
|---|---|
| Criteria | `CatalogueFilter` gains the minimum and maximum rating, availability, quality below a threshold, missing fields (`[Flags] CatalogueMissingFields`: author, ISBN, cover, year), needs OCR, possible duplicates, and the sort field and direction. Every new parameter has a default, so existing callers still compile. |
| Read model | `CatalogueReadModel` narrows results in SQL. Title and author use `LIKE` with escaped wildcards. The shared evaluator then applies the extended criteria, the sort and the paging. `CountAsync` is a default interface method that the read model implements. `BookSummaryProjection` gains `DateAddedUtc`, `LastOpenedUtc`, `FileSizeBytes`, `HasIsbn`, `OccurrenceCount`, `WorkGroupId` and `EditionCount`. Only edition groups hide books; work groups only label them. |
| Evaluator | `CatalogueCriteriaEvaluator` (Application) is the in-memory twin, and `CatalogueViewModel.ApplyFilterAndSort` delegates to it (D-20). Sorting puts unknown values last and breaks ties by title, then BookId. |
| Sorts | Adds `DateAdded`, `LastOpened`, `FileSize` and `Quality`. The menu labels (`Catalogue.Sort.{field}`) and `SortDirectionText` are localised in English and French. |
| Date added (D-23) | The premise was corrected: no date added was stored. A nullable `Books.AddedUtc` column was added (migration `20261119102849_Sept23Phase10BookAddedUtc`), and `BookRegistrationService` sets it. Existing books stay null. The schema freeze advanced from 44 to 45 migrations, SHA `9103bd5b...cb31`. A rehearsal migrates a copy with `CatalogueMigrator` and restores the verified backup. |
| Smart shelves | `SmartShelfCriteria.ToConditions` and `TryToFilter` round-trip every criterion. A damaged or non-expressible query fails closed and returns no books. Smart-shelf counts use the same path. |
| View state | The saved state is applied once per view-model lifetime, so a refresh no longer overwrites a live filter change (task 10.4). `CatalogueViewState` gains `QualityBelow`, `MissingFields`, `NeedsOcrOnly` and `PossibleDuplicatesOnly`, all defaulted, so old files still load. Damaged values are ignored: a NaN quality, unknown flags and unknown sort names. |

### Gates (slice 2)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean") |
| `scripts/Test-Fast.ps1` | **PASS**: 1,763 of 1,763 (Tests 1,323, Ui 388, Architecture 52) |
| Filter `Sept23Phase10Criteria\|CatalogueRefresh` | PASS: 74 in Tests and 2 in Ui (`CatalogueRefreshConcurrencyTests` stays green) |
| `OgmaLibrary.Tests.Architecture` | PASS, 52 of 52, within Test-Fast |
| Real-window journey, keyboard, screen reader, RTL, 200% text, light and dark screenshots | NOT_ASSESSED in this slice (no behaviour journey was run; the evidence is headless and in unit tests) |
| `Test-RequirementAccountability.ps1` | NOT_ASSESSED in this slice |

### Open after slice 2

- The criteria controls for quality, missing fields and possible duplicates exist in the view model, but the filter drawer UI does not expose them yet.
- Unverified observation, not yet a K number: `ShellModule` gives `ShelfSidebarViewModel` a `CatalogueFilterViewModel` instance that differs from `CatalogueViewModel.Filter`. `MainShellViewModel.Navigation` has its own shelf wiring, so check whether selecting a shelf actually filters before logging it.

## Slice 3: filter bar, chips, explained no-results and smart shelves from filters

| Area | Result |
|---|---|
| Filter bar | New `CatalogueFilterBarViewModel` over the single `CatalogueFilterViewModel` (no second copy of the criteria). The drawer now offers status, rating, file availability, metadata quality, missing details (author, ISBN, cover, year), needs OCR, possible duplicates and collection. Each control has a `Catalogue.Filter.*` AutomationId and a localised accessible name. The drawer scrolls. |
| Chips | `ActiveFilterChips` and `RemoveFilter` delegate to the filter bar. There is one chip per active criterion (title, author, collection, status, rating, availability, quality, each missing field, needs OCR, duplicates), and each chip names its value, for example "Rating: 3 or higher". Removing a chip clears only that criterion (CODE: `Sept23Phase10FilterBarTests`). |
| No results | The filtered-empty state adds `Catalogue.FilteredEmpty.Detail` ("Active filters: ...") next to the existing Clear button. |
| Smart shelves | "Save as smart shelf" (name box, button, hint, polite status) calls `ShelfSidebarViewModel.CreateSmartShelfAsync`, which serialises `SmartShelfCriteria.ToConditions` and calls `CreateShelfAsync(isSmart: true, query)`. It is disabled with a hint when no filter is active or when a collection filter applies (D-25). Manual shelf creation is unchanged. A failed save is logged through `AppLog` and shows a localised message. |
| Counts | Collections counts come from `ICatalogueReadModel.CountAsync` for every shelf after each sidebar load, which follows every catalogue load. Smart shelves show the `ic_cat_filter` glyph (existing icon catalogue), named "Smart shelf". |
| Grid parity | `ShelfProjection` gains `Query` (smart shelves only, D-26). `CatalogueViewModel.SetSmartShelves` lets the in-memory grid evaluate a selected smart shelf by its conditions, so the grid count equals the shelf count (CODE: test `SmartShelf_FromFilter_ShowsTheSameCountAsTheFilteredGrid`). |
| Status premise (D-24) | The plan says "status (reading state)"; the criteria model's status is the lifecycle status (Active, Unavailable, Excluded). The bar exposes lifecycle status; reading state is not a criterion. |
| Localisation | Every new string is in English and French (`Filter.Bar.*`, `Filter.Chip.*Format`, `Catalogue.FilteredEmpty.ActiveFormat`, `Collections.SmartShelf`). The generic `Filter.Chip.Status/Rating/Availability` keys were replaced. |
| Typeface | Unchanged: Public Sans for UI and cards, Spectral for generated covers; no new faces. Only Phase 09 tokens are used. |
| Skills | `empty-error-and-loading-states` (Development engine) rule 2 was applied to the no-results state. avalonia-desktop-development, accessibility-wcag-2-2-compliance, interaction-design-patterns and product-excellence-standard were not read: NOT_ASSESSED. |

### Gates (slice 3)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 errors |
| `dotnet format --verify-no-changes --no-restore` | PASS (after a line-ending fix to the new file) |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean") |
| `scripts/Test-Fast.ps1` | **PASS**: 1,771 of 1,771 (Tests 1,323, Ui 396, Architecture 52) |
| Filter `Sept23Phase10FilterBar` | PASS, 8 of 8 |
| Related UI tests (Navigation, Catalogue, Shelf, Filter, Localization) | PASS, 99 of 99 |
| Real-window Navigation journey, 1280x800, under the desktop lock (isolated data folder per session) | Run `p10-s3-nav`: 2 PASS, 1 FAIL. `Navigation_EveryDestinationAndPrimaryActionIsReachable` failed on its first step: "Shell.Nav.Library centre {X=112,Y=63} is covered by ToolBar id='view_1000' name=''". The same failure was recorded before this slice in close-out run `co-all-1`; the covering element is not an Ogma control. Re-run `p10-s3-nav2`: **3 PASS, 0 FAIL**. The intermittent occlusion is pre-existing and not caused by this slice. |
| Filter drawer, smart-shelf save and Collections glyph in a real window; light and dark, RTL, 200% text, keyboard and screen-reader walkthrough | NOT_ASSESSED in this slice (no journey drives the drawer yet; G10 belongs to slice 4 onward). Evidence is headless (`FilterDrawer_ExposesEveryCriterionWithAnAutomationId`). |
| `Test-RequirementAccountability.ps1` | NOT_ASSESSED in this slice |

### Open after slice 3

- K115: the Collections sidebar holds its own filter instance (slice 4, task 10.6).
- Sidebar counts call `CountAsync` once per shelf, which repeats the smart-shelf evaluation already done in `GetShelvesAsync`. This is acceptable at the current scale; server-side counting is Phase 24 (D-20).
- The intermittent Navigation occlusion by a foreign `ToolBar` (`view_1000`) is still unexplained. If it recurs, the harness should name the owning process.

## Slice 4: multi-select, selection toolbar and shelf membership

Commits: `e1a9f6b4` (feature), `aba334b8` (real-window selection, focus and menu fixes),
`fb2950e8` (toolbar clear of the overlay inspector).

| Area | Result |
|---|---|
| Multi-select | The grid and list use `SelectionMode="Multiple"` and bind only `SelectedItems` to `CatalogueViewModel.SelectedBooks`. `SelectedBook` stays the primary selection (the first pick, D-30) and drives the detail pane. The home strip and directory stay single-select. Ctrl+click, Shift+arrow and Ctrl+A work. A refresh that replaces cards restores the selection once and keeps the primary. |
| Toolbar (D-27) | Shown at two or more selected books, or at one when the Library is filtered to a manual collection. Add to collection, Remove from collection, Edit… (disabled until slice 5), Mark as read (D-29), Enrich (only with a metadata provider enabled) and Clear selection. Each has a `Catalogue.Selection.*` id and a localised name. |
| Card menu and drop (D-28) | Each card's ContextFlyout offers Add to collection ▸ (manual collections) and Remove from collection. Cards drag onto Collections rows; the Collections rail item opens Collections while a drag is over it. `ShelfMembershipViewModel` re-reads the shelves first, so a smart or deleted collection gets a localised warning and nothing is written. Failures go to `IUserNotifier` and `AppLog`, never as exception text. |
| Real-window fixes | Four defects that only appeared with background processing reloading the catalogue: (1) two-way `SelectedItem` beside `SelectedItems`, plus reused grid containers keeping a stale `IsSelected`, dropped or tripled picks; (2) a refresh hid the focused card, so arrows stopped working, and the grid panel now refocuses it; (3) the sidebar cleared and refilled its shelves, briefly disabling Add to collection, and shelves now sync in place; (4) the Add to collection menu was empty until `Opening`, and an empty `MenuFlyout` never opens in a real window, so it is now built whenever the collections or the culture change. |
| Overlay inspector | Below 1280 px of client width the inspector covered the toolbar's Clear selection. While the overlay is open, the toolbar is now inset by the inspector's width. |
| Typeface | Unchanged: Public Sans for UI and cards, Spectral for generated covers, no new faces; Phase 09 tokens only. |

### Gates (slice 4)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean") |
| `scripts/Test-Fast.ps1` | **PASS**: 1,783 of 1,783 (Tests 1,323, Ui 408, Architecture 52) |
| Filter `Sept23Phase10Selection` | PASS, 12 of 12. The focus-after-refresh and overlay-toolbar assertions were mutation-checked: each fails with its fix disabled. |
| Golden journeys `-Tag Catalogue`, 1280x800, under the desktop lock | Run `20261119-132548`: A11yNames, G1, G2, G6, **J-CAT-1 PASS**, **J-CAT-2 PASS** (menu opened by a real click, filter shows exactly the two picked books). Earlier runs failed on the defects above (`20261119-124005`, `-124702`, `-125416`). [Evidence](evidence/sept-23-kaizen/phase-10/slice-4/). |
| J-CAT-2 drag onto a collection | **NOT_ASSESSED**: injected mouse input does not start an OLE drag, so Collections did not open. The drop path is covered headlessly (deleted and smart drops rejected). |
| G10 Organise | **NOT ASSESSED**: still the Phase 01 placeholder. J-CAT-1 and J-CAT-2 cover slice 4; G10 should be bound once bulk edit (slice 5) lands. |
| 1024x768, dark theme, RTL, 200% text, screen reader | NOT_ASSESSED in this slice |
| `Test-RequirementAccountability.ps1` | NOT_ASSESSED in this slice |
| Skills | Development engine skills were not read this slice: NOT_ASSESSED. |

### Open after slice 4

- K115 stays open (D-31): sharing the Library filter with the sidebar needs id-stable shelf selection first.
- K116: `BulkEditCommand.NewStatus` is documented as reading status but writes the lifecycle status (slice 5).
- K117: a notification toast covers the right end of the selection toolbar while it shows.
- The drag journey needs a real OLE drag driver; until then it is NOT_ASSESSED.

## Slice 5: bulk edit with preview and undo (CAT-005)

Commits: `e14a593c` (planner, undoable command, snapshot undo, K116), `f3e7b873` (dialog,
toast and Activity Centre Undo, Edit… enabled), and the journey and records commit after it.

Premise correction: the plan assumed `UpdateMetadataFieldAsync` already validated values. It did
not, so the slice added shared `BulkEditFieldRules`, used by both the preview and the write (D-32).

| Area | Result |
|---|---|
| Command and rules | `BulkEditCommand` carries author and year. `BulkEditFieldRules` normalise and validate author (semicolon-separated), year and rating; the planner and the write path share them (D-32). K116 is fixed: the field is `NewLifecycleStatus`, and the form offers only author, year and rating (D-33). |
| Preview | `BulkEditPlanner` returns one row per selected book with its current and new value and whether it changes. `BulkEditDialog` (`BulkEdit.*` ids) shows a field picker, a value box with persistent labels and help, an assertive validation message, a "n of m books will change" summary and the rows. Apply is enabled only when at least one row changes; Cancel writes nothing. Each row's id `BulkEdit.Row.{bookId}` and full accessible description sit on the title text, because a `Border` has no automation peer (the first real-window run failed on this). |
| Undo | `BulkEditUndoableCommand` captures a snapshot of every touched field before writing and restores it on undo (D-34). It goes on the shared `ICommandHistory` (LIFO, 20). Undo is offered in a toast and in a new Activity Centre "Edits" list. Only the newest edit is undoable; a stale toast Undo does nothing, and a failed undo marks the entry unavailable so the log and history stay in step (D-35). |
| Toast placement (K117) | The toast is lifted above the selection toolbar (margin `16,16,16,112`) and has `Shell.Toast.Action` and `Shell.Toast.Dismiss` ids (D-36). |
| Strings and design | Every new string is in English and French. Phase 09 tokens only; Public Sans and Spectral only; `Catalogue.Item.{id}` ids unchanged. |

### Gates (slice 5)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean") |
| Architecture tests | PASS, 52 of 52 |
| `scripts/Test-Fast.ps1` | **PASS**: Tests 1,327, Ui 413, Architecture 52 |
| Filter `Sept23Phase10BulkEdit` | PASS: 4 SQLite tests (the preview equals the applied result; undo restores author, year, status, rating, tags and shelf; planning alone writes nothing; no-change and invalid values cannot apply) and 5 headless dialog tests. The row test asserts that the id is on an element with a control-view automation peer. |
| J-CAT-3 at 1280x800 under the desktop lock | Run `20261119-140258` FAILED (rows not exposed to UIA). After the fix, runs `20261119-141118` and `20261119-141641` **PASS**: filter Missing: Author, select two books, Edit…, set the author, preview rows found, apply (both books leave the filtered list), Undo from the toast (both return). The other catalogue journeys pass in the same runs. [Evidence](evidence/sept-23-kaizen/phase-10/slice-5/), including a capture of the dialog itself. |
| K117 in a real window | MEASURED across two shots at 1280x800 in run `20261119-141641`: the toast's bottom edge (y 680) sits above the toolbar's top edge (y 697). The toast and toolbar were not on screen together, because a successful apply clears the selection. |
| G10 Organise | **NOT ASSESSED**: still the Phase 01 placeholder; binding it to J-CAT-3 is left to a later slice. |
| 1024x768, dark theme, RTL, 200% text, screen reader | NOT_ASSESSED in this slice |
| `Test-RequirementAccountability.ps1` | NOT_ASSESSED in this slice |
| Skills | Development engine skills were not read this slice: NOT_ASSESSED. |

### Open after slice 5

- K115 stays open: the D-31 deferral named slice 5, but it still needs id-stable shelf selection and was not taken up.
- G10 is still a placeholder; J-CAT-3 is the natural binding.
- Risk: undo restores a snapshot, so a background enrichment that wrote the same field between apply and undo would be overwritten. The journey ran during processing without a conflict, but no test covers the race.
- The drag journey still needs a real OLE drag driver.

## Slice 6: copies, editions, merge and split (CAT-007)

Commit: `feat(catalogue): copies and editions badge, compare, merge and split with undo (CAT-007)`.

Premise corrections:

- No read contract mapped a book to its file occurrences or file facts. The slice adds `IBookOccurrenceReader` (`BookOccurrenceReader`).
- Books scanned since launch had no canonical identity graph (aliases, occurrences). The reader fills the graph on demand for the books it is asked about, so Merge and Compare work on newly scanned books.
- `IIdentityGroupingService` has no expected-version parameter. Undo therefore checks the version optimistically (D-39); the check is not atomic.
- Keep separate cannot be persisted by the service, so it is omitted and registered as K118 (Phase 11, open; D-41).

| Area | Result |
|---|---|
| Badge | Grid and list cards show "2 copies", "2 editions · 1 work" (joined with " · " when both apply) or "Possible duplicate", with a tooltip saying that files are never deleted. The badge text ends the card's accessible name. Id `Catalogue.Item.Badge.Identity`; the `Catalogue.Item.{id}` ids are unchanged. |
| One card per work (D-37) | `CatalogueReadModel` collapses a work group to the card with the lowest book id after paging, as edition groups already are; a page can therefore be short (known limitation, shared with edition groups). `CatalogueViewModel.IsSameCard` now compares the identity fields. Before this fix a merged card stayed stale until restart. |
| Compare | `CompareCopiesDialog` and `CompareCopiesViewModel` show each file side by side: name, location, size, a content fingerprint prefix, condition, added and last seen, and an identical or different verdict. The dialog is read-only and never deletes, moves or renames a file. When no title is stored, each column is named by its own file name, as the card is (fixed after the first real-window run showed "Untitled"). |
| Merge, Split, Undo | `IdentityGroupingViewModel` adds Merge, Split and Compare to the selection toolbar (`Catalogue.Selection.Merge`, `.Split` and `.Compare`). Each change raises a toast with a one-shot Undo. A conflict or refusal gives a localised warning (D-38, D-39). `IIdentityGroupingService` and `IBookOccurrenceReader` are resolved only in `ShellModule`. |
| Possible duplicates (D-40) | `DuplicateCandidates` holds the shared rules: a shared normalised ISBN, or a shared content hash. `LibraryHealthService.LoadDuplicatesAsync` delegates to it, and the catalogue filter adds books held as two or more files. A test proves the two agree. |
| Strings and design | Every new string is in English and French. Phase 09 tokens only, with no hard-coded colour, `FontSize` or `Foreground`. The typeface is Public Sans and Spectral only. |

### Gates (slice 6)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors (re-run after the last fix) |
| `dotnet format --verify-no-changes --no-restore` | PASS (re-run after the last fix) |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean"); no XAML has changed since |
| `scripts/Test-Fast.ps1` | **PASS**: Tests 1,333, Ui 416, Architecture 52. This ran before the Compare title fix; after the fix, only the Identity filter was re-run. |
| Filter `Sept23Phase10Identity` | PASS: 7 SQLite tests (merge then undo, split then undo, conflict, refusal of two works, Compare, the file-name title, parity with Library Health) and 3 headless tests (badges in grid and list, the accessible name and tooltip in English and French, the Compare dialog columns and Close). |
| J-CAT-4 at 1280x800 under the desktop lock | **PASS** in run `20261119-150819` (with J-CAT-1/2/3, G1, G2, G6 and A11yNames, all PASS) and in run `20261119-151646` after the title fix. The corpus duplicate's card is named with "2 copies". Compare shows 2 columns and closes. Merging two books shows one "2 editions · 1 work" card in place of two, Undo in the toast brings both back, and the library file count is unchanged. [Evidence](evidence/sept-23-kaizen/phase-10/slice-6/). |
| Journey runner | `Invoke-GoldenJourneys.ps1 -Journey` rejected hyphenated ids such as `J-CAT-4`; it now accepts them. |
| G10 Organise | NOT ASSESSED (still a placeholder) |
| 1024x768, dark theme, RTL, 200% text, screen reader | NOT_ASSESSED in this slice |
| `Test-RequirementAccountability.ps1` | NOT_ASSESSED in this slice |
| Skills | Development engine skills were not read this slice: NOT_ASSESSED. |

### Open after slice 6

- K118: Keep separate (Phase 11).
- At 1280x800, with the inspector open, the selection toolbar wraps onto a second line ("Clear selection"); everything stays reachable. This was observed, not measured against a budget.
- Undo's version check is optimistic, not atomic (D-39).
- When a work collapses, a page can be short (D-37).

## Slice 7: library health dashboard, localisation parity and G10 (META-008)

Commit: `feat(catalogue): library health dashboard and key parity (META-008)`.

| Area | Result |
|---|---|
| Dashboard | `LibraryHealthView` and `LibraryHealthViewModel` (`Health.View`). There are eight tiles (`Health.Tile.{Kind}`): missing author, ISBN, cover and year, failed jobs, unavailable files, possible duplicates and low quality, plus Needs attention when that service is present. Each tile has a count in its accessible name and a help tooltip. Book counts come from `CountAsync` over the tile's own criteria; job, file and duplicate counts come from `GetHealthSnapshotAsync` (D-43). |
| Actions | A book tile replaces the filter and shows the catalogue. Failed jobs lists each job with Retry. Needs attention opens the existing list. Batch Pause and Resume show only when providers are enabled (D-44). Refresh and Back are available, and failures give a localised message and an `AppLog` entry. |
| Entry points (D-42) | Settings → Library (`Settings.Library.Health`), the status bar (`Shell.Status.LibraryHealth`) and the palette command `library.health`. |
| Strings | `LocalizationKeyParityTests`: English and French have the same keys, no value is empty and no value is mis-encoded. K119 (five French values were mojibake) is fixed. `SortDirectionText` and `PrimaryAuthor` no longer hard-code English (D-45). The 14 status badges have localised tooltips in grid and list (D-46). |
| Render test | `HealthDashboardRenderTests` renders the shipped view, not an inline fake, and publishes `health-en.png`. |
| Journeys | The real G10 (shelf, smart shelf, bulk edit and undo, merge and undo) and J-CAT-5 (dashboard → missing-author tile → filtered catalogue; Needs attention tile → list) are in `CatalogueOrganiseTests` (D-47). |
| Design | Phase 09 tokens only. The tiles use the new theme variant `Button.tile` (the design-token lint rejected local brushes on a button). |

### Gates (slice 7)

| Gate | Result |
|---|---|
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes --no-restore` | PASS, after line endings were normalised |
| `scripts/Test-DesignTokens.ps1` | PASS ("clean"), after `Button.tile` |
| `scripts/Test-Fast.ps1` | PASS: Tests 1,350, Ui 417, Architecture 52 |
| `scripts/Test-RequirementAccountability.ps1` | PASS (162 IDs assigned) |
| Filter `LibraryHealth` or `LocalizationKeyParity` | PASS, 17 of 17 |
| G10 and J-CAT-5 at 1280x800 under the desktop lock | **NOT_ASSESSED**. Run `20261119-155855` failed at launch: `TimeoutException`, "main window did not appear within 30 s". At the time, the owner's interactive desktop was running a full-screen game. A diagnostic launch found the visible "Ogma Library" top-level window after 15 s, but `Process.MainWindowHandle` stayed 0. This is a harness and desktop condition, not an assertion failure. Real input was not injected again over a desktop in use. [Evidence](evidence/sept-23-kaizen/phase-10/slice-7/). |
| 1024x768, dark theme, RTL, 200% text, screen reader | NOT_ASSESSED in this slice |
| Skills | Development engine skills were not read this slice: NOT_ASSESSED |

### Open after slice 7

- Re-run G10 and J-CAT-5 on an idle desktop. Their oracles have never executed.
- Failed-job rows show the stable job type and failure codes (for example "Enrich · provider_timeout"), not localised text.
- K118 (Keep separate) and the open items from slice 6.

## Slice 8: record and evidence

Head: `aee9e2dd`. Rollback point for the phase: `d7e75952` (`d7e759521a729a370904f409d9a20210d74ed778`).
This slice changes only records and evidence; no product code changed.

### Tasks 10.1 to 10.12

| # | Task | Status | Where |
|---|---|---|---|
| 10.1 | Container-level accessible names in grid, list and directory | **Done**. Before this phase K14 had been fixed for title-only names on the grid and list. | Slice 1, `b50d0d21`: composed name, badges, directory id `Catalogue.Directory` |
| 10.2 | Virtualised grid with keyboard navigation | **Done** (CODE, headless). The 2,000-book real-window frame budget is NOT_ASSESSED (K120, Phase 24). | Slice 1, `b50d0d21` (D-19) |
| 10.3 | Progressive covers | **Pre-existing**: progressive refresh during processing landed in Phase 06 (T06.6, `MainShellViewModel`). This phase added the cover-unavailable badge with its reason. | Phase 06; slice 1, `b50d0d21` (reason inference K114) |
| 10.4 | Filter model with read-model predicates; restore once | **Done**. Server-side paging is **moved** to Phase 24 (D-20). | Slice 2, `1174e30a` |
| 10.5 | Filter bar, chips, no-results, Save as smart shelf | **Done** | Slice 3, `d617a4d9` |
| 10.6 | Shelf membership: menu, drag, remove | **Done**. The menu path passes in a real window. The drag path is headless only: real-window OLE drag is NOT_ASSESSED, and drag on macOS is moved to Phase 27. | Slice 4, `e1a9f6b4`, `aba334b8` |
| 10.7 | Multi-select and selection toolbar | **Done**. Enrich shows only when a provider is enabled. | Slice 4, `e1a9f6b4`, `fb2950e8` |
| 10.8 | Bulk edit with preview, apply and undo | **Done** | Slice 5, `e14a593c`, `f3e7b873`, `3b0ecf50` |
| 10.9 | Copies and editions, Compare, Merge, Split, Undo | **Done**, except *Keep separate*, which is **moved** to Phase 11 (K118, D-41) | Slice 6, `59e42021` |
| 10.10 | Library health dashboard | **Done**. Localised failed-job rows are **moved** to Phase 11 (K121). | Slice 7, `aee9e2dd` |
| 10.11 | Needs attention for invalid files | **Pre-existing**: Phase 05 (G6 stable) keeps invalid files out of the book list and adds the Needs attention panel. Moving `IngestionFailure` rows to `FileIssues` is **moved** to Phase 11 (D-22). Slice 7 adds the Needs attention health tile. | Phase 05; slice 7 |
| 10.12 | Localisation and badge tooltips | **Done**: English and French key parity, badge tooltips and K119. Exception: K121. | Slice 7, `aee9e2dd` |

### Decisions

These preamble decisions were recorded at the start of the phase:
- **D-18**: proceed although Phases 06 and 09 are IMPLEMENTED, not COMPLETE.
- **D-19**: an in-repo virtualising panel inside the existing ListBox; `OGMA_CATALOGUE_VIRTUALIZE=0` restores the `WrapPanel`.
- **D-20**: one criteria model with read-model and view-model parity; server-side paging moves to Phase 24.
- **D-21**: copies show "2 copies" and Compare; merge and split act on work groups.
- **D-22**: moving `IngestionFailure` rows to `FileIssues` moves to Phase 11.

Slice decisions D-23 to D-47 are in the sections above and in the execution log. Slice 8 adds
**D-48**: a real-window run does not start while the owner's interactive session is in use (input
within the last 5 minutes). The journeys are then recorded as NOT_ASSESSED rather than injecting
input over the owner's desktop. `baseline.json` is unchanged, because D-17 requires 3 consecutive
passes at both sizes and no Phase 10 journey has passed at 1920x1080.

### Gates on `aee9e2dd` (slice 8)

Verbatim output: [gates and scroll](evidence/sept-23-kaizen/phase-10/slice-8/gates-and-scroll-20261119.md).

| Gate | Class | Result |
|---|---|---|
| `./scripts/Test-RequirementAccountability.ps1` | MEASURED | PASS: 101 FRs, 29 NFRs, 32 controls; all 162 IDs assigned |
| `dotnet restore OgmaLibrary.sln --locked-mode` | MEASURED | PASS (exit 0) |
| `dotnet format --verify-no-changes --no-restore` | MEASURED | PASS (exit 0) |
| `dotnet build -c Release --no-restore` | MEASURED | PASS: 0 warnings, 0 errors |
| `dotnet list package --vulnerable --include-transitive` | MEASURED | PASS: 0 vulnerable packages in 11 of 11 projects |
| `dotnet format analyzers ... --severity warn` | MEASURED | PASS (exit 0) |
| `./scripts/Test-DesignTokens.ps1` | MEASURED | PASS ("clean") |
| `./scripts/Test-Fast.ps1` | MEASURED | PASS: Tests 1,350, Ui 417, Architecture 52; 0 failed |
| `src/shelf3d` npm gates | — | Not applicable: unchanged in Phase 10 |
| 2,000-book scroll, real window | — | **NOT_ASSESSED**: the harness has no frame timer and there is no 2,000-book journey (K120, Phase 24) |
| 2,000-book scroll, headless proxy | HEURISTIC | Median assertion passes. Layout p95 250.13 ms and 212.42 ms; worst step 438.51 ms and 345.65 ms (two runs); 26 containers realised. Above the plan's 16 ms / 100 ms targets (K120). |

### Real-window journeys (slice 8)

**NOT_ASSESSED on this head** (D-48). At 2026-11-19, before the run, the owner's desktop was in active
use: the last input was 2 s earlier, and the foreground window was "FC Live Editor Launcher" (with EA
and FIFA Mod Manager windows open). Under D-48, `Invoke-GoldenJourneys.ps1 -Tag Catalogue -Sizes 1280x800,1920x1080`
was not started. A background check then waited 45 minutes for 5 minutes of idle time. It never came: the check ended
at 20:11:06 with `STILL_ACTIVE ... lastIdleSec=0`. The screenshots this slice asked for at both sizes were therefore not captured
on this head: grid, filter bar, no-results, shelf, bulk-edit preview, Compare and health, plus a fresh
UIA dump.

Existing real-window evidence from earlier slices, all at 1280x800 only (MEASURED):

| Journey | Last PASS | Evidence |
|---|---|---|
| A11yNames (zero record names), G1, G2, G6, J-CAT-1, J-CAT-2 (menu), J-CAT-3, J-CAT-4 | Run `20261119-150819` | [results](evidence/sept-23-kaizen/phase-10/slice-6/catalogue-20261119-150819-results.md) |
| J-CAT-1, J-CAT-2 | Run `20261119-132548` | [results](evidence/sept-23-kaizen/phase-10/slice-4/catalogue-20261119-132548-results.md) |
| J-CAT-3 bulk-edit preview, applied, undone | Run `20261119-141641` | [preview](evidence/sept-23-kaizen/phase-10/slice-5/j-cat-3-bulk-edit-preview-1280x800.png), [results](evidence/sept-23-kaizen/phase-10/slice-5/catalogue-20261119-141641-results.md) |
| J-CAT-4 Compare, merge, undo | Run `20261119-151646` | [compare](evidence/sept-23-kaizen/phase-10/slice-6/j-cat-4-compare-dialog-1280x800.png), [results](evidence/sept-23-kaizen/phase-10/slice-6/j-cat-4-20261119-151646-results.md) |
| G10, J-CAT-5 | Never executed | [run `20261119-155855`](evidence/sept-23-kaizen/phase-10/slice-7/g10-jcat5-20261119-155855-results.md) failed at launch on a busy desktop |
| Any Phase 10 journey at 1920x1080 | Never executed | — |

Health dashboard appearance: headless only (CODE). `HealthDashboardRenderTests` renders the shipped
view and writes `health-en.png` to its test output; no real-window capture exists.

### Typeface statement

No new typefaces in Phase 10. Cards use Public Sans for author, badges and captions. Card titles use
the Phase 09 `ogma-display` class, and generated covers use Spectral. Sizes and colours come only from
Phase 09 tokens; `Test-DesignTokens.ps1` is clean on `aee9e2dd`.

### NOT_ASSESSED, with owners

| Item | Owner |
|---|---|
| Screen-reader listening test of the catalogue (UIA name evidence only) | Phase 21 |
| 50k-book scroll on reference hardware; 2,000-book real-window frame timing (K120) | Phase 24 |
| Provider-backed batch enrichment (the dashboard shows Pause and Resume only when providers are enabled) | Owner (provider terms) |
| Drag onto a collection on macOS; real-window OLE drag on Windows | Phase 27 (macOS); Phase 01 harness (OLE drag driver) |
| PX-GATE evidence: 200% text, Arabic RTL, the longest declared language, dark theme, 1024x768, the keyboard and screen-reader walkthrough, profiling on reference hardware, an independent verifier's signature | Phase 21 (accessibility, text scale, RTL), Phase 24 (profiling), owner (independent verifier) |
| Every Phase 10 journey at 1920x1080; G10 and J-CAT-5 at any size; slice 8 screenshots and UIA dump on this head | Next Phase 10 evidence run on an idle desktop |
| Development engine skills for the slices that did not read them | Recorded per slice above |

### Phase status (slice 8)

**IMPLEMENTED, not COMPLETE.** Against `08-master-plan.md` §5:

| # | Definition of Done item | Holds? |
|---|---|---|
| 1 | Every task done or moved with a reason | Yes (table above) |
| 2 | Full gate set on a clean checkout, including the real-window journey suite | **No**: the static gates and the fast suite pass; the journey suite was not run on this head |
| 3 | New behaviour tested at the right layer | Partly: unit and headless tests throughout; real-window coverage at 1280x800 for J-CAT-1 to J-CAT-4; none for G10 or J-CAT-5 |
| 4 | Before and after screenshots of touched journeys at both sizes | **No**: 1280x800 only, for J-CAT-3 and J-CAT-4 |
| 5 | Strings localised, tokens only, meaningful names | Mostly: key parity and the token lint pass; K121 is open |
| 6 | Completion record with commands, results, evidence, NOT_ASSESSED items and rollback | Yes (this record) |
| 7 | Status register and execution status updated | Yes (slice 8) |

Open items to reach COMPLETE: run `-Tag Catalogue -Sizes 1280x800,1920x1080` on an idle desktop
(G10 and J-CAT-5 included), capture the listed screenshots and a UIA dump, then fix or reassign
K120 and K121.
