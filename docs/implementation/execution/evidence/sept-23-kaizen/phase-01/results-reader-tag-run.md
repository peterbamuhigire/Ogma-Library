# Golden journey results

Run `phase01-reader-r3`; filter `Category=E2E&(Tag=Reader)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\Ogma-Library\.claude\worktrees\agent-a88fae4c2798e7386\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1280x800 | BASELINE-FAIL | K32, K94 (Phase 04) | 84.3 | artifacts\e2e\phase01-reader-r3\G3 |
| G3 | G3_Read_TurnFiftyPagesAndReturn | 1920x1080 | BASELINE-FAIL | K32, K94 (Phase 04) | 64.9 | artifacts\e2e\phase01-reader-r3\G3 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1280x800 | PASS |  | 60.5 | artifacts\e2e\phase01-reader-r3\G5 |
| G5 | G5_Resume_SamePageAfterRelaunch | 1920x1080 | PASS |  | 68.9 | artifacts\e2e\phase01-reader-r3\G5 |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1280x800 | BASELINE-FAIL | K30 (Phase 04) | 137.6 | artifacts\e2e\phase01-reader-r3\G8\worker-kill |
| G8 | G8_WorkerKilledDuringReading_AppSurvivesAndLogs | 1920x1080 | BASELINE-FAIL | K30 (Phase 04) | 81.8 | artifacts\e2e\phase01-reader-r3\G8\worker-kill |
