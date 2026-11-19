# Sept-23 Phase 10 completion: catalogue experience

Status: **IN PROGRESS**. Slice 1 of 8 is done (2026-11-19). The phase is not IMPLEMENTED or
COMPLETE.
Branch: `kaizen/phase-10`. The autorun driver publishes it; this lane does not merge it.
Rollback point: `d7e75952`.
Plan: [phase-10](../../plans/sept-23-kaizen/phases/phase-10-catalogue-experience.md).
Decisions: D-18 to D-22 in the [execution log](../../plans/sept-23-kaizen/EXECUTION-LOG.md).
Defects: K14 is resolved for the grid, list, directory and home strip. K114 was found here and is
handed on. See the [defect register](../../plans/sept-23-kaizen/03-defect-register.md).

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
