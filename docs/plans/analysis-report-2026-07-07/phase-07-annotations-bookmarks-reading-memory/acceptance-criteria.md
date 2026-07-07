# Phase 07 Acceptance Criteria: Database Migration Integrity

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC07-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC07-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC07-3 | Targeted tests for `src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/Persistence/Migrations; tests/OgmaLibrary.Tests/Catalogue` pass. | Exact `dotnet test` or script command output. |
| AC07-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC07-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC07-6 | Projected score moves from 71.0% to 73.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
