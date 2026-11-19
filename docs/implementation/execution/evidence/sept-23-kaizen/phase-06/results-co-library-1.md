# Golden journey results

Run `co-library-1`; filter `Category=E2E&(Journey=G2|Journey=G6|Journey=LaunchCycles)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | FAIL |  | 101 | artifacts\e2e\co-library-1\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | FAIL |  | 95.4 | artifacts\e2e\co-library-1\G2 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1280x800 | PASS |  | 27.7 | artifacts\e2e\co-library-1\G6 |
| G6 | G6_InvalidFiles_AreFlaggedNotShownAsBooks | 1920x1080 | PASS |  | 28.4 | artifacts\e2e\co-library-1\G6 |
| LaunchCycles | TwentyLaunchCloseCycles_LeaveNoStrayProcesses | 1280x800 | PASS |  | 111.7 | artifacts\e2e\co-library-1\LaunchCycles |

## New failures
- **G2 1280x800** (G2_AddLibrary_ScanShowsValidBooksWithCovers): TrueException: The detail inspector for 'The Physics of Everyday Light' shows '(no ISBN line)'; expected ISBN 978-0-306-40615-7 (Phase 06).
- **G2 1920x1080** (G2_AddLibrary_ScanShowsValidBooksWithCovers): TrueException: The detail inspector for 'The Physics of Everyday Light' shows '(no ISBN line)'; expected ISBN 978-0-306-40615-7 (Phase 06).
