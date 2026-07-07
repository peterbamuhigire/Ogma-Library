# Phase 24 Acceptance Criteria: Final Working Software Acceptance

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC24-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC24-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC24-3 | Targeted tests for `entire repository; release artifacts; docs/qa; docs/analysis-report-2026-07-07` pass. | Exact `dotnet test` or script command output. |
| AC24-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC24-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC24-6 | Projected score moves from 91.5% to 92.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
