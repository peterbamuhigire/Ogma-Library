# Golden journey results

Run `co-reader-3`; filter `Category=E2E&(Journey=G3|Journey=G5|Journey=G8)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | PASS (baseline cleared) | K32, K94 (Phase 04) | 50.3 | artifacts\e2e\co-reader-3\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | BASELINE-FAIL | K32, K94 (Phase 04) | 51.9 | artifacts\e2e\co-reader-3\G3 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 51.4 | artifacts\e2e\co-reader-3\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 52.1 | artifacts\e2e\co-reader-3\G5 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | PASS |  | 13.4 | artifacts\e2e\co-reader-3\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | PASS |  | 12.5 | artifacts\e2e\co-reader-3\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 31.1 | artifacts\e2e\co-reader-3\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 29.8 | artifacts\e2e\co-reader-3\G8\worker-kill |
