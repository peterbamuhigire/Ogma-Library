# Phase 05 Tasks: Untrusted PDF Worker Isolation

Complete tasks top to bottom. Each task must include code, tests, and task-level checks before moving on.

| Task | Findings / standards | Files or modules affected | Change required | Governing skill | Effort | Risk notes |
| --- | --- | --- | --- | --- | ---: | --- |
| P05-T1 | F-SEC-001; P04-R1 / CTRL-OGMA-004..007; audit report `docs/analysis-report-2026-07-07` | `docs/analysis-report-2026-07-07/*`; `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; src/OgmaLibrary.Infrastructure/Pdf; src/OgmaLibrary.App/CompositionRoot.cs; tests/OgmaLibrary.Tests/Security` | Re-read the audit finding rows and the relevant reference docs. Confirm no prerequisite finding is still open. Record any impossible instruction through the Deviation Protocol. | implementation-status-auditor | 0.25 d | Prevents silent rescoping. |
| P05-T2 | F-SEC-001; P04-R1 / CTRL-OGMA-004..007 | `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; src/OgmaLibrary.Infrastructure/Pdf; src/OgmaLibrary.App/CompositionRoot.cs; tests/OgmaLibrary.Tests/Security` | Implement the smallest production change that directly resolves the finding set. Preserve existing architecture boundaries and public contracts unless the referenced ADR/SRS explicitly requires a change. | web-app-security-audit; code-safety-scanner; system-architecture-design | 1.0-2.0 d | Main implementation risk; do not touch unrelated modules. |
| P05-T3 | Advanced testing strategy; no regression rule | Matching test projects under `tests/` for the affected modules | Add or update focused tests that fail before the implementation and pass after it. Include negative-path tests for safety, privacy, localization, performance, or release behavior when applicable. | advanced-testing-strategy | 0.5-1.0 d | Tests must not encode weakened behavior. |
| P05-T4 | Documentation must match code | README, ADR, deployment, QA, benchmark, or user-guide docs affected by `src/OgmaLibrary.Workers; src/OgmaLibrary.Reader; src/OgmaLibrary.Infrastructure/Assets; tests/OgmaLibrary.Tests/Security` | Update project documentation so it describes the new actual behavior, commands, artifacts, and remaining limitations. | doc-architect | 0.25-0.75 d | Stale documentation is a phase failure. |
| P05-T5 | Traceability | `docs/analysis-report-2026-07-07/99-findings-register.md`; this phase directory | Mark resolved findings only after verification. Create `COMPLETED.md` with evidence, deviations, score movement, and command output summaries. | evidence-pack-builder / traceability-matrix | 0.25 d | Do not mark partial fixes resolved. |
| P05-T6 | Full regression requirement | Whole repository | Run full verification from `verification.md`, then run the full test suite. Fix in-scope failures and re-run the full verification. | advanced-testing-strategy | 0.5 d | Canonical gate must remain green. |

## Extra Touches Allowed

Only mechanical updates are allowed outside the listed files: project references, imports/usings, localization resources, test fixtures, CI command wiring, and documentation indexes. The approved Deviation Protocol expansion adds `src/OgmaLibrary.Infrastructure/Pdf` and `src/OgmaLibrary.App/CompositionRoot.cs` because the original in-process renderer could not satisfy the worker-isolation objective. Record each extra touch in `COMPLETED.md`.
## Phase-Specific Work Packages

- WP1: Constrain the PDF worker filesystem, network, process-spawn, and temp-directory permissions.
- WP2: Add malicious fixture/fault-injection tests for HTTP, path traversal, process start, and temp escape attempts.
- WP3: Verify malformed/password-protected PDFs fail gracefully without crashing the app.
- WP4: Document the isolation boundary and supported platform differences.
