# Phase 15 — OCR, Advanced Reader & Power Tools

Single mission: make scanned PDFs searchable via an optional resumable Tesseract
OCR pipeline, support password-protected PDFs via an OS credential flow, scaffold
split view (V2), and bring batch enrichment and smart shelves to production scale.

---

## 1. Title & one-line mission

**Phase 15 — OCR, Advanced Reader & Power Tools**

Deliver the OCR pipeline for scanned PDFs (Tesseract, resumable background job,
golden-corpus fixture), password-protected PDF unlock via OS credential flow with
consent, split-view scaffold (V2), and scale-hardened batch enrichment and smart
shelves — completing the V1 reader and power-librarian feature set.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| Tier | V1 (OCR, password PDF, batch enrichment at scale, smart shelves at scale) · V2 (split view scaffold) |
| Estimate | 3 engineer-weeks |
| Owner | Peter Bamuhigire / Chwezi Core Systems |
| PRD build-phase mapping | Original Phase 5 (reader power features) + Phase 4 (metadata scale) |
| Platforms | Windows + macOS (Tesseract native per-platform; OS credential flow DPAPI/Keychain) |
| ADRs in scope | ADR-0004 (PDFium wrapper); ADR-0005 (SQLite sidecar) |
| Key gate | Beta gate G2 (PDFium wrapper from Phase 01; password-protected PDF) |
| Functional IDs | FR-READ-009, FR-READ-010, FR-READ-012; FR-META-006 |

---

## 3. Objectives

1. **OCR pipeline operational.** `OcrJob` for a scanned PDF runs as an optional
   resumable background job; extracted text is stored in `ExtractedPages` and
   indexed in FTS5; the book is marked `IsOcrDerived = true`; the pipeline is
   recoverable after interruption without duplicate work (NFR-OGMA-009).
2. **Scanned PDFs become searchable.** After OCR, the `simple-text` search path
   finds content in books that were previously image-only; the `scanned-image-only`
   golden-corpus fixture drives this acceptance test.
3. **Password-protected PDFs unlockable.** The user can unlock a password-
   protected PDF via the OS credential flow (DPAPI on Windows, Keychain on macOS);
   the password is never stored in the SQLite catalogue or plaintext files; a
   beta gate G2 test covers the full flow (FR-READ-009).
4. **Split view scaffolded (V2).** `SplitViewViewModel` and `SplitViewView` are
   scaffolded with a clear V2 tracking item; the split-view route exists and shows
   a "coming in V2" placeholder; no breaking change is needed in V2 (FR-READ-012).
5. **Batch enrichment at scale.** The Phase 07 enrichment pipeline sustains
   2,000-book enrichment runs under provider rate limits with retries; paused/
   failed state is visible and resumable (FR-META-006, NFR-OGMA-009).
6. **Smart shelves at scale.** Virtual shelf queries (Phase 06) execute within
   NFR-OGMA-002 budget (≤ 2 s P95) on a 2,000-book corpus; query plan explained
   in the index-manager UI.

---

## 4. Scope

### In scope

- `OcrJob` (background job): per-book, resumable, uses Tesseract.NET (cross-
  platform managed wrapper for Tesseract 5); stores text in `ExtractedPages` rows;
  marks `Books.IsOcrDerived`; enqueues FTS5 reindex after completion.
- Tesseract language data files: ship English (`eng`) by default; additional
  languages downloadable on demand (language-data download is a V2 UX refinement;
  Phase 15 ships `eng`).
- Cross-platform Tesseract native binary: `Tesseract.NET` or `PaddleOCR` managed
  wrapper; native binaries for win-x64 and osx-arm64 / osx-x64 included in the
  sidecar; determined by Phase 01 spike notes (document in ADR-0004 amendment or
  new ADR-0013 if the choice differs from PDFium wrapper patterns).
- `OcrJobStatus` surface in the Index Manager (Phase 10): progress %, page count,
  estimated time, pause/cancel/retry controls.
