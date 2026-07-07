# Phase 05 Acceptance Criteria: Untrusted PDF Worker Isolation

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC05-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC05-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC05-3 | Targeted tests for `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; src/OgmaLibrary.Infrastructure/Pdf; tests/OgmaLibrary.Tests/Security` pass. | Exact `dotnet test` or script command output. |
| AC05-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC05-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC05-6 | Projected score moves from 66.5% to 69.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
