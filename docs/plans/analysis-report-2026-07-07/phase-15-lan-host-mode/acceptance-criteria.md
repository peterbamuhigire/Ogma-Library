# Phase 15 Acceptance Criteria: WCAG 2.2 AA Accessibility Gate

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC15-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC15-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC15-3 | Targeted tests for `src/OgmaLibrary.App/Views; src/OgmaLibrary.App/Themes; tests/OgmaLibrary.Tests.Ui; docs/qa` pass. | Exact `dotnet test` or script command output. |
| AC15-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC15-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC15-6 | Projected score moves from 83.5% to 85.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
