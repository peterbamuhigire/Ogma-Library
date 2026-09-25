# Sept-23 Phase 00 completion: ground truth, repository and toolchain recovery

Status: **COMPLETE** (2026-09-25). Rollback point: `d9585a2` (the plan commit before any Phase 00 change).
Plan: [phase-00](../../plans/sept-23-kaizen/phases/phase-00-ground-truth-and-repo-recovery.md).

## Tasks

| Task | Result | Commit |
|---|---|---|
| T00.1 Resolve D-01 | The owner chose restore; `git restore tests/` brought back 210 files. The solution builds. | — (no diff against HEAD) |
| T00.2 Pin the SDK | `global.json` pins 10.0.401 (`rollForward: latestFeature`); CI installs from `global.json` | `b11bd14` |
| T00.3 Format gate | Two IMPORTS fixes. The working-tree line endings were re-checked-out to match `.gitattributes` (283 files, no content change). The gate reports 0 diagnostics. | `1895fbf` |
| T00.4 Split slow tests | 8 more tests tagged `Category=Benchmark` (13 in total). `scripts/Test-Fast.ps1`, `Test-Performance.ps1` and a nightly workflow added. | `e8fcb4d` |
| T00.5 Loopback-only tests | The `UseLoopbackLanHost()` helper covers 5 test files; a guard test was added | `ec61054` |
| T00.6 OCR test file lock | Root cause was in production code (`KillProcessTree` did not wait for exit). Fixed there; the test passes 10 of 10. | `f568bc6` |
| T00.7 Tree hygiene | 3.4 GB of `src/*/tmp` build output deleted; ignore rule added | `30284ca` |
| T00.8 Plan authority (D-02) | Recorded in `CLAUDE.md`, the aug-39 README, the astra README, the Sept-10 Kaizen and `00-execution-status.md`. F25 fixed: the FR-READ matrix rows are corrected to SRS v2.1. | `30284ca` |
| T00.9 CI split | The per-commit test step uses `--filter "Category!=Benchmark"`; benchmarks run nightly | `e8fcb4d` |
| Extra (K04) | Render tests no longer rewrite `docs/developer-guide/images` (opt-in via `OGMA_UPDATE_DOC_SCREENSHOTS=1`) | `e2606bc` |

## Measured results (Windows 11, 4 cores / 8 threads, SDK 10.0.401)

| Gate | Before | After |
|---|---|---|
| Solution build (owner's tree) | MSB3202, fails | 0 errors, 0 warnings |
| `dotnet format --verify-no-changes` | 2 IMPORTS errors (clean clone); 23,271 in the owner's tree | 0 |
| Requirement accountability | pass | pass (after the READ-row correction) |
| Per-commit tests | 1,161 tests, core suite 18 min, 1 OCR failure | 1,149 tests, 4.5 min total, 0 failures |
| Benchmark tests | mixed into the per-commit run | 13, nightly (`Category=Benchmark`) |
| OCR test, 10 consecutive runs | intermittent failure | 10/10 pass |
| `%TEMP%` leftovers after a full run | 30 `ogma-*` items + worker sandboxes | 0 |
| Tracked files modified by a test run | `docs/developer-guide/images/*.png` | none |
| Firewall prompt from `testhost` | observed | host tests bind loopback; mDNS is faked. A prompt-free run on a machine without rules is **NOT ASSESSED** (existing rules on this machine). |

## New findings recorded

- K06 root cause: `packages.lock.json` comes from a Debug restore, so Release inherits `Avalonia.Diagnostics`. Owner: Phase 26.
- The worker sandbox leak was a production defect, not only a test flake. It is fixed here and noted for Phase 04 (reader session teardown should move off the UI thread).

## NOT ASSESSED

| Item | Owner |
|---|---|
| CI run of the new workflows on GitHub (Windows and macOS) | Observed on the next push; recorded in the Phase 01 record |
| Firewall behaviour on a machine without prior rules | Phase 01 CI (fresh runner) |
