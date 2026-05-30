# Phase 06 — Test Plan

The catalogue browsing UI has three primary quality dimensions: functional correctness
of the ViewModel logic (sort/filter/shelf/bulk-edit), performance (NFR-OGMA-002/003
and NFR-PROD-005), and accessibility (NFR-PROD-007/008). R1 tests cover the bulk-edit
undo path and the unavailable-file display contract.

---

## Applicable test layers

| Layer | Applies | Notes |
| --- | --- | --- |
| 1. Domain unit | No | No domain logic introduced |
| 2. Infrastructure integration | Partial | `ICatalogueWriteService` (shelf CRUD, metadata edit, bulk edit) integration with EF Core |
| 3. PDF layer | No | No PDF I/O |
| 4. Search | No | Sort/filter is in-memory LINQ; FTS5 deferred to Phase 10 |
| 5. AI | No | Not in scope |
| 6. UI | Yes | ViewModel unit tests; Avalonia UI automation (Headless driver) for keyboard/SR walkthrough; virtual scroll frame-rate |
| 7. 3D | No | 3D is a placeholder slot only |
| 8. Performance | Yes | 2,000-book load, filter, scroll benchmarks |
| 9. Packaging | No | Not in scope |

---

## Golden corpus fixtures used

| Fixture | Used by | Oracle |
| --- | --- | --- |
| Synthetic 2,000-book corpus (by seed, Phase 04) | Load, filter, sort, scroll performance | All books appear; no missing covers on first render pass |
| `BookSummaryProjection` with all five rating values (1-5) | `FilterConjunctive_AllConditionsMustMatch` | Rating filter narrows correctly |
| Book with 3 shelves, no file-path dependency | `Shelf_BookInMultipleShelves_NoPathDependency` | 3 `ShelfBook` rows; `Shelves.Query` null (virtual, not smart) |
| Book with all five metadata field groups populated | `BookDetail_AllFiveFieldGroups_Populated` | No group tab empty |

---

## Test categories and oracles

### 1. View toggle & navigation contract

| Test | Oracle | Tier |
| --- | --- | --- |
| `ViewToggle_AllThreeViews_OpenSameBookDetail` | `OpenDetailAsync(BookId)` called with same `BookId` from grid, list, and directory | MVP |
| `MainShellViewModel_ViewToggle_ChangesCurrentView` | `CurrentView` enum changes on each toggle command | MVP |
| `AllViews_NavigateVia_IBookDetailNavigationService` (arch test) | No `MainShellViewModel` ref in View/ViewModel code-behind | MVP |

### 2. Grid view

| Test | Oracle | Tier |
| --- | --- | --- |
| `GridScroll_60FPS_2000Books` | Avalonia frame counter ≥ 60 FPS P95 during programmatic scroll | MVP (NFR-PROD-005) |
| `GridCell_AutomationProperties_Correct` | Cell ARIA name = "{title} by {author}, {status}" | MVP (NFR-PROD-008) |
| `AsyncCoverLoader_PlaceholderShown_BeforeLoad` | Cell renders placeholder before JPEG decoded | MVP |

### 3. Sort & filter

| Test | Oracle | Tier |
| --- | --- | --- |
| `FilterAndSort_2000Books_Under150ms` | P95 elapsed < 150 ms; 10-iteration measurement | MVP (NFR-OGMA-003) |
| `FilterConjunctive_AllConditionsMustMatch` | Status=Read AND Rating=5 returns only books satisfying both | MVP |
| `FilterClear_ResetsAllFilters` | `ClearAll()` → `HasActiveFilters = false`, count = total | MVP |
| `SortByTitle_Ascending_OrderCorrect` | `FilteredItems[0].Title` < `FilteredItems[1].Title` lexicographically | MVP |
| `SortByRating_Descending_OrderCorrect` | `FilteredItems[0].Rating` ≥ `FilteredItems[1].Rating` | MVP |
| `DirectoryView_FolderClick_FiltersToSubtree` | Items filtered to folder-2 books only | MVP |

### 4. Shelves

