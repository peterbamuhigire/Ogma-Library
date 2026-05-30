# Phase 15 — Tasks

---

## WP1 — Data Model

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP1-T1 | Add `IsOcrDerived` (bool, default false) and `IsPasswordProtected` (bool, default false) columns to `Books` table in EF Core migration M015; both nullable upgrade paths for existing rows | Phase 04 | 1 h | FR-READ-010, FR-READ-009 |
| P15-WP1-T2 | Add `Source` column to `ExtractedPages` table: enum text `"Extraction"` | `"OCR"`; default `"Extraction"` for existing rows | Phase 10 | 0.5 h | FR-READ-010 |
| P15-WP1-T3 | Implement `Down()` for M015: drop the two `Books` columns and revert `ExtractedPages.Source` to nullable | P15-WP1-T1..T2 | 0.5 h | reversibility |
| P15-WP1-T4 | Integration test `Migration_M015_UpAndDown_LeavesData_Intact`: apply M015 Up + Down on a seeded test database; assert all pre-existing rows survive | P15-WP1-T1..T3 | 1 h | reversibility |

---

## WP2 — OCR Pipeline

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP2-T1 | `TesseractOcrAdapter` implementing `IOcrAdapter`: wraps `Tesseract.TesseractEngine`; initializes with `sidecar/tessdata/` path; `RecognizeAsync(Bitmap, CancellationToken) -> OcrPageResult` returning text + confidence (0..1) | Phase 01 spike notes | 2 h | FR-READ-010 |
| P15-WP2-T2 | `OcrJob` domain aggregate: `BookId`, `TotalPages`, `ProcessedPages`, `Status` (Pending/InProgress/Paused/Completed/Failed), `CreatedAt`, `UpdatedAt`; in `Jobs` table (Phase 05 schema) | P15-WP1 | 1 h | FR-READ-010, NFR-OGMA-009 |
| P15-WP2-T3 | `OcrJobWorker` (background worker, IHostedService): dequeues Pending/InProgress jobs; for each unprocessed page (determined by existing `ExtractedPages` rows for this BookId + `Source="OCR"`): render page at 300 DPI via PDFium, call `TesseractOcrAdapter.RecognizeAsync`, write `ExtractedPage`; update `OcrJob.ProcessedPages`; honor `CancellationToken` for graceful pause | P15-WP2-T1..T2 | 3 h | FR-READ-010, NFR-OGMA-009 |
| P15-WP2-T4 | Resume logic: on `OcrJobWorker` startup, re-enqueue any job with `Status == InProgress` (indicating abnormal shutdown) by querying `ExtractedPages` for `Source="OCR"` to find last processed page; set `ProcessedPages = last_ocr_page_count` | P15-WP2-T3 | 1 h | NFR-OGMA-009 |
| P15-WP2-T5 | On job completion: set `Books.IsOcrDerived = true`; enqueue `FtsReindexJob` for the book (Phase 10 job queue); also enqueue `EmbeddingJob` for Phase 11 if embeddings are enabled | P15-WP2-T3 | 1 h | FR-READ-010, search integration |
| P15-WP2-T6 | Unit test `TesseractOcrAdapter_RecognizesText_FromSyntheticBitmap`: feed a bitmap of "Hello world" text; assert recognized text contains "Hello" | P15-WP2-T1 | 1 h | FR-READ-010 |
| P15-WP2-T7 | Integration test `OcrJob_ScannedPdf_BecomesSearchable`: run `OcrJobWorker` on `scanned-image-only` golden-corpus fixture; assert FTS5 search for a known word in the scanned PDF returns the book | P15-WP2-T3..T5 | 2 h | FR-READ-010 golden-corpus |
| P15-WP2-T8 | Fault-injection test `OcrJob_Recovery_AfterInterruption_NoDuplicatePages`: cancel `OcrJobWorker` after page 3 of a 10-page PDF; restart; assert pages 1-3 are NOT re-inserted and pages 4-10 are processed exactly once | P15-WP2-T4 | 2 h | NFR-OGMA-009 |
| P15-WP2-T9 | Memory profile test `OcrJob_VeryLargePdf_NoOutOfMemory`: process `very-large` golden-corpus fixture (1,000+ pages) in the worker; assert no `OutOfMemoryException`; spot-check peak memory < 512 MB | P15-WP2-T3 | 1.5 h | R4 recoverability |

---

