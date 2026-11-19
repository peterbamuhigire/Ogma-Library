# Sept-23 Phase CO completion: close-out of phases 02, 05, 06, 07, 08, 13 and 17

Status: **IN PROGRESS** (2026-11-19). Slices 1 to 4 of 6 are done. This is the canonical record for
the close-out unit (decision D-16); `phase-sept23-closeout-completion.md` points here.

## Slice 1: harness robustness (K102, K103, K98)

Executor session, branch `kaizen/phase-CO`, base `ebc9bf6e`.

| Item | Change | Commit |
|---|---|---|
| K102 | `Uia.Retry<T>` (3 attempts in all, 200 ms apart, reusing `Uia.IsTransient`) wraps `Uia.ListItems` and both harness tree walks (`UiaAudit` name audit and `UiaAudit.Snapshot`) | `a7e40e6b`, `7d8823e4` |
| K103 | `Invoke-GoldenJourneys.ps1` runs restore, build and `dotnet test` through `Invoke-Native` (scoped `$ErrorActionPreference = 'Continue'`, stderr passed on as text); a thrown run error is written into results.md and exits 1 | `a7e40e6b` |
| K98 | AGENT_BRIEF fast gate is `--filter "Category!=Benchmark&Category!=E2E"` (or `scripts/Test-Fast.ps1`) | `34172fdc` |

### Results

| Check | Result | Evidence |
|---|---|---|
| `UiaRetryTests` (3 cases: two COMExceptions then success; persistent stale element rethrown after 3 attempts; non-transient not retried) | PASS 3/3 | `dotnet test --filter FullyQualifiedName~UiaRetryTests`; also PASS inside run `co-s1` |
| `scripts/Test-GoldenJourneyRunner.ps1` (redirected child process: `-ReportOnly` PASS -> exit 0, FAIL -> exit 1, live no-match `dotnet test` -> exit 1; results.md in all three) | PASS | command output |
| `dotnet build tests/OgmaLibrary.Tests.E2E -c Release` | PASS, 0 warnings | command output |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | PASS | command output |
| Run `co-s1-run1`: Harness, Smoke, G2 at both sizes, `2>&1 \| Out-File`, desktop lock `phase-CO` | 8 PASS, 1 FAIL (G2 1280x800, K102 COMException from `UiaAudit.Snapshot`); results.md written and exit 1 under redirection, which proves K103 on a real failure | `evidence/sept-23-kaizen/phase-01/results-co-s1-run1.md` |
| Run `co-s1` (same command, after `7d8823e4`) | **9 PASS, 0 FAIL**; G2 PASS at 1280x800 and 1920x1080 | `evidence/sept-23-kaizen/phase-01/results-co-s1.md` |

Acceptance for slice 1 is met: `artifacts/e2e/co-s1/results.md` exists despite the redirection,
and G2 passes at both sizes.

### Premise corrections

- The work order put the K98 wrong filter in root `CLAUDE.md` too. Root `CLAUDE.md` has no
  `Category!=Performance` filter (its test line has no filter), so only AGENT_BRIEF changed.
- The K102 failure in the first run came from `UiaAudit.Snapshot`, not `ListItems`. The retry
  now covers both harness tree walks. Journey-local `FindAll*` calls (G07, Navigation) are not
  changed.
- The `-ReportOnly` check alone cannot reproduce K103 because it makes no native call, and a
  no-match or bad-filter `dotnet test` writes nothing to stderr. Only a real failing journey
  (run `co-s1-run1`) exercised the terminating-stderr path.
- The work order's commit subject was 75 characters; the commit hook enforces 72, so it reads
  `fix(e2e): retry list-item lookup, survive redirected output (K102, K103)`.

### NOT ASSESSED

- Stability of G2 under D-17: one clean run is not three consecutive passes. Slice 2 must repeat
  it.
- Fast suite (`scripts/Test-Fast.ps1`): NOT ASSESSED in this slice. Only test-project code and
  scripts changed, and the E2E project is excluded from the fast suite.