- Password-protected PDF flow: detect encrypted PDF at open; prompt user via OS
  credential dialog (not Avalonia custom dialog, so the password is never in
  managed memory longer than needed); pass to PDFium's `FPDF_LoadDocument` with
  password parameter; store credential in OS store under key
  `Ogma:BookPassword:<contentHash>`; consent step before storing.
- `PasswordUnlockViewModel` and minimal Avalonia wrapper triggering the OS
  credential UI.
- `IsPasswordProtected` and `IsOcrDerived` fields added to `Books` table (EF Core
  migration M015, reversible).
- Split-view scaffold: `SplitViewViewModel`, `SplitViewView.axaml` with "coming
  in V2" placeholder panel; route registered; no functional reader duplication.
- Batch enrichment scale hardening: rate-limit wrapper with exponential backoff;
  provider quota detection; bulk-job progress in Health Dashboard (Phase 07 UI);
  resumable after app restart (NFR-OGMA-009 extension to enrichment jobs).
- Smart-shelf query performance: SQLite query plan review; add composite index for
  the most common smart-shelf filter combinations; benchmark against 2,000-book
  corpus.
- All strings en + fr; icon manifest for this phase.

### Explicitly out of scope

- Full split-view reader implementation (V2; post-Phase 15).
- Additional OCR languages beyond English (V2 UX; language-data download).
- Full OCR accuracy improvement loop (post-V1 tooling).
- AI answer mode with OCR content (Phase 13 V2).
- Handwriting recognition (post-V2).
- PDF write-back of OCR text layer (post-V1).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-READ-009 | V1 | Password-protected PDF via OS credential flow | `PasswordPdf_UnlockViaOsCredentialFlow_Opens_Book` integration test (beta gate G2); `Password_NeverStoredInCatalogue` security test |
| FR-READ-010 | V1 | OCR-index scanned PDF; mark OCR-derived | `OcrJob_ScannedPdf_BecomesSearchable` golden-corpus test; `Books.IsOcrDerived` assertion |
| FR-READ-012 | V2 | Split view (scaffold only) | `SplitView_Route_Exists_ShowsV2Placeholder` test |
| FR-META-006 | V1 | Batch enrichment at scale; paused/failed visible | `BatchEnrichment_2000Books_CompletesWithRetry` perf integration test; pause/resume UI visible |
| NFR-OGMA-009 | V1 | Background job recoverable without duplicate work | `OcrJob_Recovery_AfterInterruption_NoDuplicatePages` fault-injection test |
| NFR-OGMA-002 | V1 | Smart shelf query ≤ 2 s P95 (2,000 books) | `SmartShelf_QueryBenchmark_2000Books` perf test |

---

## 6. Dependencies

### Depends on

| Dependency | Why |
| --- | --- |
| Phase 01 — Risk Spikes | PDFium wrapper confirmed (G2); Tesseract native binary strategy noted |
| Phase 04 — Data layer | EF Core migration M015 extends `Books` table |
| Phase 05 — Ingestion pipeline | Background job infrastructure; `IsPasswordProtected` detection already flagged in scan |
| Phase 07 — Metadata & Health | Health dashboard and batch enrichment base; Phase 15 scale-hardens |
| Phase 08 — Reader core | `FPDF_LoadDocument` password parameter; PDFium wrapper |
| Phase 10 — Search & Indexing | FTS5 index; `ExtractedPages` table; Index Manager UI |
| Phase 11 — Semantic search | `SearchChunks` table; OCR-derived chunks enqueued after OCR completes |
| ADR-0004 (PDFium wrapper) | PDFium password API confirmed |

### Unblocks

| Unblocked | How |
| --- | --- |
| Phase 21 — A11y audit | Split-view route exists; can be a11y-audited even as a scaffold |
| Phase 23 — Extension SDK | `IOcrProvider` extension point seeded here for community OCR engines |

---

## 7. Architecture & approach

### 7.1 Bounded contexts touched

