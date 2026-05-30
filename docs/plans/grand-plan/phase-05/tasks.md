# Phase 05 — Tasks

Task IDs: `P05-WPN-TN`. Each task lists requirement/NFR/CTRL traceability,
estimate in hours (ideal engineering time), and within-phase dependencies.

---

## WP1 — Library root selection & storage

**Goal:** The user can select a folder as the library root; the path persists across
restarts; re-selecting or rescanning is triggered from the UI.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP1-T1 | Define `LibraryRootRecord` entity (or use `Settings` table): `Id`, `AbsolutePath`, `ExcludedFolders` (JSON array), `LastScanUtc`. | FR-LIB-001 | 1 | Phase 04 schema |
| P05-WP1-T2 | Implement `ILibraryRootService` interface: `SelectAsync()`, `GetCurrentAsync()`, `SetExcludedFoldersAsync()`, `TriggerRescanAsync()`. | FR-LIB-001 | 1 | P05-WP1-T1 |
| P05-WP1-T3 | Implement `LibraryRootService` using OS file-picker dialog (`StorageProvider` in Avalonia); persist to DB. | FR-LIB-001 | 2 | P05-WP1-T2 |
| P05-WP1-T4 | Unit test: `LibraryRootService_PersistsPath` — select a path, restart DI scope, assert `GetCurrentAsync()` returns same path. | FR-LIB-001 | 1 | P05-WP1-T3 |
| P05-WP1-T5 | Unit test: `LibraryRootService_RescanTriggersDiscovery` — call `TriggerRescanAsync()`; assert `IDiscoveryService.StartAsync()` is called. | FR-LIB-001 | 1 | P05-WP1-T3 |

---

## WP2 — Recursive discovery & excluded folders

**Goal:** `DiscoveryService` finds all PDFs recursively, skips excluded folders,
emits events per file, and runs entirely off the UI thread.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP2-T1 | Implement `IDiscoveryService.StartAsync(rootPath, excludedFolders, channel, ct)`: `Directory.EnumerateFiles("*.pdf", AllDirectories)` filtered by excluded-folder prefix match; write to `Channel<DiscoveredFile>`. | FR-LIB-002 | 3 | P05-WP1-T2 |
| P05-WP2-T2 | Normalize discovered paths using `RelativePath` value object (forward-slash, case-fold per OS). | FR-LIB-003; NFR-PROD-009 | 1 | P05-WP2-T1 |
| P05-WP2-T3 | Integration test: `DiscoveryService_DiscoversPdfs_Recursively` — create temp dir with 5 PDFs in 3 sub-folders; assert all 5 `DiscoveredFile` events emitted. | FR-LIB-002 | 2 | P05-WP2-T1 |
| P05-WP2-T4 | Integration test: `DiscoveryService_HonorsExcludedFolders` — exclude one sub-folder; assert its PDFs are not emitted. | FR-LIB-002 | 1 | P05-WP2-T3 |
| P05-WP2-T5 | Integration test: `DiscoveryService_PathsNormalized_CaseFolded` — create files on Windows; assert `RelativePath` is lowercase. | FR-LIB-003 | 1 | P05-WP2-T2 |

---

## WP3 — Identity matching & new-book registration

**Goal:** Each discovered file is matched against the catalogue; new books are
inserted; existing books get their `BookFile.FileStatus` updated.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP3-T1 | Implement `IIdentityMatcher.MatchAsync(DiscoveredFile) → MatchOutcome`: delegates to `IBookIdentityService`; on `ExactMatch` updates `BookFile.LastSeenUtc`; on `NewBook` calls `IBookRegistrationService.RegisterAsync()`. | FR-LIB-003 | 3 | Phase 04 `IBookIdentityService` |
| P05-WP3-T2 | Implement `BookRegistrationService.RegisterAsync()`: insert `Book` + `BookFile` rows; write `Job(JobType=GenerateThumbnail)` with idempotency key. | FR-LIB-003; NFR-OGMA-009 | 2 | P05-WP3-T1 |
| P05-WP3-T3 | Integration test: `IngestionPipeline_RematuresRenamedFile` — import file A; rename on disk; rescan; assert no new `Book` row; `BookFile.RelativePath` updated. | FR-LIB-003 (R1) | 2 | P05-WP3-T1 |
| P05-WP3-T4 | Integration test: `IngestionPipeline_RematchesMovedFile` — import file A; move to sub-folder; rescan; assert single `Book` row; `BookFile.RelativePath` updated. | FR-LIB-003 (R1) | 2 | P05-WP3-T3 |
| P05-WP3-T5 | Integration test: `IngestionPipeline_IdempotentRescan` — scan same folder twice; assert `Books` row count does not increase. | NFR-OGMA-009 | 1 | P05-WP3-T2 |

