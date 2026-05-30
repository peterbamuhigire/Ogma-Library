# Phase 07 — Metadata Enrichment & Collection Health

Intelligently enrich every book's metadata from trusted providers, let the user
review and control every field, write back to PDF only under strict backup-first
guarantees, and give the power-librarian a real-time dashboard of the collection's
health.

---

## 1. Title & one-line mission

**Phase 07 — Metadata Enrichment & Collection Health.**
Implement ISBN detection from all four sources, reversible lookup against Google Books
and Open Library with a principled confidence-merge model, field-level provenance,
optional backup-verified PDF write-back, batch enrichment under rate limits, a
metadata quality score, and the library health dashboard — satisfying FR-META-001
through FR-META-007, NFR-PROD-010, and ADR-0008.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | MVP (FR-META-001..003, ISBN detection, provider lookup, confidence merge, review UI); V1 (FR-META-004..007, provenance, PDF write-back, batch enrichment, quality score, health dashboard) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | Original Phase 3 (metadata repair) + V1 collection health |
| **Platforms** | Windows 10+ (x64/ARM64) and macOS 12+ (x64/Apple Silicon) |
| **Status** | Planned — not yet started |

---

## 3. Objectives

When this phase is done, all of the following are true:

1. ISBN-10 and ISBN-13 are detected from all four sources (filename, XMP, PDF DocInfo,
   first pages of content) with normalization, check-digit validation, and source-priority
   ranking (FR-META-001).
2. Detected ISBNs trigger a lookup against Google Books and Open Library; results are
   stored with `Provider`, `Timestamp`, and `Confidence`; both providers are called
   concurrently (FR-META-002).
3. The user can review proposed metadata in a confidence-merge UI — accept, reject, or
   manually override individual field values — with a clear display of source and
   confidence; the merge model uses the SRS §8.3 formula (FR-META-003).
4. Field-level provenance (V1): every `BookMetadataField` carries `Source`,
   `SourceTimestamp`, and `Confidence`; the book-detail panel shows provenance
   indicators per field (FR-META-004).
5. PDF write-back (V1): the user can push accepted metadata back into the PDF's DocInfo
   after a backup is taken, a field diff is confirmed, and integrity is verified;
   the original is restored if the write fails (FR-META-005, NFR-PROD-010, ADR-0008).
6. Batch enrichment (V1): the user can trigger enrichment for multiple books; the job
   respects API rate limits (configurable tokens-per-second), shows per-book
   progress, and surfaces paused/failed books (FR-META-006).
7. Each book has a metadata quality score (V1) computed from field completeness and
   confidence; the filter panel can filter to books below a threshold (FR-META-007).
8. The library health dashboard (V1) surfaces duplicates, missing covers/ISBNs,
   unavailable files, and failed jobs in a single power-librarian panel — actionable,
   not just informational.

---

## 4. Scope

### In scope

- `IIsbnDetectionService`: detect from filename (regex), XMP (PdfPig XMP reader),
  DocInfo (PdfPig `DocumentInformation.Subject/Keywords`), and first-page text
  (simple regex scan of the first 2 pages via PdfPig text extraction); normalize to
  ISBN-13; validate Luhn check digit; rank by source priority
  (DocInfo > XMP > first-page > filename).
- `IMetadataProviderClient` interface + `GoogleBooksClient` + `OpenLibraryClient`
  implementations; both behind the single `IMetadataProviderAggregator` which calls
  them concurrently and merges results.
- Confidence merge model (SRS §8.3): resolving the context gap on provider trust
  weights and field-match scoring — a concrete formula is adopted in this phase (see §7).
- `IConfidenceMergeService`: accepts multi-provider lookup results + existing fields;
  produces a `MergedMetadataProposal` per field with `AcceptedValue`, `Confidence`,
  `Source`, and `AlternativeValues[]`.
- Enrichment review UI: slide-in panel (accessible from book-detail "Enrich" button
  in Phase 06); shows current value vs. proposed value per field; Accept / Reject /
  Edit inline per field; "Accept all above 0.8" bulk accept.
