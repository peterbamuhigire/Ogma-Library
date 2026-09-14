# Phase 20: system hardening, recovery, performance and packaging

Status: planned. Owner: security/release lead; reviewers: independent security QA and native-platform engineers.
Dependencies:01-19 feature/state acceptance; native preparation begins in01.
Estimate:10-18 person-days, subject to containment/packaging findings. Findings:F11/F17/F20/F21/F28/F30.
Requirements:all applicable security/privacy/performance/operations controls; old phases10/17/37/38.
Skills:E4/E5/E6/E1/R1/R2/K1; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

The tested application remains safe and usable under untrusted PDFs, crashes, poor networks and real installation/update conditions. Build success is only one input.

## Work slices

1. Threat-model actual desktop/PDF/native-worker/LAN/AI boundaries. Review path canonicalization, symlinks, hostile content, resource limits, bridge messages, profile data and egress. Obtain independent containment/security review rather than certify from a pattern scan.
2. Execute disposable fault drills: corrupt/password/oversized PDFs, worker timeout/crash, app kill, disk full, denied permissions, DB corruption/migration failure, changed root, network loss and revoked session. Verify safe error messages and redacted diagnostics.
3. Verify complete backup/restore and portability: catalogue identities, original references, annotations, keys where legally/technically portable, derived indexes, profiles and school policy. Test recovery on another supported machine and rollback after update.
4. Run canonical reference-machine budgets for cold start, first page, page turns, search2k/50k, OCR/extraction,3D frame/memory and classroom load/soak. Separate warm caches/synthetic fixtures from real workloads, report percentiles/peaks and failure slices.
5. Review pinned .NET/Avalonia/native dependencies against current supported sources and vulnerability feeds. Local audit runtime10.0.1 differs from the current .NET10 patch recorded in sourceS08; choose/test a supported patch and SDK pin in a separate change, never relabel the old test run.
6. Produce Windows/macOS release candidates with actual native libraries, fonts, OCR data,3D assets, licenses, SBOM, update trust and signing/notarization as applicable. Define supported architecture/OS channels explicitly; no unsupported Mac variant implied.
7. Test clean install, first run without SDK, upgrade, offline startup, uninstall/retain-data decision and rollback from the exact signed artifact. Rehearse integrity/signature failure rejection.

## Acceptance

- No unresolved critical/high exploitable security or data-loss/privacy issue; independent review and negative tests retained.
- Original source PDFs and private reading state survive relevant crash/recovery drills; containment limits are verified on each target OS.
- Canonical NFRs pass on approved reference hardware; deviations have evidence and explicit scope decisions, not averaged-away failures.
- All shipped native dependencies work in clean environments; vulnerability, format/analyzer, architecture, functional/UI and3D gates pass with skipped/unsupported cases visible.
- Signed artifacts, hashes, provenance and native install/upgrade/rollback records reconcile. The promoted artifact is the tested artifact.
- Recovery runbook is executed by someone other than its author; metrics/diagnostics detect the failure without exposing private text.

## Recovery

Keep last accepted binary and schema-compatible backup; distinguish binary rollback from data restoration. Disable optional AI/Host/3D independently where safe. Block release if restoration or signature verification fails. Repeat affected phase19 native checks on the packaged build.
