# Sept-23 Phase 11 completion: book detail inspector

Status: **IN PROGRESS** (2026-11-19). Slices 1 to 4 of 8 are done.
Branch: `kaizen/phase-11`. The autorun driver publishes it; this lane does not merge it.
Rollback point: `bde93dac`.
Plan: [phase-11](../../plans/sept-23-kaizen/phases/phase-11-book-detail-inspector.md).
Decisions: D-49 to D-71 in the [execution log](../../plans/sept-23-kaizen/EXECUTION-LOG.md).

## Typeface decision

Applied per D-55: **Spectral** for the inspector title, **Public Sans** elsewhere and
**JetBrains Mono** only inside Technical details. Slice 1 adds one caption-sized error line in
Public Sans through existing tokens. Slice 2 sets the title in Spectral (`ogma-display`,
`Type.Size.Title`, at most 2 lines with a tooltip) and puts path, size and SHA-256 in JetBrains
Mono (`ogma-mono`) inside the collapsed Technical details expander only. `Test-DesignTokens.ps1`
is clean after both slices.

## Slice 1: click-path audit, awaited Read and primary-action state machine

| Task | Result | Evidence |
|---|---|---|
| 11.1 click-path audit | 21 handlers traced. One fire-and-forget (`ReadButton_Click`), now awaited. 14 handlers surface raw `ex.Message`; they are left for 11.7/11.9 | [click-path-audit.md](evidence/sept-23-kaizen/phase-11/click-path-audit.md) (CODE) |
| 11.8 awaited Read | `ReadButton_Click` uses `UiActions.Run`. `OpenReaderAsync` preflights with `IBookFileLocator`, does not call the reader when the file is missing, logs reader failures through `AppLog.ReaderOpenFailed` and shows a localised `OpenErrorText` (`Detail.OpenError`). It also adds `IsOpeningReader` | `BookDetailPrimaryActionTests` (MEASURED, headless) |
| Shell | When `Reader.OpenAsync` reports failure for a Read from the inspector, `MainShellViewModel.OpenReaderAsync` returns to the catalogue, re-shows the inspector and calls `BookDetail.ReportReaderOpenFailed`. The success path is unchanged | `ShellReaderNavigationTests.MainShell_OpenReaderFromInspectorFails_ReturnsToInspectorWithLocalisedError` (MEASURED, headless) |
| 11.2 view-model part | Adds `PrimaryActionKind { None, Read, ContinueReading, LocateFile, Unlock }`, `PrimaryActionText` ("Continue reading · p. N", one-based from the zero-based `CurrentPage`), `CanExecutePrimaryAction` and `ExecutePrimaryActionAsync`. `LocateFile` and `Unlock` are stubs with honest localised messages; slice 6 completes them | One fixture per kind, plus culture change |
| Strings | Nine EN and FR keys: `Catalogue.BookDetail.Primary.*` and `Catalogue.BookDetail.Open.*` | Key parity in the fast suite |

**Premise correction.** The work order says "if `Reader.OpenAsync` throws". Since Phase 02
(T02.2), `ReaderViewModel.OpenAsync` does not throw. It logs the failure and returns `false`,
and it still throws on cancellation. The shell therefore acts on `false`. It returns to the
inspector only when the Read came from the inspector, meaning the inspector was visible and
showed that book. Other callers (grid, search) keep the reader's own error state, so their
behaviour is unchanged.

**Precedence.** The order is no book → None, then file unavailable → LocateFile, then
password-protected → Unlock, then saved page > 0 → ContinueReading, then Read.