---

## WP4 — Unavailable-file flagging

**Goal:** Files removed from disk are flagged without any user data being deleted.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP4-T1 | Implement `IUnavailableFileFlagService.FlagMissingFilesAsync(libraryRootId, ct)`: load all `Present` BookFiles; check `File.Exists`; flag missing ones; write `AuditEvent`. | FR-LIB-004 | 3 | Phase 04 schema |
| P05-WP4-T2 | Integration test: `UnavailableFileFlagging_PreservesAnnotations` — add book + annotations; delete file from disk; rescan; assert `Annotations` rows intact, `Book.Status = Unavailable`. | FR-LIB-004 (R1) | 2 | P05-WP4-T1 |
| P05-WP4-T3 | Integration test: `UnavailableFileFlagging_PreservesProgress` — add book + reading progress; delete file; rescan; assert `ReadingProgress` row intact. | FR-LIB-004 (R1) | 1 | P05-WP4-T2 |
| P05-WP4-T4 | Integration test: `UnavailableFileFlagging_ReactivatesOnReappearance` — flag file as missing; restore file to disk; rescan; assert `Book.Status = Active`. | FR-LIB-004 | 1 | P05-WP4-T1 |
| P05-WP4-T5 | Unit test: `UnavailableFileFlagging_WritesAuditEvent` — assert `AuditEvents` row with `EventType = "BookMarkedUnavailable"` created per flagged file. | NFR-PROD-013 | 1 | P05-WP4-T1 |

---

## WP5 — Metadata extraction (PDF DocInfo/XMP)

**Goal:** PdfPig extracts `Title`, `Author`, `Subject`, `CreationDate`, `ISBN`
(from XMP and DocInfo) into `BookMetadataFields` with `Source = "PDF"`.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP5-T1 | Implement `IMetadataExtractionService.ExtractAsync(bookId, filePath, ct)`: open PDF via PdfPig; read `DocumentInformation` (Title, Author, Subject, Creator, CreationDate); read XMP packet if present; upsert `BookMetadataFields`. | FR-META-001 precursor | 3 | Phase 04 `BookMetadataFields` |
| P05-WP5-T2 | Normalize extracted ISBN via `Isbn` value object (Phase 04); store `Books.IsbnNormalized` if valid. | FR-META-001 | 1 | P05-WP5-T1 |
| P05-WP5-T3 | Integration test: `MetadataExtraction_DocInfo_PopulatesFields` using golden-corpus `simple-text.pdf`. | FR-META-001 | 2 | P05-WP5-T1 |
| P05-WP5-T4 | Integration test: `MetadataExtraction_XmpIsbn_Normalizes` using golden-corpus `isbn-in-xmp.pdf`. | FR-META-001 | 1 | P05-WP5-T2 |
| P05-WP5-T5 | Integration test: `MetadataExtraction_BadMetadata_StoresEmptyNotThrows` using golden-corpus `bad-metadata.pdf`. | FR-META-001 | 1 | P05-WP5-T1 |
| P05-WP5-T6 | Integration test: `MetadataExtraction_PasswordProtected_IsLoggedNotCrashed` using golden-corpus `password-protected.pdf`; assert `Job.Status = Failed`, `Job.ErrorMessage` contains "password". | FR-LIB-007 | 1 | P05-WP5-T1 |

---

## WP6 — Background worker & job management

