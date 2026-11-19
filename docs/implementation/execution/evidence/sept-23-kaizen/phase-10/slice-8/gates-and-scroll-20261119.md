# Phase 10 slice 8: gate set and 2,000-book scroll measurement (2026-11-19)

Head: `aee9e2dd` (branch `kaizen/phase-10`), clean tree, Windows 11 Pro 10.0.26200, .NET 10 SDK
from `global.json`. No OgmaLibrary process from this worktree was running. Results are copied
verbatim from the tool output; only the summary lines are kept.

## Gate set (MEASURED)

| Command | Exit | Verbatim result |
|---|---|---|
| `./scripts/Test-RequirementAccountability.ps1` | success | `Requirement accountability verified: 101 FRs, 29 NFRs, 32 controls; all 162 IDs are assigned in the roadmap matrix.` |
| `dotnet restore OgmaLibrary.sln --locked-mode` | 0 | `All projects are up-to-date for restore.` |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | 0 | `0 Warning(s)` / `0 Error(s)` / `Time Elapsed 00:01:41.37` |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | 0 | no output (no changes needed) |
| `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | 0 | 11 of 11 projects: `has no vulnerable packages given the current sources.`; 0 projects with vulnerable packages |
| `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal` | 0 | no output (no diagnostics) |
| `./scripts/Test-DesignTokens.ps1` | 0 | `Design-token lint: clean.` |
| `./scripts/Test-Fast.ps1` | 0 | `Passed! - Failed: 0, Passed: 52 ... OgmaLibrary.Tests.Architecture.dll`; `Passed! - Failed: 0, Passed: 1350 ... OgmaLibrary.Tests.dll`; `Passed! - Failed: 0, Passed: 417 ... OgmaLibrary.Tests.Ui.dll` |
| `src/shelf3d` npm gates | n/a | `src/shelf3d` did not change in Phase 10 |

## 2,000-book scroll

| Measure | Class | Result |
|---|---|---|
| Real-window frame timing, 2,000 books (`New-SyntheticCorpus.py --count 2000`) | — | **NOT_ASSESSED**. The E2E harness has no frame timer, and no journey loads a 2,000-book corpus or measures scroll frames. The owner was also using the desktop interactively (see the journey section of the completion record). Owner: Phase 24. |
| Headless proxy `Sept23Phase10GridTests.Grid_ScrollThrough2000Books_LayoutStaysWithinFrameBudget` (`Category=Benchmark`, 1280x800, 386 steps of one third of a viewport, layout plus dispatcher per step) | HEURISTIC | Run 1: `scroll steps 386; layout p95 250.13 ms; max 438.51 ms; realised 26`. Run 2: `Passed ... [23 s]`, `scroll steps 386; layout p95 212.42 ms; max 345.65 ms; realised 26`. |

The proxy asserts only the median step (< 50 ms), and passes. Its p95 and worst step are above
the plan's targets (render p95 at most 16 ms, no hitch over 100 ms), and above slice 1's figures
(p95 86-161 ms). The machine was running other interactive applications during these runs, so
contention is a likely confounder. This is not established, and it is recorded as K120.
Containers realised stay bounded at 26.
