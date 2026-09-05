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
- Added a reusable real-PDF adapter benchmark and processed seven local PDFs
  covering 3,326 pages and 1,057,996 words with zero file errors in 27.1
  seconds on the verification rerun. The benchmark is content-safe and
  reports allocation telemetry.
- Added database-backed real-corpus mode and indexed all seven PDFs: 3,326
  pages, 7 extraction artifacts, 5,096 search chunks, and zero failed books or
  pages in 40.2 seconds, including clean SQLite teardown.
- Added a reproducible mixed-quality batch benchmark covering 500 books and
  1,500 pages (full, scanned, and partial page layers). After bounding shared
  context tracking, the Windows run completed in 9.190 seconds and allocated
  296,590,296 bytes with no failed books.
- Removed PdfPig all-page materialization from `PdfiumAdapter`; direct page
  resolution reduced the observed Windows real-corpus peak working set from
  3,821,600,768 bytes to 492,982,272 bytes on the same seven-file corpus.
  Extraction remained error-free with 3,326 pages and 1,057,996 words; see
  `evidence/phase-11-real-pdf-memory-2026-09-05.md`.
- Added a persistent production-worker resource benchmark and replayed the
  same seven-file corpus three times per file (21 runs). Every run completed
  without an error; maximum private memory was 195,772,416 bytes and maximum
  peak working set was 247,021,568 bytes against the configured 805,306,368
  byte worker ceiling. See
  `evidence/phase-11-worker-resource-repeat-2026-09-05.md`.

## Remaining phase gate

The extraction pipeline now calls the artifact, ISBN evidence, and TOC
services; persists page-aware deterministic manifests; and records TOC quality.
The local Windows page-retention/resource subgate, real-adapter corpus, and
synthetic 500-book pipeline throughput subgates are evidenced. The cumulative
8.63 GB allocation remains a measurement of total managed allocations, not
peak working set, and still warrants profiling on a representative corpus.
Representative real 500-book acceptance, repeated approved per-book resource
ceilings for the complete database-backed worker pipeline, native
cross-platform measurements, and physical UI/accessibility evidence remain
open before Phase 11 can be marked complete. The persistent production-worker
extraction-session repetition subgate is closed for the tested Windows corpus.
