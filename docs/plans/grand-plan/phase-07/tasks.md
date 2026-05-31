# Phase 07 — Tasks

Task IDs: `P07-WPN-TN`. Traceability to FR/NFR/CTRL/ADR, estimates in hours
(ideal), and within-phase dependencies.

---

## Current Progress — 2026-05-31

Completed / implemented:

- WP1 core ISBN detection service exists and is covered by metadata tests.
- WP2 provider clients now cover Google Books and Open Library ISBN lookup plus
  title/author fallback search. Results are stored with provenance and audit
  events through `MetadataProviderAggregator`.
- WP3 confidence merge is implemented and now includes cover URL, rating,
  ratings count, page count, and language fields.
- WP5 apply/provenance is implemented for `BookMetadataFields`; accepted Title,
  Author, ISBN, and Year are also mirrored into catalogue tables used by the UI.
- WP6 service-level PDF DocInfo write-back exists with backup, diff, verify, and
  restore behavior; runtime path validation now uses the selected library root.
- WP7 job execution now processes `Enrich` jobs through `BookIngestionWorker` and
  queues enrichment after local metadata extraction.
- Runtime DI registers metadata enrichment services in the app composition root.

Still pending:

- WP4 enrichment review UI and per-field accept/reject/edit controls.
- WP7 token-bucket provider rate limits, pause/resume/cancel UI, and per-book
  progress panel.
- Provider settings for ISBNdb, Amazon Product Advertising API, and API League.
- Cover image download/caching into sidecar assets.
- First-run payload preview/consent before online metadata calls.

## WP1 — ISBN detection (all four sources)

**Goal:** `IIsbnDetectionService` detects ISBN-10/13 from filename, XMP, DocInfo,
and first-page text; normalizes to ISBN-13; validates the check digit; returns
ranked results.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP1-T1 | Implement `IsbnRegex`: compile once; pattern covers ISBN-10 and ISBN-13 with optional hyphens/spaces; unit test against 20 known ISBN strings including edge cases. | FR-META-001 | 2 | Phase 04 `Isbn` value object |
| P07-WP1-T2 | Implement `IsbnDetectionService.DetectFromFilename(path)`: apply `IsbnRegex` to base name only; return `IsbnSource.Filename`. | FR-META-001 | 1 | P07-WP1-T1 |
| P07-WP1-T3 | Implement `DetectFromDocInfo(filePath)`: open PDF via PdfPig; read `DocumentInformation.Subject`, `.Keywords`, `.Title`; apply `IsbnRegex`. | FR-META-001 | 2 | Phase 05 PdfPig integration |
| P07-WP1-T4 | Implement `DetectFromXmp(filePath)`: read XMP metadata from PdfPig; scan `dc:identifier` and `prism:isbn` namespaces; apply `IsbnRegex`. | FR-META-001 | 2 | P07-WP1-T3 |
| P07-WP1-T5 | Implement `DetectFromFirstPages(filePath)`: extract text from pages 0-1 via PdfPig; apply `IsbnRegex`; return all unique candidates. | FR-META-001 | 2 | P07-WP1-T3 |
| P07-WP1-T6 | Implement normalization: ISBN-10 → ISBN-13 conversion; strip hyphens/spaces; validate Luhn check digit (mod-10 for ISBN-13, mod-11 for ISBN-10); return `Isbn.Invalid` on failure. | FR-META-001 | 2 | Phase 04 `Isbn` value object |
| P07-WP1-T7 | Implement source-priority ranking: DocInfo > XMP > FirstPage > Filename; `DetectAsync` returns `IsbnDetectionResult { BestIsbn, AllCandidates, SourceRanked }`. | FR-META-001 | 1 | P07-WP1-T2..T6 |
| P07-WP1-T8 | Integration test: `IsbnDetection_DocInfo_HigherPriority_ThanFilename` — PDF with ISBN in DocInfo and a different ISBN in filename; assert DocInfo wins. | FR-META-001 | 1 | P07-WP1-T7 |
| P07-WP1-T9 | Integration test: `IsbnDetection_InvalidCheckDigit_Rejected` — ISBN with wrong check digit in first page; assert not in `AllCandidates`. | FR-META-001 | 1 | P07-WP1-T6 |
| P07-WP1-T10 | Golden-corpus test: run `DetectAsync` on `isbn-in-xmp.pdf` and `isbn-in-docinfo.pdf`; assert `BestIsbn` is the known ISBN for each. | FR-META-001 | 1 | P07-WP1-T7 |

