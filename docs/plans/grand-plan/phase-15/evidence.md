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
and enqueue a follow-up FTS reindex job. The native Tesseract provider and
hosted `OcrWorker` are registered through the composition root without loading
native OCR binaries until a queued OCR job actually runs. English language data
is supplied through `Tesseract.Data.English`, which copies
`tessdata/eng.traineddata` to the app output.
The WP4 reader handoff now opens protected PDFs through `OpenProtectedAsync`,
passes the password to the PDFium adapter, clears provider-owned password
buffers after open, and covers the known-password PDF render path.
The first WP6 scale-hardening slice adds provider-specific rate limiting and
429/503 retry handling to the Google Books and Open Library HTTP clients.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase15OcrSchemaTests` | Passed: 3 schema/source/migration tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OcrJobProcessorTests` | Passed: 2 OCR processor reliability tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OcrWorkerTests` | Passed: 1 hosted worker scheduling test |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~IndexManagerServiceTests` | Passed: 4 index-manager backend tests including OCR job status projection |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~SearchViewModelTests` | Passed: 8 UI/view-model tests including localized OCR job progress |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~ShellReaderNavigationTests` | Passed: 4 shell route tests including split-view V2 scaffold |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~PasswordProviderTests` | Passed: 5 password-provider security/handoff tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PasswordProviderTests\|FullyQualifiedName~ReaderSessionServiceTests\|FullyQualifiedName~PdfiumAdapterPasswordTests"` | Passed: 16 password/session/PDFium handoff tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~PdfiumAdapterPasswordTests` | Passed: 1 real PDFium password-protected render test |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RateLimitedHttpClientTests` | Passed: 3 provider rate-limit/retry tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |
| `Test-Path src\OgmaLibrary.Infrastructure\bin\Release\net10.0\tessdata\eng.traineddata` | Passed: English tessdata copied to Release output |

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
| Native OCR adapter | `TesseractOcrProvider` wraps the local Tesseract engine behind `IOcrProvider` |
| English OCR data | `Tesseract.Data.English` supplies `tessdata/eng.traineddata` without committing the binary to git |
| Hosted OCR worker | `OcrWorker` polls `IOcrJobProcessor` as a hosted service and backs off on idle/error states |
| OCR status surface | `IndexManagerService` projects recent `OcrJob` progress from the Jobs table; `IndexManagerViewModel` localizes active job count, state, and page progress |
| Split-view scaffold | `SplitViewViewModel`, `SplitViewView`, and `MainShellViewModel.OpenSplitViewScaffold()` provide the Phase 15 V2 route with localized placeholder copy |
| Password provider boundary | `IPasswordProvider`, `PasswordRequest`, and disposable `PasswordResult` define the no-catalogue-secret unlock contract |
| Windows credential provider | `WindowsPasswordProvider` checks Windows Credential Manager and uses the OS credential prompt for missing passwords |
| Password unlock view model | `PasswordUnlockViewModel` requests passwords without writing secret values to EF rows or sidecars |
| Protected reader handoff | `IReaderSessionService.OpenProtectedAsync` and `IPdfRendererFactory.Open(filePath, password)` carry OS-supplied passwords to the renderer boundary |
| PDFium password render path | `PdfiumAdapter` detects required passwords, opens protected PDFs with supplied credentials, and clears its session password buffer on dispose |
| Provider HTTP resilience | `RateLimitedHttpClientHandler` spaces Google Books/Open Library requests and retries 429/503 responses with bounded exponential backoff |
| OCR ADR | `docs/adrs/0011-local-tesseract-ocr.md` records the local Tesseract decision and packaging consequences |

## Remaining Phase 15 Work

- WP2 OCR golden-corpus fixture.
- WP3 OCR trigger/pause/cancel/retry controls.
- WP4 macOS Keychain provider and book-detail forget-password UI.
- WP5 split-view scaffold is complete; V2 implementation remains out of Phase 15 scope.
- WP6 batch enrichment chunk recovery, pause/resume UI, failed CSV export, and 2,000-book integration benchmark.
- WP7 smart-shelf performance optimization.
- WP8 OCR extension point.
- WP9 golden-corpus, security review, and full remote CI evidence.