- `ICatalogueWriteService.ApplyMergedMetadataAsync()`: writes accepted fields to
  `BookMetadataFields`; records `MetadataLookup` row; writes `AuditEvent`.
- Field-level provenance (V1): `BookMetadataField.Source`, `SourceTimestamp`,
  `Confidence`, `IsOverridden` — all already in the Phase 04 schema; this phase
  populates and displays them.
- PDF write-back (V1, FR-META-005, ADR-0008):
  - Backup `originalfile.pdf` → `.ogma/backups/<timestamp>_pre_writeback_<sha8>.pdf`.
  - Write accepted metadata fields to PDF DocInfo using PDFsharp.
  - SHA-256 verify the output file is valid PDF (PdfPig can open it).
  - On any failure: restore from backup; write `AuditEvent`; surface error to user.
  - User must confirm the field diff before write-back proceeds.
- Batch enrichment (V1, FR-META-006): `BatchEnrichmentJob` — background `Job` rows
  per book; token-bucket rate limiter (configurable tokens/second); per-book status
  visible in a batch progress panel; paused/failed books retryable.
- Metadata quality score (V1, FR-META-007): computed score 0.0-1.0 per book based on
  field completeness (which fields are present) and average confidence of enriched
  fields; stored in `Books.QualityScore`; updated on any metadata write.
- Library health dashboard (V1): dedicated panel with 5 sections:
  - **Duplicates**: books with matching ISBN or SHA-256 in the same library.
  - **Missing covers**: books without a cover sidecar file.
  - **Missing ISBNs**: books without a validated ISBN.
  - **Unavailable files**: books with `Status = Unavailable`.
  - **Failed jobs**: jobs with `Status = Failed` (any type).
  Each section lists affected books with actions (Enrich, Rescan, Remove, Retry).
- Rate-limit configuration in Settings: Google Books RPM and Open Library RPM
  (defaults: 30 and 60 RPM respectively).
- All API calls route through the `IAiProvider` / general HTTP egress layer for
  audit (not AI but same privacy-egress contract — provider calls are logged to
  `MetadataLookups` and `AuditEvents`).
- All UI strings in `en` + `fr`; full WCAG 2.2 AA keyboard + screen-reader.
- Rich icon set (see `icons.md`): health dashboard, enrich, ISBN, merge/diff, write-back,
  quality score, duplicate, provider source badges.

### Explicitly out of scope

- Full-text search (Phase 10); the health dashboard may link to search but does not
  build it.
- AI-generated metadata (Phase 13); the AI field group in book-detail is read-only here.
- Batch enrichment for full-text content (Phase 15).
- OCR-based ISBN detection (Phase 15, FR-READ-010).
- The DPIA for Google Books / Open Library API calls is drafted in Phase 19; this phase
  implements the call with a payload-preview (titles/ISBNs only, no content) as the
  minimum privacy control.
- Calibre / Zotero importer (Phase 23).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-META-001 | MVP | ISBN detection from 4 sources; normalize, validate, rank | `IsbnDetection_AllFourSources_DetectAndNormalize`; `IsbnDetection_InvalidCheckDigit_Rejected` |
| FR-META-002 | MVP | Google Books + Open Library lookup; store source, timestamp, confidence | `MetadataLookup_BothProviders_CalledConcurrently`; `MetadataLookup_ResultsStoredWithProvenance` |
| FR-META-003 | MVP | Reviewable confidence merge; accept/reject/edit | `ConfidenceMerge_ProducesProposal_PerField`; `EnrichmentUI_AcceptAll_AppliesProposals` |
| FR-META-004 | V1 | Field-level provenance | `FieldProvenance_DisplayedInBookDetail`; `FieldProvenance_IsOverride_Respected` |
| FR-META-005 | V1 | PDF write-back with backup + diff + verify + restore | `PdfWriteBack_BackupBeforeWrite`, `PdfWriteBack_RestoredOnFailure`, `PdfWriteBack_DiffConfirmedByUser` |
| FR-META-006 | V1 | Batch enrichment under rate limits; paused/failed visible | `BatchEnrichment_RateLimit_Respected`; `BatchEnrichment_PausedFailed_Visible` |
| FR-META-007 | V1 | Metadata quality score; filter books below threshold | `QualityScore_ComputedPerBook`; `Filter_BelowQualityThreshold_Works` |
| NFR-PROD-010 | V1 | Reversible transactional destructive ops (write-back) | `PdfWriteBack_RestoredOnFailure` (R1) |
| NFR-PROD-011 | MVP | Privacy-tier + payload preview for provider calls | `ProviderLookup_PayloadPreview_ShownBeforeFirstCall` |
| ADR-0008 | MVP | DB-first annotations/metadata; PDF write-back later | Confirmed by enrichment-first approach; write-back V1 only |
| SRS §8.3 gap | MVP | Provider trust weights + field-match scoring formula | Formula documented; `ConfidenceMerge_UsesFormula_Correctly` unit test |
| CTRL-OGMA-018 | MVP | Local audit trail | `MetadataLookup_WritesAuditEvent`; `PdfWriteBack_WritesAuditEvent` |

