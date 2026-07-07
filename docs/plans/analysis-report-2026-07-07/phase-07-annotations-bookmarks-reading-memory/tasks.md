# Phase 07 Tasks: Database Migration Integrity

Complete tasks top to bottom. Each task must include code, tests, and task-level checks before moving on.

| Task | Findings / standards | Files or modules affected | Change required | Governing skill | Effort | Risk notes |
| --- | --- | --- | --- | --- | ---: | --- |
| P07-T1 | F-DATA-001, F-DATA-002, F-DATA-003; audit report `docs/analysis-report-2026-07-07` | `docs/analysis-report-2026-07-07/*`; `src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/Persistence/Migrations; tests/OgmaLibrary.Tests/Catalogue` | Re-read the audit finding rows and the relevant reference docs. Confirm no prerequisite finding is still open. Record any impossible instruction through the Deviation Protocol. | implementation-status-auditor | 0.25 d | Prevents silent rescoping. |
| P07-T2 | F-DATA-001, F-DATA-002, F-DATA-003 | `src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/Persistence/Migrations; tests/OgmaLibrary.Tests/Catalogue` | Implement the smallest production change that directly resolves the finding set. Preserve existing architecture boundaries and public contracts unless the referenced ADR/SRS explicitly requires a change. | database-design-engineering; database-reliability; advanced-testing-strategy | 1.0-2.0 d | Main implementation risk; do not touch unrelated modules. |
| P07-T3 | Advanced testing strategy; no regression rule | Matching test projects under `tests/` for the affected modules | Add or update focused tests that fail before the implementation and pass after it. Include negative-path tests for safety, privacy, localization, performance, or release behavior when applicable. | advanced-testing-strategy | 0.5-1.0 d | Tests must not encode weakened behavior. |
| P07-T4 | Documentation must match code | README, ADR, deployment, QA, benchmark, or user-guide docs affected by `src/OgmaLibrary.Infrastructure/Catalogue; src/OgmaLibrary.Infrastructure/Persistence/Migrations; tests/OgmaLibrary.Tests/Catalogue` | Update project documentation so it describes the new actual behavior, commands, artifacts, and remaining limitations. | doc-architect | 0.25-0.75 d | Stale documentation is a phase failure. |
| P07-T5 | Traceability | `docs/analysis-report-2026-07-07/99-findings-register.md`; this phase directory | Mark resolved findings only after verification. Create `COMPLETED.md` with evidence, deviations, score movement, and command output summaries. | evidence-pack-builder / traceability-matrix | 0.25 d | Do not mark partial fixes resolved. |
| P07-T6 | Full regression requirement | Whole repository | Run full verification from `verification.md`, then run the full test suite. Fix in-scope failures and re-run the full verification. | advanced-testing-strategy | 0.5 d | Canonical gate must remain green. |

## Extra Touches Allowed

Only mechanical updates are allowed outside the listed files: project references, imports/usings, localization resources, test fixtures, CI command wiring, and documentation indexes. Record each extra touch in `COMPLETED.md`.
## Phase-Specific Work Packages

- WP1: Remove or tightly document the Phase 18 schema repair compatibility path.
- WP2: Align entity comments with actual token hashing and security behavior.
- WP3: Add corruption, backup, restore, WAL, and foreign-key verification evidence.
- WP4: Confirm FTS/semantic index rebuild paths survive migration and restore.