- **Reader** (primary: password unlock, split-view scaffold, OCR job trigger).
- **Search Index** (FTS5 re-index after OCR; `ExtractedPages` schema).
- **Ingestion Pipeline** (OCR as a background job in the job queue).
- **Metadata Enrichment** (batch enrichment scale hardening).
- **Settings & Security** (OS credential store for PDF passwords).

### 7.2 OCR pipeline architecture

```
Trigger: user requests OCR for a book, or LibraryHealthJob finds scanned books
  -> OcrJob enqueued in Jobs table with status Pending
     -> OcrJobWorker picks up job:
         1. Load PDF via PDFium (no password needed for OCR — OCR runs on images)
         2. For each page not yet in ExtractedPages:
            a. Render page to Bitmap via PDFium at 300 DPI
            b. TesseractEngine.Process(bitmap) -> TextPage
            c. Write ExtractedPage row (PageNumber, Text, Source="OCR", Confidence)
            d. Update Job.ProgressPage
            e. Check CancellationToken -> if cancelled, write Job.Status=Paused
         3. On completion: set Books.IsOcrDerived = true, enqueue FtsReindexJob
  -> FtsReindexJob: rebuilds FTS5 external-content index for affected BookId
```

**Resume logic:** on startup, any job with `Status == Paused` or `Status ==
InProgress` (indicating abnormal shutdown) is re-enqueued; the worker queries
`ExtractedPages` for `BookId` + `Source="OCR"` to determine the last processed
page and starts from `LastPage + 1` — no duplicate page processing.

**Cross-platform Tesseract:**
- `Tesseract.NET` (NuGet `Tesseract`) ships native binaries for `win-x64`,
  `osx-arm64`, `osx-x64`, and `linux-x64` in its package.
- Language data (`eng.traineddata`) is shipped in the app bundle under
  `sidecar/tessdata/`; path configured via `TesseractEngine(dataPath, "eng")`.
- If the managed wrapper is not performant enough on macOS (noted as a Phase 01
  spike risk), an alternative native invocation via `Process.Start("tesseract")`
  with a temp-file round-trip is the fallback; document the choice in ADR-0013.

### 7.3 Password-protected PDF flow

```
User opens a book -> Reader.OpenAsync(bookId)
  -> PDFium.LoadDocument(path) -> returns null (encrypted)
  -> Detect: FPDF_GetLastError() == FPDF_ERR_PASSWORD
  -> IPasswordProvider.GetPasswordAsync(bookId):
       1. Check OS credential store: CredentialManager (Win) / Keychain (mac)
          under key "Ogma:BookPassword:<contentHash>"
       2. If found -> return stored password (no UI)
       3. If not found -> trigger OS credential dialog:
            Win: CredUIPromptForWindowsCredentials (or CredentialDialog)
            macOS: LAContext.evaluatePolicy or SecKeychainFind prompt
       4. Consent step: "Store this password for future opens?" checkbox
          (unchecked by default; FR-READ-009 implies explicit consent)
       5. If store consented -> write to OS credential store
  -> PDFium.LoadDocument(path, password) -> open PDF
  -> If still fails -> surface PasswordIncorrectException to user (try again)
```

Security rules:
- The password string is never written to the SQLite catalogue or any log.
- The password is wiped from managed memory after `FPDF_LoadDocument` (pin array,
  clear after use, where the managed wrapper allows).
- The OS credential store is the only persistence path; see ADR security controls.
- `Password_NeverStoredInCatalogue` test: after unlock, assert zero rows in any
  catalogue table contain the test password string.

### 7.4 Split-view scaffold (V2)

`SplitViewView.axaml` is a Grid with two `ContentControl` cells; both cells show
the same `V2PlaceholderView` with copy "Split view is coming in V2." The route
`navigation://split-view` is registered; a tracking item in the change log points
to the V2 implementation scope. The scaffold exists so the Phase 21 a11y audit
can cover the route and no breaking change is needed in V2.

### 7.5 Batch enrichment scale hardening

