# Phase 12 Acceptance Criteria: 3D Shelf Platform Acceptance

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC12-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC12-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC12-3 | Targeted tests for `src/OgmaLibrary.Bookshelf3D; src/shelf3d; src/OgmaLibrary.App/Views/Shelf3D; docs/benchmarks` pass. | Exact `dotnet test` or script command output. |
| AC12-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC12-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC12-6 | Projected score moves from 80.0% to 81.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