Gates on the slice-1 tree:
- MEASURED, passing: the Release build (0 warnings), `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean) and `Test-Fast.ps1` (Architecture 52, Tests 1,365, Ui 418;
  0 failed).
- Not run in slice 1 (NOT_ASSESSED): the real-window journey (deleted file → in-panel error)
  at both sizes and screenshots. They are planned for the phase's journey slice.

## Slice 2: header, overflow menu and four sections

| Task | Result | Evidence |
|---|---|---|
| Header | Compact 56×80 cover, Spectral title (`Detail.Title`), authors (`Detail.Authors`, 2 lines, trimmed, tooltip), year, Close. One `Button.primary` (`Detail.Action.Read`) bound to `PrimaryActionText` and enabled by `CanExecutePrimaryAction`. Status ComboBox, rating 1-5 and favourite moved up (D-57) | `Phase11InspectorStructureTests` header and primary-action tests (MEASURED, headless) |
| Overflow | `Detail.Action.More` `MenuFlyout`: Enrich, Run OCR, Add to collection (submenu), Write metadata to PDF…, Show in folder, Forget password. *Remove from library* is omitted, registered as K122 (D-58, D-59) | `OverflowMenu_OffersSecondaryActionsWithAccessibleNames` (MEASURED) |
| Sections | `Border.segmented` (`Detail.Sections`) with four `Button.segment`s driven by `SelectedSection`. Placement per D-61; Technical details is collapsed by default | `FourSegments_*`, `IsbnLivesUnderDetails`, `HashIsHidden_*`, `SegmentRow_IsASingleRow` at 360/520 (MEASURED) |
| Footer | Removed; Enrich, Run OCR and Forget password are reachable from the overflow menu | Structure tests |
| Projection | No description, subjects or shelves fields exist; nothing is invented (D-60) | Code: `BookDetailProjection` |
| Updated tests | `BookDetailCurationTests` selects sections instead of tabs. G02 E2E activates `Detail.Section.Details`. `BookDetailFileAndProvenanceTests` and `Sept23Phase09TabOverflowTests` pass unchanged | Targeted filter: 149 passed |
| Strings | Eleven EN and FR keys (`Catalogue.BookDetail.Section.*`, `More`, `Technical*`, `AddToShelf`, `WriteMetadata`, `ShowInFolder`) | Key parity in the fast suite |

Frames (headless, MEASURED render, not a real window): `inspector-360-light.png`,
`inspector-360-dark.png`, `inspector-520-light.png` and `inspector-520-dark.png` in
[evidence/sept-23-kaizen/phase-11](evidence/sept-23-kaizen/phase-11/). The panel stays 360 px
wide at 520 (D-62).

Gates on the slice-2 tree:
- MEASURED, passing: the Release build (0 warnings), `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean), `Test-Fast.ps1` (Architecture 52, Tests 1,365, Ui 431;
  0 failed) and the targeted Phase11/BookDetail/Sept23Phase09 filter (149 passed).
- NOT_ASSESSED (not run in this slice): the real-window journey, the G02 E2E run, 200% text,
  Arabic RTL, the screen-reader walkthrough and the keyboard path through the overflow menu.

## Slice 3: inline metadata editing with provenance and undo (11.4)

| Task | Result | Evidence |
|---|---|---|
| Field VM | `DetailFieldViewModel` with Display, Edit, Saving and Error states. Validation comes from `MetadataFieldEditRules`, and errors are localised inline (`Detail.Field.{Name}.Error`, assertive live region). An unchanged value is not written | `DetailFieldEditTests` VM cases (MEASURED) |
| Keys | Enter or blur saves; Esc cancels, is marked handled so the inspector stays open, and returns focus to the field | `Phase11DetailFieldEditTests` Enter/Esc/invalid-year (MEASURED, headless keyboard) |
| Undoable save | `MetadataFieldEditUndoableCommand` (captures a snapshot, then updates) runs through the `ICommandHistory` bound in `ShellModule`. The panel shows *Undo* (`Detail.Action.UndoFieldEdit`) with the field named (D-64) | SQLite round-trip and undo for Title, Year, ISBN, DOI and Author (MEASURED) |
| Write path | User row plus canonical column mirror and audit events (D-63). Invalid values throw before any write | `DetailFieldEditTests` SQLite cases |
| Provenance | Per-field `Flyout` on a named, focusable info button (`Detail.Field.{Name}.Provenance`), loaded on first open. It shows source, confidence and "Edited by you". *Load provenance* remains in Activity as the fallback | Provenance headless test (MEASURED) |
| Refresh | After a save or undo the detail is re-read and `Book` is set on the UI thread (`ConfigureAwait(false)`, then `UpdateOnUiThread`). Failures are logged with `AppLog.ViewModelOperationFailed` and shown only as localised text | Write-failure test |
| Strings | `Catalogue.BookDetail.Edit.*` keys in EN and FR | Key parity in the fast suite |
| Updated test | `IsbnLivesUnderDetails` now finds the ISBN as an editable field under Details (updated, not deleted) | Ui Phase11 filter |

