# Phase 11 ISBN Evidence Acceptance Evidence

Date: 2026-09-04

## Implemented and verified

- `ExtractionPipelineService` begins a versioned extraction artifact and now
  persists all validated ISBN candidates returned by the detector against that
  artifact.
- `ExtractedIsbnEvidence` retains normalized ISBN value, ISBN-10/ISBN-13 kind,
  source rank, candidate rank, best-candidate flag, and detection timestamp.
- Replacing evidence is scoped to the same book and artifact, so reruns cannot
  accumulate stale candidates or change the book's canonical metadata directly.
- Migration `20260904103000_Phase11IsbnEvidence` creates the table, validation
  constraints, foreign keys, and lookup/uniqueness indexes.

## Automated proof

- Infrastructure Release build: passed, 0 warnings, 0 errors.
- Focused extraction/artifact suite: 11 passed, 0 failed.
- Covered behaviours include artifact association, ranked DocInfo/first-page
  evidence, source retention, replacement idempotency, and pipeline integration.

## Remaining Phase 11 gates

TOC quality persistence/consumption, mixed malformed and Unicode corpus
evidence, and measured extraction resource/throughput evidence remain open.
Physical licensed corpus and platform/release gates are not assessed here.
