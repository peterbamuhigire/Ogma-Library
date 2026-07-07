# Phase 14 Acceptance Criteria: Localization and Copy Debt Closure

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC14-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC14-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC14-3 | Targeted tests for `src/OgmaLibrary.App/Views; src/OgmaLibrary.App/ViewModels; src/OgmaLibrary.App/Localization; tests/OgmaLibrary.Tests.Ui` pass. | Exact `dotnet test` or script command output. |
| AC14-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC14-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC14-6 | Projected score moves from 82.5% to 83.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
