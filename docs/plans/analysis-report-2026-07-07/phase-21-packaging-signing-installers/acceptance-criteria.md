# Phase 21 Acceptance Criteria: Cross-Platform Release Candidate QA

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC21-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC21-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC21-3 | Targeted tests for `docs/qa; docs/benchmarks; tests; release artifacts` pass. | Exact `dotnet test` or script command output. |
| AC21-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC21-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC21-6 | Projected score moves from 90.0% to 90.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