Phase 07 built the enrichment pipeline. Phase 15 hardens it for 2,000-book runs:

- `RateLimitedHttpClient` wrapper: per-provider rate limit (Google Books: 1 req/s;
  Open Library: 5 req/s); exponential backoff with jitter on 429/503.
- `BatchEnrichmentJob` chunk size: 50 books per chunk; each chunk is a recoverable
  unit (failure of chunk N does not retry chunks 0..N-1).
- Paused/failed state: visible in Health Dashboard with a "Resume" button;
  failed books list downloadable as CSV.
- `BatchEnrichment_2000Books_CompletesWithRetry` test: uses a mock HTTP client
  that returns 429 every 5th request; asserts all 2,000 books are eventually
  enriched and the job reaches `Completed` status.

### 7.6 Smart shelf query optimization

Phase 06 built virtual shelves. Phase 15 ensures queries are fast at 2,000 books:

- Analyze query plans via `EXPLAIN QUERY PLAN` in SQLite for the common
  smart-shelf filter combinations (tag + status, author + year range, shelf +
  rating).
- Add composite indices: `IX_Books_Status_Year`, `IX_ShelfBooks_ShelfId_BookId`,
  `IX_BookMetadataFields_FieldName_Value` (covering index for common filters).
- Benchmark `SmartShelf_QueryBenchmark_2000Books`: assert P95 ≤ 2 s (NFR-OGMA-002)
  on the 2,000-book synthetic corpus.

### 7.7 Cross-platform notes

| Concern | Windows | macOS |
| --- | --- | --- |
| Tesseract native binary | `win-x64` in `Tesseract` NuGet package | `osx-arm64` + `osx-x64` fat binary in NuGet package |
| OCR output quality | Tesseract 5 LSTM engine; `eng` language | Same; language data is identical |
| Password credential dialog | `CredUIPromptForWindowsCredentials` (Win32 P/Invoke via `AdvisoryServices` shim) | `LAContext.evaluatePolicy(.deviceOwnerAuthentication)` via macOS interop |
| Password credential storage | Windows Credential Manager (DPAPI-backed) | macOS Keychain Services |
| EF Core migration M015 | SQLite `ALTER TABLE` (add nullable columns); `Down()` drops columns | Same (SQLite cross-platform) |

---

## 8. Work breakdown (summary)

Full detail in `tasks.md`.

| WP | Work Package | Key tasks |
| --- | --- | --- |
| WP1 | Data model | EF Core migration M015; `IsOcrDerived`, `IsPasswordProtected` fields |
| WP2 | OCR pipeline | `OcrJob`, `OcrJobWorker`, Tesseract integration, resume logic, FTS5 enqueue |
| WP3 | OCR UI | `OcrJobStatus` in Index Manager; trigger button in Health Dashboard |
| WP4 | Password PDF | `IPasswordProvider`, OS credential dialog (Win + mac), consent, security tests |
| WP5 | Split-view scaffold | Route, ViewModel, V2 placeholder view |
| WP6 | Batch enrichment scale | `RateLimitedHttpClient`, chunk recovery, Health Dashboard pause/resume |
| WP7 | Smart-shelf perf | Composite indices, query plan review, benchmark |
| WP8 | Extension point | `IOcrProvider` extension-point interface (internal, seeded for Phase 23) |
| WP9 | Integration & golden-corpus | OCR searchability test; fault injection; password security test |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest** — `icons.md` lists OCR, password/unlock,
      split-view, batch, and smart-shelf-perf icons; procurement request appended.
- [x] **i18n (en/fr)** — All OCR status messages, password prompt labels, split-
      view placeholder, batch status, and query-plan UI copy resource-keyed; `fr`
      in same PR; pseudolocale check passes.
- [x] **Accessibility** — OCR trigger button and status chip keyboard-navigable;
      password dialog uses OS-native credential UI (already accessible per OS);
      split-view scaffold is keyboard-navigable (placeholder).
