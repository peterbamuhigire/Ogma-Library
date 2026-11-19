# Golden journey results

Run `co-reader-1`; filter `Category=E2E&(Journey=G3|Journey=G5|Journey=G8)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | PASS (baseline cleared) | K32, K94 (Phase 04) | 46.9 | artifacts\e2e\co-reader-1\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | PASS (baseline cleared) | K32, K94 (Phase 04) | 46.3 | artifacts\e2e\co-reader-1\G3 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 50.2 | artifacts\e2e\co-reader-1\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 52.2 | artifacts\e2e\co-reader-1\G5 |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1280x800 | PASS |  | 14 | artifacts\e2e\co-reader-1\G8\corrupt-settings |
| G8 | G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage | 1920x1080 | PASS |  | 12.9 | artifacts\e2e\co-reader-1\G8\corrupt-settings |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | PASS |  | 30.4 | artifacts\e2e\co-reader-1\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | PASS |  | 29.5 | artifacts\e2e\co-reader-1\G8\worker-kill |