Typeface: there is no new face. Field labels, values, editors and errors use Public Sans through
existing tokens. The title stays Spectral, and JetBrains Mono stays inside Technical details only.

Gates on the slice-3 tree:
- MEASURED, passing: the Release build (0 warnings), `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean), Phase11 filters (Tests 42, Ui 17) and `Test-Fast.ps1`
  (Architecture 52, Tests 1,380, Ui 435; 0 failed).
- NOT_ASSESSED (not run in this slice): real-window inline editing at both sizes, the
  screen-reader walkthrough of the editor and Flyout, 200% text, Arabic RTL and screenshots.

## Slice 4: enrichment review card, accept-all with undo, and the raw-error purge (11.6, 11.7)

| Task | Result | Evidence |
|---|---|---|
| Review card | Under Activity, a `Detail.Review.Card` Border is named "N suggested changes" (`Detail.Review.Title`). Each row shows the localised field, "current → proposed", source and confidence, plus named Accept and Reject buttons (`Detail.Review.Accept.{Field}`, `Detail.Review.Reject.{Field}`). The status line is a polite live region (`Detail.Review.Status`) | `ReviewCard_RendersItsCount_AndNamedButtons` (MEASURED, headless) |
| Activity badge | `Detail.Section.Activity.Badge` shows the count. The segment is named "Activity, N suggested changes" and the badge hides when nothing is pending | Same test |
| Accept all and Undo | *Accept all* calls `PreviewAsync`, then `ApplyAsync` with actor `local-user` (D-68), and keeps `BatchId` and `UndoToken`. *Undo* (`Detail.Review.Undo`) calls `UndoAsync` and reloads the detail. A single unedited Accept takes the same path with one id; an edited Accept and every Reject stay on `DecideAsync` (D-67) | `AcceptAll_ThenUndo_RestoresThePriorValues`, `SingleAccept_GoesThroughTheBulkService_AndIsUndoable`, `Reject_StaysOnTheSingleDecisionService` (MEASURED) |
| Partial failure and conflict | Fields that were not applied are listed by localised label and logged; the user never sees the service's text. An undo conflict shows `Review.UndoConflict` and retires the token (D-69) | `PartialFailure_IsReportedPerField_WithoutRawErrors`, `UndoConflict_ShowsALocalisedExplanation` (MEASURED) |
| Raw-error purge | All 12 `ex.Message` sites in `BookDetailViewModel.cs` now log through `AppLog.ViewModelOperationFailed` and show actionable localised text. Service-reported failures log through the new event 3005 (D-70). The eight `*FailedFormat` keys no longer have `{0}` in EN or FR | `Phase11NoRawErrorTests`: 16 failing paths in EN and fr-FR (MEASURED). `Select-String … 'ex\.Message'` on the file returns nothing |
| No-metadata state | `Detail.State.NoMetadata` in Overview offers *Enrich* (`.Enrich`) when lookup is wired; otherwise `.Hint` points to Settings (D-71) | `NoMetadata_OffersEnrich_OrPointsToSettings` (true/false) |
| Wiring | `ShellModule` passes `IMetadataBulkReviewService` to the inspector | Release build |
| Updated tests | Three Phase 06 `BookDetailViewModelTests` assertions that expected raw enrich/OCR text now expect the localised copy (updated, not deleted) | Fast suite |
| Strings | `Catalogue.BookDetail.Review.*`, `NoMetadata.*`, `MetadataReview.LoadFailed`, `Curation.TagsTooMany`/`TagTooLong` and `ReadingMemory.SaveFailed`, all in EN and FR | Key parity in the fast suite |

Typeface: there is no new face. The card, badge and no-metadata state use Public Sans through
existing tokens (`Type.Size.Caption`, `Brush.Surface.*`, `Brush.Accent.Primary`/`OnAccent`,
`Radius.S`). The title stays Spectral, and JetBrains Mono stays inside Technical details only.

New defect: K123. Raw `ex.Message` remains in `HostSharingViewModel` and
`StudentSmartSearchViewModel`; this is outside the inspector and targeted at Phases 19 and 20.

Gates on the slice-4 tree:
- MEASURED, passing: the Release build (0 warnings), `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean), Phase11 filters (Tests 42, Ui 41) and `Test-Fast.ps1`
  (Architecture 52, Tests 1,380, Ui 459; 0 failed).
