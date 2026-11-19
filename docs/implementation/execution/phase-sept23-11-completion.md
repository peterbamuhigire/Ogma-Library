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
