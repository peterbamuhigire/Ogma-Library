# Phase 09 Acceptance Criteria: Metadata, OCR, and Health Reliability

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC09-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC09-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC09-3 | Targeted tests for `src/OgmaLibrary.Infrastructure/Metadata; src/OgmaLibrary.Infrastructure/Search; src/OgmaLibrary.Infrastructure/Ocr; tests/OgmaLibrary.Tests/Metadata` pass. | Exact `dotnet test` or script command output. |
| AC09-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC09-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC09-6 | Projected score moves from 75.0% to 77.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
