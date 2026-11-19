# Golden journey results

Run `co-all-3`; filter `Category=E2E`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1280x800 | PASS |  | 23.1 | artifacts\e2e\co-all-3\A11yNames\catalogue |
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 21.3 | artifacts\e2e\co-all-3\A11yNames\catalogue |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1280x800 | PASS |  | 25.1 | artifacts\e2e\co-all-3\A11yNames\search |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 24.4 | artifacts\e2e\co-all-3\A11yNames\search |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1280x800 | PASS |  | 13.9 | artifacts\e2e\co-all-3\G1 |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1920x1080 | PASS |  | 14.2 | artifacts\e2e\co-all-3\G1 |
| G10 | G10_Organise_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G10 | G10_Organise_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G11 | G11_Classroom_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G11 | G11_Classroom_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G12 | G12_Shelf3D_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G12 | G12_Shelf3D_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | PASS |  | 34.1 | artifacts\e2e\co-all-3\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | PASS |  | 34 | artifacts\e2e\co-all-3\G2 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | BASELINE-FAIL | K32, K94 (Phase 04) | 46.7 | artifacts\e2e\co-all-3\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | PASS (baseline cleared) | K32, K94 (Phase 04) | 52.8 | artifacts\e2e\co-all-3\G3 |
| G4 | G4_Search_ExpectedBookInTopThree | 1280x800 | PASS |  | 70 | artifacts\e2e\co-all-3\G4 |
| G4 | G4_Search_ExpectedBookInTopThree | 1920x1080 | PASS |  | 71 | artifacts\e2e\co-all-3\G4 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 51.8 | artifacts\e2e\co-all-3\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 58.4 | artifacts\e2e\co-all-3\G5 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | PASS |  | 30.6 | artifacts\e2e\co-all-3\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | PASS |  | 29.7 | artifacts\e2e\co-all-3\G6 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1280x800 | PASS |  | 15.7 | artifacts\e2e\co-all-3\G7 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1920x1080 | PASS |  | 15.2 | artifacts\e2e\co-all-3\G7 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | PASS |  | 13.7 | artifacts\e2e\co-all-3\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | PASS |  | 14.5 | artifacts\e2e\co-all-3\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 32.7 | artifacts\e2e\co-all-3\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 32.3 | artifacts\e2e\co-all-3\G8\worker-kill |
| G9 | G9_Metadata_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| G9 | G9_Metadata_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-all-3 |
| Harness | AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements | n/a | PASS |  | 1.4 | artifacts\e2e\co-all-3 |
| Harness | RecordDumpDetector_FlagsRecordToStringNames | n/a | PASS |  | 0 | artifacts\e2e\co-all-3 |
| Harness | Retry_NonTransientFailure_IsNotRetried | n/a | PASS |  | 0 | artifacts\e2e\co-all-3 |
| Harness | Retry_PersistentStaleElement_RethrowsAfterThreeAttempts | n/a | PASS |  | 0 | artifacts\e2e\co-all-3 |
| Harness | Retry_TwoComExceptionsThenSuccess_ReturnsItems | n/a | PASS |  | 0 | artifacts\e2e\co-all-3 |
| Harness | WindowSize_ParsesStrictly | n/a | PASS |  | 0 | artifacts\e2e\co-all-3 |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 120.4 | artifacts\e2e\co-all-3\LaunchCycles |
| Navigation | Navigation_CommandPaletteRunsCommandsAndCloses | 1280x800 | PASS |  | 14.1 | artifacts\e2e\co-all-3\Navigation |
| Navigation | Navigation_CommandPaletteRunsCommandsAndCloses | 1920x1080 | PASS |  | 14.6 | artifacts\e2e\co-all-3\Navigation |
| Navigation | Navigation_DrawersAreExclusiveAndStandaloneHasNoDeadEntries | 1280x800 | PASS |  | 15.1 | artifacts\e2e\co-all-3\Navigation |
| Navigation | Navigation_DrawersAreExclusiveAndStandaloneHasNoDeadEntries | 1920x1080 | PASS |  | 15.4 | artifacts\e2e\co-all-3\Navigation |
| Navigation | Navigation_EveryDestinationAndPrimaryActionIsReachable | 1280x800 | PASS |  | 16.8 | artifacts\e2e\co-all-3\Navigation |
| Navigation | Navigation_EveryDestinationAndPrimaryActionIsReachable | 1920x1080 | PASS |  | 16.3 | artifacts\e2e\co-all-3\Navigation |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1280x800 | PASS (baseline cleared) | K97, K40 (Phase 13) | 47 | artifacts\e2e\co-all-3\ScanOcrSearch |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1920x1080 | PASS (baseline cleared) | K97, K40 (Phase 13) | 63.2 | artifacts\e2e\co-all-3\ScanOcrSearch |
| Settings | Settings_EnvironmentOverride_IsShownDisabledWithTheReason | 1280x800 | PASS |  | 12.6 | artifacts\e2e\co-all-3\Settings |
| Settings | Settings_EnvironmentOverride_IsShownDisabledWithTheReason | 1920x1080 | PASS |  | 13.5 | artifacts\e2e\co-all-3\Settings |
| Settings | Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart | 1280x800 | PASS |  | 27.7 | artifacts\e2e\co-all-3\Settings |
| Settings | Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart | 1920x1080 | PASS |  | 27.5 | artifacts\e2e\co-all-3\Settings |
| Smoke | Smoke_LaunchesReachesShellAndCloses | 1280x800 | PASS |  | 14.7 | artifacts\e2e\co-all-3\Smoke |