---

## WP2 — Provider clients & aggregator

**Goal:** `IGoogleBooksClient` and `IOpenLibraryClient` call their respective APIs
concurrently, with retry, timeout, and result serialized to `MetadataLookup` rows.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP2-T1 | Register `HttpClient` named clients in DI (`GoogleBooks`, `OpenLibrary`) with `BaseAddress`, timeout (10 s), and Polly retry policy (3 retries, exponential backoff, jitter). | FR-META-002 | 2 | Phase 02 DI |
| P07-WP2-T2 | Implement `GoogleBooksClient.LookupAsync(isbn13)`: GET `volumes?q=isbn:{isbn}`; deserialize `VolumeInfo`; map to `ProviderMetadataResult`. | FR-META-002 | 3 | P07-WP2-T1 |
| P07-WP2-T3 | Implement `OpenLibraryClient.LookupAsync(isbn13)`: GET `api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data`; deserialize; map to `ProviderMetadataResult`. | FR-META-002 | 3 | P07-WP2-T1 |
| P07-WP2-T4 | Implement `MetadataProviderAggregator.AggregateAsync(isbn13, ct)`: `Task.WhenAll([googleClient, openLibraryClient])`; catch per-client exceptions; persist each result to `MetadataLookups`; write `AuditEvent`. | FR-META-002; CTRL-OGMA-018 | 3 | P07-WP2-T2; P07-WP2-T3 |
| P07-WP2-T5 | Integration test: `MetadataLookup_BothProviders_CalledConcurrently` — mock clients with 200 ms delay each; assert `AggregateAsync` total < 400 ms (concurrent, not serial). | FR-META-002 | 2 | P07-WP2-T4 |
| P07-WP2-T6 | Integration test: `MetadataLookup_ResultsStoredWithProvenance` — assert `MetadataLookups` row has `Provider`, `Timestamp`, `Confidence` non-null after `AggregateAsync`. | FR-META-002 | 1 | P07-WP2-T4 |
| P07-WP2-T7 | Integration test: `MetadataLookup_ProviderFails_OtherResultStored` — Google Books mock throws; assert Open Library result stored; overall returns partial result, not exception. | FR-META-002 | 1 | P07-WP2-T4 |
| P07-WP2-T8 | Architecture test: `ProviderClients_RouteThroughHttpClientFactory` — no `new HttpClient()` in the metadata namespace. | Phase 02 arch-test | 1 | P07-WP2-T1 |

---

## WP3 — Confidence merge model

**Goal:** `IConfidenceMergeService` applies the SRS §8.3 formula to produce a
`MergedMetadataProposal` per field, ready for user review.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP3-T1 | Define `MergedMetadataProposal` record: `{ FieldName, ProposedValue, CurrentValue, MergedConfidence, Source, Alternatives[] }`. | FR-META-003; SRS §8.3 gap | 1 | WP2 |
| P07-WP3-T2 | Implement `ConfidenceCalculator.FieldConfidence(field, provider, existingValue, lookupAge)`: apply trust weight × match score × recency bonus. | SRS §8.3 gap | 2 | P07-WP3-T1 |
| P07-WP3-T3 | Implement `StringMatchScorer`: Levenshtein for Title/Author; exact-match for ISBN/Year; cosine-similarity stub for Description (full implementation Phase 11). | SRS §8.3 gap | 2 | P07-WP3-T2 |
| P07-WP3-T4 | Implement `ConfidenceMergeService.MergeAsync(bookId, lookupResults)`: iterate fields; for each: call `FieldConfidence` for each provider result; select max; produce `MergedMetadataProposal`. | FR-META-003 | 3 | P07-WP3-T2 |
| P07-WP3-T5 | Unit test: `ConfidenceMerge_ProducesProposal_PerField` — 2-provider result set; assert proposal for Title, Author, ISBN, Year, Publisher, Description all present. | FR-META-003 | 1 | P07-WP3-T4 |
| P07-WP3-T6 | Unit test: `ConfidenceMerge_UsesFormula_Correctly` — known inputs → expected `MergedConfidence` value (tolerance ±0.01). | SRS §8.3 gap | 2 | P07-WP3-T2 |
| P07-WP3-T7 | Unit test: `ConfidenceMerge_ManualOverride_AlwaysWins` — existing field has `IsOverridden = true`; assert proposal's `MergedConfidence = 1.0`, `Source = "UserOverride"`. | FR-META-003 | 1 | P07-WP3-T4 |
| P07-WP3-T8 | Document formula in `docs/architecture/confidence-merge-formula.md`. | Documentation | 1 | P07-WP3-T2 |