- [x] **Privacy/egress** — No egress in OCR pipeline (Tesseract runs locally);
      password stored in OS credential store only; `Password_NeverStoredInCatalogue`
      R2 test.
- [x] **Reversibility** — EF Core migration M015 has `Down()` (reversible);
      OCR-extracted text deletion is offered in the book-detail settings;
      credential can be cleared per-book in the reader toolbar.
- [x] **Performance budgets** — NFR-OGMA-009 (job recovery without duplicates);
      NFR-OGMA-002 (smart shelf ≤ 2 s P95 at 2,000 books).
- [x] **Bounded-context tests** — `OcrContext_HasNo_DirectDependency_On_Reader`;
      password handling is isolated to `Infrastructure.Security`.
- [x] **Documentation** — XML doc on `OcrJob`, `IPasswordProvider`; ADR-0004
      amended with Tesseract native binary decision; or new ADR-0013 created.

---

## 10. Definition of Done

- [ ] Every FR/NFR ID in section 5 has a passing test or tagged gap.
- [ ] Golden-corpus `scanned-image-only` fixture makes books searchable after OCR
      (`OcrJob_ScannedPdf_BecomesSearchable` passes).
- [ ] `dotnet format`, `dotnet build`, `dotnet test`, architecture tests pass on
      Windows and macOS CI.
- [ ] New strings in `en` + `fr`; pseudolocale check passes.
- [ ] OCR, password/unlock, split-view, and batch icons wired from `IconCatalog`
      with accessible labels; `icons.md` complete.
- [ ] `OcrJob_Recovery_AfterInterruption_NoDuplicatePages` fault-injection test
      passes (NFR-OGMA-009).
- [ ] `Password_NeverStoredInCatalogue` security test passes (FR-READ-009, R2).
- [ ] `PasswordPdf_UnlockViaOsCredentialFlow_Opens_Book` integration test passes
      (beta gate G2, FR-READ-009).
- [ ] `BatchEnrichment_2000Books_CompletesWithRetry` integration test passes
      (FR-META-006).
- [ ] `SmartShelf_QueryBenchmark_2000Books` asserts P95 ≤ 2 s (NFR-OGMA-002).
- [ ] EF Core migration M015 `Down()` tested.
- [ ] `SplitView_Route_Exists_ShowsV2Placeholder` test passes (FR-READ-012 V2 scaffold).
- [ ] `/code-review` and `/security-review` (password credential flow) completed;
      findings resolved.
- [ ] ADR-0004 or ADR-0013 updated with Tesseract binary decision.

---

## 11. Skills to use

Full detail in `skills.md`.

