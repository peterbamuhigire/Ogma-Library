# Golden journey results

Run `co-s1`; filter `Category=E2E&(Journey=Harness|Journey=Smoke|Journey=G2)`; sizes 1280x800,1920x1080; themes Light; exe `C:\wamp64\www\_autorun\Ogma-Library\kaizen\wt\artifacts\e2e\app\OgmaLibrary.App.exe`.

| Journey | Test | Size | Result | Known defects | s | Evidence |
|---|---|---|---|---|---|---|
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1280x800 | PASS |  | 54 | artifacts\e2e\co-s1\G2 |
| G2 | G2_AddLibrary_ScanShowsValidBooksWithCovers | 1920x1080 | PASS |  | 41.8 | artifacts\e2e\co-s1\G2 |
| Harness | AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements | n/a | PASS |  | 1.8 | artifacts\e2e\co-s1 |
| Harness | RecordDumpDetector_FlagsRecordToStringNames | n/a | PASS |  | 0 | artifacts\e2e\co-s1 |
| Harness | Retry_NonTransientFailure_IsNotRetried | n/a | PASS |  | 0 | artifacts\e2e\co-s1 |
| Harness | Retry_PersistentStaleElement_RethrowsAfterThreeAttempts | n/a | PASS |  | 0 | artifacts\e2e\co-s1 |
| Harness | Retry_TwoComExceptionsThenSuccess_ReturnsItems | n/a | PASS |  | 0 | artifacts\e2e\co-s1 |
| Harness | WindowSize_ParsesStrictly | n/a | PASS |  | 0 | artifacts\e2e\co-s1 |
| Smoke | Smoke_LaunchesReachesShellAndCloses | 1280x800 | PASS |  | 13.3 | artifacts\e2e\co-s1\Smoke |