---

## 6. Dependencies

### Depends on

| Phase / Decision | What is needed |
| --- | --- |
| Phase 04 — Data Layer | `BookMetadataFields` (Source, Confidence, IsOverridden), `MetadataLookups`, `AuditEvents`, `Jobs` tables |
| Phase 05 — Ingestion | `IIsbnDetectionService` extends `IMetadataExtractionService` (DocInfo/XMP ISBN); Phase 05 populates the initial `IsbnNormalized` |
| Phase 06 — Browsing | "Enrich" button in `BookDetailView`; filter by quality score; health dashboard entry in sidebar |
| Phase 03 — Design System | Localization, icon tokens, command palette |
| Phase 00 — Decision Closure | Provider trust weights confirmed (SRS §8.3 gap); rate-limit defaults confirmed |
| Phase 01 — Risk Spikes | HTTP egress strategy confirmed; PDFsharp write-back validated |

### Unblocks

- **Phase 10** — Search index benefits from enriched metadata (Title, Author, ISBN
  quality improved).
- **Phase 12** — AI Gateway shares the payload-preview pattern established here.
- **Phase 15** — OCR-based ISBN detection extends `IIsbnDetectionService`.
- **Phase 19** — DPIA for provider calls drafted here; Phase 19 hardens the full
  security posture.
- **Phase 20** — Write-back backup/restore is one of the 8 public-beta gates (G5).

---

## 7. Architecture & approach

### Bounded context touched

**Metadata Enrichment** — a distinct bounded context. It reads `Books` and
`BookMetadataFields` via `ICatalogueReadModel`; it writes via `ICatalogueWriteService`
and `IMetadataWriteService`. It never touches `CatalogueDbContext` directly.

### ISBN detection pipeline (FR-META-001)

Source priority (highest → lowest):
1. **DocInfo** (`Document.Information.Subject` / `Keywords`): provided by PdfPig.
2. **XMP**: `dc:identifier` or `prism:isbn` namespaces.
3. **First two pages of text**: regex `\b(?:ISBN[-: ]?(?:97[89])?[\d- ]{9,17}\d)\b`.
4. **Filename**: same regex applied to the file's base name.

`IIsbnDetectionService.DetectAsync(filePath)` returns `IsbnDetectionResult`:
`{ SourceRanked: IsbnSource[], BestIsbn: Isbn?, AllCandidates: Isbn[] }`.

Normalization: strip hyphens/spaces; convert ISBN-10 to ISBN-13 (multiply-add
check digit); validate Luhn mod-10/mod-11.

### Confidence merge model (resolves SRS §8.3 context gap)

**Formula (adopted here; owner approval requested before implementation):**

```
FieldConfidence(f, provider) =
  ProviderWeight(provider)                       // see trust weights below
  × MatchScore(f, existingValue)                 // 0.0–1.0 string similarity
  × RecencyBonus(lookupAge)                      // 1.0 for < 7 days, 0.85 for < 30 days, 0.7 older

MergedConfidence(f) = max over providers of FieldConfidence(f, p)
```

**Provider trust weights (proposed defaults — owner to confirm):**

