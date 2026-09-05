# Phase 38 Evidence - Current-Head Windows Candidate

Date: 2026-09-05

The current head produced a self-contained unsigned Windows `win-x64`
candidate. The candidate was archived outside the repository and its exact
artifact digest was verified by `Test-ReleaseCandidate.ps1`.

| Field | Result |
| --- | --- |
| Version | 0.1.0 |
| Runtime | win-x64 |
| Artifact | `OgmaLibrary-0.1.0-win-x64.zip` |
| SHA-256 | `42bfc492967bc014d7d371525f73ae27f941bc884fd8bf3ec3af55353af6c8e1` |
| Signature | Not supplied; unsigned candidate only |
| Integrity gate | Pass |
| Installed/hardware gate | Not assessed |

This evidence closes the current local packaging and digest-integrity
sub-gates. It does not claim Authenticode, macOS notarization, clean-install,
performance, rollback, or owner acceptance.
