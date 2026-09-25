# Golden journey results

Run `phase01-baseline-r2`; filter `Category=E2E`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\Ogma-Library\.claude\worktrees\agent-a88fae4c2798e7386\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1280x800 | PASS |  | 28.5 | artifacts\e2e\phase01-baseline-r2\A11yNames\catalogue |
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 24.7 | artifacts\e2e\phase01-baseline-r2\A11yNames\catalogue |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1280x800 | PASS (baseline cleared) | K40, K41 (Phase 13) | 72.1 | artifacts\e2e\phase01-baseline-r2\A11yNames\search |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1920x1080 | BASELINE-FAIL | K40, K41 (Phase 13) | 120.1 | artifacts\e2e\phase01-baseline-r2\A11yNames\search |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1280x800 | PASS |  | 17.1 | artifacts\e2e\phase01-baseline-r2\G1 |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1920x1080 | PASS |  | 18.2 | artifacts\e2e\phase01-baseline-r2\G1 |
| G10 | G10_Organise_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G10 | G10_Organise_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G11 | G11_Classroom_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G11 | G11_Classroom_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G12 | G12_Shelf3D_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G12 | G12_Shelf3D_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | BASELINE-FAIL | K21, K20 (Phase 05) | 131.2 | artifacts\e2e\phase01-baseline-r2\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | BASELINE-FAIL | K21, K20 (Phase 05) | 136.2 | artifacts\e2e\phase01-baseline-r2\G2 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | BASELINE-FAIL | K32, K94 (Phase 04) | 91 | artifacts\e2e\phase01-baseline-r2\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | BASELINE-FAIL | K32, K94 (Phase 04) | 68.1 | artifacts\e2e\phase01-baseline-r2\G3 |
| G4 | G4_Search_ExpectedBookInTopThree | 1280x800 | BASELINE-FAIL | K40, K41 (Phase 13) | 203.9 | artifacts\e2e\phase01-baseline-r2\G4 |
| G4 | G4_Search_ExpectedBookInTopThree | 1920x1080 | BASELINE-FAIL | K40, K41 (Phase 13) | 203.3 | artifacts\e2e\phase01-baseline-r2\G4 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 125.7 | artifacts\e2e\phase01-baseline-r2\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 96.9 | artifacts\e2e\phase01-baseline-r2\G5 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | BASELINE-FAIL | K21 (Phase 05) | 120.9 | artifacts\e2e\phase01-baseline-r2\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | BASELINE-FAIL | K21 (Phase 05) | 128.4 | artifacts\e2e\phase01-baseline-r2\G6 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1280x800 | BASELINE-FAIL | G7 (Phases 15-16) | 29.1 | artifacts\e2e\phase01-baseline-r2\G7 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1920x1080 | BASELINE-FAIL | G7 (Phases 15-16) | 36.9 | artifacts\e2e\phase01-baseline-r2\G7 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | BASELINE-FAIL | K95, K28 (Phase 05) | 32.4 | artifacts\e2e\phase01-baseline-r2\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | BASELINE-FAIL | K95, K28 (Phase 05) | 41.5 | artifacts\e2e\phase01-baseline-r2\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | FAIL |  | 38.3 | artifacts\e2e\phase01-baseline-r2\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | FAIL |  | 45.8 | artifacts\e2e\phase01-baseline-r2\G8\worker-kill |
| G9 | G9_Metadata_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| G9 | G9_Metadata_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| Harness | AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements | n/a | PASS |  | 1.7 | artifacts\e2e\phase01-baseline-r2 |
| Harness | RecordDumpDetector_FlagsRecordToStringNames | n/a | PASS |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| Harness | WindowSize_ParsesStrictly | n/a | PASS |  | 0 | artifacts\e2e\phase01-baseline-r2 |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 245.7 | artifacts\e2e\phase01-baseline-r2\LaunchCycles |
| Smoke | Smoke_LaunchesReachesShellAndCloses | 1280x800 | PASS |  | 15.5 | artifacts\e2e\phase01-baseline-r2\Smoke |

## New failures
- **G8 1280x800** (G8_WorkerKilledDuringReading_AppSurvivesAndLogs): ArgumentException: Process with an Id of 31136 is not running.
- **G8 1920x1080** (G8_WorkerKilledDuringReading_AppSurvivesAndLogs): ArgumentException: Process with an Id of 40896 is not running.
