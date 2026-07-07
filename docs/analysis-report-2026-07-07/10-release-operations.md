# Release, Deployment, and Operations

Score: **30 / 100**. Weight: 5%.

Coverage reviewed: DeploymentOps reference, ADR-0009, grand-plan Phase 22/23 tasks, and CI/build artifact readiness.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-REL-001 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Release must have executable packaging configuration and channel feeds. | Critical | DeploymentOps says no Velopack/MSIX/DMG configuration and no release host/channel feeds exist. | Users cannot install or update the app through a supported path. |
| F-REL-002 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Desktop releases must be signed/notarized with key custody evidence. | Critical | DeploymentOps says no certificates are provisioned and no signing step exists. | Builds are untrusted and unsuitable for beta. |
| F-REL-003 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Update trust chain and rollback must be tested. | High | Tamper rejection is impossible until packaging exists; rollback drill has never run. | Bad updates cannot be safely rejected or rolled back. |
| F-REL-004 | `artifacts/reference-extracts-2026-07-07/Ogma-Library_DeploymentOps.txt:1` | Public beta needs a release go/no-go gate, tester comms, and operational drills. | High | DeploymentOps recommends NO-GO and lists Phase 23 soak/drills/SLOs. | Beta launch would be unmanaged and unsupported. |

90%+ means Dev/Alpha/Beta/Stable artifacts are produced by CI, Windows/macOS signing runs with protected secrets, tampered packages/descriptors are rejected, and rollback/signing-key/malicious-update drills have dated records.