- NOT_ASSESSED (not run in this slice): the real-window review journey at both sizes,
  screenshots, 200% text, Arabic RTL, and the screen-reader and keyboard walkthrough of the card.

## Slice 5: two-step write-back consent with backup and restore (11.6)

| Task | Result | Evidence |
|---|---|---|
| Dialog | `WriteBackDialog` (`WriteBack.Dialog`) opens from *Write metadata to PDF…* after `PrepareWriteBackAsync` succeeds. Step 1, "Review changes", shows the plain-language diff (`WriteBack.Diff`) and the backup path (`WriteBack.BackupPath`); *Cancel* (`WriteBack.Cancel`) takes the focus. Step 2, "Confirm", shows *Back* (focused) and *Write to PDF* (`WriteBack.Confirm`); neither is a default button. The result step shows a live status (`WriteBack.Status`), *Restore original* and *Close* | `Phase11WriteBack_DialogStepsAreNamedAndCancelHasInitialFocus` (MEASURED, headless) |
| No write before consent | `WriteAsync` runs only from the step-2 confirm | `Phase11WriteBack_NoWriteBeforeStepTwoConfirm` (MEASURED) |
| Cancel and Esc | *Cancel*, Esc and closing from the title bar all discard an unconfirmed token without writing (D-73) | `…CancelDiscardsTokenWithoutWriting`, `…ClosingBeforeConfirmDiscardsToken`, `…EscapeCancelsAndDiscardsToken` (MEASURED) |
| Restore after close | The token survives a successful write. The Activity card (`Detail.WriteBack.Card`, `Detail.WriteBack.Restore`, "Restore original") calls `RestoreBackupAsync` with the same token (D-72) | `…RestoreAfterDialogClosesCallsRestoreBackup`, `…InspectorHasNoInlineBlockAndShowsRestoreUnderActivity` (MEASURED) |
| Failures | A write that returns false shows `WriteBack.Failed`; a throw shows `WriteBack.FailedFormat`. Both use error styling, and no raw text appears | `…WriteReturningFalseSurfacesLocalisedError`, `…WriteThrowingShowsNoRawExceptionText` (MEASURED) |
| Integration | The real `PdfWriteBackService` writes a fixture copy and restores it; the SHA-256 matches the original. With the file set read-only, the write returns false and the bytes do not change | `Phase11WriteBackIntegrationTests` (2, MEASURED) |
| Inline block removed | The Details write-back preview and its three buttons are gone; the status line moved to Activity (`Detail.WriteBack.Status`) | Headless test above |
| Strings | 15 new `Catalogue.BookDetail.WriteBack.*` keys, plus reworded `Confirm` and `DiffFormat`, in EN and FR | `…FrenchStringsAreLocalised`; key parity in the fast suite |

