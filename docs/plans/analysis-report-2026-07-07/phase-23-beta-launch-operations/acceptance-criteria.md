# Phase 23 Acceptance Criteria: Beta Soak and Operational Drills

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC23-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC23-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC23-3 | Targeted tests for `docs/operations; docs/deployment; docs/qa; release feed artifacts` pass. | Exact `dotnet test` or script command output. |
| AC23-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC23-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC23-6 | Projected score moves from 91.0% to 91.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
