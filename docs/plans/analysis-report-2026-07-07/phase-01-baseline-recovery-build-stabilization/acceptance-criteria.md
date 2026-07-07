# Phase 01 Acceptance Criteria: Restore and Build Stabilization

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC01-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC01-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC01-3 | Targeted tests for `Directory.Build.props; OgmaLibrary.sln; src/*/*.csproj; tests/*/*.csproj; package lock/audit artifacts` pass. | Exact `dotnet test` or script command output. |
| AC01-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC01-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC01-6 | Projected score moves from 57.0% to 60.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
