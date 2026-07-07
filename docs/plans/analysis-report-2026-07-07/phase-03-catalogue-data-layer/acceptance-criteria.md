# Phase 03 Acceptance Criteria: Canonical Test Recovery and Known Failure Fix

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC03-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC03-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC03-3 | Targeted tests for `tests/OgmaLibrary.Tests/Metadata; src/OgmaLibrary.Infrastructure/Metadata; src/OgmaLibrary.Application/Metadata` pass. | Exact `dotnet test` or script command output. |
| AC03-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC03-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC03-6 | Projected score moves from 61.5% to 64.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
