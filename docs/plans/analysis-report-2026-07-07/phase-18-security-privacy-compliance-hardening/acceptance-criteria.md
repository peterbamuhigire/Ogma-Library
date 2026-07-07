# Phase 18 Acceptance Criteria: Packaging and Installers

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC18-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC18-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC18-3 | Targeted tests for `build; scripts; .github/workflows; src/OgmaLibrary.App; docs/deployment` pass. | Exact `dotnet test` or script command output. |
| AC18-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC18-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC18-6 | Projected score moves from 87.0% to 88.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