**Goal:** `BookIngestionWorker` processes the `Jobs` queue off the UI thread;
`JobRecoveryService` requeues interrupted jobs at startup; per-file failures do not
cancel siblings.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP6-T1 | Implement `BookIngestionWorker : BackgroundService` — dequeue `Job` rows; dispatch to the appropriate handler (extract, thumbnail, spine); catch-per-job; update `Job.Status`. | NFR-OGMA-009; NFR-PROD-005 | 4 | WP3; WP5 |
| P05-WP6-T2 | Implement `JobRecoveryService.RecoverAsync()` — on startup query `Jobs WHERE Status = 'Running'`; set `Status = Queued`, `RetryCount += 1`; write `AuditEvent`. | NFR-OGMA-009 | 2 | Phase 04 `Jobs` table |
| P05-WP6-T3 | Integration test: `IngestionWorker_PerFileIsolation_SiblingJobsContinueOnFailure` — inject a fault on job 3 of 5; assert jobs 4 and 5 complete; job 3 has `Status = Failed`. | NFR-PROD-006 | 2 | P05-WP6-T1 |
| P05-WP6-T4 | Integration test: `JobRecovery_AtStartup_RequeuesRunningJobs` — manually set 2 jobs to `Running` in DB; instantiate `JobRecoveryService`; assert both back to `Queued`. | NFR-OGMA-009 | 1 | P05-WP6-T2 |
| P05-WP6-T5 | Integration test: `IngestionPipeline_UIThread_NeverStallsAbove100ms` — run full ingestion of 20 PDFs; assert max UI-thread dispatch time < 100 ms (measured via `Dispatcher.UIThread.Post` wrapper that records latency). | NFR-PROD-005 | 3 | P05-WP6-T1 |
| P05-WP6-T6 | Architecture test: `Workers_HasNoDependencyOn_UIProject` — assert `OgmaLibrary.Workers` assembly does not reference `OgmaLibrary.App`. | Phase 02 arch-test harness | 1 | WP6 |

---

## WP7 — Thumbnail & spine generation

**Goal:** SkiaSharp + PDFium generate cover, thumbnail, and spine assets in the
background; per-asset failures are logged in `Jobs` and retryable.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP7-T1 | Implement `IThumbnailService.GenerateCoverAsync(bookId, filePath, ct)`: PDFium render page 0 at 144 DPI → `SKBitmap` → resize to 200x300 → JPEG 85% → write via `SidecarService`. | FR-LIB-005 | 3 | Phase 04 `ISidecarService`; Phase 01 PDFium spike |
| P05-WP7-T2 | Implement `ISpineService.GenerateSpineAsync(bookId, filePath, ct)`: PDFium render page 0 → crop+scale to 7x100 px → JPEG 85% → write via `SidecarService`. | FR-LIB-005 | 2 | P05-WP7-T1 |
| P05-WP7-T3 | Integration test: `ThumbnailService_GeneratesCover_InBackground` — run `GenerateCoverAsync` for `simple-text.pdf`; assert sidecar cover file exists, is valid JPEG, dimensions 200x300. | FR-LIB-005 | 2 | P05-WP7-T1 |
| P05-WP7-T4 | Integration test: `SpineService_GeneratesSpine_CorrectDimensions` — assert spine file is 7x100 JPEG. | FR-LIB-005 | 1 | P05-WP7-T2 |
| P05-WP7-T5 | Integration test: `ThumbnailService_FailureRecorded_AndRetryable` — provide a corrupt PDF; assert `Job.Status = Failed`; call `RetryJobAsync`; assert job re-enqueued. | FR-LIB-005 | 2 | P05-WP7-T1 |
| P05-WP7-T6 | Integration test: `ThumbnailService_NativeLibraries_LoadOnBothPlatforms` — runs in CI on Windows and macOS; assert no `DllNotFoundException`. | Phase 01 spike; cross-platform | 1 | P05-WP7-T1 |

---

## WP8 — Incremental rescan

**Goal:** Rescanning a library where most files are unchanged is fast; only
changed/new/missing files go through the full pipeline.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP8-T1 | In `IIdentityMatcher.MatchAsync`, add fast-path: query `BookFiles WHERE RelativePath = ? AND SizeBytes = ? AND MtimeTicks = ?`; if match → update `LastSeenUtc` only; skip all further stages. | FR-LIB-006 | 2 | WP3 |
| P05-WP8-T2 | Integration test: `IncrementalRescan_SkipsUnchangedFiles` — scan once; rescan; assert no new `Job` rows created for unchanged files. | FR-LIB-006 | 2 | P05-WP8-T1 |
| P05-WP8-T3 | Integration test: `IncrementalRescan_Requeues_ChangedFiles` — modify one file's mtime; rescan; assert exactly 1 new `Job` row for that file. | FR-LIB-006 | 1 | P05-WP8-T1 |
| P05-WP8-T4 | Performance test: `IncrementalRescan_2000UnchangedFiles_Under10s` — seed 2,000 `BookFile` rows; rescan; assert wall-clock < 10 s. | NFR-OGMA-002 precondition | 2 | P05-WP8-T1 |

---

## WP9 — Scan progress UI

