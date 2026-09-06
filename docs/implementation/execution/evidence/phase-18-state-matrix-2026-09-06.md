# Phase 18 Application State Matrix Evidence

Date: 2026-09-06

## Executable state coverage

| State | Surface and behavior | Executable evidence |
| --- | --- | --- |
| Empty | No-library shell remains actionable and localized | `SkeletonRenderTests.MainWindow_RendersAndCapturesScreenshot_English`; French counterpart |
| Loading | Bootstrap shell is visible before composition completes | `StartupShellRenderTests.BootstrapShell_RendersBeforeApplicationComposition` |
| Processing | Scan and index progress remain visible and bounded | `ScanProgressTests.MainWindow_AfterScan_ShowsScannedCount`; `SearchViewModelTests.IndexManager_RebuildButton_ShowsProgress` |
| Degraded | Required startup failure renders a focused recovery surface | `StartupShellRenderTests.RequiredFailure_RendersFocusedRecoverableDegradedShell` |
| Offline | Client mode shows and clears an explicit offline chip | `ShellReaderNavigationTests.MainShell_ClassroomOfflineChip_VisibleInClientModeAndClearsOnReconnect` |
| Error | Operational adapter text is mapped to stable localized UI state | `SearchViewModelTests.IndexManagerViewModel_DoesNotRenderRawOperationalErrors` |
| Retry | Startup and index controls expose state-safe retry/rebuild paths | `StartupShellRenderTests.RequiredFailure_RendersFocusedRecoverableDegradedShell`; index-manager tests |
| Ready | Optional failure keeps the usable catalogue visible | `StartupShellRenderTests.OptionalFailure_KeepsCatalogueVisibleBesideRecoveryPanel` |
| Command palette | Search, filtering, execution, and close behavior are typed | `SearchViewModelTests.CommandPalette_FiltersAndExecutesKnownCommands` |

## Boundary

This matrix closes repository-verifiable state behavior and command-palette
execution. Physical focus order, platform animation, high-DPI layout,
Narrator/VoiceOver announcement timing, and screenshot acceptance remain
`NOT ASSESSED`.

Focused state-matrix run: **28 passed, 0 failed, 0 skipped**. The Phase 18
font/design-system slice separately passed **5/5**, and the Release application
build completed with **0 warnings and 0 errors**.