| Skill | Task |
| --- | --- |
| `sdlc-meta:advanced-testing-strategy` | WP2 OCR golden-corpus; WP9 fault injection |
| `devops-cloud:reliability-engineering` | WP2 OcrJob resume logic; WP6 batch enrichment recovery |
| `security-scanning:security-hardening` | WP4 password credential flow; R2 security test |
| `security:code-safety-scanner` | WP4 password memory hygiene; credential storage |
| `frontend-ux:interaction-design-patterns` | WP3 OCR status UI; WP5 split-view placeholder |
| `backend-databases:database-internals` | WP7 SQLite query plan; composite indices |
| `frontend-ux:frontend-performance` | WP7 smart-shelf benchmark |

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `OcrJob`, `OcrJobWorker`, `TesseractOcrAdapter` | `src/OgmaLibrary.Workers/Ocr/` |
| `IPasswordProvider`, `WindowsCredentialProvider`, `MacOsKeychainProvider` | `src/OgmaLibrary.Infrastructure/Security/` |
| `PasswordUnlockViewModel` | `src/OgmaLibrary.App/ViewModels/Reader/` |
| EF Core migration M015 | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/` |
| `SplitViewView`, `SplitViewViewModel` | `src/OgmaLibrary.App/Views/Reader/` |
| `RateLimitedHttpClient`, batch enrichment hardening | `src/OgmaLibrary.Infrastructure/Enrichment/` |
| SQLite composite indices (M015b migration) | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/` |
| `IOcrProvider` extension-point interface | `src/OgmaLibrary.Application/Ocr/Extensions/` |
| Integration tests (OCR + password + batch + shelf perf) | `tests/OgmaLibrary.Tests.Integration/` |
| OCR golden-corpus scenario | `tests/golden-corpus/ocr-pipeline/` |
| `icons.md` (Phase 15 manifest) | `docs/plans/grand-plan/phase-15/icons.md` |
| ADR-0013 (Tesseract binary decision) | `docs/adr/0013-ocr-tesseract-native.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Tesseract native binary not available for all platforms in the NuGet package | R3 | Phase 01 spike confirms; if missing, ship native binary as a separate sidecar with `RuntimeIdentifier`-conditioned copy |
| macOS password dialog API deprecation or sandbox restriction | R2 | Test on macOS 13+ in CI; use `LAContext` which is supported in sandboxed apps; document Keychain entitlement requirements for App Store |
| OCR quality too low for useful search on scanned PDFs | R5 | Phase 15 ships "best effort"; mark OCR-derived in UI so user is informed; future improvement via language-data or alternative engine |
| `OcrJob` restart causes out-of-memory on very large PDFs (1,000+ pages) | R4 | Process pages in chunks of 50; dispose `Bitmap` and `TextPage` after each page; memory profile test on `very-large` golden-corpus fixture |
| Batch enrichment at 2,000 books exceeds rate limit budget | R5 | `RateLimitedHttpClient` with exponential backoff; user can pause and resume; Health Dashboard shows current rate and ETA |
| Smart-shelf query regression if new filters added post-Phase 15 | R3 | Benchmark is a CI fixture; any new filter combination that exceeds 2 s P95 fails the benchmark on the next PR that introduces it |

---

## 14. Owner asks

1. **Icon procurement.** Procure the icon set listed in `icons.md` (OCR,
   password/unlock, split-view, batch, smart-shelf perf icons) in the Phase 03
   style.

2. **Tesseract decision.** Phase 01 spike should have recorded whether
   `Tesseract.NET` is the chosen wrapper or an alternative. Confirm the ADR
   number (ADR-0013 proposed here) and the exact native binary packaging strategy
   for MSIX and DMG.

3. **Password credential consent wording.** The consent checkbox before storing
   a PDF password in the OS credential store needs final en/fr copy. Draft en:
   "Remember this password on this device (stored securely in your system
   keychain)." French copy needs native review.

4. **OCR language scope.** Confirm that English-only (`eng.traineddata`) is
   acceptable for the V1 shipping build, and that additional language data is
   a V2 feature. This affects the size of the app bundle.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Chwezi Core Systems | v1.0 baseline authored |
| 2026-06-01 | Implementation | Started WP1 data model: OCR/password flags, extracted-page source tracking, source-aware OCR indexing, reversible migration, and focused schema tests. |
| 2026-06-01 | Implementation | Added WP2 OCR processor, native Tesseract provider seam, English tessdata packaging, hosted OCR worker, and resumable job tests. |
| 2026-06-01 | Implementation | Added WP3 OCR job status projection in Index Manager with localized active-job and page-progress display. |
| 2026-06-01 | Implementation | Added WP5 split-view V2 scaffold route, view model, view, localized placeholder, and shell navigation test. |
| 2026-06-01 | Implementation | Started WP4 password flow: provider contract, Windows Credential Manager provider, unlock view model, and no-plaintext-catalogue tests. |
| 2026-06-01 | Implementation | Added WP4 protected-reader handoff: password-aware session open, PDFium adapter password support, known-password render test, and buffer-clearing checks. |
| 2026-06-01 | Implementation | Started WP6 batch enrichment scale hardening with provider-specific rate limiting and bounded 429/503 retry handling for metadata HTTP clients. |
| 2026-06-01 | Implementation | Added WP6 batch chunk metadata: enrichment jobs are tagged in recoverable 50-book chunks while retaining per-book retry isolation. |