---

## WP4 — Enrichment review UI

**Goal:** The enrichment review panel (activated from the "Enrich" button in Phase 06
book detail) shows each proposed field with accept/reject/edit and a bulk-accept action.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP4-T1 | Implement `EnrichmentReviewViewModel`: loads `MergedMetadataProposal[]` for a book; exposes per-field `FieldProposalViewModel` (ProposedValue, CurrentValue, Confidence, IsAccepted, IsRejected, IsEditing). | FR-META-003 | 3 | WP3; Phase 06 `IBookDetailNavigationService` |
| P07-WP4-T2 | Build `EnrichmentReviewView` (Avalonia slide-in panel): per-field rows with current value (left), proposed value (right), confidence bar, Accept / Reject / Edit buttons; "Accept all above [threshold]" with configurable threshold. | FR-META-003 | 4 | P07-WP4-T1; Phase 03 tokens |
| P07-WP4-T3 | Wire `ic_enrich` icon (now active, not disabled); open `EnrichmentReviewView` on click; wire to `IBookDetailNavigationService`. | FR-META-003; ICON-SYSTEM.md | 1 | P07-WP4-T2 |
| P07-WP4-T4 | Implement inline edit within the review panel: clicking Edit → text input pre-filled with proposed value; Confirm saves to `FieldProposalViewModel.EditedValue`. | FR-META-003 | 2 | P07-WP4-T2 |
| P07-WP4-T5 | Unit test: `EnrichmentUI_AcceptAll_AppliesProposals` — Accept-all at threshold 0.8; assert only proposals with `MergedConfidence ≥ 0.8` are accepted. | FR-META-003 | 1 | P07-WP4-T1 |
| P07-WP4-T6 | Accessibility: enrichment panel Tab-navigable; Accept/Reject keyboard-operable; field rows have screen-reader labels with current and proposed values. | NFR-PROD-008 | 1 | P07-WP4-T2 |
| P07-WP4-T7 | Externalize all enrichment review strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P07-WP4-T2 |

---

## WP5 — Apply merge & provenance

**Goal:** Accepted proposals are written to `BookMetadataFields` with full provenance;
`AuditEvents` and `MetadataLookups` are updated.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP5-T1 | Implement `ICatalogueWriteService.ApplyMergedMetadataAsync(bookId, acceptedProposals[])`: upsert `BookMetadataFields` rows with `Source`, `SourceTimestamp`, `Confidence`, `IsOverridden = true` for manual edits; call `RecalculateQualityScoreAsync`. | FR-META-003; FR-META-004 | 3 | WP3; Phase 04 schema |
| P07-WP5-T2 | Write `AuditEvent(EventType = "MetadataApplied", BeforeJson = old fields, AfterJson = new fields)` per apply. | CTRL-OGMA-018 | 1 | P07-WP5-T1 |
| P07-WP5-T3 | Integration test: `FieldProvenance_DisplayedInBookDetail` — apply a proposal; assert Phase 06 book-detail `EnrichmentGroup` shows provider name, confidence, and timestamp. | FR-META-004 | 2 | P07-WP5-T1 |
| P07-WP5-T4 | Integration test: `FieldProvenance_IsOverride_Respected` — field with `IsOverridden = true`; run `MergeAsync`; assert proposal's confidence = 1.0, source = "UserOverride". | FR-META-004 | 1 | P07-WP5-T1 |
| P07-WP5-T5 | Integration test: `MetadataApply_WritesAuditEvent` — assert `AuditEvents` row present with non-null `BeforeJson` and `AfterJson`. | CTRL-OGMA-018 | 1 | P07-WP5-T2 |

