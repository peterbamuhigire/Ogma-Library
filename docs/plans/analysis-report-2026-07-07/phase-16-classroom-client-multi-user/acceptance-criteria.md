# Phase 16 Acceptance Criteria: Reference-Hardware Performance Gate

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC16-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC16-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC16-3 | Targeted tests for `docs/governance/REFERENCE-HARDWARE.md; docs/benchmarks; tests/OgmaLibrary.Tests/Catalogue; tests/OgmaLibrary.Tests/LanHost` pass. | Exact `dotnet test` or script command output. |
| AC16-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC16-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC16-6 | Projected score moves from 85.0% to 86.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
