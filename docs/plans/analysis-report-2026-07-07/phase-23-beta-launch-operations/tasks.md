# Phase 23 Tasks: Beta Soak and Operational Drills

Complete tasks top to bottom. Each task must include code, tests, and task-level checks before moving on.

| Task | Findings / standards | Files or modules affected | Change required | Governing skill | Effort | Risk notes |
| --- | --- | --- | --- | --- | ---: | --- |
| P23-T1 | F-REL-004, F-FUNC-002, F-PERF-003; audit report `docs/analysis-report-2026-07-07` | `docs/analysis-report-2026-07-07/*`; `docs/operations; docs/deployment; docs/qa; release feed artifacts` | Re-read the audit finding rows and the relevant reference docs. Confirm no prerequisite finding is still open. Record any impossible instruction through the Deviation Protocol. | implementation-status-auditor | 0.25 d | Prevents silent rescoping. |
| P23-T2 | F-REL-004, F-FUNC-002, F-PERF-003 | `docs/operations; docs/deployment; docs/qa; release feed artifacts` | Implement the smallest production change that directly resolves the finding set. Preserve existing architecture boundaries and public contracts unless the referenced ADR/SRS explicitly requires a change. | deployment-release-engineering; ai-incident-response; reliability-engineering | 1.0-2.0 d | Main implementation risk; do not touch unrelated modules. |
| P23-T3 | Advanced testing strategy; no regression rule | Matching test projects under `tests/` for the affected modules | Add or update focused tests that fail before the implementation and pass after it. Include negative-path tests for safety, privacy, localization, performance, or release behavior when applicable. | advanced-testing-strategy | 0.5-1.0 d | Tests must not encode weakened behavior. |
| P23-T4 | Documentation must match code | README, ADR, deployment, QA, benchmark, or user-guide docs affected by `docs/operations; docs/deployment; docs/qa; release feed artifacts` | Update project documentation so it describes the new actual behavior, commands, artifacts, and remaining limitations. | doc-architect | 0.25-0.75 d | Stale documentation is a phase failure. |
| P23-T5 | Traceability | `docs/analysis-report-2026-07-07/99-findings-register.md`; this phase directory | Mark resolved findings only after verification. Create `COMPLETED.md` with evidence, deviations, score movement, and command output summaries. | evidence-pack-builder / traceability-matrix | 0.25 d | Do not mark partial fixes resolved. |
| P23-T6 | Full regression requirement | Whole repository | Run full verification from `verification.md`, then run the full test suite. Fix in-scope failures and re-run the full verification. | advanced-testing-strategy | 0.5 d | Canonical gate must remain green. |

## Extra Touches Allowed

Only mechanical updates are allowed outside the listed files: project references, imports/usings, localization resources, test fixtures, CI command wiring, and documentation indexes. Record each extra touch in `COMPLETED.md`.
## Phase-Specific Work Packages

- WP1: Run the soak window against signed beta artifacts and live feeds.
- WP2: Execute incident, rollback, malicious-update, and signing-key tabletop drills.
- WP3: Prepare tester communication, known issues, support intake, and go/no-go record.
- WP4: Review SLO/error budget evidence before launch authorization.