---

## WP6 — PDF write-back (V1)

**Goal:** The user can push accepted metadata into the PDF DocInfo under
backup-first, diff-confirmed, verify-and-restore guarantees.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP6-T1 | Implement `PdfWriteBackService.PrepareBackupAsync(bookId)`: copy PDF to `.ogma/backups/<ts>_pre_writeback_<sha8>.pdf` via `SidecarService`; return `BackupToken`. | FR-META-005; NFR-PROD-010 | 2 | Phase 04 `ISidecarService` |
| P07-WP6-T2 | Implement `PdfWriteBackService.BuildDiff(currentDocInfo, acceptedFields)`: produce `FieldDiff[]` (FieldName, OldValue, NewValue). | FR-META-005 | 1 | WP5 |
| P07-WP6-T3 | Build `PdfWriteBackDiffView` (modal dialog): table of old/new values; Confirm / Cancel buttons; Cancel discards write-back, leaves PDF unchanged. | FR-META-005 | 2 | P07-WP6-T2; Phase 03 tokens |
| P07-WP6-T4 | Implement `PdfWriteBackService.WriteAsync(bookId, acceptedFields, backupToken)`: (a) write temp file via PDFsharp, (b) verify with PdfPig, (c) atomic rename temp → original, (d) update `Books.Sha256Hash`/`MtimeTicks`, (e) log `AuditEvent(WriteBackSucceeded)`. | FR-META-005; ADR-0008 | 5 | P07-WP6-T1; Phase 01 PDFsharp spike |
| P07-WP6-T5 | Implement restore path in `WriteAsync` catch block: `File.Copy(backup, original, overwrite: true)`; delete temp; log `AuditEvent(WriteBackFailed)`; surface error to user. | FR-META-005 (R1) | 2 | P07-WP6-T4 |
| P07-WP6-T6 | Integration test: `PdfWriteBack_BackupBeforeWrite` — assert backup file present before write completes. | FR-META-005 (R1) | 1 | P07-WP6-T4 |
| P07-WP6-T7 | Integration test (fault-injection): `PdfWriteBack_RestoredOnFailure` — inject exception at PDFsharp write step; assert original PDF byte-identical to pre-write; `AuditEvent(WriteBackFailed)` present. | FR-META-005 (R1) | 3 | P07-WP6-T5 |
| P07-WP6-T8 | Integration test: `PdfWriteBack_DiffConfirmedByUser` — assert `WriteAsync` is not called until `PdfWriteBackDiffView` is confirmed. | FR-META-005 | 1 | P07-WP6-T3 |
| P07-WP6-T9 | Document write-back protocol in `docs/architecture/pdf-writeback-protocol.md`. | Documentation | 1 | P07-WP6-T4 |

---

## WP7 — Batch enrichment (V1)

