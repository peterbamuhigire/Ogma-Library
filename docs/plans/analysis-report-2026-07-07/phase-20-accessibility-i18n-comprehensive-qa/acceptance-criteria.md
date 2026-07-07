# Phase 20 Acceptance Criteria: Update Trust Chain and Rollback

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC20-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC20-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC20-3 | Targeted tests for `scripts; .github/workflows; docs/deployment; tests/OgmaLibrary.Tests/Release; docs/qa` pass. | Exact `dotnet test` or script command output. |
| AC20-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC20-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC20-6 | Projected score moves from 89.0% to 90.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