| Provider | Weight |
| --- | --- |
| Google Books | 0.85 |
| Open Library | 0.80 |
| User manual override | 1.00 (always accepted) |
| PDF DocInfo | 0.50 |

**Field-match scoring:** normalized Levenshtein distance for `Title` and `Author`;
exact-match for `ISBN` and `Year`; cosine-similarity for `Description`.

All parameters are configurable in settings (a JSON settings blob); the formula
and defaults are recorded in `docs/architecture/confidence-merge-formula.md`.

### Provider clients (FR-META-002)

`IGoogleBooksClient.LookupAsync(isbn13) → GoogleBooksResult`: HTTP GET to
`https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}`; parse `items[0].volumeInfo`.

`IOpenLibraryClient.LookupAsync(isbn13) → OpenLibraryResult`: HTTP GET to
`https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data`.

Both clients:
- Are called concurrently via `Task.WhenAll`.
- Have configurable `HttpClient` timeout (default 10 s).
- Handle `HttpRequestException` and `TaskCanceledException` gracefully; log to
  `MetadataLookups` with `Confidence = 0.0` on failure.
- Are injected via `IHttpClientFactory` (named clients with retry policy via Polly).
- Route through the privacy-egress audit: `AuditEvents(EventType = "ProviderLookup",
  EntityId = BookId, BeforeJson = null, AfterJson = serialized ISBN + provider name)`.

**Privacy / payload preview (NFR-PROD-011):** the first provider call for a library
shows a payload-preview toast: "We will send the following data to Google Books:
[ISBN, title]". The user can cancel. This preview is shown once per library per
provider (persisted in settings). All calls are metadata-only (no content).

### PDF write-back (FR-META-005, ADR-0008)

Sequence:
1. User clicks "Write to PDF" in enrichment review panel.
2. `PdfWriteBackService` presents a field diff: current DocInfo fields vs. accepted
   enrichment values.
3. User confirms.
4. Backup: `File.Copy(original.pdf, .ogma/backups/<ts>_<sha8>.pdf)`.
5. Write: PDFsharp opens `original.pdf`, updates `DocumentInformation` fields,
   saves as `original.pdf` (in-place via a temp file → rename).
6. Verify: PdfPig opens the output file; asserts it is a valid PDF and that
   written fields round-trip correctly.
7. On any exception in steps 5-6: restore backup → delete output temp file → log
   `AuditEvent(EventType = "WriteBackFailed")` → surface error to user.
8. On success: update `Books.Sha256Hash` (file content changed), `Books.MtimeTicks`;
   log `AuditEvent(EventType = "WriteBackSucceeded")`.

### Batch enrichment (FR-META-006)

- `BatchEnrichmentOrchestrator.StartAsync(bookIds, ct)`: creates one
  `Job(JobType = Enrich, IdempotencyKey = SHA256(bookId))` per book.
