# Sept-23 Phase CO completion: close-out of phases 02, 05, 06, 07, 08, 13 and 17

Status: **IN PROGRESS** (2026-11-19). Slice 1 of 6 is done. This is the canonical record for
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
