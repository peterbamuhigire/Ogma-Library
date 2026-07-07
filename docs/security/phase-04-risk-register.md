# Phase 04 Security Risk Register

Date: 2026-07-07

| ID | Severity | Risk | Current control | Disposition |
| --- | --- | --- | --- | --- |
| P04-R1 | High | Untrusted PDF processing still needs worker-isolation fault injection for network, file-system, and child-process attempts. | Phase 05 subprocess worker boundary, sandboxed temp/output handoff, Windows child-process job limit, and `PdfWorkerIsolationTests`. | Closed in Phase 05; platform-specific hardening/performance evidence remains in release gates. |
| P04-R2 | High | At-rest encryption and device-secret lifecycle are not yet implemented for student private data and optional catalogue encryption. | Credential-store abstractions and no-plaintext password tests. | Assigned to Phase 06. |
| P04-R3 | Medium | SAST gate uses built-in analyzers plus format verification rather than a SARIF-producing security analyzer package. | CI analyzer scan and warnings-as-errors build gate. | Add SARIF analyzer package only after rule policy and suppressions are owner-approved. |
| P04-R4 | Medium | External secret scanners are not guaranteed on local workstations. | High-confidence PowerShell secret scan in CI and local verification. | Supplement with gitleaks/trufflehog when available in release CI. |
| P04-R5 | Medium | Classroom DPIA jurisdiction decisions for minors remain a release blocker. | Phase 18 DPIA service tests and threat model traceability. | Assigned to Phase 06 and later privacy sign-off work. |
| P04-R6 | Medium | LAN Host TOFU remains vulnerable on first enrollment if an attacker controls the network at pairing time. | Certificate fingerprint exposure, QR/join flow, and audit trail. | Keep as disclosed residual risk until mutual authentication is implemented or accepted. |
