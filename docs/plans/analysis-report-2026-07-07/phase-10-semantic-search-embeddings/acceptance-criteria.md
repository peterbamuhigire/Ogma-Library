# Phase 10 Acceptance Criteria: AI Answer Mode Completion

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC10-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC10-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC10-3 | Targeted tests for `src/OgmaLibrary.Application/Ai; src/OgmaLibrary.Infrastructure/AI; src/OgmaLibrary.App/Views/Ai; tests/OgmaLibrary.Tests/Ai` pass. | Exact `dotnet test` or script command output. |
| AC10-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC10-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC10-6 | Projected score moves from 77.0% to 78.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
