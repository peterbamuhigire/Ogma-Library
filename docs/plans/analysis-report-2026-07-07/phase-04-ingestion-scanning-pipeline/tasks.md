# Phase 04 Tasks: Security Threat Model and SAST Baseline

Complete tasks top to bottom. Each task must include code, tests, and task-level checks before moving on.

| Task | Findings / standards | Files or modules affected | Change required | Governing skill | Effort | Risk notes |
| --- | --- | --- | --- | --- | ---: | --- |
| P04-T1 | F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001; audit report `docs/analysis-report-2026-07-07` | `docs/analysis-report-2026-07-07/*`; `docs/security; docs/qa; .github/workflows; tests/OgmaLibrary.Tests/Security; tests/OgmaLibrary.Tests/LanHost` | Re-read the audit finding rows and the relevant reference docs. Confirm no prerequisite finding is still open. Record any impossible instruction through the Deviation Protocol. | implementation-status-auditor | 0.25 d | Prevents silent rescoping. |
| P04-T2 | F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001 | `docs/security; docs/qa; .github/workflows; tests/OgmaLibrary.Tests/Security; tests/OgmaLibrary.Tests/LanHost` | Implement the smallest production change that directly resolves the finding set. Preserve existing architecture boundaries and public contracts unless the referenced ADR/SRS explicitly requires a change. | web-app-security-audit; code-safety-scanner; security-scanning:stride-analysis-patterns | 1.0-2.0 d | Main implementation risk; do not touch unrelated modules. |
| P04-T3 | Advanced testing strategy; no regression rule | Matching test projects under `tests/` for the affected modules | Add or update focused tests that fail before the implementation and pass after it. Include negative-path tests for safety, privacy, localization, performance, or release behavior when applicable. | advanced-testing-strategy | 0.5-1.0 d | Tests must not encode weakened behavior. |
| P04-T4 | Documentation must match code | README, ADR, deployment, QA, benchmark, or user-guide docs affected by `docs/security; docs/qa; .github/workflows; tests/OgmaLibrary.Tests/Security; tests/OgmaLibrary.Tests/LanHost` | Update project documentation so it describes the new actual behavior, commands, artifacts, and remaining limitations. | doc-architect | 0.25-0.75 d | Stale documentation is a phase failure. |
| P04-T5 | Traceability | `docs/analysis-report-2026-07-07/99-findings-register.md`; this phase directory | Mark resolved findings only after verification. Create `COMPLETED.md` with evidence, deviations, score movement, and command output summaries. | evidence-pack-builder / traceability-matrix | 0.25 d | Do not mark partial fixes resolved. |
| P04-T6 | Full regression requirement | Whole repository | Run full verification from `verification.md`, then run the full test suite. Fix in-scope failures and re-run the full verification. | advanced-testing-strategy | 0.5 d | Canonical gate must remain green. |

## Extra Touches Allowed

Only mechanical updates are allowed outside the listed files: project references, imports/usings, localization resources, test fixtures, CI command wiring, and documentation indexes. Record each extra touch in `COMPLETED.md`.
## Phase-Specific Work Packages

- WP1: Create the executable threat model from LAN host, AI provider, PDF worker, and classroom flows.
- WP2: Add SAST/dependency/secret scan commands to CI or documented local verification.
- WP3: Translate threat mitigations into concrete tests or checklists before code hardening begins.
- WP4: Record open residual risks in the risk register instead of burying them in phase notes.
