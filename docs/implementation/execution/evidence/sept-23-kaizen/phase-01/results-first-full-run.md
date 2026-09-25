# Golden journey results

Run `phase01-baseline`; filter `Category=E2E`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\Ogma-Library\.claude\worktrees\agent-a88fae4c2798e7386\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1280x800 | PASS |  | 22.6 | artifacts\e2e\phase01-baseline\A11yNames\catalogue |
| A11yNames | CatalogueItems_HaveReadableAccessibleNames | 1920x1080 | PASS |  | 22.1 | artifacts\e2e\phase01-baseline\A11yNames\catalogue |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1280x800 | BASELINE-FAIL | K40, K41 (Phase 13) | 120 | artifacts\e2e\phase01-baseline\A11yNames\search |
| A11yNames | SearchResults_HaveReadableAccessibleNames | 1920x1080 | BASELINE-FAIL | K40, K41 (Phase 13) | 133.5 | artifacts\e2e\phase01-baseline\A11yNames\search |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1280x800 | PASS |  | 16.8 | artifacts\e2e\phase01-baseline\G1 |
| G1 | G1_FirstRun_EmptyStateAndCallToActionAreVisible | 1920x1080 | PASS |  | 15.3 | artifacts\e2e\phase01-baseline\G1 |
| G10 | G10_Organise_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G10 | G10_Organise_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G11 | G11_Classroom_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G11 | G11_Classroom_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G12 | G12_Shelf3D_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G12 | G12_Shelf3D_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | FAIL |  | 32.1 | artifacts\e2e\phase01-baseline\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | BASELINE-FAIL | K21, K20 (Phase 05) | 135.1 | artifacts\e2e\phase01-baseline\G2 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | BASELINE-FAIL | K32, K94 (Phase 04) | 91.7 | artifacts\e2e\phase01-baseline\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | BASELINE-FAIL | K32, K94 (Phase 04) | 86.8 | artifacts\e2e\phase01-baseline\G3 |
| G4 | G4_Search_ExpectedBookInTopThree | 1280x800 | BASELINE-FAIL | K40, K41 (Phase 13) | 207.8 | artifacts\e2e\phase01-baseline\G4 |
| G4 | G4_Search_ExpectedBookInTopThree | 1920x1080 | BASELINE-FAIL | K40, K41 (Phase 13) | 227.6 | artifacts\e2e\phase01-baseline\G4 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 101 | artifacts\e2e\phase01-baseline\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 88.7 | artifacts\e2e\phase01-baseline\G5 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | BASELINE-FAIL | K21 (Phase 05) | 140 | artifacts\e2e\phase01-baseline\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | BASELINE-FAIL | K21 (Phase 05) | 128.3 | artifacts\e2e\phase01-baseline\G6 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1280x800 | BASELINE-FAIL | G7 (Phases 15-16) | 36.4 | artifacts\e2e\phase01-baseline\G7 |
| G7 | G7_Advisor_UnconfiguredShowsRouteToSettings | 1920x1080 | BASELINE-FAIL | G7 (Phases 15-16) | 32.4 | artifacts\e2e\phase01-baseline\G7 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | BASELINE-FAIL | K95, K28 (Phase 05) | 34.4 | artifacts\e2e\phase01-baseline\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | BASELINE-FAIL | K95, K28 (Phase 05) | 35.6 | artifacts\e2e\phase01-baseline\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | BASELINE-FAIL | K30 (Phase 04) | 283 | artifacts\e2e\phase01-baseline\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | BASELINE-FAIL | K30 (Phase 04) | 81.1 | artifacts\e2e\phase01-baseline\G8\worker-kill |
| G9 | G9_Metadata_Placeholder | 1280x800 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| G9 | G9_Metadata_Placeholder | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\phase01-baseline |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 253.2 | artifacts\e2e\phase01-baseline\LaunchCycles |
| Smoke | Smoke_LaunchesReachesShellAndCloses | 1280x800 | PASS |  | 16.4 | artifacts\e2e\phase01-baseline\Smoke |

## New failures
- **G2 1280x800** (G2_AddLibrary_ScanShowsValidBooksWithCovers): EqualException: Assert.Equal() Failure: Values differ
Expected: Dialog
Actual:   Hook
