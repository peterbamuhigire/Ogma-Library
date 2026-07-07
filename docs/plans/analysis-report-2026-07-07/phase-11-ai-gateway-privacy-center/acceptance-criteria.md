# Phase 11 Acceptance Criteria: Split View Reader Workflow

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC11-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC11-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC11-3 | Targeted tests for `src/OgmaLibrary.App/ViewModels/Reader; src/OgmaLibrary.App/Views/Reader; src/OgmaLibrary.Reader; tests/OgmaLibrary.Tests.Ui` pass. | Exact `dotnet test` or script command output. |
| AC11-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC11-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC11-6 | Projected score moves from 78.5% to 80.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
