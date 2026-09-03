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
- `PdfTableOfContentsService` covers malformed input, Unicode titles, page
  targets, hierarchy levels, bounded title normalization, and empty outlines;
  pipeline TOC chunks are included in the deterministic artifact manifest.

## Remaining Phase 11 gates

Measured extraction resource/throughput evidence over the large/mixed corpus
remains open. The malformed/Unicode/TOC correctness sub-gates are covered by
`Phase11TocExtractionTests` (2 passed).
Physical licensed corpus and platform/release gates are not assessed here.
