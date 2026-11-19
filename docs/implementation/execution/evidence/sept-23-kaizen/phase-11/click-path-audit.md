# Phase 11 task 11.1: book-detail click-path audit

Source: `src/OgmaLibrary.App/Views/Catalogue/BookDetailView.axaml.cs` traced to
`src/OgmaLibrary.App/ViewModels/Catalogue/BookDetailViewModel.cs` at `bde93dac`, then
re-checked after slice 1. View-model line numbers (`l.`) refer to the file after slice 1.
Evidence class: CODE (read and traced; not a runtime trace).

"Awaited" means the handler goes through `UiActions.Run` (Phase 02 safe handler: awaited,
logged as `ui.action.failed`, never `async void`). "Raw text" means the view model formats
`ex.Message` into user-visible status text, which AGENT_BRIEF rule 6 forbids.

| # | Handler | View-model call | Awaited? | State reset | Error surface |
|---|---|---|---|---|---|
| 1 | `CloseButton_Click` | `Close()` | Sync | `IsVisible = false` only; book, tabs and drafts kept | None needed |
| 2 | `ReadButton_Click` (header and footer) | `OpenReaderAsync()` | **Before: no** (`_ = vm.OpenReaderAsync()`, fire-and-forget). **After slice 1: yes** | Clears `OpenErrorText`; `IsOpeningReader` true → false in `finally` | **Before:** none; a missing file returned silently and a reader failure was unobserved. **After:** localised `OpenErrorText` (`Detail.OpenError`), logged via `AppLog.ReaderOpenFailed`; shell returns to the inspector when `Reader.OpenAsync` reports failure |
| 3 | `EnrichButton_Click` | `EnrichMetadataAsync()` | Yes | `IsEnriching` reset | `EnrichmentStatusText` with **raw text** (VM l.1803) |
| 4 | `RunOcrButton_Click` | `RunOcrAsync()` | Yes | `IsQueueingOcr` reset | `OcrStatusText` with **raw text** (l.1854); queue result reason also shown verbatim |
| 5 | `ForgetPasswordButton_Click` | `ForgetPasswordAsync()` | Yes | none | `PasswordStatusText` with **raw text** (l.1889) |
| 6 | `ReadingMemoryField_LostFocus` | `SaveReadingMemoryAsync()` | Yes | `IsSavingReadingMemory` reset | `ReadingMemoryStatusText = ex.Message` (**raw**, l.1960); saves on every focus loss |
| 7 | `SaveReadingMemoryButton_Click` | `SaveReadingMemoryAsync()` | Yes | as 6 | as 6 |
| 8 | `LoadReadingHistoryButton_Click` | `LoadReadingHistoryAsync()` | Yes | `IsLoadingHistory` reset | `ReadingHistoryStatusText` with **raw text** (l.1602) |
| 9 | `LoadTocButton_Click` | `LoadTocAsync()` | Yes | `IsLoadingToc` reset | Localised status; not logged |
| 10 | `PrepareWriteBackButton_Click` | `PrepareWriteBackAsync()` | Yes | `IsPreparingWriteBack` reset; missing file → `MissingFileText` | Localised `WriteBack.Failed`; not logged |
| 11 | `ConfirmWriteBackButton_Click` | `ConfirmWriteBackAsync()` | Yes | `IsWritingWriteBack` reset | `WriteBackStatusText` with **raw text** (l.1421) |
| 12 | `RestoreWriteBackButton_Click` | `RestoreWriteBackAsync()` | Yes | busy flag reset | **Raw text** (l.1473) |
| 13 | `CancelWriteBackButton_Click` | `CancelWriteBack()` | Sync | Clears token, proposals and diff | Localised `WriteBack.Cancelled` |
| 14 | `LoadProvenanceButton_Click` | `LoadProvenance()` | Sync | Fills from loaded projection | None (no I/O) |
| 15 | `OpenProviderAttributionButton_Click` | `TopLevel.Launcher.LaunchUriAsync` | Yes | none | Fixed-host HTTPS allow-list; a launch failure is only logged by `UiActions` |
| 16 | `CurationStatusButton_Click` | `SetReadingStatusAsync` → `UpdateCurationAsync` | Yes | `IsUpdatingCuration` reset | `CurationStatusText` with **raw text** (l.2002) |
| 17 | `RatingButton_Click` | `SetRatingAsync` → `UpdateCurationAsync` | Yes | as 16 | as 16 |
| 18 | `FavouriteButton_Click` | `ToggleFavouriteAsync` → `UpdateCurationAsync` | Yes | as 16 | as 16 |
| 19 | `SaveTagsButton_Click` | `SaveTagsAsync()` | Yes | `IsSavingTags` reset | `TagsStatusText = ex.Message` for validation (**raw**, l.1642) and failure (l.1667) |
| 20 | `AcceptMetadataProposalButton_Click` | `AcceptMetadataProposalAsync` → review | Yes | `IsReviewingMetadata` reset | `MetadataReviewStatusText` with **raw text** (l.1741, l.2038) |
| 21 | `RejectMetadataProposalButton_Click` | `RejectMetadataProposalAsync` → review | Yes | as 20 | as 20 |

## Silent resets

- Changing the selected book (`Book` setter) clears history, contents, provenance, the
  prepared write-back and, from slice 1, `OpenErrorText`. Unsaved tag and reading-memory
  drafts are overwritten by the new book without a prompt (reading memory is saved on focus
  loss, so a draft is normally already persisted; tags are not).
- `Close()` hides the panel but does not cancel in-flight work; completions are guarded by a
  book-id check in write-back and (slice 1) Read only.
- Esc (`CatalogueShellView.axaml.cs:185`) closes only the drawer and search, not the inspector.

## Fire-and-forget calls

- `ReadButton_Click` was the only fire-and-forget handler. Slice 1 awaits it through
  `UiActions.Run` (`catalogue.read_button_click`).
- Other catches in the view model swallow without logging (they show status text only). They
  are not fire-and-forget, but they do not reach the Phase 02 log.

## Disposition

- Row 2: fixed in slice 1.
- Raw `ex.Message` rows 3–8, 11, 12, 16–21 and the unlogged catches: Phase 11 task 11.7/11.9
  (states and behaviour) per the phase document; the work order lists the 12 uses as known.
- Esc and draft loss on book change: Phase 11 slices covering 11.9 and 11.4–11.6.
