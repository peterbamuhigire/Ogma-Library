# Phase 07 — Test Plan

The primary quality gates are: R1 (PDF write-back must be byte-for-byte reversible),
privacy/egress (provider calls must not leak content — only ISBN + title), and
functional correctness of the confidence merge formula (which has a known test oracle
from the documented SRS §8.3 formula).

---

## Applicable test layers

| Layer | Applies | Notes |
| --- | --- | --- |
| 1. Domain unit | Yes | `IsbnRegex`; `Isbn` check-digit validation; `ConfidenceCalculator`; `QualityScoreCalculator`; `StringMatchScorer` |
| 2. Infrastructure integration | Yes | `GoogleBooksClient`; `OpenLibraryClient`; `MetadataProviderAggregator`; `PdfWriteBackService`; `ICatalogueWriteService.ApplyMergedMetadataAsync`; health dashboard queries |
| 3. PDF layer | Yes | PdfPig ISBN extraction (XMP, DocInfo, first pages); PDFsharp write and PdfPig verify; golden-corpus PDFs |
| 4. Search | No | Not in scope |
| 5. AI | No | Not in scope |
| 6. UI | Partial | `EnrichmentReviewViewModel` unit tests; `HealthDashboardViewModel` unit tests; Avalonia automation for keyboard walkthrough |
| 7. 3D | No | Not in scope |
| 8. Performance | Yes | Health dashboard < 500 ms; batch enrichment rate-limit timing |
| 9. Packaging | No | Not in scope |

---

## Golden corpus fixtures

| Fixture | Used by | Oracle |
| --- | --- | --- |
| `corpus/isbn-in-xmp.pdf` | `IsbnDetection_XmpSource` | Known ISBN from XMP namespace |
| `corpus/isbn-in-docinfo.pdf` | `IsbnDetection_DocInfoSource` | Known ISBN from DocInfo |
| `corpus/simple-text.pdf` | `IsbnDetection_FirstPage_WhenPresent` | ISBN present in page text |
| `corpus/bad-metadata.pdf` | `IsbnDetection_InvalidCheckDigit_Rejected` | No valid ISBN; `BestIsbn = null` |
| `corpus/password-protected.pdf` | `PdfWriteBack_EncryptedPdf_ReturnsError` | Write-back fails gracefully; no data loss |
| `corpus/non-english.pdf` | `ConfidenceMerge_NonAscii_Title_Handles` | Non-ASCII Levenshtein distance computed without error |
| `corpus/simple-text.pdf` | `PdfWriteBack_RestoredOnFailure` | Byte-identical restore after injected fault |
| Synthetic 2,000-book corpus | `HealthDashboard_2000Books_Under500ms` | All 5 queries < 500 ms total |

---

## Test categories and oracles

### 1. ISBN detection

| Test | Oracle | Tier |
| --- | --- | --- |
| `IsbnDetection_AllFourSources_DetectAndNormalize` | `BestIsbn` non-null for each of 4 source types; normalized to ISBN-13 | MVP |
| `IsbnDetection_DocInfo_HigherPriority_ThanFilename` | DocInfo ISBN wins over filename ISBN when both present | MVP |
| `IsbnDetection_InvalidCheckDigit_Rejected` | Wrong check digit → not in `AllCandidates` | MVP |
| `IsbnDetection_Isbn10_ConvertedToIsbn13` | `978`-prefixed ISBN-13 produced from valid ISBN-10 | MVP |
| `IsbnDetection_MultipleIsbnOnPage_AllReturned` | Two ISBNs on page 0 → `AllCandidates.Count = 2` | MVP |

### 2. Provider clients

| Test | Oracle | Tier |
| --- | --- | --- |
| `MetadataLookup_BothProviders_CalledConcurrently` | Total elapsed < 400 ms when each mock has 200 ms delay | MVP |
| `MetadataLookup_ResultsStoredWithProvenance` | `MetadataLookups` row: `Provider`, `Timestamp`, `Confidence` all non-null | MVP |
| `MetadataLookup_ProviderFails_OtherResultStored` | Google Books throws → Open Library result stored; no unhandled exception | MVP |
| `MetadataLookup_WritesAuditEvent` | `AuditEvents(EventType = "ProviderLookup")` present | MVP |
| `ProviderClients_RouteThroughHttpClientFactory` (arch test) | No `new HttpClient()` in metadata namespace | MVP |