| Test | Oracle | Tier |
| --- | --- | --- |
| `Shelf_BookInMultipleShelves_NoPathDependency` | 3 `ShelfBook` rows; no file-path column in `ShelfBook` | MVP |
| `SmartShelf_EvaluatesCondition` | 3 of 5 books returned for `Rating >= 4` condition | MVP |
| `Shelf_Delete_DoesNotDeleteBooks` | `DeleteShelfAsync` removes `Shelf` + `ShelfBook` rows; `Books` rows intact | MVP (R1) |
| `Shelf_Rename_Persists` | `RenameShelfAsync` → restart DI → `ShelfProjection.Name` updated | MVP |

### 5. Book-detail panel

| Test | Oracle | Tier |
| --- | --- | --- |
| `BookDetail_AllFiveFieldGroups_Populated` | All 5 tab group ViewModels have ≥ 1 non-null field | MVP |
| `BookDetail_InlineEdit_PersistsToDb` | Edit title field → `ICatalogueWriteService.UpdateMetadataFieldAsync` called with new value | MVP |
| `BookDetail_Undo_RevertsInlineEdit` | Edit title; `Ctrl+Z`; assert original title restored | MVP (R1) |
| `BookDetail_OpenedByKeyboard` | `Enter` on focused grid cell → detail panel focused without mouse | MVP (NFR-PROD-007) |

### 6. Bulk edit (V1)

| Test | Oracle | Tier |
| --- | --- | --- |
| `BulkEdit_PreviewShownBeforeApply` | `BulkEditPreviewView` presented before `BulkEditAsync` invoked | V1 |
| `BulkEdit_Undo_RevertChanges` (R1) | Undo → all 5 books restored to pre-edit tags/status | V1 |
| `BulkEdit_MultiSelect_CtrlClick` | Ctrl+Click adds to selection without replacing it | V1 |
| `BulkEdit_AuditEvent_Written` | `AuditEvents` row with `BeforeJson` + `AfterJson` written per bulk edit | V1 |

### 7. Performance baselines

| Test | Budget | Corpus | Method |
| --- | --- | --- | --- |
| `CatalogueLoad_2000Books_Under2s` | < 2 s P95 | Synthetic 2,000-book corpus | `Stopwatch`; 5 iterations; assert P95 |
| `FilterAndSort_2000Books_Under150ms` | < 150 ms P95 | Same corpus | `Stopwatch`; 10 iterations; assert P95 |
| `GridScroll_60FPS_2000Books` | ≥ 60 FPS P95 | Same corpus with JPEG stubs | Avalonia diagnostic frame counter |
| `MainWindow_FirstScreen_Under1s` | < 1 s | Empty or minimal DB | App launch to first `Loaded` event |

### 8. Accessibility

| Test | Oracle | Tier |
| --- | --- | --- |
| Full keyboard walkthrough (manual, documented) | Tab through: grid → sort → filter → shelf → detail → settings; no keyboard trap | MVP (NFR-PROD-007) |
| Screen-reader walkthrough (manual, documented) | Grid cell, list row, shelf item, filter chip, detail panel all read correctly | MVP (NFR-PROD-008) |
| Pseudolocale render | No truncation or overlap in any view under `qps-Ploc` locale | MVP (I18N) |
| AA contrast check (automated) | All text/chip combinations pass 4.5:1 contrast ratio | MVP (NFR-PROD-008) |

---

## Fault-injection / reversibility tests

| Fault | Injected in | R-tier | Verification |
| --- | --- | --- | --- |
| `UpdateMetadataFieldAsync` throws `DbUpdateException` | Inline-edit in book detail | R1 | Field reverts to original; error toast shown; no partial write in DB |
| `BulkEditAsync` throws mid-way | `ICatalogueWriteService` mock | R1 | Undo command not added to history; user sees error; books in pre-edit state |
| `DeleteShelfAsync` throws | `ICatalogueWriteService` mock | R4 | Shelf remains in list; error shown; `Books` rows intact |

---

## CI matrix

| Runner | .NET | Architecture | Required? |
| --- | --- | --- | --- |
| Windows 10 x64 | .NET 10 LTS | x64 | Yes |
| macOS 12 x64 | .NET 10 LTS | x64 | Yes |
| macOS 14 Apple Silicon | .NET 10 LTS | ARM64 | Yes |

Performance tests use the synthetic 2,000-book corpus seeded deterministically
(fixed PRNG seed = 42); results are comparable across runs and runners.
Frame-rate tests are marked `[Explicit]` on CI (require a GPU context) and are
run on a dedicated performance runner (to be confirmed in Phase 20).