## Slice 2: full baseline on the head and triage

Executor session, head `4f8f19f1` (the slice 1 head). Nothing was fixed in this slice. Gate logs
are in `artifacts/co-gates/` (local, git-ignored). The three run summaries are committed as
`evidence/sept-23-kaizen/phase-CO/results-co-all-{1,2,3}.md`.

### Gates

| Gate | Result (MEASURED) |
|---|---|
| `scripts/Test-RequirementAccountability.ps1` | PASS: 101 FRs, 29 NFRs, 32 controls; all 162 IDs assigned |
| `dotnet restore --locked-mode` | PASS |
| `dotnet format --verify-no-changes` | PASS |
| `dotnet build -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet list package --vulnerable --include-transitive` | PASS: 11 of 11 projects have no vulnerable packages |
| `dotnet format analyzers --severity warn` | PASS |
| `scripts/Test-Fast.ps1` | PASS 1,674 of 1,674: Architecture 52, core 1,246 (5 min 50 s), UI 376 |

The core suite took 5 min 50 s. K105 recorded 18 min 29 s, so K105 did not reproduce here. That
is one measurement, so K105 stays with its owner.

### Journey runs

The command was `Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080` inside
`Use-DesktopLock.ps1 -Owner phase-CO`, through a child `powershell` with output redirected to a
file. `OGMA_E2E_KEEP=1` was set so the app logs of passing journeys could be searched. Every
test used its own fresh temporary data directory (56 per run) and the synthetic corpus.

| Run | Rows | PASS | PASS (baseline cleared) | BASELINE-FAIL | FAIL | NOT ASSESSED | Exit |
|---|---|---|---|---|---|---|---|
| `co-all-1` | 50 | 37 | 1 | 3 | 1 | 8 | 1 |
| `co-all-2` | 50 | 38 | 2 | 2 | 0 | 8 | 0 |
| `co-all-3` | 50 | 38 | 3 | 1 | 0 | 8 | 0 |

The 8 NOT ASSESSED rows in each run are the G9 to G12 placeholders at both sizes. They belong to
later phases and are out of this unit's scope. Rows count each test once per size, and the run
is Light theme only. The `phase09-visual-final` count (78 PASS, 16 NOT ASSESSED) covered more
themes, so the two totals cannot be compared.

### Triage table (journey x size x run)

A journey cell is PASS only if every test of that journey passed at that size. BF means
BASELINE-FAIL, and PASS\* means PASS (baseline cleared).

| Journey | Size | co-all-1 | co-all-2 | co-all-3 | Under D-17 | Class / owner |
|---|---|---|---|---|---|---|
| G1 | 1280x800 / 1920x1080 | PASS / PASS | PASS / PASS | PASS / PASS | 3 consecutive passes at both sizes | n/a |
| G2 | both | PASS | PASS | PASS | 3 consecutive at both (4 including `co-s1`) | n/a |
| G3 | 1280x800 | BF | BF | BF | baselined | product, Phase 04 (K32/K94) |
| G3 | 1920x1080 | BF | BF | PASS\* | baselined | product, Phase 04 (K32/K94) |
| G4 | both | PASS | PASS | PASS | 3 consecutive at both | n/a (was intermittent before) |
| G5 | both | PASS | PASS | PASS | 3 consecutive at both | n/a (was intermittent before) |
| G6 | both | PASS | PASS | PASS | 3 consecutive at both | n/a |
| G7 | both | PASS | PASS | PASS | 3 consecutive at both | n/a |
| G8 | both | PASS | PASS | PASS | 3 consecutive at both | n/a |
| Navigation (3 tests) | 1280x800 | FAIL | PASS | PASS | 2 consecutive passes | harness/environment, K107 (Phase 01) |
| Navigation (3 tests) | 1920x1080 | PASS | PASS | PASS | 3 consecutive | n/a |
| Settings | both | PASS | PASS | PASS | 3 consecutive at both | n/a |
| A11yNames (2 tests) | both | PASS | PASS | PASS | 3 consecutive at both | n/a |
| ScanOcrSearch | 1280x800 | BF | PASS\* | PASS\* | 2 consecutive passes; baseline stays | product, Phase 13 (K40) |
| ScanOcrSearch | 1920x1080 | PASS\* | PASS\* | PASS\* | 3 consecutive passes | product, Phase 13 |
| LaunchCycles (20 cycles) | 1280x800 only | PASS | PASS | PASS | single-size test, 3 consecutive | n/a |
| Smoke, Harness | 1280x800 / n/a | PASS | PASS | PASS | 3 consecutive | n/a |