### 3. Confidence merge model

| Test | Oracle | Tier |
| --- | --- | --- |
| `ConfidenceMerge_ProducesProposal_PerField` | `MergedMetadataProposal[]` contains entries for Title, Author, ISBN, Year, Publisher, Description | MVP |
| `ConfidenceMerge_UsesFormula_Correctly` | Known inputs (trust weight, match score, age) → expected `MergedConfidence` ±0.01 | MVP |
| `ConfidenceMerge_ManualOverride_AlwaysWins` | `IsOverridden = true` → `MergedConfidence = 1.0` | MVP |
| `ConfidenceMerge_RecencyBonus_Applied` | Lookup > 30 days old → confidence reduced by recency factor | MVP |
| `ConfidenceMerge_NonAscii_Title_Handles` | Non-ASCII title Levenshtein computed without exception | MVP |

### 4. Enrichment review UI

| Test | Oracle | Tier |
| --- | --- | --- |
| `EnrichmentUI_AcceptAll_AppliesProposals` | Only proposals with `MergedConfidence ≥ threshold` accepted | MVP |
| `EnrichmentUI_RejectField_ExcludesFromApply` | Rejected field not in `acceptedProposals[]` passed to `ApplyMergedMetadataAsync` | MVP |
| `EnrichmentUI_InlineEdit_OverridesProposal` | Edited value stored as `IsOverridden = true` with `MergedConfidence = 1.0` | MVP |

### 5. Field provenance

| Test | Oracle | Tier |
| --- | --- | --- |
| `FieldProvenance_DisplayedInBookDetail` | Phase 06 `EnrichmentGroup` shows provider, confidence, timestamp | V1 |
| `FieldProvenance_IsOverride_Respected` | Override field gets confidence 1.0 in subsequent merge | V1 |
| `MetadataApply_WritesAuditEvent` | `BeforeJson` and `AfterJson` non-null in `AuditEvents` | MVP |

### 6. PDF write-back (R1 focus)

| Test | Oracle | Tier |
| --- | --- | --- |
| `PdfWriteBack_BackupBeforeWrite` (R1) | Backup file present before write completes | V1 |
| `PdfWriteBack_RestoredOnFailure` (R1) | Injected exception → original PDF byte-identical; `WriteBackFailed` audit event present | V1 |
| `PdfWriteBack_DiffConfirmedByUser` | `WriteAsync` not called until user confirms diff | V1 |
| `PdfWriteBack_VerifyAfterWrite` | PdfPig can open output; written fields round-trip correctly | V1 |
| `PdfWriteBack_EncryptedPdf_ReturnsError` | Password-protected PDF → graceful error; no data loss | V1 |
| `PdfWriteBack_UpdatesSha256Hash` | `Books.Sha256Hash` updated after successful write | V1 |

### 7. Batch enrichment

| Test | Oracle | Tier |
| --- | --- | --- |
| `BatchEnrichment_RateLimit_Respected` | 3 books, 1 req/s limit → elapsed ≥ 3 s | V1 |
| `BatchEnrichment_PausedFailed_Visible` | Paused jobs in `Queued` state; visible in batch panel | V1 |
| `BatchEnrichment_IdempotentRetry` | Re-enqueue failed job; assert no duplicate `MetadataLookups` row (idempotency key prevents it) | V1 |

### 8. Quality score

| Test | Oracle | Tier |
| --- | --- | --- |
| `QualityScore_ComputedPerBook` | Score ∈ [0.0, 1.0]; changes when metadata written | V1 |
| `QualityScore_ZeroFields_ZeroScore` | Book with no metadata fields → score = 0.0 | V1 |
| `QualityScore_AllFields_HighConfidence_HighScore` | All 7 core fields present, all confidence = 1.0 → score = 1.0 | V1 |
| `Filter_BelowQualityThreshold_Works` | `MaxQualityScore = 0.5` → only low-quality books | V1 |