**Goal:** `BatchEnrichmentOrchestrator` enriches multiple books respecting rate limits;
`EnrichmentWorker` processes the queue; a batch progress panel shows status.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP7-T1 | Implement `BatchEnrichmentOrchestrator.StartAsync(bookIds, ct)`: create `Job(JobType = Enrich, IdempotencyKey = …)` rows; enqueue to `EnrichmentWorker`. | FR-META-006; NFR-OGMA-009 | 2 | Phase 04 `Jobs` table |
| P07-WP7-T2 | Implement `TokenBucketRateLimiter` wrapper using `System.Threading.RateLimiting.TokenBucketRateLimiter`; configurable tokens/second per provider. | FR-META-006 | 2 | .NET 10 `RateLimiting` |
| P07-WP7-T3 | Implement `EnrichmentWorker (BackgroundService)`: dequeue enrichment jobs; call `MetadataProviderAggregator.AggregateAsync`; apply auto-accept for confidence ≥ user-configured threshold; update `Job.Status`. | FR-META-006 | 3 | P07-WP7-T1; P07-WP7-T2; WP2; WP5 |
| P07-WP7-T4 | Build `BatchEnrichmentView`: progress bar + current book label + completed/failed counts + Pause/Resume/Cancel buttons. | FR-META-006 | 2 | P07-WP7-T3; Phase 03 tokens |
| P07-WP7-T5 | Add Pause / Resume: `CancellationTokenSource` swap in `EnrichmentWorker`; persists in-progress jobs to `Jobs` table so Resume re-queues them. | FR-META-006 | 2 | P07-WP7-T3 |
| P07-WP7-T6 | Integration test: `BatchEnrichment_RateLimit_Respected` — configure 1 req/s; enrich 3 books; assert total elapsed ≥ 3 s. | FR-META-006 | 2 | P07-WP7-T2 |
| P07-WP7-T7 | Integration test: `BatchEnrichment_PausedFailed_Visible` — pause mid-batch; assert `Job.Status` for incomplete items = `Queued` (not lost). | FR-META-006 | 1 | P07-WP7-T5 |
| P07-WP7-T8 | Externalize batch enrichment strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P07-WP7-T4 |

---

## WP8 — Metadata quality score (V1)

**Goal:** Each book has a `QualityScore` that quantifies field completeness and
confidence; it can be filtered in the Phase 06 filter panel.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP8-T1 | Add `Books.QualityScore REAL DEFAULT 0.0` column via EF Core migration. | FR-META-007 | 1 | Phase 04 `Books` entity |
| P07-WP8-T2 | Implement `QualityScoreCalculator.Calculate(book, metadataFields)`: apply the formula from §7; return value in [0.0, 1.0]. | FR-META-007 | 2 | P07-WP8-T1 |
| P07-WP8-T3 | Implement `ICatalogueWriteService.RecalculateQualityScoreAsync(BookId)`: call `QualityScoreCalculator`; persist to `Books.QualityScore`. | FR-META-007 | 1 | P07-WP8-T2 |
| P07-WP8-T4 | Extend Phase 06 `FilterPanelViewModel` with `QualityScoreMax` filter (0-100%); extend `CatalogueFilter` record with `MaxQualityScore` field. | FR-META-007; FR-CAT-002 | 2 | Phase 06 `FilterPanelViewModel` |
| P07-WP8-T5 | Unit test: `QualityScore_ComputedPerBook` — seed book with 5/7 core fields present, confidence avg 0.8; assert score ∈ [0.0, 1.0]. | FR-META-007 | 1 | P07-WP8-T2 |
| P07-WP8-T6 | Unit test: `Filter_BelowQualityThreshold_Works` — set filter `MaxQualityScore = 0.5`; assert only books with score ≤ 0.5 returned. | FR-META-007 | 1 | P07-WP8-T4 |

---

## WP9 — Library health dashboard (V1)

