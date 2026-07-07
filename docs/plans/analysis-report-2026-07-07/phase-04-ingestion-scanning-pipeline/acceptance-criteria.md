# Phase 04 Acceptance Criteria: Security Threat Model and SAST Baseline

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC04-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC04-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC04-3 | Targeted tests for `docs/security; docs/qa; .github/workflows; tests/OgmaLibrary.Tests/Security; tests/OgmaLibrary.Tests/LanHost` pass. | Exact `dotnet test` or script command output. |
| AC04-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC04-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC04-6 | Projected score moves from 64.0% to 66.5% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