- `EnrichmentWorker` (BackgroundService): dequeues enrichment jobs; applies
  token-bucket rate limiter (Polly's `RateLimiter` or `System.Threading.RateLimiting`).
- Rate limit defaults: Google Books 30 RPM, Open Library 60 RPM — configurable in
  settings.
- Batch progress panel: progress bar, current book, completed/failed counts, Pause /
  Resume / Cancel buttons.
- Failed books show the error and a per-item Retry button.

### Library health dashboard (power-librarian persona)

Five sections with actionable items:

| Section | Data source | Primary action |
| --- | --- | --- |
| Duplicates | Books with same `IsbnNormalized` OR same `Sha256Hash` in the library | Merge (deferred to Phase 06 bulk-edit) / Flag |
| Missing covers | `Books` with no `Cover` sidecar file | Regenerate thumbnail |
| Missing ISBNs | `Books WHERE IsbnNormalized IS NULL` | Enrich |
| Unavailable files | `Books WHERE Status = 'Unavailable'` | Rescan / Remove record |
| Failed jobs | `Jobs WHERE Status = 'Failed'` | Retry / Dismiss |

The dashboard VM aggregates these via 5 targeted `ICatalogueReadModel` queries;
total load < 500 ms for a 2,000-book library (instrumented in tests).

### Metadata quality score (FR-META-007)

```
QualityScore(book) =
  (fieldPresenceScore × 0.6) + (averageConfidence × 0.4)

fieldPresenceScore =
  (count of present core fields) / (count of core fields)
  core fields: Title, Author, ISBN, Year, Publisher, Cover, Description
```

`Books.QualityScore` (REAL, 0.0-1.0) is updated by
`ICatalogueWriteService.RecalculateQualityScoreAsync(BookId)` after any metadata
write. Phase 06 filter panel is extended with a "Quality below X%" filter chip.

### Cross-platform notes

- HTTP clients: `HttpClient` is cross-platform; no Win/macOS difference.
- PDFsharp write-back: confirmed cross-platform in Phase 01 spike.
- File backup `File.Copy`: cross-platform; temp-file rename is atomic on macOS
  (`File.Move` with `overwrite = true`); on Windows requires target deletion first
  (handle this explicitly).
- Rate limiter: `System.Threading.RateLimiting.TokenBucketRateLimiter` (.NET 7+,
  available in .NET 10 LTS); no platform difference.

---

## 8. Work breakdown (summary)

Full detail in `tasks.md`.

| WP | Title | Key tasks |
| --- | --- | --- |
| WP1 | ISBN detection (all 4 sources) | `IIsbnDetectionService`; regex; PdfPig XMP + DocInfo + text extraction |
| WP2 | Provider clients | `IGoogleBooksClient`; `IOpenLibraryClient`; concurrent call; `IMetadataProviderAggregator`; retry/timeout |
| WP3 | Confidence merge model | `IConfidenceMergeService`; SRS §8.3 formula; field-scoring; `MergedMetadataProposal` |
| WP4 | Enrichment review UI | Slide-in enrichment panel; per-field accept/reject/edit; "Accept all" above threshold |
| WP5 | Apply merge + provenance | `ApplyMergedMetadataAsync`; `BookMetadataFields` write; `AuditEvents`; `MetadataLookups` |
| WP6 | PDF write-back (V1) | `PdfWriteBackService`; backup → diff → write → verify → restore; `AuditEvents` |
| WP7 | Batch enrichment (V1) | `BatchEnrichmentOrchestrator`; `EnrichmentWorker`; token-bucket rate limit; batch progress UI |
| WP8 | Metadata quality score (V1) | `QualityScore` formula; `Books.QualityScore` column + migration; quality filter chip |
| WP9 | Library health dashboard (V1) | `HealthDashboardViewModel`; 5 sections; actionable items; icons |
| WP10 | Privacy payload preview | Payload-preview toast for first provider call; settings persistence |
| WP11 | Tests, performance gate, docs | Full test suite; write-back fault injection; dashboard load benchmark |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons** — health dashboard, enrich, ISBN, merge/diff, write-back,
      quality score, duplicate, provider source badges defined in `icons.md`;
      procurement request issued to owner.
- [x] **i18n (en/fr)** — enrichment panel labels, confidence levels, health section
      titles, write-back diff dialog, batch progress copy, quality-score labels, and
      all error/toast strings externalized; `fr.resx` complete for all MVP surfaces.
- [x] **Accessibility** — enrichment review panel keyboard-navigable; accept/reject
      per field accessible via keyboard; health dashboard sections Tab-navigable;
      batch progress panel has `role=progressbar`; write-back diff modal fully
      keyboard-operable.
- [x] **Privacy/egress** — provider calls go to Google Books and Open Library only;
      payload = ISBN + title only (no content); payload-preview toast shown before
      first call per library per provider (NFR-PROD-011); all calls logged to
      `AuditEvents` (CTRL-OGMA-018); `MetadataLookups` table records every outbound
      request.
- [x] **Reversibility (R1)** — PDF write-back: backup → diff → verify → restore;
      fault-injection test; inline metadata edit undo via `ICommandHistory` (Phase 06);
      no irreversible metadata operation.
- [x] **Performance budgets** — health dashboard load < 500 ms for 2,000 books;
      enrichment review panel opens < 300 ms; both instrumented in CI.
- [x] **Bounded-context tests** — `MetadataEnrichment_DoesNotInstantiate_CatalogueDbContext`;
      `ProviderClients_RouteThroughHttpClientFactory` (no raw `new HttpClient()`).
- [x] **Documentation** — XML doc comments; `docs/architecture/confidence-merge-formula.md`;
      `docs/architecture/pdf-writeback-protocol.md`; ADR-0008 ratified.

---

## 10. Definition of Done

Global DoD (README §6) plus:

- [ ] `IsbnDetection_AllFourSources_DetectAndNormalize` passes on golden-corpus
      `isbn-in-xmp.pdf` and `isbn-in-docinfo.pdf`.
- [ ] `MetadataLookup_BothProviders_CalledConcurrently`: both clients called with
      `Task.WhenAll`; total time < sum of sequential times.
- [ ] `ConfidenceMerge_ProducesProposal_PerField`: `MergedMetadataProposal` contains
      entries for Title, Author, ISBN, Year, Publisher, Description.
- [ ] `EnrichmentUI_AcceptAll_AppliesProposals`: "Accept all" with threshold 0.8
      writes only fields with `MergedConfidence ≥ 0.8` to `BookMetadataFields`.
- [ ] `ProviderLookup_PayloadPreview_ShownBeforeFirstCall`: first call to Google Books
      presents a payload-preview toast with the exact ISBN and title to be sent.
- [ ] `PdfWriteBack_BackupBeforeWrite` (R1): backup file present in `.ogma/backups/`
      before write-back proceeds.
- [ ] `PdfWriteBack_RestoredOnFailure` (R1): injected failure after write → original
      PDF byte-identical; `AuditEvent(WriteBackFailed)` present.
- [ ] `PdfWriteBack_DiffConfirmedByUser`: write-back cannot proceed without user
      confirming the field diff dialog.
- [ ] `BatchEnrichment_RateLimit_Respected`: 10 books enriched; assert elapsed time
      ≥ (10 / configured RPM) × 60 s.
- [ ] `QualityScore_ComputedPerBook`: assert `Books.QualityScore` is in [0.0, 1.0]
      and changes after metadata write.
- [ ] Health dashboard: all five sections load < 500 ms for a 2,000-book corpus.
- [ ] All five health-section actions (Enrich, Regenerate, Rescan, Remove, Retry)
      trigger the correct service call.
- [ ] `MetadataEnrichment_DoesNotInstantiate_CatalogueDbContext` architecture test green.
- [ ] All write-back audit events present in `AuditEvents` for both success and failure.
- [ ] Builds and tests pass on **both** Windows and macOS CI runners.
- [ ] `en` + `fr` resource keys complete for all MVP surfaces; pseudolocale passes.

---

## 11. Skills to use

Full invocation detail in `skills.md`.

- `backend-databases:database-design-engineering` — provenance model, quality score
  column migration, MetadataLookups indexing.
- `architecture:api-error-handling` — `IGoogleBooksClient` / `IOpenLibraryClient`
  retry/timeout/circuit-breaker via Polly.
- `devops-cloud:reliability-engineering` — batch enrichment job rate limiter,
  resumable jobs, crash recovery.
- `frontend-ux:data-visualization` — health dashboard section counts, quality score
  histogram, batch progress bar.
- `sdlc-meta:advanced-testing-strategy` — PDF write-back fault injection; R1 test
  design for write-back restore path.
- `superpowers:brainstorming` — before WP3 (confidence merge formula design) and WP9
  (health dashboard UX) to explore options.
- `frontend-ux:premium-ui-ux-design` — enrichment review panel design; health
  dashboard visual language.
- `documentation-generation:architecture-decision-records` — ratify ADR-0008; draft
  `confidence-merge-formula.md` and `pdf-writeback-protocol.md`.
- `/code-review` — after WP6 (write-back, safety-critical) and WP9 (health dashboard).
- `/security-review` — WP2 (provider clients) and WP10 (payload preview): confirm
  no content leaks to providers.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `IIsbnDetectionService` + impl | `OgmaLibrary.Infrastructure/Metadata/` |
| `IGoogleBooksClient` + `IOpenLibraryClient` + impls | `OgmaLibrary.Infrastructure/Metadata/Providers/` |
| `IMetadataProviderAggregator` + impl | `OgmaLibrary.Infrastructure/Metadata/` |
| `IConfidenceMergeService` + impl | `OgmaLibrary.Application/Metadata/` |
| `EnrichmentReviewView` + `EnrichmentReviewViewModel` | `OgmaLibrary.App/Views/Metadata/` |
| `ICatalogueWriteService.ApplyMergedMetadataAsync` extension | `OgmaLibrary.Infrastructure/Catalogue/` |
| `PdfWriteBackService` | `OgmaLibrary.Infrastructure/Metadata/` |
| `BatchEnrichmentOrchestrator` + `EnrichmentWorker` | `OgmaLibrary.Workers/` |
| Batch progress panel (`BatchEnrichmentView`) | `OgmaLibrary.App/Views/Metadata/` |
| `HealthDashboardView` + `HealthDashboardViewModel` | `OgmaLibrary.App/Views/Health/` |
| `Books.QualityScore` migration | `OgmaLibrary.Infrastructure/Persistence/Migrations/` |
| `en.resx` + `fr.resx` metadata enrichment keys | `OgmaLibrary.App/Resources/` |
| Icon assets (placeholder or procured) | `OgmaLibrary.App/Assets/icons/metadata/`, `.../health/` |
| Tests (unit + integration + fault-injection) | `OgmaLibrary.Tests/Metadata/` |
| `docs/architecture/confidence-merge-formula.md` | `docs/architecture/` |
| `docs/architecture/pdf-writeback-protocol.md` | `docs/architecture/` |
| ADR-0008 ratified | `docs/adr/` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Google Books API rate limit (100 req/day free tier) causes slow batch enrichment | R3 | Default 30 RPM in config; user can provide API key for higher quota; display warning when approaching limit; pause job automatically |
| Open Library API flakiness (known downtime) | R4 | Polly retry (3 attempts, exponential backoff); if both providers fail, job status = `Failed`; user notified |
| PDF write-back corrupts a PDF file (R1 risk) | R1 | Backup ALWAYS before write; verify with PdfPig after write; restore if verify fails; this path has dedicated fault-injection tests |
| PDFsharp does not support all PDF encryption variants | R1 | If PDFsharp cannot open an encrypted PDF for write-back, surface an error ("Write-back unavailable for encrypted PDFs") and skip; no silent corruption |
| Confidence merge formula produces unexpected results for non-English titles | R5 | Levenshtein distance on Unicode strings; test with golden corpus `non-english.pdf`; owner can adjust weights in settings |
| Health dashboard query performance on large libraries (> 2,000 books) | R3 | Five targeted queries with covering indexes; benchmark with 2,000-book synthetic corpus; < 500 ms gate in testing |
| SRS §8.3 formula not yet approved by owner | R5 | Formula is proposed here; WP3 is blocked until owner confirms or amends; interim = simple max-confidence-wins |

---

## 14. Owner asks

1. **Confidence merge formula approval** (resolves SRS §8.3 gap): review the formula
   in §7 (provider trust weights, field-match scoring, recency bonus); confirm or
   amend before WP3 begins. This is the most important decision for this phase.
2. **Provider trust weights**: confirm the proposed weights (Google Books 0.85, Open
   Library 0.80) or provide preferred values.
3. **Rate-limit defaults**: confirm 30 RPM (Google Books) and 60 RPM (Open Library)
   as the default rate limits for batch enrichment.
4. **PDF write-back tier**: FR-META-005 is V1 — confirm this is acceptable to defer
   from the MVP release (i.e., enrichment in MVP writes to the database only, not
   back to the PDF file).
5. **Health dashboard MVP vs. V1**: the full health dashboard (FR-META-007, all five
   sections) is V1 tier. Confirm whether a minimal health summary (failed jobs count
   only) should appear at MVP.
6. **Icon procurement request** — see `icons.md` for the full list of ~25 icons.
   Please procure before WP9 (health dashboard) and WP4 (enrichment UI) finalization.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand-plan agent | Initial draft authored. |