**Goal:** The main window shows real-time scan progress in a status-bar panel;
the panel is accessible, localized, and uses the colorful scan icons.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP9-T1 | Implement `ScanProgressViewModel` with `Phase` (string, localized key), `FilesDiscovered`, `FilesCompleted`, `FilesFailed`, `ProgressPct`, `IsCancellable` — updated via `IScanProgressService`. | FR-LIB-001; NFR-PROD-005 | 2 | Phase 03 MVVM base; WP6 |
| P05-WP9-T2 | Implement `IScanProgressService`: thread-safe event-based update; posts to `Dispatcher.UIThread` for Avalonia binding. | NFR-PROD-005 | 2 | P05-WP9-T1 |
| P05-WP9-T3 | Build `ScanProgressView` (Avalonia UserControl): status bar chip showing phase label + progress bar + file count + failure count chip + Cancel button; expand to full progress sheet on click. | FR-LIB-001 | 3 | P05-WP9-T1; Phase 03 design tokens |
| P05-WP9-T4 | Wire `ic_scan_library` (oak-amber), `ic_scan_progress` (oak-amber spinner), `ic_scan_error` (clay) icons; use `IconCatalog` registry. | ICON-SYSTEM.md | 1 | Phase 03 `IconCatalog` |
| P05-WP9-T5 | Externalize all strings to `en.resx` + `fr.resx`; run pseudolocale test. | I18N-STRATEGY.md | 1 | P05-WP9-T3 |
| P05-WP9-T6 | Accessibility walkthrough: scan progress panel is reachable by Tab; Cancel button has AutomationProperties.Name; progress bar has role progressbar + current value. | NFR-PROD-008 | 1 | P05-WP9-T3 |

---

## WP10 — Scan health report (V1)

**Goal:** A dedicated panel surfaces all scan failures in actionable categories
with retry and navigation affordances, satisfying FR-LIB-007.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP10-T1 | Implement `ScanHealthViewModel`: loads `Jobs WHERE Status = Failed OR Status = PasswordProtected`; groups into four categories (failed, password, missing thumbnails, metadata gaps). | FR-LIB-007 | 3 | Phase 04 `Jobs` table; WP6 |
| P05-WP10-T2 | Build `ScanHealthView` (Avalonia): tabbed panel; each tab lists items with file path, error message, timestamp, and action buttons (Retry / Open folder); Retry-all button per tab. | FR-LIB-007 | 3 | P05-WP10-T1; Phase 03 design tokens |
| P05-WP10-T3 | Wire health icons: `ic_health_warning` (clay), `ic_health_ok` (sage), `ic_retry` (oak-amber) via `IconCatalog`. | ICON-SYSTEM.md | 1 | Phase 03 `IconCatalog` |
| P05-WP10-T4 | Externalize all health-report strings to `en.resx` + `fr.resx`. | I18N-STRATEGY.md | 1 | P05-WP10-T2 |
| P05-WP10-T5 | Integration test: `HealthReport_ShowsAllFailureCategories` — seed one job in each failure category; assert health VM exposes each category non-empty. | FR-LIB-007 | 2 | P05-WP10-T1 |
| P05-WP10-T6 | Integration test: `HealthReport_RetryAll_RequeuesFailedJobs` — trigger RetryAll on failed tab; assert all `Status = Failed` jobs set to `Status = Queued`. | FR-LIB-007 | 1 | P05-WP10-T1 |

---

## WP11 — Tests, performance gate & documentation

**Goal:** All required tests are green on both platforms; performance baselines
are recorded; documentation is current.

| ID | Task | Req / NFR | Est (h) | Depends on |
| --- | --- | --- | --- | --- |
| P05-WP11-T1 | Full golden-corpus ingestion test: scan all 11 corpus PDFs; assert `Books` count = 10 (1 password-protected skipped); 10 covers generated; 10 `BookMetadataFields` rows with `Source = "PDF"`. | Test Strategy: golden corpus | 3 | All WPs |
| P05-WP11-T2 | Run `IncrementalRescan_2000UnchangedFiles_Under10s`; record baseline in CI artifact. | NFR-OGMA-002 | 1 | WP8 |
| P05-WP11-T3 | Run `IngestionPipeline_UIThread_NeverStallsAbove100ms`; record baseline. | NFR-PROD-005 | 1 | WP6 |
| P05-WP11-T4 | Author ADR amendment if discovery/worker architecture deviates from HLD §3. | Documentation | 1 | All WPs |
| P05-WP11-T5 | Update `CLAUDE.md` with ingestion service interfaces and `Workers` project structure. | SOURCE-SUMMARY.md §L7 | 1 | All WPs |