### 9. Health dashboard

| Test | Oracle | Tier |
| --- | --- | --- |
| `HealthDashboard_ShowsAllFiveCategories` | All 5 sections non-empty when seeded | V1 |
| `HealthDashboard_2000Books_Under500ms` | 5 queries complete in < 500 ms total | V1 |
| `HealthDashboard_EnrichAction_TriggersJob` | Enrich button → `Job(Enrich)` row created | V1 |
| `HealthDashboard_DuplicateDetection_ByIsbn` | 2 books with same ISBN → both in Duplicates section | V1 |

### 10. Privacy / payload preview

| Test | Oracle | Tier |
| --- | --- | --- |
| `ProviderLookup_PayloadPreview_ShownBeforeFirstCall` | Toast presented; second call to same provider does not show toast | MVP |
| `ProviderLookup_Cancelled_NoHttpCallMade` | Cancel → `HttpClient` not invoked | MVP |
| `ProviderLookup_PayloadContainsOnlyIsbnAndTitle` | Outbound HTTP request body/URL contains no content or annotations | MVP (privacy audit) |

### 11. Architecture tests

| Test | Oracle |
| --- | --- |
| `MetadataEnrichment_DoesNotInstantiate_CatalogueDbContext` | No `CatalogueDbContext` in `Application/Metadata/` or `Infrastructure/Metadata/` (writes via `ICatalogueWriteService`) |
| `ProviderClients_RouteThroughHttpClientFactory` | No `new HttpClient()` in metadata namespace |

---

## Fault-injection strategy for write-back (R1)

The PDF write-back path is the highest-risk operation in this phase. The following
faults are injected to verify the restore path:

| Fault | Injected at | Expected outcome |
| --- | --- | --- |
| `IOException` after backup but before PDFsharp write | `PdfWriteBackService` mock | Original PDF unchanged; backup present; `WriteBackFailed` audit event |
| `PdfSharpException` during write | `IPdfSharpAdapter` mock | Same as above |
| `IOException` during atomic rename (temp → original) | `IFileSystem.Move` mock | Original PDF unchanged (rename failed); backup restored from `.bak` |
| `IOException` during PdfPig verification | `IPdfVerifier` mock | Temp file deleted; original restored from backup |
| Process killed mid-write (simulated via `Environment.FailFast` in test process) | Separate test process | On next startup: `Jobs WHERE Status = Running` for write-back jobs → re-queued; user asked to verify result |

All fault-injection tests run on **both Windows and macOS** CI runners because
the atomic rename semantics differ (`File.Move` with `overwrite = true` is atomic
on macOS/Linux; on Windows, `target` must not exist for atomic replace — the
service must handle both paths explicitly).

---

## Performance baselines

| Metric | Budget | Corpus | Method |
| --- | --- | --- | --- |
| Health dashboard 5 queries | < 500 ms total | Synthetic 2,000-book corpus | `Stopwatch.Elapsed`; 5 iterations |
| Enrichment panel open | < 300 ms | Single book with 2-provider results | `Stopwatch` from "Enrich" button click to panel visible |
| Batch enrichment 10 books (mock providers, 30 RPM) | ≥ 20 s (rate-limit test) | Synthetic | `Stopwatch.Elapsed`; assert ≥ floor |
| ISBN detection on very-large PDF | < 2 s | `corpus/very-large-1000pp.pdf` | `Stopwatch.Elapsed` |

---

## CI matrix

| Runner | .NET | Architecture | Required? |
| --- | --- | --- | --- |
| Windows 10 x64 | .NET 10 LTS | x64 | Yes |
| macOS 12 x64 | .NET 10 LTS | x64 | Yes |
| macOS 14 Apple Silicon | .NET 10 LTS | ARM64 | Yes (write-back atomic rename) |

The `PdfWriteBack_RestoredOnFailure` fault-injection test must pass on all three
runners. The atomic rename behavior difference between Windows and macOS/ARM64 is
precisely what needs cross-platform verification.
