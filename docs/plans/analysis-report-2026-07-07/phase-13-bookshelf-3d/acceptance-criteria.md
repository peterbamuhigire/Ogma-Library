# Phase 13 Acceptance Criteria: Premium Visual System and Reader Controls

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC13-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC13-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC13-3 | Targeted tests for `src/OgmaLibrary.App/Themes; src/OgmaLibrary.App/Icons; src/OgmaLibrary.App/Views/Reader; src/OgmaLibrary.App/Assets` pass. | Exact `dotnet test` or script command output. |
| AC13-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC13-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC13-6 | Projected score moves from 81.5% to 82.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
