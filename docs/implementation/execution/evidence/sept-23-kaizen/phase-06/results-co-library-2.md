# Golden journey results

Run `co-library-2`; filter `Category=E2E&(Journey=G2|Journey=G6|Journey=LaunchCycles)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | PASS |  | 83.2 | artifacts\e2e\co-library-2\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | PASS |  | 85.7 | artifacts\e2e\co-library-2\G2 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | PASS |  | 28.1 | artifacts\e2e\co-library-2\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | PASS |  | 27.6 | artifacts\e2e\co-library-2\G6 |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 104.6 | artifacts\e2e\co-library-2\LaunchCycles |