### Stabilisation lane's pending `-All` items

| Item | co-all-1 | co-all-2 | co-all-3 | Source |
|---|---|---|---|---|
| `ui.thread.violation` (`log.uiThreadViolations` in all 36 timings files per run) | 0 | 0 | 0 | timings JSON |
| `ui.thread.violation` lines in all kept app data (56 dirs per run) | 0 | 0 | 0 | `%TEMP%/ogma-e2e/<run>` |
| `no such table` lines in all kept app data | 0 | 0 | 0 | `%TEMP%/ogma-e2e/<run>` |

### Failure classification

- **G3 (product, Phase 04, K32/K94).** Turn latency is within budget. The p95 per run and size is
  175 to 231 ms, below the 250 ms budget. The failure is a stall: the reader ignores a press
  (4 ignored presses in every failing row) and the page does not advance within 10 s, after 9
  to 17 turns. The one passing row (`co-all-3`, 1920x1080) also had a 4,208 ms stall at turn 16,
  then recovered. The stall is still reproducible, so the baseline entry stays. Under the work
  order this is re-registered to Phase 04/12. The budget is not tuned.
- **ScanOcrSearch (product, Phase 13, K40).** It passed 5 of 6 times. In `co-all-1` at 1280x800,
  search for the multi-word phrase 'amber library lantern' did not list 'Scanned Pamphlet' in the
  top three. Neither the message nor a passing row shows the `Untitled` fallback, so K97 was not
  observed in any of the 6 rows. K40 is intermittent, not fixed. Its cause (indexing timing or
  ranking) is NOT_ASSESSED. Under D-17 the baseline entry stays: 1280x800 has only 2 consecutive
  passes.
- **Navigation at 1280x800, run 1 (harness/environment, new K107).** The UIA `FromPoint` hit at
  the Library button's screen centre was `ToolBar id='view_1000'`. No Ogma source declares a
  ToolBar or that id, and the failure screenshot shows the button uncovered. The probable cause is
  a foreign window, but its owner is NOT_ASSESSED because the hit's process was not recorded.
  This is not a trivial selector fix, so it is registered as K107 and not changed here.
- **Load (K96/K71).** No failure in these runs is attributed to load. The fast suite ran
  alone, before the journeys, and the three journey runs ran one after another.

### NOT ASSESSED

- G9 to G12 (placeholders; later phases).
- Dark theme, 200% text, `fr`/`qps-ploc`, keyboard-only: not part of this slice's command.
- K105's root cause: one fast run is not enough evidence.

## Slice 3: reader journeys (Phase 02 proof: G3, G5, G8)

Evidence: [`evidence/sept-23-kaizen/phase-02/co-reader-journeys.md`](evidence/sept-23-kaizen/phase-02/co-reader-journeys.md)
and the `results-co-*.md` summaries beside it. All values MEASURED, inside `Use-DesktopLock.ps1`.

- **Premise corrected.** The work order assumed presses were dropped in flight. They were not:
  turns take about 150 ms, and every stall began about 30 s after launch, when automatic OCR was
  queued, and ended in a 15 s worker `request_timeout`. In co-diag-1 the PDF workers sat idle
  while the app used about 1.6 cores. In co-diag-2, raising the thread-pool minimum (diagnostic
  only) passed G3 at both sizes with 0 re-presses. Root cause: the OCR and extraction page loops
  ran sync-over-async `ExtractTextLayer` on pool threads and starved the pool (K94).
