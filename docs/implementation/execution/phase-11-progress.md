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

## Remaining phase gate

The extraction pipeline now calls the artifact and ISBN evidence services and
persists page-aware deterministic manifests. It still needs a durable TOC
quality manifest/consumer, a mixed malformed/Unicode/TOC corpus, and measured
resource/throughput evidence before phase 11 can be marked complete.
