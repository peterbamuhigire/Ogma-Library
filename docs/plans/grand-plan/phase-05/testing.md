# Phase 05 — Test Plan

The ingestion pipeline has two primary quality gates: R1 (data-loss prevention,
especially the unavailable-file flagging path) and NFR-PROD-005 (no UI stall > 100 ms).
The golden-corpus integration test is the end-to-end correctness oracle.

---

## Applicable test layers

| Layer | Applies | Notes |
| --- | --- | --- |
| 1. Domain unit | Partial | `RelativePath` normalization; `BookMatchResult` dispatch logic |
| 2. Infrastructure integration | Yes | All pipeline stages; `DiscoveryService`; `IdentityMatcher`; `MetadataExtractionService`; `ThumbnailService`; `JobRecoveryService` |
| 3. PDF layer | Yes | PdfPig metadata extraction; SkiaSharp + PDFium thumbnail generation; golden-corpus PDFs |
| 4. Search | No | Text extraction deferred to Phase 10 |
| 5. AI | No | Not in scope |
| 6. UI | Partial | `ScanProgressViewModel` state assertions; Avalonia UI automation for the progress panel and health report |
| 7. 3D | No | Not in scope |
| 8. Performance | Yes | Incremental rescan 2,000 files < 10 s; UI-thread stall < 100 ms |
| 9. Packaging | No | Not in scope |

---

## Golden corpus fixtures

| Fixture | Used by | Oracle |
| --- | --- | --- |
| `corpus/simple-text.pdf` | Metadata extraction; thumbnail generation | DocInfo fields populated; cover 200x300 JPEG written |
| `corpus/bad-metadata.pdf` | `MetadataExtraction_BadMetadata_StoresEmptyNotThrows` | No exception; `BookMetadataFields` rows with empty values stored; `Source = "PDF"` |
| `corpus/password-protected.pdf` | Per-file isolation; health report | `Job.Status = Failed`; `ErrorMessage` contains "password"; no `Books` row created |
| `corpus/isbn-in-xmp.pdf` | XMP ISBN extraction | `Books.IsbnNormalized` populated; check-digit valid |
| `corpus/isbn-in-docinfo.pdf` | DocInfo ISBN extraction | `Books.IsbnNormalized` populated |
| `corpus/very-large-1000pp.pdf` | SHA-256 performance; thumbnail page-0 | Hash computed < 2 s; cover generated from page 0 |
| `corpus/rotated-pages.pdf` | Thumbnail orientation | Cover thumbnail uses PDFium's rotation-corrected render |
| `corpus/non-english.pdf` | Metadata encoding | Non-ASCII title stored correctly in `BookMetadataFields` |
| Synthetic 2,000-book corpus (by seed) | Incremental rescan performance | Rescan < 10 s wall-clock; no false re-queues |

---

## Test categories and oracles

### 1. Library root

| Test | Oracle | Tier |
| --- | --- | --- |
| `LibraryRootService_PersistsPath` | Path survives DI scope restart | MVP |
| `LibraryRootService_RescanTriggersDiscovery` | `IDiscoveryService.StartAsync` called | MVP |

### 2. Discovery

| Test | Oracle | Tier |
| --- | --- | --- |
| `DiscoveryService_DiscoversPdfs_Recursively` | All PDFs in all sub-folders emitted via channel | MVP |
| `DiscoveryService_HonorsExcludedFolders` | Excluded-folder PDFs not emitted | MVP |
| `DiscoveryService_PathsNormalized_CaseFolded` | `RelativePath` is lowercase on Windows | MVP |
| `DiscoveryService_EmptyFolder_NoEvents` | No `FileDiscoveredEvent` when folder has no PDFs | MVP |
| `DiscoveryService_Cancellation_StopsCleanly` | `CancellationToken` cancel stops enumeration; no exception thrown | MVP |

### 3. Identity matching

| Test | Oracle | Tier |
| --- | --- | --- |
| `IngestionPipeline_RematuresRenamedFile` (R1) | No new `Book` row after rename; `BookFile.RelativePath` updated | MVP |
| `IngestionPipeline_RematchesMovedFile` (R1) | No new `Book` row after folder move | MVP |
| `IngestionPipeline_IdempotentRescan` | `Books` count identical after second scan | MVP |
| `IngestionPipeline_NewFile_CreatesBookAndJob` | New `Book` row + `Job(GenerateThumbnail)` row | MVP |

### 4. Unavailable-file flagging (R1 focus)

| Test | Oracle | Tier |
| --- | --- | --- |
| `UnavailableFileFlagging_PreservesAnnotations` (R1) | `Annotations` row count unchanged after flagging | MVP |
| `UnavailableFileFlagging_PreservesProgress` (R1) | `ReadingProgress` row intact after flagging | MVP |
| `UnavailableFileFlagging_PreservesBookmarks` (R1) | `Bookmarks` row intact | MVP |
| `UnavailableFileFlagging_SetsBookStatusUnavailable` | `Book.Status = Unavailable` | MVP |
| `UnavailableFileFlagging_ReactivatesOnReappearance` | `Book.Status = Active` after file restored | MVP |
| `UnavailableFileFlagging_WritesAuditEvent` | `AuditEvents` row with correct `EventType` | MVP |

