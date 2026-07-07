# Phase 08 Acceptance Criteria: Catalogue UX and Asset Completion

| Criterion | Pass condition | Evidence required |
| --- | --- | --- |
| AC08-1 | Every finding assigned to this phase has a concrete code, test, release, or documentation change. | Diff references and finding IDs in `COMPLETED.md`. |
| AC08-2 | No safety gate is weakened: warnings-as-errors, NuGet audit, validation, security checks, and release gates remain enabled. | Command output and reviewed diffs. |
| AC08-3 | Targeted tests for `src/OgmaLibrary.App/Views/Catalogue; src/OgmaLibrary.App/ViewModels/Catalogue; src/OgmaLibrary.App/Assets; localization resources` pass. | Exact `dotnet test` or script command output. |
| AC08-4 | Full repository verification passes after targeted fixes. | Full-suite command output. |
| AC08-5 | Documentation affected by the phase is current and traceable. | Updated docs and findings-register row changes. |
| AC08-6 | Projected score moves from 73.0% to 75.0% only if all above criteria pass. | `COMPLETED.md` score table. |

A criterion cannot be marked pass by assumption. If evidence cannot be produced, invoke the Deviation Protocol.