**Goal:** The power-librarian health dashboard surfaces 5 categories of collection
health issues with actionable items, loading < 500 ms for 2,000 books.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP9-T1 | Implement `HealthDashboardViewModel`: 5 section ViewModels; each loads via targeted `ICatalogueReadModel` query; exposes `IsLoading`, `ItemCount`, `Items`. | FR-META-007 (health section); power-librarian persona | 4 | Phase 04 `ICatalogueReadModel` |
| P07-WP9-T2 | Queries: Duplicates (GROUP BY `IsbnNormalized` HAVING COUNT > 1 UNION GROUP BY `Sha256Hash`); Missing covers (no sidecar cover file); Missing ISBNs; Unavailable files; Failed jobs. | FR-META-007 | 3 | P07-WP9-T1 |
| P07-WP9-T3 | Build `HealthDashboardView`: 5-tab panel; each tab = count badge + scrollable item list + per-item action button. | FR-META-007 | 4 | P07-WP9-T1; Phase 03 tokens |
| P07-WP9-T4 | Wire actions: Enrich button → `BatchEnrichmentOrchestrator.StartAsync([bookId])`; Regenerate thumbnail → `IThumbnailService.GenerateCoverAsync`; Rescan → `ILibraryRootService.TriggerRescanAsync`; Retry job → `IJobRepository.RequeueAsync`. | FR-META-007; Phase 05 services | 3 | P07-WP9-T3 |
| P07-WP9-T5 | Performance test: `HealthDashboard_2000Books_Under500ms` — assert all 5 queries load in < 500 ms total. | Power-librarian persona; NFR-PROD-003 | 2 | P07-WP9-T2 |
| P07-WP9-T6 | Integration test: `HealthDashboard_ShowsAllFiveCategories` — seed books with each health issue; assert each section non-empty. | FR-META-007 | 2 | P07-WP9-T1 |
| P07-WP9-T7 | Wire health dashboard icons (`ic_health_dashboard`, `ic_duplicate`, `ic_missing_cover`, `ic_missing_isbn`, `ic_quality_score`) via `IconCatalog`. | ICON-SYSTEM.md | 1 | Phase 03 `IconCatalog` |
| P07-WP9-T8 | Accessibility: Tab between sections; item list keyboard-navigable; action buttons labeled. | NFR-PROD-008 | 1 | P07-WP9-T3 |
| P07-WP9-T9 | Externalize all health dashboard strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P07-WP9-T3 |

---

## WP10 — Privacy payload preview

**Goal:** The first provider call per library per provider shows a payload-preview
toast; the user can cancel; the preference is persisted.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP10-T1 | Implement `IPayloadPreviewService.ShouldPromptAsync(providerId, libraryRootId)`: query settings for `HasAcknowledgedProviderPayload`; return true only on first call. | NFR-PROD-011 | 1 | Phase 04 settings persistence |
| P07-WP10-T2 | Build payload-preview toast: "We will send the following to {Provider}: ISBN={isbn}, Title={title}. [Send] [Cancel]". | NFR-PROD-011 | 2 | P07-WP10-T1 |
| P07-WP10-T3 | Wire into `MetadataProviderAggregator.AggregateAsync`: call `ShouldPromptAsync`; if true, show toast and await user decision; if cancelled, abort lookup. | NFR-PROD-011 | 2 | P07-WP10-T2; WP2 |
| P07-WP10-T4 | Integration test: `ProviderLookup_PayloadPreview_ShownBeforeFirstCall` — first call → toast presented; second call → no toast. | NFR-PROD-011 | 1 | P07-WP10-T3 |
| P07-WP10-T5 | Integration test: `ProviderLookup_Cancelled_NoHttpCallMade` — user cancels preview → assert `HttpClient` not called. | NFR-PROD-011 | 1 | P07-WP10-T3 |

---

## WP11 — Tests, performance gate & documentation

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P07-WP11-T1 | Run `PdfWriteBack_RestoredOnFailure` fault-injection test on both Windows and macOS CI; confirm atomic rename behavior matches. | FR-META-005 (R1) | 2 | WP6 |
| P07-WP11-T2 | Run `HealthDashboard_2000Books_Under500ms`; record baseline in CI artifact. | Power-librarian NFR | 1 | WP9 |
| P07-WP11-T3 | Confirm `/security-review` for WP2 (provider clients) and WP10 (payload preview): no content leaks; ISBN + title only sent. | NFR-PROD-011 | 1 | WP2; WP10 |
| P07-WP11-T4 | Ratify ADR-0008 (DB-first, write-back later); file as `docs/adr/ADR-0008.md` Accepted. | ADR-0008 | 1 | WP6 |
| P07-WP11-T5 | Update `CLAUDE.md` with metadata enrichment service interfaces and health dashboard entry point. | Documentation | 1 | All WPs |
| P07-WP11-T6 | Architecture test: `MetadataEnrichment_DoesNotInstantiate_CatalogueDbContext`. | Phase 02 arch-test | 1 | All WPs |