Typeface: there is no new face. The dialog's step title uses `TextBlock.heading` (Spectral).
The body and the backup path use Public Sans. JetBrains Mono is not used. Colours, sizes and
radii come from tokens (`Brush.Text.*`, `Brush.Status.Danger`, `Brush.Surface.*`,
`Type.Size.Small`/`Caption`, `Radius.S`/`M`).

New defect: K124. The restore point is not reloaded from the durable `WriteBackPlan` after a
book switch or a restart. It is targeted at Phase 11 or 15.

Gates on the slice-5 tree:
- MEASURED, passing: the Release build (0 warnings), `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean), Phase11 and write-back filters (Tests 58, Ui 54) and
  `Test-Fast.ps1` (Architecture 52, Tests 1,382, Ui 469; 0 failed).
- NOT_ASSESSED (not run in this slice): the real-window write-back journey on a synthetic
  fixture copy, screenshots at both widths, light and dark, 200% text, Arabic RTL, and the
  screen-reader and keyboard walkthrough of the dialog.

## Slice 6: explicit states and hash-verified file relink (11.7)

| Task | Result | Evidence |
|---|---|---|
| States | `InspectorState` (None, Loading, Ready, MissingFile, PasswordProtected, Processing, ProcessingFailed, NoMetadata) by the D-77 priority. The view has one template per state: `Detail.State.Loading` (a token skeleton, no spinner), `Detail.State.MissingFile`, `Detail.State.Password` (reason plus *Forget password*), `Detail.State.Processing` (live `TextStatusText` and `OcrStatusText`), `Detail.State.Failed` (reason plus *Retry*) and the existing `Detail.State.NoMetadata` | `Phase11/InspectorStateTests` (VM, MEASURED); `Phase11InspectorStateViewTests` (headless, MEASURED; one frame per state in `artifacts/phase-11/inspector-state-*.png`) |
| Primary action | MissingFile: *Locate file…*. Password: *Unlock*, disabled with a reason (D-76, K125). Other states: *Read* or *Continue reading* | Same tests |
| Relink | `IBookRelinkService.RelinkAsync` returns Relinked, HashMismatch, OutsideLibraryRoots, NotFound or Failed. `BookRelinkService` checks the enabled roots through `PathGuard`, computes SHA-256, updates the file row and book status and writes the `BookFileRelinked` audit event in one save. Bound in `CatalogueProcessingModule` and passed by `ShellModule`. The view's storage-provider PDF picker feeds `LocateFilePicker`. Outcomes are localised in `Detail.RelinkStatus` and never show the path | `Phase11/BookRelinkServiceTests`: match, mismatch, outside, disabled root, `..` traversal, missing path, unknown book, empty or relative path (MEASURED) |
| Deleted while open | A failed Read re-evaluates to MissingFile; a refresh after the file returns gives Ready (D-80) | `InspectorStateTests` (MEASURED) |
| Strings | 15 new `Catalogue.BookDetail.State.*` and `.Relink.*` keys, plus "Locate file…", in EN and FR | Key parity in the fast suite |

Typeface: there is no new face. Card titles and body text use Public Sans, and the title stays Spectral.
JetBrains Mono is not used. Colours, sizes and radii come from tokens (`Brush.Surface.Footer`,
`Brush.Surface.Sunken`, `Brush.Surface.CardBorder`, `Brush.Surface.OnParchment*`,
`Type.Size.Caption`, `Radius.S`).

New defect: K125, the protected-book *Unlock*, targeted at Phase 12.

Gates on the slice-6 tree:
- MEASURED, passing: the Release build, `dotnet format --verify-no-changes`,
  `Test-DesignTokens.ps1` (clean), Phase11 filters (Tests 77, Ui 59), Architecture (52) and
  `Test-Fast.ps1` (Architecture 52, Tests 1,415, Ui 477; 0 failed).
- NOT_ASSESSED (not run in this slice): the real-window relink journey on the synthetic corpus,
  screenshots at 1280×800 and 1920×1080, light and dark, 200% text, Arabic RTL, and the
  screen-reader and keyboard walkthrough of the state cards.