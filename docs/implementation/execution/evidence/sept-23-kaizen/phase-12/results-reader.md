# Golden journey results

Run `p12-reader`; filter `Category=E2E&(Tag=Reader)`; sizes 1280x800,1920x1080; themes Light, Dark; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | PASS (baseline cleared) | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 35.6 | artifacts\e2e\p12-reader\Light\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | PASS (baseline cleared) | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 35.5 | artifacts\e2e\p12-reader\Dark\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | PASS (baseline cleared) | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 38.9 | artifacts\e2e\p12-reader\Dark\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | PASS (baseline cleared) | K32, K94, K109 (Phase 04 (stall, K109 via Phase 06/24); Phase 12 (p95 budget)) | 39 | artifacts\e2e\p12-reader\Light\G3 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 47.6 | artifacts\e2e\p12-reader\Dark\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 46.3 | artifacts\e2e\p12-reader\Light\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 44.7 | artifacts\e2e\p12-reader\Light\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 43.6 | artifacts\e2e\p12-reader\Dark\G5 |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 41.2 | artifacts\e2e\p12-reader\Dark\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 27.4 | artifacts\e2e\p12-reader\Light\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 28.3 | artifacts\e2e\p12-reader\Light\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 26.8 | artifacts\e2e\p12-reader\Dark\G8\worker-kill |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1280x800 | PASS |  | 58.3 | artifacts\e2e\p12-reader\Light\J-READ-1 |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1280x800 | PASS |  | 59.2 | artifacts\e2e\p12-reader\Dark\J-READ-1 |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1280x800 | PASS |  | 56.8 | artifacts\e2e\p12-reader\Light\J-READ-1 |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1920x1080 | PASS |  | 67.5 | artifacts\e2e\p12-reader\Light\J-READ-1 |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1920x1080 | PASS |  | 64.2 | artifacts\e2e\p12-reader\Light\J-READ-1 |
| J-READ-1 | JRead1_Soak_TurnPagesWithinBudget | 1920x1080 | PASS |  | 68.8 | artifacts\e2e\p12-reader\Dark\J-READ-1 |
| J-READ-2 | JRead2_FindJumpAndBack | 1280x800 | PASS |  | 29.5 | artifacts\e2e\p12-reader\Dark\J-READ-2 |
| J-READ-2 | JRead2_FindJumpAndBack | 1280x800 | PASS |  | 26.7 | artifacts\e2e\p12-reader\Light\J-READ-2 |
| J-READ-2 | JRead2_FindJumpAndBack | 1280x800 | PASS |  | 28 | artifacts\e2e\p12-reader\Light\J-READ-2 |
| J-READ-2 | JRead2_FindJumpAndBack | 1920x1080 | PASS |  | 28.4 | artifacts\e2e\p12-reader\Dark\J-READ-2 |
| J-READ-2 | JRead2_FindJumpAndBack | 1920x1080 | PASS |  | 27.7 | artifacts\e2e\p12-reader\Light\J-READ-2 |
| J-READ-2 | JRead2_FindJumpAndBack | 1920x1080 | PASS |  | 29.2 | artifacts\e2e\p12-reader\Light\J-READ-2 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1280x800 | PASS |  | 28.5 | artifacts\e2e\p12-reader\Dark\J-READ-3 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1280x800 | PASS |  | 26.8 | artifacts\e2e\p12-reader\Light\J-READ-3 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1280x800 | PASS |  | 28.2 | artifacts\e2e\p12-reader\Light\J-READ-3 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1920x1080 | PASS |  | 29.2 | artifacts\e2e\p12-reader\Dark\J-READ-3 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1920x1080 | PASS |  | 27.8 | artifacts\e2e\p12-reader\Light\J-READ-3 |
| J-READ-3 | JRead3_LockedBook_WrongThenRightPassword | 1920x1080 | PASS |  | 29.5 | artifacts\e2e\p12-reader\Light\J-READ-3 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1280x800 | NOT ASSESSED |  | 27.1 | artifacts\e2e\p12-reader\Light\J-READ-4 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1280x800 | NOT ASSESSED |  | 27.2 | artifacts\e2e\p12-reader\Dark\J-READ-4 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1280x800 | NOT ASSESSED |  | 26.8 | artifacts\e2e\p12-reader\Light\J-READ-4 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1920x1080 | NOT ASSESSED |  | 27.7 | artifacts\e2e\p12-reader\Dark\J-READ-4 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1920x1080 | NOT ASSESSED |  | 26.3 | artifacts\e2e\p12-reader\Light\J-READ-4 |
| J-READ-4 | JRead4_SplitView_TwoBooks | 1920x1080 | NOT ASSESSED |  | 27 | artifacts\e2e\p12-reader\Light\J-READ-4 |
| J-READ-5 | JRead5_CitationExport_AllFormats | 1280x800 | PASS |  | 31.1 | artifacts\e2e\p12-reader\Light\J-READ-5 |
| J-READ-5 | JRead5_CitationExport_AllFormats | 1280x800 | PASS |  | 31.6 | artifacts\e2e\p12-reader\Dark\J-READ-5 |
| J-READ-5 | JRead5_CitationExport_AllFormats | 1920x1080 | PASS |  | 30.7 | artifacts\e2e\p12-reader\Dark\J-READ-5 |
| J-READ-5 | JRead5_CitationExport_AllFormats | 1920x1080 | PASS |  | 31.1 | artifacts\e2e\p12-reader\Light\J-READ-5 |
| J-READ-5 | JRead5_CitationExport_AllFormats | 1920x1080 | PASS |  | 32 | artifacts\e2e\p12-reader\Light\J-READ-5 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1280x800 | PASS |  | 44.9 | artifacts\e2e\p12-reader\Light\J-READ-6 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1280x800 | PASS |  | 44.7 | artifacts\e2e\p12-reader\Dark\J-READ-6 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1280x800 | PASS |  | 45.6 | artifacts\e2e\p12-reader\Light\J-READ-6 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1920x1080 | PASS |  | 42.8 | artifacts\e2e\p12-reader\Dark\J-READ-6 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1920x1080 | PASS |  | 43.9 | artifacts\e2e\p12-reader\Light\J-READ-6 |
| J-READ-6 | JRead6_QuitAndReopen_ExactPageResumes | 1920x1080 | PASS |  | 44.2 | artifacts\e2e\p12-reader\Light\J-READ-6 |
| J-READ-AREA | JReadArea_PageAreaShare | 1280x800 | PASS |  | 25.9 | artifacts\e2e\p12-reader\Dark\J-READ-AREA |
| J-READ-AREA | JReadArea_PageAreaShare | 1280x800 | PASS |  | 28.4 | artifacts\e2e\p12-reader\Light\J-READ-AREA |
| J-READ-AREA | JReadArea_PageAreaShare | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\p12-reader\Dark\J-READ-AREA |
| J-READ-AREA | JReadArea_PageAreaShare | 1920x1080 | NOT ASSESSED |  | 0 | artifacts\e2e\p12-reader\Light\J-READ-AREA |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1280x800 | NOT ASSESSED |  | 24.6 | artifacts\e2e\p12-reader\Light\J-READ-N1 |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1280x800 | NOT ASSESSED |  | 24.9 | artifacts\e2e\p12-reader\Dark\J-READ-N1 |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1280x800 | NOT ASSESSED |  | 24.3 | artifacts\e2e\p12-reader\Light\J-READ-N1 |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1920x1080 | NOT ASSESSED |  | 24 | artifacts\e2e\p12-reader\Dark\J-READ-N1 |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1920x1080 | NOT ASSESSED |  | 25.6 | artifacts\e2e\p12-reader\Light\J-READ-N1 |
| J-READ-N1 | JReadN1_TruncatedPdf_NoCrashNoPage | 1920x1080 | NOT ASSESSED |  | 24.1 | artifacts\e2e\p12-reader\Light\J-READ-N1 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1280x800 | PASS |  | 26.3 | artifacts\e2e\p12-reader\Light\J-READ-N2 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1280x800 | PASS |  | 25.6 | artifacts\e2e\p12-reader\Light\J-READ-N2 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1280x800 | PASS |  | 27.8 | artifacts\e2e\p12-reader\Dark\J-READ-N2 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1920x1080 | PASS |  | 26 | artifacts\e2e\p12-reader\Light\J-READ-N2 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1920x1080 | PASS |  | 25.5 | artifacts\e2e\p12-reader\Light\J-READ-N2 |
| J-READ-N2 | JReadN2_CancelledPasswordPrompt_NoReader | 1920x1080 | PASS |  | 26.1 | artifacts\e2e\p12-reader\Dark\J-READ-N2 |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1280x800 | PASS |  | 26.9 | artifacts\e2e\p12-reader\Light\J-READ-SHARP |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1280x800 | PASS |  | 28.1 | artifacts\e2e\p12-reader\Light\J-READ-SHARP |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1280x800 | PASS |  | 28.5 | artifacts\e2e\p12-reader\Dark\J-READ-SHARP |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1920x1080 | PASS |  | 27.6 | artifacts\e2e\p12-reader\Light\J-READ-SHARP |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1920x1080 | PASS |  | 28.7 | artifacts\e2e\p12-reader\Dark\J-READ-SHARP |
| J-READ-SHARP | JReadSharp_TextSharpnessAt100Percent | 1920x1080 | PASS |  | 26.5 | artifacts\e2e\p12-reader\Light\J-READ-SHARP |
