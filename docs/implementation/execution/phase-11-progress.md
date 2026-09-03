# Phase 11 Progress - PDF Extraction and ISBN Primitives

Date: 2026-09-04

## Delivered in this increment

- Added versioned `ExtractionArtifacts` persistence keyed by book, content hash,
  and extractor version.
- Added idempotent begin, completion and failure lifecycle operations with page
  counts and a deterministic output manifest hash.
- Added the root-bounded `IPdfInputBroker`, which rejects traversal, bad
  extensions, invalid PDF magic and oversized inputs before parser entry.
- Added tests for artifact idempotency/lifecycle and all broker validation paths.
- Wired the extraction pipeline to the versioned artifact lifecycle.
- Added durable ranked ISBN evidence keyed by extraction artifact, retaining
  every validated source candidate without overwriting canonical book metadata.
- Added a migration and integration coverage for evidence replacement,
  artifact association, rank/source retention, and rerun-safe persistence.
- Added bounded PdfPig outline extraction with Unicode normalization, title/page
  validation, depth/entry caps, partial/failure quality states, and TOC search
  chunks linked to the extraction artifact.
- Added durable TOC entry count/quality fields to the artifact manifest and
  malformed/Unicode outline acceptance coverage.
- Added a reproducible mixed-quality batch benchmark covering 32 books and 96
  pages (full, scanned, and partial page layers); the Windows run completed in
  2.631 seconds and allocated 83,489,592 bytes.

## Remaining phase gate

The extraction pipeline now calls the artifact, ISBN evidence, and TOC
services; persists page-aware deterministic manifests; and records TOC quality.
The benchmark is a local baseline, not target-scale acceptance. Phase 11 still
needs measured resource/throughput evidence over the large/mixed corpus before
it can be marked complete.