## WP3 — OCR UI

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP3-T1 | `OcrJobStatusViewModel`: exposes `Status`, `ProgressPercent`, `ProcessedPages`, `TotalPages`, `BookTitle`; `PauseCommand`, `CancelCommand`, `RetryCommand` | P15-WP2-T2 | 1.5 h | FR-READ-010 UI |
| P15-WP3-T2 | Add OCR status chip to Index Manager view (Phase 10 UI): list of in-progress and queued OCR jobs with `ic_ocr_active` / `ic_ocr_paused` icons; progress bar; pause/cancel/retry buttons | P15-WP3-T1 | 2 h | FR-READ-010 UI |
| P15-WP3-T3 | "Run OCR" button in book-detail settings panel (and Health Dashboard "scanned books" row): triggers `OcrJobWorker` enqueue for the specific book; disabled if job already queued or completed | P15-WP3-T1 | 1 h | FR-READ-010 trigger |
| P15-WP3-T4 | `IsOcrDerived` badge in book-detail header: `ic_ocr_derived` icon + "OCR-derived text" tooltip; visible when `Books.IsOcrDerived == true` | Phase 08 | 0.5 h | FR-READ-010 provenance |
| P15-WP3-T5 | Externalize all strings en + fr; wire icons; keyboard navigation for OCR UI controls | P15-WP3-T2..T4 | 1 h | i18n, a11y |

---

## WP4 — Password-Protected PDF

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP4-T1 | `IPasswordProvider` interface: `Task<PasswordResult> GetPasswordAsync(BookId, string contentHash, CancellationToken)` returning `{Password, WasStored, UserCancelled}` | Phase 02 | 0.5 h | FR-READ-009 |
| P15-WP4-T2 | `WindowsPasswordProvider` implementing `IPasswordProvider`: checks Windows Credential Manager under key `Ogma:BookPassword:<contentHash>`; if absent, calls `CredUIPromptForWindowsCredentials` via P/Invoke; consent checkbox → `CredWrite` if ticked | P15-WP4-T1 | 2.5 h | FR-READ-009, Windows |
| P15-WP4-T3 | `MacOsPasswordProvider` implementing `IPasswordProvider`: checks macOS Keychain under service `Ogma` + account `BookPassword:<contentHash>` via `Security.framework` interop; if absent, prompts via `LAContext.evaluatePolicy` + `NSAlert` or Keychain UI; consent → `SecKeychainAddInternetPassword` if ticked | P15-WP4-T1 | 3 h | FR-READ-009, macOS |
| P15-WP4-T4 | Register `IPasswordProvider` per platform in DI composition root | P15-WP4-T2..T3 | 0.5 h | architecture |
| P15-WP4-T5 | `PasswordUnlockViewModel`: invokes `IPasswordProvider.GetPasswordAsync`; on success passes password to `IReaderService.OpenProtectedAsync(bookId, password)`; on wrong password shows error + retry; on cancel shows "Book is locked" empty state | P15-WP4-T1 | 1.5 h | FR-READ-009 |
| P15-WP4-T6 | PDFium: `FPDF_LoadDocument(path, password)` via existing `IPdfiumWrapper`; if returns null after password attempt → `PasswordIncorrectException` | Phase 08 | 0.5 h | FR-READ-009 |
| P15-WP4-T7 | "Forget password" button in book-detail settings: removes credential from OS store; `Books.IsPasswordProtected` remains true | P15-WP4-T5 | 0.5 h | FR-READ-009 reversibility |
| P15-WP4-T8 | Security test `Password_NeverStoredInCatalogue`: after unlock with password "test-secret-42", assert zero rows in any catalogue table contain the string "test-secret-42" | P15-WP4-T5..T6 | 1 h | FR-READ-009, R2 |
| P15-WP4-T9 | Integration test `PasswordPdf_UnlockViaOsCredentialFlow_Opens_Book`: use `password-protected` golden-corpus fixture (known password "ogma-test-password"); mock `IPasswordProvider` to return the known password; assert PDFium opens the document and page 1 renders | P15-WP4-T5..T6 | 1.5 h | FR-READ-009, beta gate G2 |
| P15-WP4-T10 | Externalize password-prompt strings en + fr; "forget password" button icon `ic_reader_lock_open`; "locked" icon `ic_reader_locked`; accessible labels | P15-WP4-T5..T7 | 0.5 h | i18n, a11y |

---

## WP5 — Split-View Scaffold (V2)

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP5-T1 | `SplitViewViewModel`: `IsV2Placeholder = true`; `Book1` and `Book2` properties (nullable BookId); both null in V2 placeholder | Phase 08 | 0.5 h | FR-READ-012 scaffold |
| P15-WP5-T2 | `SplitViewView.axaml`: Grid 2-column layout; both cells show `V2PlaceholderPanel` with `ic_reader_split` icon + copy "Split view is coming in V2."; route registered at `navigation://split-view` | P15-WP5-T1 | 1 h | FR-READ-012 scaffold |
| P15-WP5-T3 | "Split view" menu item / toolbar button in reader toolbar: navigates to `navigation://split-view`; icon `ic_reader_split` | Phase 08 | 0.5 h | FR-READ-012 scaffold |
| P15-WP5-T4 | Test `SplitView_Route_Exists_ShowsV2Placeholder`: navigate to split-view route; assert V2PlaceholderPanel is visible; assert no reader instance is loaded | P15-WP5-T1..T2 | 0.5 h | FR-READ-012 |
| P15-WP5-T5 | Add TODO comment + change-log entry pointing to V2 implementation scope; externalize placeholder copy en + fr | P15-WP5-T2 | 0.25 h | FR-READ-012 tracking |

