# Phase 10 — PDF Validation and Containment

> [Roadmap index](./README.md) · [Previous](./phase-09-duplicate-and-bibliographic-resolution.md) · [Next](./phase-11-pdf-extraction-and-isbn-primitives.md)

## Objective
Treat PDFs as hostile input using brokered, resource-bounded platform containment.

## Business/Product Rationale
A personal library processes arbitrary documents under the user's account; process separation alone is insufficient.

## SDLC Requirements
CTRL-005..008, FR-READ-007/008, security and reliability NFRs.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Pdf/WindowsChildProcessLimit.cs` and the PDF worker use a child subprocess, environment flags and a limited Windows Job Object; no true network/filesystem sandbox or macOS profile exists.

## Gap Analysis
Worker can inherit broad user access; password environment variable is inspectable; resource limits incomplete.

## Architectural Impact
Create an input/output broker and `IPdfSandboxAdapter` implementations for Windows and macOS.

## Database Work
Validation result, encryption state, parser version, resource/failure code.

## Backend Work
Magic/MIME/structure validation, one-file broker, secure password IPC, timeout/CPU/memory/output limits.

## Frontend Work
Unsupported/corrupt/password/resource-limit states and retry with password.

## PDF Processing Impact
All parse/render/OCR entry points cross the sandbox contract.

## Metadata Impact
No metadata is trusted until validation succeeds.

## Search Impact
Failed files remain catalogued but unindexed.

## AI/RAG Impact
No content leaves failed/untrusted stages.

## 3D Bookshelf Impact
Fallback assets for unrenderable files.

## External Integrations
None from sandbox; network denied and tested.

## Privacy Requirements
Passwords are one-shot, never logged or persisted without explicit OS-store choice.

## Security Requirements
Least-privilege filesystem, no child process, no network, resource ceilings and output validation.

## Performance Requirements
Sandbox startup is pooled or bounded without weakening isolation.

## Error & Recovery Behaviour
Kill isolated worker on timeout; one PDF cannot stop batch; typed retry policy.

## Logging/Observability
Sandbox profile/version, timings and reason codes, not content/password.

## Testing
Unit validation; DB results; hostile/malformed/encrypted pipeline fixtures; real network/filesystem/process escape tests on both OSes; API/UI states; E2E batch isolation; resource performance tests.

## Skills Engines Applied
`skills-web-dev` security; Windows admin containment; macOS platform evidence; `srs-skills` controls.

## Dependencies
Phases 5–6.

## Parallelisation
Windows and macOS adapters against one hostile conformance suite.

## Migration Considerations
Previously processed assets are not declared safe; revalidate by policy/version.

## Definition of Done
- [ ] Escape tests prove denied network/filesystem/child process.
- [ ] CPU/memory/time/output limits work.
- [ ] Password is not in environment/log/database.
- [ ] One failure cannot block a scan.
- [ ] Security review approves both platforms.

## Kaizen Review
1. Complexity: broker/sandbox adapters. 2. One PDF entry boundary. 3. Simplify parser callers. 4. Delete self-reported isolation flags as proof. 5. Document threat model. 6. Pattern: hostile-input broker. 7. Debt decreases substantially.
