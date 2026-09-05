# Phase 12 — Release conformance and continuous improvement

**Depends on:** Phases 1–11; canonical phases 1, 17, 37–39.
**Outcome:** a maintainable, honest release statement and recurring Kaizen loop.

## Work

- Publish the versioned reader capability profile, known limits, unsupported
  actions policy, engine/errata/dependency manifest and evidence index.
- Add CI gates for direct parser bypasses, profile drift, corpus regressions,
  resource limits, stale provenance, security tests and package contents.
- Add a release checklist covering signed Windows artifacts, notarized macOS
  artifacts, native/OCR licenses, clean install, rollback and backup/restore.
- Re-score the audit using the same dimensions and cap rule; publish raw score,
  evidence score, uncertainties, blockers and owner decisions.
- Schedule re-audit after every PDF engine, native asset, sandbox, extraction,
  rendering or schema change, and at least once per release.

## Standardise and teach

Update ADR-0004, ADR-0008, the canonical 39-phase plan, root README, developer
runbook and support playbook. Document how to add a fixture, classify a failure,
approve a profile change and recover a bad artifact. Do not close a phase by
relabeling missing physical/live evidence.

## Exit criteria

Owner accepts the profile and known limits; all P0/P1 blockers are closed or
explicitly release-blocking; evidence is reproducible from a clean build; the
next review date and responsible person are recorded.