---

## WP6 — Batch Enrichment Scale Hardening

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP6-T1 | `RateLimitedHttpClient` wrapper: per-provider rate-limit configuration (Google Books 1 req/s, Open Library 5 req/s); exponential backoff with jitter on 429/503 (max 5 retries, base 2 s, max 60 s); inject into enrichment pipeline | Phase 07 | 2 h | FR-META-006 |
| P15-WP6-T2 | `BatchEnrichmentJob` chunk recovery: process books in chunks of 50; on chunk failure, mark affected books `EnrichmentStatus = Failed`; restart resumes from next unprocessed chunk | Phase 07 | 1.5 h | FR-META-006, NFR-OGMA-009 |
| P15-WP6-T3 | Health Dashboard pause/resume for batch enrichment: `PauseEnrichmentCommand`, `ResumeEnrichmentCommand`; rate display (req/min); estimated completion time | Phase 07 UI | 1.5 h | FR-META-006 UI |
| P15-WP6-T4 | Failed-books CSV export from Health Dashboard: list of BookId + title + failure reason + retry count | P15-WP6-T3 | 0.5 h | FR-META-006 |
| P15-WP6-T5 | Integration test `BatchEnrichment_2000Books_CompletesWithRetry`: mock HTTP client returns 429 every 5th request; seed 2,000-book corpus; assert all books reach `EnrichmentStatus = Completed`; assert total HTTP call count ≤ 2,800 (2,000 successes + up to 400 retries with some waste) | P15-WP6-T1..T2 | 2 h | FR-META-006 |

---

## WP7 — Smart-Shelf Query Optimization

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP7-T1 | Run `EXPLAIN QUERY PLAN` for the 5 most common smart-shelf filter combinations on the 2,000-book corpus; document findings in `docs/benchmarks/phase-15/query-plans.md` | Phase 06, Phase 04 | 1 h | NFR-OGMA-002 |
| P15-WP7-T2 | Add composite indices in migration M015b (separate from M015): `IX_Books_Status_Year`, `IX_ShelfBooks_ShelfId_BookId` (covering), `IX_BookMetadataFields_FieldName_Value` | P15-WP7-T1 | 1.5 h | NFR-OGMA-002 |
| P15-WP7-T3 | Benchmark `SmartShelf_QueryBenchmark_2000Books`: 5 filter combinations on the 2,000-book synthetic corpus; assert P95 ≤ 2,000 ms for each; record baseline in `docs/benchmarks/phase-15/smart-shelf-baseline.json` | P15-WP7-T2 | 1.5 h | NFR-OGMA-002 |
| P15-WP7-T4 | Index Manager UI (Phase 10): add "Smart Shelf Query Stats" panel showing last query time and index health for the Phase 15 indices | Phase 10 | 1 h | NFR-OGMA-002 UI |

---

## WP8 — Extension Point

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP8-T1 | `IOcrProvider` interface with `[ExtensionPoint]` attribute (Phase 13 pattern): `RecognizeAsync(Stream pageImage, string languageHint, CancellationToken) -> OcrPageResult`; internal visibility | P15-WP2-T1 | 0.5 h | Phase 23 readiness |
| P15-WP8-T2 | `TesseractOcrProvider` implements `IOcrProvider`; wraps `TesseractOcrAdapter` | P15-WP8-T1 | 0.5 h | architecture |
| P15-WP8-T3 | Architecture test `OcrExtensionPoint_IsInternal_In_Phase15`: assert `IOcrProvider` is not public | P15-WP8-T1 | 0.25 h | Phase 23 control |

---

## WP9 — Integration & Golden-Corpus

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P15-WP9-T1 | Add OCR golden-corpus scenario `tests/golden-corpus/ocr-pipeline/`: `scanned-image-only.pdf` fixture + expected searchable word list; oracle: after OCR, FTS5 query for each word returns the fixture book | P15-WP2-T7 | 1 h | FR-READ-010 |
| P15-WP9-T2 | Add password golden-corpus scenario `tests/golden-corpus/password-protected/`: `password-protected.pdf` fixture (created with known password "ogma-test-password"); oracle: after unlock, page 1 renders without exception | P15-WP4-T9 | 0.5 h | FR-READ-009, G2 |
| P15-WP9-T3 | Run `/security-review` focusing on: password memory hygiene (WP4), OS credential store key format (WP4), `ogma://` scheme handler (inherited from Phase 14, confirm no regression) | All WPs | — | security |
| P15-WP9-T4 | Run `/code-review --effort high` on WP2 (OCR worker) and WP4 (password flow); record and resolve findings | All WPs | — | Global DoD |
