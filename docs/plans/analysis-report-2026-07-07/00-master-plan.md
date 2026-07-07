# Master Remediation Plan

Date: 2026-07-07
Audit source: `docs/analysis-report-2026-07-07/`
Baseline score: **57.0%**
Target score after 24 phases: **92.0%**

## Ordering Rationale

The sequence starts with restore/build blockers because no later verification is credible while canonical restore fails. It then closes ADR/runtime decisions, recovers the test baseline, performs security/privacy hardening, repairs data and core workflow gaps, raises UI/accessibility/performance quality, and finishes with packaging, signing, update trust, beta operations, and final working-software acceptance.

## Phase Sequence and Score Trajectory

| Phase | Title | Findings resolved | Projected score |
| ---: | --- | --- | --- |
| 01 | Restore and Build Stabilization | F-BLD-001, F-TEST-001 | 57.0 -> 60.0 |
| 02 | Runtime and Architecture Decision Closure | F-BLD-002, F-BLD-003, F-DOC-001, F-ARCH-001 | 60.0 -> 61.5 |
| 03 | Canonical Test Recovery and Known Failure Fix | F-TEST-002, F-FUNC-001, F-PERF-004 | 61.5 -> 64.0 |
| 04 | Security Threat Model and SAST Baseline | F-SEC-001, F-SEC-004, F-SEC-005, F-ARCH-001 | 64.0 -> 66.5 |
| 05 | Untrusted PDF Worker Isolation | F-SEC-001, F-SEC-002 | 66.5 -> 69.0 |
| 06 | At-Rest Encryption and Secret Lifecycle | F-SEC-002, F-SEC-003, F-SEC-005 | 69.0 -> 71.0 |
| 07 | Database Migration Integrity | F-DATA-001, F-DATA-002, F-DATA-003 | 71.0 -> 73.0 |
| 08 | Catalogue UX and Asset Completion | F-FUNC-002, F-FUNC-004, F-UI-001, F-UI-002 | 73.0 -> 75.0 |
| 09 | Metadata, OCR, and Health Reliability | F-FUNC-001, F-PERF-004, F-DATA-003 | 75.0 -> 77.0 |
| 10 | AI Answer Mode Completion | F-ARCH-002, F-FUNC-003, F-SEC-005 | 77.0 -> 78.5 |
| 11 | Split View Reader Workflow | F-ARCH-003, F-FUNC-003 | 78.5 -> 80.0 |
| 12 | 3D Shelf Platform Acceptance | F-ARCH-004, F-PERF-001 | 80.0 -> 81.5 |
| 13 | Premium Visual System and Reader Controls | F-UI-001, F-UI-003, F-UI-005 | 81.5 -> 82.5 |
| 14 | Localization and Copy Debt Closure | F-UI-002, F-DOC-002 | 82.5 -> 83.5 |
| 15 | WCAG 2.2 AA Accessibility Gate | F-UI-004, F-TEST-004 | 83.5 -> 85.0 |
| 16 | Reference-Hardware Performance Gate | F-PERF-002, F-PERF-004 | 85.0 -> 86.0 |
| 17 | Observability, Telemetry, and SLOs | F-PERF-003, F-REL-004 | 86.0 -> 87.0 |
| 18 | Packaging and Installers | F-REL-001, F-FUNC-002 | 87.0 -> 88.0 |
| 19 | Signing and Notarization | F-REL-002 | 88.0 -> 89.0 |
| 20 | Update Trust Chain and Rollback | F-REL-003, F-REL-004 | 89.0 -> 90.0 |
| 21 | Cross-Platform Release Candidate QA | F-TEST-003, F-TEST-004, F-UI-004 | 90.0 -> 90.5 |
| 22 | Documentation and Evidence Consolidation | F-DOC-002, F-DOC-003, F-DOC-001 | 90.5 -> 91.0 |
| 23 | Beta Soak and Operational Drills | F-REL-004, F-FUNC-002, F-PERF-003 | 91.0 -> 91.5 |
| 24 | Final Working Software Acceptance | All findings in 99-findings-register.md | 91.5 -> 92.0 |

## Phase Completion Status

| Phase | Status | Current projected score | Evidence |
| ---: | --- | ---: | --- |
| 01 | Complete | 60.0% | `phase-01-baseline-recovery-build-stabilization/COMPLETED.md` |

## Dependency Map

- Phase 01 unlocks all later verification by restoring build/test execution.
- Phase 02 depends on Phase 01 and ratifies the runtime/package decisions that constrain all code work.
- Phase 03 depends on Phase 01 and establishes a green regression baseline.
- Phases 04-06 depend on Phase 03 and close security/privacy foundations before user-facing polish.
- Phase 07 depends on Phases 01-06 because data migration integrity must account for security and restore behavior.
- Phases 08-14 depend on the stable build, test, security, and data foundation.
- Phase 15 depends on UI/localization work so WCAG evidence is meaningful.
- Phase 16 depends on functionality/UI stabilization so performance numbers are representative.
- Phase 17 depends on performance and release-readiness definitions.
- Phases 18-20 depend on green tests and security controls because packaging/signing/update feeds must not distribute unsafe builds.
- Phase 21 depends on packaged artifacts.
- Phase 22 consolidates documentation after implementation evidence exists.
- Phase 23 depends on release artifacts, SLOs, and documentation.
- Phase 24 is the final all-findings working-software gate.

## Traceability Rule

Every Critical and High finding from `99-findings-register.md` is assigned to at least one phase. Medium findings are assigned where they materially affect the same module or release gate. A finding may only be marked Resolved after its phase verification passes and `COMPLETED.md` records evidence.

## Commit Rule

One phase, one commit after verification and documentation. Commit messages must follow the phase implementation prompt format and include `Resolves:` with the exact finding IDs.