- **Fix `fc153ba6`.** Both loops now await `ExtractTextLayerAsync`. Two new unit tests use
  renderers whose sync call throws; both were red before the fix. OCR, extraction, Phase 17 and
  OCR golden tests 46/46; `dotnet test tests/OgmaLibrary.Tests.UI -c Release --filter
  "FullyQualifiedName~Reader"` 137/137.
- **Journeys co-reader-1 to -3.** G5 and G8 (both tests) passed 6 of 6 rows. G3 passed 3 of 6:
  1 stall without an engine restart (co-reader-2, 1920x1080) and 2 rows over the 250 ms p95 budget
  alone (252 and 422 ms). There was no `request_timeout` in any run. The stability rule is not
  met, so the G3 baseline stays, with K109 added and the reason corrected. The p95 budget was not
  changed; it stays under K32 with Phase 12.
- **No VM change for presses.** `ReaderViewModel.GoNextAsync` computes its target from a page
  number that may be stale while a load is in flight (K108, not reproduced). Changing it together
  with the harness's re-press would make the journey overshoot, so it goes to Phase 04.
- **Phase 02 extras.** T02.3: 0 synthetic titles and 0 corpus folder names in 24 kept logs,
  0 unhandled or Critical lines, 0 `ui.thread.violation`. `Category=SingleInstance` E2E does not
  exist: NOT_ASSESSED, to Phase 24 (LaunchCycles and Smoke are partial evidence only).
- **New defects.** K108 (stale page target, Phase 04); K109 (remaining blocking renderer calls
  and in-process Tesseract on pool threads, Phases 06/24); K110 (EF `TransactionError` at
  shutdown in G8 1920x1080, Phase 24).

### NOT ASSESSED

- Fast suite and full journey suite after `fc153ba6` (only the targeted tests and G3/G5/G8 ran).
- Dark theme, 200% text, `fr`, keyboard-only for the reader journeys.

## Slice 4: library and processing journeys (Phases 05 and 06: G2, G6, LaunchCycles)

Evidence: [`evidence/sept-23-kaizen/phase-06/co-library-journeys.md`](evidence/sept-23-kaizen/phase-06/co-library-journeys.md),
with `results-co-library-{1,2}.md` and two screenshots beside it. All values MEASURED, inside
`Use-DesktopLock.ps1`. No product code changed.

- **Test `b8bc973a`.** G2 now asserts that the inspector ISBN matches expected.json, and that the
  status bar never shows N > M and reaches the finished summary.
- **Premise corrected.** Run `co-library-1` failed G2 at both sizes with "(no ISBN line)". The
  ISBN is on the inspector's Bibliographic tab, and the File tab opens by default. This was a
  test error, not a product failure, so the test now selects that tab.
- **Run `co-library-2`: 5 of 5 PASS** (G2 and G6 at both sizes, plus LaunchCycles).
  - The ISBN reads `9780306406157` at both sizes.
  - The status was sampled 19 times per size, 18 of them "Preparing books". None showed N > M,
    and the finished summary was reached.
- **No G2 or G6 product failure** occurred in slice 2 or here, so no Phase 05/06 fix was needed.
  G6 confirms the validity notice and the Locked badge.
- **LaunchCycles (K71).** 40 cycles over two runs, with 0 kills and 0 orphaned workers. The
  slowest exit was 882 ms. K71 is not re-registered.
- **Phase 05 rows re-registered.** These are NOT_ASSESSED because no journey exists, and the
  unit-test coverage is named in the Phase 05 record:
  - `Category=Roots`: Phase 10.
  - Open-PDF loose book: Phase 10.
  - Cancel mid-scan: Phase 24.
  - `Category=Watcher`: Phase 24.
- **New defects.**
  - K111 (Phase 11, copy with Phase 22): hard-coded `ISBN:`, `DOI:` and `Size:` labels, and an
    empty DOI shown as "DOI: ".
  - K112 (Phase 22): the finished summary says "Added 0" after the first scan.

