# Security, Privacy, and Compliance

Score: **55 / 100**. Weight: 14%.

Coverage reviewed: LAN host, classroom client/admin, AI gateway/provider adapters, PDF/password paths, DPIA, Risk Register, dependency audit behavior, and Phase 19 controls.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-SEC-001 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1`, `docs/plans/grand-plan/phase-19/README.md:163` | Security hardening must be complete before beta. | Critical | Deployment readiness says Phase 19 threat model execution, SAST baseline, untrusted-PDF isolation hardening, at-rest encryption, and classroom DPIA are not started/closed. | Public beta would ship without the documented security gate. |
| F-SEC-002 | `docs/plans/grand-plan/phase-19/README.md:282` | Sensitive local catalogue/student data requires at-rest encryption decision and proof. | High | SQLCipher is still a Phase 19 spike option. | Lost-device exposure remains unresolved. |
| F-SEC-003 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DPIA.txt:1`, `artifacts/reference-extracts-2026-07-07/Ogma-Library_RiskRegister.txt:1` | Minor/student processing requires jurisdiction and controller decisions before pilot. | Critical | DPIA and risk docs keep classroom/minor data decisions open. | School pilots could create legal/privacy exposure. |
| F-SEC-004 | `src/OgmaLibrary.Infrastructure/LanHost/KestrelHostModeListener.cs:219`, `docs/plans/grand-plan/phase-19/tasks.md:42` | LAN host attack surface requires tested abuse controls. | High | Kestrel host exists; fault injection for worker/network/process isolation is still planned. | Local network clients and shared files may be exposed to untested abuse paths. |
| F-SEC-005 | `src/OgmaLibrary.Application/Ai/IAiProviderFactory.cs:14`, `src/OgmaLibrary.Infrastructure/AI/Providers/AiProviderFactory.cs:62` | Provider secrets need explicit storage, redaction, and lifecycle controls. | Medium | API key is part of provider binding and required by factory; release secret handling evidence is incomplete. | Misconfiguration or accidental logging risk remains until controls are verified. |

90%+ means Phase 19 security gates are implemented and independently verified, SAST/dependency scans pass in CI, untrusted PDF worker controls are fault-injected, DPIA decisions are signed off, and LAN host behavior is threat-tested.
