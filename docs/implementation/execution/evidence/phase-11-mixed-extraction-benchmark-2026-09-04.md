# Phase 11 Evidence - Mixed Extraction Benchmark

Date: 2026-09-04

## Corpus and method

The integration benchmark runs the real `ExtractionPipelineService` against 32
deterministically seeded books with three page qualities per book: selectable
full text, scanned/empty text, and partial text. It measures wall-clock time and
managed allocations around the batch.

## Windows result

```text
books=32
pages=96
elapsedMilliseconds=2631
allocatedBytes=83489592
booksIndexed=32
booksFailed=0
```

## Interpretation

This establishes a reproducible local mixed-quality baseline and verifies that
all books complete through artifact, page, chunk, ISBN, and TOC pipeline paths.
It does not represent the roadmap's target-scale large/mixed corpus, native
PDF diversity, or cross-platform resource acceptance. Those remain open.
