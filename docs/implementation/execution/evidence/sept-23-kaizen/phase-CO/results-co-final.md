# Golden journey results

Run `co-final`; filter `Category=E2E`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1280x800 | PASS |  | 20.7 | artifacts\e2e\co-final\A11yNames\catalogue |
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 20.4 | artifacts\e2e\co-final\A11yNames\catalogue |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1280x800 | PASS |  | 24.4 | artifacts\e2e\co-final\A11yNames\search |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 24.4 | artifacts\e2e\co-final\A11yNames\search |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1280x800 | PASS |  | 13.2 | artifacts\e2e\co-final\G1 |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1920x1080 | PASS |  | 12.7 | artifacts\e2e\co-final\G1 |
| G10 | G10_Organise_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G10 | G10_Organise_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G11 | G11_Classroom_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G11 | G11_Classroom_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G12 | G12_Shelf3D_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G12 | G12_Shelf3D_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | PASS |  | 85 | artifacts\e2e\co-final\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | PASS |  | 86 | artifacts\e2e\co-final\G2 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | BASELINE-FAIL | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 52.5 | artifacts\e2e\co-final\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | BASELINE-FAIL | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 45.7 | artifacts\e2e\co-final\G3 |
| G4 | G4_Search_ExpectedBookInTopThree | 1280x800 | PASS |  | 35.9 | artifacts\e2e\co-final\G4 |
| G4 | G4_Search_ExpectedBookInTopThree | 1920x1080 | PASS |  | 35.7 | artifacts\e2e\co-final\G4 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 53.4 | artifacts\e2e\co-final\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 51.8 | artifacts\e2e\co-final\G5 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | PASS |  | 28.2 | artifacts\e2e\co-final\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | PASS |  | 28.2 | artifacts\e2e\co-final\G6 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1280x800 | PASS |  | 12.7 | artifacts\e2e\co-final\G7 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1920x1080 | PASS |  | 13.2 | artifacts\e2e\co-final\G7 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | PASS |  | 13.5 | artifacts\e2e\co-final\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | PASS |  | 15 | artifacts\e2e\co-final\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 36.4 | artifacts\e2e\co-final\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 34.9 | artifacts\e2e\co-final\G8\worker-kill |
| G9 | G9_Metadata_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| G9 | G9_Metadata_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\co-final |
| Harness | AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements | n/a | PASS |  | 2 | artifacts\e2e\co-final |
| Harness | RecordDumpDetector_FlagsRecordToStringNames | n/a | PASS |  | 0 | artifacts\e2e\co-final |
| Harness | Retry_NonTransientFailure_IsNotRetried | n/a | PASS |  | 0 | artifacts\e2e\co-final |
| Harness | Retry_PersistentStaleElement_RethrowsAfterThreeAttempts | n/a | PASS |  | 0 | artifacts\e2e\co-final |
| Harness | Retry_TwoComExceptionsThenSuccess_ReturnsItems | n/a | PASS |  | 0 | artifacts\e2e\co-final |
| Harness | WindowSize_ParsesStrictly | n/a | PASS |  | 0 | artifacts\e2e\co-final |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 108.2 | artifacts\e2e\co-final\LaunchCycles |
| Navigation | Navigation_CommandPaletteRunsCommandsAndCloses | 1280x800 | PASS |  | 15.8 | artifacts\e2e\co-final\Navigation |
| Navigation | Navigation_CommandPaletteRunsCommandsAndCloses | 1920x1080 | PASS |  | 14.9 | artifacts\e2e\co-final\Navigation |
| Navigation | Navigation_DrawersAreExclusiveAndStandaloneHasNoDeadEntries | 1280x800 | PASS |  | 16.2 | artifacts\e2e\co-final\Navigation |
| Navigation | Navigation_DrawersAreExclusiveAndStandaloneHasNoDeadEntries | 1920x1080 | PASS |  | 15.2 | artifacts\e2e\co-final\Navigation |
| Navigation | Navigation_EveryDestinationAndPrimaryActionIsReachable | 1280x800 | PASS |  | 16.2 | artifacts\e2e\co-final\Navigation |
| Navigation | Navigation_EveryDestinationAndPrimaryActionIsReachable | 1920x1080 | PASS |  | 15.8 | artifacts\e2e\co-final\Navigation |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1280x800 | PASS |  | 88.3 | artifacts\e2e\co-final\ScanOcrSearch |
| ScanOcrSearch | ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText | 1920x1080 | PASS |  | 69.6 | artifacts\e2e\co-final\ScanOcrSearch |
| Settings | Settings_EnvironmentOverride_IsShownDisabledWithTheReason | 1280x800 | PASS |  | 11.7 | artifacts\e2e\co-final\Settings |
| Settings | Settings_EnvironmentOverride_IsShownDisabledWithTheReason | 1920x1080 | PASS |  | 11.8 | artifacts\e2e\co-final\Settings |
| Settings | Settings_EverySection_OpensAndIsCaptured | 1280x800 | PASS |  | 16 | artifacts\e2e\co-final\Settings |
| Settings | Settings_EverySection_OpensAndIsCaptured | 1920x1080 | PASS |  | 15.6 | artifacts\e2e\co-final\Settings |
| Settings | Settings_SecondFolderAddedFromSettings_JoinsCatalogueAfterRescan | 1280x800 | PASS |  | 42.4 | artifacts\e2e\co-final\Settings |
| Settings | Settings_SecondFolderAddedFromSettings_JoinsCatalogueAfterRescan | 1920x1080 | PASS |  | 43 | artifacts\e2e\co-final\Settings |
| Settings | Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart | 1280x800 | PASS |  | 23 | artifacts\e2e\co-final\Settings |
| Settings | Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart | 1920x1080 | PASS |  | 25 | artifacts\e2e\co-final\Settings |
| Smoke | Smoke_LaunchesReachesShellAndCloses | 1280x800 | PASS |  | 14.1 | artifacts\e2e\co-final\Smoke |
