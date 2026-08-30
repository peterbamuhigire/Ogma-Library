# Phase 11 Progress - PDF Extraction and ISBN Primitives

Date: 2026-08-30

## Delivered in this increment

- Added versioned `ExtractionArtifacts` persistence keyed by book, content hash,
  and extractor version.
- Added idempotent begin, completion and failure lifecycle operations with page
  counts and a deterministic output manifest hash.
- Added the root-bounded `IPdfInputBroker`, which rejects traversal, bad
  extensions, invalid PDF magic and oversized inputs before parser entry.
- Added tests for artifact idempotency/lifecycle and all broker validation paths.

## Remaining phase gate

The existing extraction pipeline still needs to call the artifact service,
persist page/TOC quality manifests, retain ISBN evidence in canonical records,
and supply resource/Unicode/TOC corpus and throughput evidence before phase 11
can be marked complete.
