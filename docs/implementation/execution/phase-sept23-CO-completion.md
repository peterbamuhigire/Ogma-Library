# Sept-23 Phase CO completion: close-out of phases 02, 05, 06, 07, 08, 13 and 17

Status: **IN PROGRESS** (2026-11-19). Slices 1 and 2 of 6 are done. This is the canonical record for
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
