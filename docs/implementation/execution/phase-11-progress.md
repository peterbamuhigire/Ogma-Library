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

## Remaining phase gate

The extraction pipeline now calls the artifact, ISBN evidence, and TOC
services; persists page-aware deterministic manifests; and records TOC quality.
It still needs measured extraction resource/throughput evidence over the
large/mixed corpus before phase 11 can be marked complete.
