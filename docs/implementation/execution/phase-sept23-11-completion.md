# Sept-23 Phase 11 completion: book detail inspector

Status: **IN PROGRESS** (2026-11-19). Slice 1 of 8 is done.
Branch: `kaizen/phase-11`. The autorun driver publishes it; this lane does not merge it.
Rollback point: `bde93dac`.
Plan: [phase-11](../../plans/sept-23-kaizen/phases/phase-11-book-detail-inspector.md).
Decisions: D-49 to D-55 in the [execution log](../../plans/sept-23-kaizen/EXECUTION-LOG.md).

## Typeface decision

Applied per D-55: **Spectral** for the inspector title, **Public Sans** elsewhere and
**JetBrains Mono** only inside Technical details. Slice 1 adds one caption-sized error line in
Public Sans through existing tokens; `Test-DesignTokens.ps1` is clean.

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
