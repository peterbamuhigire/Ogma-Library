# Phase 15 — Test Plan

---

## 1. Test layers active

| Layer | Active | Notes |
| --- | --- | --- |
| Domain unit | Minimal | `OcrJob` status transitions |
| Infrastructure unit | Yes | `TesseractOcrAdapter`, `OcrJobWorker`, `RateLimitedHttpClient` |
| Integration | Yes | OCR pipeline golden-corpus; password unlock; batch enrichment |
| Fault injection | Yes | OCR resume-after-interruption (NFR-OGMA-009) |
| Security | Yes | `Password_NeverStoredInCatalogue` (R2); credential store isolation |
| Performance | Yes | `SmartShelf_QueryBenchmark_2000Books`; `OcrJob_VeryLargePdf_NoOutOfMemory` |
| Accessibility | Yes | OCR status UI keyboard nav; split-view placeholder SR |
| Golden corpus | Yes | `scanned-image-only` + `password-protected` + `very-large` fixtures |
| Architecture | Yes | `IOcrProvider` extension-point visibility |
| E2E | No | Phase 21 |

---

## 2. Golden-corpus scenarios

### `tests/golden-corpus/ocr-pipeline/`

- **Input:** `scanned-image-only.pdf` — a 10-page PDF containing only rasterized
  text (no text layer). Content: a short English passage with known words
  (e.g., "Tesseract recognition accuracy").
- **Oracle:** After `OcrJobWorker` completes, FTS5 query `"Tesseract"` returns
  the fixture book. `Books.IsOcrDerived == true`. `ExtractedPages` count == 10
  with `Source == "OCR"`.
- **Confidence oracle:** Mean `OcrPageResult.Confidence` > 0.6 (Tesseract
  confidence; lower bar for image quality variation across CI environments).

### `tests/golden-corpus/password-protected/`

- **Input:** `password-protected.pdf` — a 5-page PDF encrypted with user password
  `"ogma-test-password"`.
- **Oracle:** Mock `IPasswordProvider` returns `"ogma-test-password"`;
  `IReaderService.OpenProtectedAsync` completes without exception; page 1
  renders (PDFium returns non-null `FPDF_PAGE`). `Books.IsPasswordProtected`
  remains `true` after unlock (correct — it IS password-protected).

### `tests/golden-corpus/very-large/`

- **Input:** Existing `very-large` corpus (Phase 05) — 1,000+ page PDF.
- **Oracle (Phase 15):** `OcrJobWorker` processes 20 pages without
  `OutOfMemoryException`; then cancels (not required to complete all 1,000 pages
  in CI). Memory peak < 512 MB during those 20 pages.

---

## 3. Fault-injection tests

| Test | Setup | Oracle |
| --- | --- | --- |
| `OcrJob_Recovery_AfterInterruption_NoDuplicatePages` | Start `OcrJobWorker` on a 10-page PDF; cancel `CancellationToken` after page 3 is written; restart worker | Pages 1-3: exactly 1 `ExtractedPage` row each; pages 4-10 processed on restart; total `ExtractedPage` count == 10 |
| `BatchEnrichment_ChunkFailure_ResumesFromNextChunk` | Mock HTTP client throws on books 51-60 (chunk 2); restart job | Books 1-50 and 61-2000 enriched; books 51-60 flagged `EnrichmentStatus = Failed`; retry of failed books succeeds |
| `RateLimitedHttpClient_429_Triggers_Backoff` | Mock returns 429; retry after 2 s returns 200 | Successful response returned; retry count == 1; no `Exception` propagated |

---

## 4. Security tests (R2)

These pass with zero failures before the phase gate:

| Test | Oracle |
| --- | --- |
| `Password_NeverStoredInCatalogue` | After unlock with password "test-secret-42": `SELECT COUNT(*) FROM Books WHERE ... LIKE '%test-secret-42%'` == 0; same for all other tables |
| `PasswordProvider_CredentialKey_Format_IsCorrect` | Assert credential key format is exactly `Ogma:BookPassword:<sha256-content-hash>` (no other format accepted) |
| `OcrJob_NeverExposesRawPdfPath_ToTesseract` | `TesseractOcrAdapter` receives only a `Bitmap` or `Stream`, never a raw file path; architecture test |

---

## 5. Performance gates

| Budget | Threshold | Test |
| --- | --- | --- |
| Smart-shelf query P95 (2,000 books) | ≤ 2,000 ms (NFR-OGMA-002) | `SmartShelf_QueryBenchmark_2000Books` |
| OCR throughput | ≥ 2 pages/s on reference hardware (for planning; not a hard CI gate) | `OcrJob_PageThroughput_Benchmark` (nightly CI only) |
| Memory (OCR on very-large PDF) | Peak < 512 MB during 20-page run | `OcrJob_VeryLargePdf_NoOutOfMemory` |

---

## 6. Accessibility tests

| Surface | Test |
| --- | --- |
| OCR status UI | Keyboard: Tab to "Run OCR" button, Enter triggers; Tab through pause/cancel/retry buttons; SR: job status and progress percentage announced |
| `IsOcrDerived` badge | SR: badge text read as "OCR-derived text"; color not sole carrier |
| Split-view placeholder | Tab to split-view menu item; Enter navigates; SR: placeholder text announced |
| Password dialog | OS-native credential UI (Win: CredUI; macOS: Keychain/LAContext) — accessibility is OS-provided; test confirms dialog appears |
| "Forget password" button | Keyboard reachable; SR: button label "Forget password for this book" |

---

## 7. CI integration

- `dotnet test` covers all unit and integration tests.
- Golden-corpus fixtures committed under `tests/golden-corpus/`; version-pinned.
- `Password_NeverStoredInCatalogue` tagged `[Category("R2")]`; zero-tolerance.
- Fault-injection tests tagged `[Category("FaultInjection")]`; run on every PR.
- `SmartShelf_QueryBenchmark_2000Books` is a BenchmarkDotNet job; results
  compared against baseline (`smart-shelf-baseline.json`); regression > 500 ms
  triggers a CI warning.
- OCR throughput benchmark is nightly only (Tesseract speed varies by hardware).
- Both Windows (x64) and macOS (arm64) CI runners must pass all tests.
