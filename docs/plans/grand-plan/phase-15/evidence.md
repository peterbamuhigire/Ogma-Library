# Phase 15 Evidence

Date started: 2026-06-01

## Current Status

Phase 15 WP1 is underway. The first slice adds schema support for OCR-derived
text and password-protected PDFs:

- `Books.IsOcrDerived` and `Books.IsPasswordProtected` with default `false`.
- `ExtractedPages.Source` with default `"Extraction"` and source-aware unique
  indexing for `(BookId, Source, PageNumber)`.
- `ExtractedTextStore` preserves the existing extraction default while allowing
  OCR and native-extraction rows for the same book/page.
- EF Core migration `20260601160606_Phase15OcrPowerReaderSchema` includes an
  explicit `Down()` path.
The first WP2 reliability slice is also in place: `IOcrProvider`,
`OcrJobPayload`, and `OcrJobProcessor` process pending/interrupted OCR jobs,
persist OCR text as `ExtractedPages.Source = "OCR"`, mark books as OCR-derived,
and enqueue a follow-up FTS reindex job.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase15OcrSchemaTests` | Passed: 3 schema/source/migration tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OcrJobProcessorTests` | Passed: 2 OCR processor reliability tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| Books OCR/password flags | `BookRow` and `BookConfiguration` map `IsOcrDerived` and `IsPasswordProtected` |
| Extracted-page source | `ExtractedPageRow` and `ExtractedPageConfiguration` map `Source` with default `"Extraction"` |
| Source-aware indexing | `IX_ExtractedPages_BookId_Source_PageNumber` allows OCR and direct extraction to coexist per page |
| Migration reversibility | `Phase15OcrPowerReaderSchema.Down()` drops new columns/indexes and restores the prior page-number unique index |
| Repository compatibility | `ExtractedPageRecord.Source` defaults to `"Extraction"` so Phase 10 callers remain source-compatible |
| OCR provider seam | `IOcrProvider` defines the native OCR boundary without coupling tests to Tesseract binaries |
| OCR job payload | `OcrJobPayload` records file path, language, total pages, and processed pages in the existing Jobs payload |
| OCR job processor | `OcrJobProcessor` renders pages through `IPdfRenderer`, calls `IOcrProvider`, writes source-tagged OCR pages, and resumes interrupted jobs by skipping persisted OCR pages |
| FTS reindex handoff | Completed OCR jobs enqueue `FtsReindexJob` idempotently for the affected book |

## Remaining Phase 15 Work

- WP2 OCR provider/worker/resume pipeline.
- WP3 OCR status UI.
- WP4 password credential provider and reader unlock flow.
- WP5 split-view scaffold.
- WP6 batch enrichment scale hardening.
- WP7 smart-shelf performance optimization.
- WP8 OCR extension point.
- WP9 golden-corpus, security review, and full remote CI evidence.
