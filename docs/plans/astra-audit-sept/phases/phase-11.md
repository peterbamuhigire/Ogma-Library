# Phase 11: full-text search, native OCR and processing operations

Status: planned. Owner: search/worker engineer; reviewer: librarian and native QA.
Dependencies:05/08/10. Estimate:7-12 person-days. Findings:F07/F15/F23/F28.
Requirements:SEARCH001-003/006, READ010; old phases17/22-24.
Skills:E1/E4/E2/D8/R1/R2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Users can find text in permitted books and understand why a book is not yet searchable. Background OCR/indexing is controllable and recoverable through a real activity interface.

## Work slices

1. Trace extraction -> chunks -> FTS5 -> result/source/page -> reader jump, including notes, tags, description and TOC. Separate library-wide find from phase08's in-document search.
2. Mount ActivityCentre/IndexManager through ordinary navigation. Present queued/running/paused/failed/canceled/complete, with count units and source coverage that reconcile to jobs and books.
3. Provide retry/cancel/pause only for handlers that support the operation. Explain noninterruptible work honestly; do not claim pause from a UI toggle while processing continues.
4. Validate real Tesseract native packaging, trained-data versions/checksums and language availability on both platforms. Replace mock-renderer-only acceptance with actual raster/scanned PDF fixtures of documented provenance.
5. Preserve OCR-derived markers, confidence/quality boundaries and source page links. Use selective OCR; do not OCR every already-textual page unnecessarily.
6. Test side-by-side FTS rebuild and atomic promotion, interrupted extraction, stale-source invalidation, poisoned jobs, bounded retries and safe diagnostics export.

## Acceptance

- A known phrase in each supported source type returns the correct book/source/page, and activating the result opens that page.
- Search safely handles empty/long/quoted/punctuation/Unicode queries and returns distinct no-match/not-indexed/failed-index states.
- Real scanned corpus accuracy meets the baselined OCR contract; language-specific failures and resource use are reported separately.
- Counts reconcile by unique book and stage; no cumulative job count is labeled books scanned.
- Job cancel/retry/restart produces no duplicate chunks, stale leases or lost completed text; pending search remains useful during rebuild.
- Native OCR dependencies and reference-hardware2k/50k benchmarks have logs, hashes and limits. Unsupported packaging is NOT ASSESSED/blocked, not passed by fixture tests.

## Recovery

Keep prior usable index generation until the replacement validates and promotes. Retain source artifacts so a failed index rebuild is recoverable. Stop retry storms and quarantine poison files with a safe explanation. Re-measure search coverage, result correctness, queue age and cancellation latency after phase12 and packaging.
