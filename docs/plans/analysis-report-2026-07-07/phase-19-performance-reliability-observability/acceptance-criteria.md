# Phase 19 Acceptance Criteria: Signing and Notarization

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC19-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC19-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC19-3 | Targeted tests for `build; scripts; .github/workflows; docs/deployment; docs/security` pass. | Exact `dotnet test` or script command output. |
| AC19-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC19-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC19-6 | Projected score moves from 88.0% to 89.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