### NOT ASSESSED

- The extended G2 has passed once. Its stability over three runs is not established.
- Phase 06: processing resumes within 5 s of relaunch. No journey measures it, so it stays with
  Phase 01/24.
- Dark theme, 200 % text and `fr` for G2 and G6.
- The fast suite: only E2E test code changed, and the E2E project is excluded from it.

## Slice 5: navigation, Settings, search and OCR journeys (Phases 07, 08, 13, 17)

Evidence: [`phase-08/co-settings-journeys.md`](evidence/sept-23-kaizen/phase-08/co-settings-journeys.md)
and [`phase-17/co-ocr-journey.md`](evidence/sept-23-kaizen/phase-17/co-ocr-journey.md). All values MEASURED, inside
`Use-DesktopLock.ps1`, with fresh data folders and the synthetic corpus.

| Run | Journeys | Rows | Result |
|---|---|---|---|
| `co-nav-1` (acceptance command) | G1, G4, G7, Navigation, Settings, A11yNames, ScanOcrSearch; both sizes; Light, Dark | 52 | 52 PASS (ScanOcrSearch 4 x PASS (baseline cleared)) |
| `co-nav-2` (after the K113 fix, baseline removed) | G4, Settings, ScanOcrSearch, A11yNames; both sizes; Light, Dark | 32 | 32 PASS |

- **J-SET-3 (`ae0b26db`).** The new journey `Settings_SecondFolderAddedFromSettings_JoinsCatalogueAfterRescan` adds a second folder from Settings > Library and presses *Rescan library*. The catalogue goes from 13 to 14 books. It passed in 8 of 8 rows.
  - *Premise corrected:* the work order said to use the existing E2E folder-picker hook. That hook covers only the shell's Choose folder, and the Settings panel always opens the native dialog. So the journey drives the real dialog through `FolderPicker.Pick`, which is the production path, and the hook was not extended.
- **Phase 08 screenshots.** A new journey, `Settings_EverySection_OpensAndIsCaptured`, captured all 8 sections in Light and Dark at both sizes (32 PNGs in `phase-08/`).
- **ScanOcrSearch (`912977c6`).** It has 6 consecutive passes at 1280x800 and 7 at 1920x1080, above the D-17 rule of 3. The baseline entry was removed, and K97 and K99 are Resolved.
  - Phase 17 is confirmed: both scanned fixtures reach *OCR text*, and both hits are labelled "From OCR text" under their catalogue titles.
  - K40's phrase miss did not recur in these rows. Its row is left to Phase 13's owner.
- **G4 flake.** Not reproduced: 7 consecutive passes per size (`co-all-1` to `-3`, then `co-nav-1` and `co-nav-2` in both themes).
  - The journey already waits for metadata (`4ca23f2`).
  - The `SearchViewModel` request loop was reviewed and has version guards and a live refresh. There was no failing evidence to fix against, so no product change was made.
- **K113, new; fix `1fc796ea`.** Accent fill brushes are used as text, which is 2.0 to 3.0:1 in Dark.
  - Found in the `co-nav-1` Dark Settings screenshot: `#2552A0` on `#1B2440`.
  - Fixed in Settings, in the Settings overview and on the search badge. The co-nav-2 pixel is now `#9FBFF2`.
  - A new fail-first UI test guards this. Nine usages owned by other phases are allow-listed and assigned to Phase 09.
- **J-SET-2:** NOT_ASSESSED, moved to Phase 11 (no mock provider endpoint).

### NOT ASSESSED

- Nothing for the fast suite: `scripts/Test-Fast.ps1` passed on `dbf79a4f`, exit 0, MEASURED. Architecture 52/52, core 1,248/1,248 (5 min 1 s) and UI 377/377.
- 200 % text, `fr`, keyboard-only and screen-reader variants of these journeys.
- J-SET-3's stability: it has 2 consecutive runs (8 rows), not 3 runs.
