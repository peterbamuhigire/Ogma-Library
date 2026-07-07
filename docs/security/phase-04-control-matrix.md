# Phase 04 Security Control Matrix

Date: 2026-07-07

| Control | Scope | Status | Evidence | Follow-up |
| --- | --- | --- | --- | --- |
| CTRL-OGMA-001 | Secrets use OS credential store abstractions. | Pass for current implemented secret paths | `PasswordProviderTests`; classroom credential-store tests; `phase-18-safety-scan-2026-06-02.md` | Platform live evidence remains release-gate work. |
| CTRL-OGMA-002 | No plaintext secrets in SQLite, logs, config, or HTTP responses. | Pass for current implemented LAN/password paths | `Password_NeverStoredInCatalogue`; `LanHostEndpointTests`; CI secret scan | Extend to future AI answer mode in Phase 10. |
| CTRL-OGMA-004..007 | Untrusted PDF worker isolation. | Open | Threat model records TB-04 and PDF worker risks. | Phase 05 implements isolation proof and fault injection. |
| CTRL-OGMA-008..011 | Path and library-root validation. | Partial pass | LAN resolver, asset, sidecar, and path-redaction tests pass. | Phase 05 expands traversal fuzzing where worker/file hardening changes code. |
| CTRL-OGMA-016..018 | AI egress consent, preview, and audit. | Partial pass | LAN school AI preview/search path is tested with profile binding and audit. | Phase 10 expands evidence-cited answer-mode controls. |
| CTRL-OGMA-020 | LAN listener is private-network scoped. | Pass for current bind/client policy | `LanBindAddressSelectorTests`; `LanClientAddressPolicyTests`; `LanHostEndpointTests`. | Cross-platform manual network evidence remains release-gate work. |
| SAST-001 | Dependency vulnerability scan is executable. | Pass | CI `Dependency vulnerability scan`; local Phase 04 evidence. | Keep scan mandatory on Windows/macOS. |
| SAST-002 | Analyzer scan is executable. | Pass | CI `SAST analyzer scan`; local Phase 04 evidence. | Add SARIF-producing analyzer package only with owner-approved rule policy. |
| SAST-003 | High-confidence secret scan is executable. | Pass | CI `Secret pattern scan`; `SecurityBaselineTests`. | Replace or supplement with gitleaks/trufflehog when available. |

## Traceability

| Finding | Control evidence |
| --- | --- |
| F-SEC-001 | Threat model, control matrix, dependency scan, analyzer scan, secret scan, risk register, and security gate tests are now executable. |
| F-SEC-004 | LAN Host threat analysis maps abuse cases to existing tests and residual Phase 05/16 work. |
| F-SEC-005 | Provider-key risk is mapped to credential-store controls, status-only admin endpoint behavior, secret scan, and token/key redaction tests. |
| F-ARCH-001 | Security/release controls remain traceable from ADR-0015 into executable CI and QA gates. |
