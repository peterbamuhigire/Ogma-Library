# Phase 38 — Performance, Reliability, Packaging and Beta

> [Roadmap index](./README.md) · [Previous](./phase-37-security-privacy-and-data-protection-hardening.md) · [Next](./phase-39-cross-platform-release-acceptance-and-handover.md)

## Objective
Meet product budgets, complete operations and produce signed release candidates for both platforms.

## Business/Product Rationale
Source builds are not installable, trustworthy products.

## SDLC Requirements
All NFR-PROD performance/reliability/release requirements, ADR-0009, CTRL-012/013.

## Current Repository State
`.github/workflows/ci.yml` builds/tests Windows and macOS, while `docs/benchmarks/` and tests are mostly synthetic; no executable packaging/signing/notarization/update-feed/rollback configuration exists.

## Gap Analysis
No reference dataset/hardware evidence, soak/crash SLO, structured operations, installers or trust chain.

## Architectural Impact
Release channels and update service become platform infrastructure; release schema freeze.

## Database Work
Migration compatibility matrix, backup/restore and release schema/version metadata.

## Backend Work
Profile/fix startup/query/worker/memory issues; updater with independently verified descriptor and rollback; diagnostics/runbooks.

## Frontend Work
Update/download/restart/rollback messaging, diagnostics and performance-safe states.

## PDF Processing Impact
Throughput/resource/hostile soak and packaged native dependency validation.

## Metadata Impact
Provider cache/rate soak.

## Search Impact
2k/5k/50k latency/index rebuild gates.

## AI/RAG Impact
Latency/cost/timeout/degraded/provider soak.

## 3D Bookshelf Impact
Reference GPU/WebView performance matrix.

## External Integrations
Velopack direct feeds, Windows MSIX where required, Apple notarization; protected signing services/secrets.

## Privacy Requirements
Crash/diagnostic/update telemetry is opt-in/minimized and documented.

## Security Requirements
Authenticode/Developer ID/notarization, checksums, SBOM, signed feed descriptor, tamper rejection and key custody.

## Performance Requirements
All documented startup/catalogue/search/reader/3D/AI/LAN/client budgets pass on named hardware.

## Error & Recovery Behaviour
Install/upgrade/downgrade/interruption/rollback and signing-key/malicious-update drills.

## Logging/Observability
Local health/SLO metrics, release IDs, crash-free calculation and privacy-safe support bundle.

## Testing
Full unit/integration/pipeline/API/filesystem/AI/E2E regression; benchmark/soak/fault injection; clean Windows/macOS installs; signature/notarization/tamper/update/rollback/migration tests.

## Skills Engines Applied
`skills-web-dev` performance/release; platform admin signing/packaging; `srs-skills` release evidence; design-system update UX.

## Dependencies
Phases 17–37.

## Parallelisation
Performance profiling and packaging engineering may proceed after feature freezes; signed RC requires both.

## Migration Considerations
Support last released schema forward migration and rollback policy; freeze schema at RC.

## Definition of Done
- [ ] All accepted performance/SLO budgets pass.
- [ ] Signed Windows installer/MSIX and signed/notarized macOS artifact install cleanly.
- [ ] Update descriptor tampering is rejected.
- [ ] Upgrade/rollback/migration drills pass.
- [ ] Release schema freeze and beta evidence pack approved.

## Kaizen Review
1. Complexity: distribution/operations. 2. One release pipeline/channel model. 3. Simplify diagnostics. 4. Remove non-executable release prose. 5. Document SLO/runbooks/key custody. 6. Pattern: promote immutable artifact. 7. Debt decreases.
