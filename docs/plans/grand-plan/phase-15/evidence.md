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
Batch enrichment jobs are now tagged with recoverable 50-book chunk metadata
while preserving the existing per-book job isolation and older file-path payloads.
WP7 smart-shelf scale work is in place: the catalogue migration adds composite
indexes for status/year, shelf membership, and metadata field/value filters; the
2,000-book benchmark verifies five common query shapes under the 2,000 ms P95
budget and records their SQLite query plans.
WP8 is now guarded by the Phase 13 extension pattern: `IOcrProvider` is marked
`[ExtensionPoint]`, kept internal through friend assemblies, and covered by an
architecture visibility test.
WP3 OCR job controls are wired through the Index Manager: queued/running OCR jobs
can be paused or cancelled, failed/paused/cancelled jobs can be retried, and the
panel exposes localized action labels for each job row.
WP3 now also has the book-detail OCR trigger: the detail panel can queue a scanned
book for OCR, shows localized queued/already-queued/error status, and the queue
service rejects duplicate jobs, missing files, and path traversal outside the
configured library root.
WP7 is complete locally: the Index Manager now exposes Smart Shelf Query Stats
with the latest measured query time and health for the three Phase 15 composite
indexes.

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
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~HealthDashboardTests\|FullyQualifiedName~RateLimitedHttpClientTests"` | Passed: 10 health/batch/rate-limit tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase15SmartShelfPerformanceTests --logger "console;verbosity=detailed"` | Passed: 3 smart-shelf index/query-plan/2,000-book benchmark tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OcrJobProcessorTests\|FullyQualifiedName~OcrWorkerTests"` | Passed: 3 OCR processor/worker tests after internal extension-point hardening |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter FullyQualifiedName~OcrExtensionPoint_IsInternal_In_Phase15` | Passed: 1 OCR extension-point visibility test |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~IndexManagerServiceTests` | Passed: 5 Index Manager tests including OCR pause/cancel/retry state transitions |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~SearchViewModelTests` | Passed: 9 search/index-manager UI tests including OCR job actions |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OcrJobQueueServiceTests\|FullyQualifiedName~BookDetail_RunOcr" --logger "console;verbosity=detailed"` | Passed: 7 OCR queue trigger/idempotency/security and book-detail status tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~SkeletonRenderTests` | Passed: 5 UI skeleton tests after adding the third book-detail action |
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
| OCR job controls | `IIndexManagerService` exposes pause/cancel/retry for OCR jobs; `IndexManagerPanelView` surfaces per-job controls with localized labels |
| Book-detail OCR trigger | `BookDetailViewModel` and `BookDetailView` expose localized Run OCR queueing with success/already-queued/error feedback |
| OCR queue service | `OcrJobQueueService` creates or resumes `OcrJob` rows idempotently and validates the resolved source PDF path under the configured library root |
| Split-view scaffold | `SplitViewViewModel`, `SplitViewView`, and `MainShellViewModel.OpenSplitViewScaffold()` provide the Phase 15 V2 route with localized placeholder copy |
| Password provider boundary | `IPasswordProvider`, `PasswordRequest`, and disposable `PasswordResult` define the no-catalogue-secret unlock contract |
| Windows credential provider | `WindowsPasswordProvider` checks Windows Credential Manager and uses the OS credential prompt for missing passwords |
| Password unlock view model | `PasswordUnlockViewModel` requests passwords without writing secret values to EF rows or sidecars |
| Protected reader handoff | `IReaderSessionService.OpenProtectedAsync` and `IPdfRendererFactory.Open(filePath, password)` carry OS-supplied passwords to the renderer boundary |
| PDFium password render path | `PdfiumAdapter` detects required passwords, opens protected PDFs with supplied credentials, and clears its session password buffer on dispose |
| Provider HTTP resilience | `RateLimitedHttpClientHandler` spaces Google Books/Open Library requests and retries 429/503 responses with bounded exponential backoff |
| Batch chunk recovery metadata | `BatchEnrichmentOrchestrator` writes `BatchEnrichmentJobPayload` with 50-book chunk indexes so large runs resume from remaining per-book jobs |
| Smart-shelf composite indexes | `Phase15SmartShelfIndexes` adds `IX_Books_Status_Year`, `IX_ShelfBooks_ShelfId_BookId`, and `IX_BookMetadataFields_FieldName_Value` |
| Smart-shelf query evidence | `docs/benchmarks/phase-15/query-plans.md` records `EXPLAIN QUERY PLAN` output for five common smart-shelf filters |
| Smart-shelf baseline | `docs/benchmarks/phase-15/smart-shelf-baseline.json` records the 2,000-book P95 timing baseline |
| Smart-shelf stats panel | Index Manager shows representative smart-shelf query time and missing-index health |
| OCR extension point | `IOcrProvider` is internal, `[ExtensionPoint]`-marked, and only visible to friend assemblies until the Phase 23 SDK review |
| OCR ADR | `docs/adrs/0011-local-tesseract-ocr.md` records the local Tesseract decision and packaging consequences |

## Remaining Phase 15 Work

- WP2 OCR golden-corpus fixture.
- WP3 Health Dashboard OCR trigger discovery, if separate from the Index Manager job surface.
- WP4 macOS Keychain provider and book-detail forget-password UI.
- WP5 split-view scaffold is complete; V2 implementation remains out of Phase 15 scope.
- WP6 batch enrichment chunk recovery, pause/resume UI, failed CSV export, and 2,000-book integration benchmark.
- WP9 golden-corpus, security review, and full remote CI evidence.