### 5. Metadata extraction

| Test | Oracle | Tier |
| --- | --- | --- |
| `MetadataExtraction_DocInfo_PopulatesFields` | `BookMetadataFields` rows for Title, Author, Subject, CreationDate with `Source = "PDF"` | MVP |
| `MetadataExtraction_XmpIsbn_Normalizes` | `Books.IsbnNormalized` = 13-digit ISBN | MVP |
| `MetadataExtraction_BadMetadata_StoresEmptyNotThrows` | No exception; empty-string fields stored | MVP |
| `MetadataExtraction_PasswordProtected_IsLoggedNotCrashed` | `Job.Status = Failed`; `ErrorMessage` non-null | MVP |
| `MetadataExtraction_NonAscii_StoredCorrectly` | Unicode title round-trips without corruption | MVP |

### 6. Background worker & job management

| Test | Oracle | Tier |
| --- | --- | --- |
| `IngestionWorker_PerFileIsolation_SiblingJobsContinueOnFailure` | Jobs 4-5 complete when job 3 faults | MVP |
| `JobRecovery_AtStartup_RequeuesRunningJobs` | `Status = Running` jobs become `Status = Queued` | MVP |
| `IngestionPipeline_UIThread_NeverStallsAbove100ms` | Max dispatch latency < 100 ms across 20-file scan | MVP (NFR-PROD-005) |

### 7. Thumbnail & spine generation

| Test | Oracle | Tier |
| --- | --- | --- |
| `ThumbnailService_GeneratesCover_InBackground` | Cover JPEG exists; dimensions 200x300; JPEG magic bytes `FF D8 FF` | MVP |
| `SpineService_GeneratesSpine_CorrectDimensions` | Spine JPEG exists; dimensions 7x100 | MVP |
| `ThumbnailService_FailureRecorded_AndRetryable` | Corrupt PDF → `Job.Status = Failed`; retry → `Status = Queued` | MVP |
| `ThumbnailService_NativeLibraries_LoadOnBothPlatforms` | No `DllNotFoundException` on Windows x64 and macOS ARM64 CI | MVP |

### 8. Incremental rescan

| Test | Oracle | Tier |
| --- | --- | --- |
| `IncrementalRescan_SkipsUnchangedFiles` | No new `Job` rows for unchanged files | MVP |
| `IncrementalRescan_Requeues_ChangedFiles` | Exactly 1 new `Job` row for the modified file | MVP |
| `IncrementalRescan_2000UnchangedFiles_Under10s` | Wall-clock < 10 s (P50; single run for trend) | MVP (NFR-OGMA-002 precondition) |

### 9. Health report (V1)

| Test | Oracle | Tier |
| --- | --- | --- |
| `HealthReport_ShowsAllFailureCategories` | VM exposes non-empty list for each of 4 categories | V1 |
| `HealthReport_RetryAll_RequeuesFailedJobs` | All `Status = Failed` jobs → `Status = Queued` | V1 |

### 10. Architecture

| Test | Oracle |
| --- | --- |
| `Workers_HasNoDependencyOn_UIProject` | `OgmaLibrary.Workers` does not reference `OgmaLibrary.App` |
| `IngestionPipeline_DoesNotCallDbContextDirectly` | No `CatalogueDbContext` instantiation in `Application/Ingestion/` namespace |

---

## Fault-injection strategy

| Fault | Injected in | R-tier | Verification |
| --- | --- | --- | --- |
| `IOException` during thumbnail write | `ThumbnailService` mock | R4 | `Job.Status = Failed`; sibling jobs unaffected |
| `PdfException` during PdfPig open | `MetadataExtractionService` mock | R4 | `Job.Status = Failed`; no uncaught exception |
| `OperationCanceledException` during scan | `CancellationToken.Cancel()` mid-channel | R4 | Worker exits cleanly; no partial `Book` row |
| File deleted between discovery and identity-match | Delete file after `FileDiscoveredEvent`, before `MatchAsync` | R1 | `Unresolvable` → `Job.Status = Failed`; no `Book` row |

---

## Performance baselines

| Metric | Budget | Corpus | Method |
| --- | --- | --- | --- |
| Incremental rescan 2,000 unchanged files | < 10 s | Synthetic seed | `Stopwatch.Elapsed`; single run; trend data |
| UI-thread max dispatch latency | < 100 ms | 20-PDF corpus | Wrapper around `Dispatcher.UIThread.Post` records timestamp; assert max |
| Cover thumbnail generation per file | < 2 s P95 | All 11 corpus PDFs | `Stopwatch` per-file; assert P95 |
| Full pipeline (import 11 corpus PDFs) | < 60 s | Golden corpus | `Stopwatch.Elapsed`; end-to-end |

---

## CI matrix

| Runner | .NET | Architecture | Required? |
| --- | --- | --- | --- |
| Windows 10 x64 | .NET 10 LTS | x64 | Yes |
| macOS 12 x64 | .NET 10 LTS | x64 | Yes |
| macOS 14 Apple Silicon | .NET 10 LTS | ARM64 | Yes (native lib validation) |

The ARM64 macOS runner is specifically required to validate SkiaSharp and PDFium
native library loading (P05-WP7-T6). Without this runner, the phase DoD is not met.
