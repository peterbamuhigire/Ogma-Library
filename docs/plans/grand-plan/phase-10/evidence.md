# Phase 10 Verification Evidence

Last updated: 2026-06-01

This file tracks implementation evidence for Phase 10 Search & Indexing.

## Current Position

Phase 10 has started with WP1, the backend portion of WP2, WP3 extraction
pipeline foundations, WP4 full-text search foundations, and WP5 backend Index
Manager foundations. The schema and repository foundation is in place:
`Books.IndexStatus`, Phase 10 extraction/chunk columns, the FTS5
external-content virtual table, trigger maintenance, Application-layer search
contracts, Infrastructure repositories, migration repair for non-model FTS5
objects, and focused tests. Metadata search now has an Application contract,
Infrastructure implementation, relevance scoring, a covering index, and a
2,000-book P95 test. WP3 now has a token chunker, extraction pipeline contract,
per-book extraction service, source-scoped page/note/tag/description chunking,
failure job recording, and background worker polling.
WP4 now has an Application-layer FTS contract, raw-SQL Infrastructure
implementation with snippets and bm25 ranking, combined metadata/FTS
deduplication, FTS integrity check, multi-source tests, and a warm 2,000-book
P95 benchmark.
WP5 now has an Application-layer Index Manager contract, status counts, event
publication, transactional rebuild reset, pipeline-driven rebuild, FTS integrity
gate, G7 rebuild reliability test, cancellation consistency, and extraction
mid-book cancellation recovery.
WP6 UI foundations now include a shell-mounted search panel, Index Manager
panel, localized en/fr strings, selection/open actions, rebuild/cancel actions,
rebuild confirmation/progress/status summaries, Ctrl+F/Ctrl+K/Escape/Enter
keyboard paths, stale-result protection, and automation names/status text on the
panels.

The phase is not complete. Phase 10 placeholder SVG icons, pseudolocale render
evidence, and generated-PDF extraction smoke coverage are wired, but premium
icon asset procurement, external TOC/scanned golden-corpus fixtures, manual
accessibility signoff, and final closeout remain pending.

## Automated Verification

| Command | Result |
| --- | --- |
| `dotnet build tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release` | Passed: 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase10SearchIndexSchemaTests\|FullyQualifiedName~CatalogueSchemaTests\|FullyQualifiedName~MigrationTests"` | Passed: 11 schema, migration, repository, and FTS5 trigger tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release` | Passed: 16 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 246, UI 93 |
| `dotnet build tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release` | Passed: 0 warnings, 0 errors after metadata-search implementation |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MetadataSearchServiceTests"` | Passed: 6 metadata-search service, scoring, special-character, field/shelf, and optimized 2,000-book P95 tests |
| `dotnet build tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release` | Passed: 0 warnings, 0 errors after WP3 extraction pipeline implementation |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~SearchExtractionWorkerTests\|FullyQualifiedName~Phase10SearchIndexSchemaTests\|FullyQualifiedName~MetadataSearchServiceTests"` | Passed: 17 WP1/WP2/WP3 search and worker tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release` | Passed: 16 architecture tests after WP3 |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after WP3 formatting |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after WP3 |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 252, UI 93 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --filter "FullyQualifiedName~FtsIndexServiceTests"` | Passed: 5 FTS search, snippet, source, integrity, combined-search, and warm P95 tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after WP4 formatting |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after WP4 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FtsIndexServiceTests\|FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~SearchExtractionWorkerTests\|FullyQualifiedName~Phase10SearchIndexSchemaTests\|FullyQualifiedName~MetadataSearchServiceTests"` | Passed: 21 WP1-WP4 search tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 257, UI 93 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~IndexManagerServiceTests"` | Passed: 9 extraction cancellation and Index Manager backend tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after WP5 backend |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after WP5 backend |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IndexManagerServiceTests\|FullyQualifiedName~FtsIndexServiceTests\|FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~SearchExtractionWorkerTests\|FullyQualifiedName~Phase10SearchIndexSchemaTests\|FullyQualifiedName~MetadataSearchServiceTests"` | Passed: 25 WP1-WP5 search tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 261, UI 93 |
| `dotnet build tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release` | Passed: 0 warnings, 0 errors after WP6 UI foundations |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~SearchViewModelTests"` | Passed: 5 search debounce/open, stale-result, Index Manager load/rebuild, shell keyboard, and rebuild progress UI tests |
| `dotnet build tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release` | Passed: 0 warnings, 0 errors after Phase 10 icon wiring |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~IconCatalogPhase10Tests\|FullyQualifiedName~SearchViewModelTests"` | Passed: 8 Phase 10 icon catalog and search/index UI tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after Phase 10 icon wiring |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after Phase 10 icon wiring |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build` | Passed: 16 architecture tests after Phase 10 icon wiring |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 261, UI 101 |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~SearchIndexPanels_Pseudolocale\|FullyQualifiedName~SearchViewModelTests\|FullyQualifiedName~IconCatalogPhase10Tests"` | Passed: 9 Phase 10 icon, search/index UI, and pseudolocale render tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after pseudolocale render coverage |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after pseudolocale render coverage |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 261, UI 102 |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExtractionPipeline_GeneratedPdfGoldenCorpus_IndexesRealAdapterText"` | Passed: generated QuestPDF fixture through real `PdfiumAdapterFactory` indexed and FTS matched |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after generated-PDF extraction smoke |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after generated-PDF extraction smoke |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~IndexManagerServiceTests\|FullyQualifiedName~FtsIndexServiceTests\|FullyQualifiedName~MetadataSearchServiceTests\|FullyQualifiedName~Phase10SearchIndexSchemaTests"` | Passed: 25 focused Phase 10 backend/search tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 262, UI 102 |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after WP6 UI foundations |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors after WP6 UI foundations |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build` | Passed: 16 architecture tests after WP6 UI foundations |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IndexManagerServiceTests\|FullyQualifiedName~FtsIndexServiceTests\|FullyQualifiedName~ExtractionPipelineServiceTests\|FullyQualifiedName~SearchExtractionWorkerTests\|FullyQualifiedName~Phase10SearchIndexSchemaTests\|FullyQualifiedName~MetadataSearchServiceTests"` | Passed: 25 WP1-WP5 search tests after WP6 UI foundations |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build` | Passed: Architecture 16, Core 261, UI 98 |

## Evidence Map

| Area | Evidence |
| --- | --- |
| WP1 schema columns | `20260601080133_Phase10SearchIndexSchema`; `Phase10Migration_AddsSearchIndexColumnsAndFts5Objects` verifies `Books.IndexStatus`, extraction quality/word/hash columns, and chunk source/created columns |
| FTS5 external-content table | Migration creates `SearchFts5` with `content='SearchChunks'` and `content_rowid='ChunkId'`; `Fts5Triggers_InsertUpdateDelete_SearchChunkKeepIndexConsistent` verifies insert/update/delete trigger behavior and `integrity-check` |
| Repository contracts | `IExtractedTextStore`, `ISearchChunkRepository`, and records/enums under `src/OgmaLibrary.Application/Search/` |
| Repository implementations | `ExtractedTextStore` and `SearchChunkRepository` under `src/OgmaLibrary.Infrastructure/Catalogue/Repositories/` |
| Idempotent extracted-page writes | `ExtractedTextStore_UpsertPage_RoundTripsAndKeepsOneRow` |
| Source-scoped chunk replacement | `SearchChunkRepository_ReplaceForBook_IsSourceScopedAndFtsBacked` |
| Migration repair for FTS objects | `CatalogueMigrator` now recreates non-model FTS5 table/triggers after normal migration or model-table repair |
| Bounded-context guard | `Architecture_Search_ApplicationContracts_StayBounded` verifies Search Application contracts do not depend on Reader, AI, Infrastructure, or EF Core |
| Developer docs | `docs/developer-guide/search-index.md` records the Phase 10 FTS5 schema and trigger rationale |
| Metadata search contract | `IMetadataSearchService` and `MetadataSearchResult` under `src/OgmaLibrary.Application/Search/` |
| Metadata search implementation | `MetadataSearchService` scores exact title, title prefix/contains, author, identifier, tag, shelf, and description matches |
| Metadata search performance | `PerfBenchmark_MetadataSearch_P95_LessThan150ms` seeds a deterministic 2,000-book corpus and asserts P95 <= 150 ms |
| Metadata search index | `20260601081206_Phase10MetadataSearchIndex` adds `IX_Books_Title_IsbnNormalized` |
| Chunker | `SearchChunker_Uses512TokenChunksWith64TokenOverlap` verifies 512-token chunks with 64-token overlap |
| Extraction pipeline contract | `IExtractionPipelineService`, `ExtractionBookResult`, and `ExtractionBatchResult` under `src/OgmaLibrary.Application/Search/` |
| Extraction pipeline implementation | `ExtractionPipelineService` extracts through `IBookFileLocator` and `IPdfRendererFactory`, writes `ExtractedPages`, and replaces page/note/tag/description/TOC chunks source-by-source |
| Implemented non-page sources | Notes from `AnnotationsV2` and legacy annotation bodies; tags/categories from `BookMetadataFields`; shelf names; descriptions/summaries from `BookMetadataFields` |
| TOC source handling | `SearchChunkSource.Toc` is replaced with an empty source set until a real PDF outline extractor exists |
| Idempotent/resumable rerun | `ExtractionPipeline_RerunWithSameHash_SkipsPagesAndAvoidsDuplicateChunks` |
| Page failure isolation | `ExtractionPipeline_PageFailure_RecordsFailedPageAndJobThenContinues` records `ExtractionFailed` jobs and continues indexing healthy pages |
| Pending/stale batch selection | `ExtractionPipeline_IndexNextBatch_FindsStaleAndPendingBooks` |
| Generated-PDF extraction smoke | `ExtractionPipeline_GeneratedPdfGoldenCorpus_IndexesRealAdapterText` runs a QuestPDF-generated PDF through `PdfiumAdapterFactory`, persists extracted page quality, writes page chunks, and verifies FTS match |
| Background scheduling | `SearchExtractionWorker_StartAsync_PollsExtractionPipeline` verifies hosted worker polling |
| UI test determinism | `tests/OgmaLibrary.Tests.Ui/TestAppBuilder.cs` disables UI-test parallelization because the shared Avalonia headless app is not parallel-safe |
| FTS search contract | `IFtsIndexService`, `FtsSearchResult`, and `FtsIntegrityResult` under `src/OgmaLibrary.Application/Search/` |
| FTS implementation | `FtsIndexService` joins `SearchFts5.rowid` to `SearchChunks.ChunkId`, returns snippets, and exposes higher-is-better scores from `bm25()` |
| Combined search | `ICombinedSearchService` and `CombinedSearchService` deduplicate metadata and full-text hits by `BookId` |
| FTS multi-source coverage | `FtsIndex_Search_MatchesDiacriticsAndMultipleSources` verifies page, note, tag, and description source hits |
| FTS integrity check | `FtsIndex_Search_SanitizesInvalidInputAndIntegrityCheckPasses` |
| FTS performance | `PerfBenchmark_FtsSearch_P95_LessThan500ms` seeds 2,000 books, warms the index, runs 50 queries, and asserts P95 <= 500 ms |
| Index Manager contract | `IIndexManagerService`, `IndexManagerStatus`, `BookIndexStatusItem`, `IndexRebuildResult`, and `IndexStatusUpdate` under `src/OgmaLibrary.Application/Search/` |
| Index Manager backend | `IndexManagerService` computes dashboard counts, approximate index size, per-book status rows, FTS integrity, and publishes status/rebuild events |
| G7 rebuild gate | `IndexRebuild_CompletesWithoutDuplicatesOrCorruption` rebuilds a 100-book corpus, preserves chunk count, and verifies FTS integrity |
| Rebuild cancellation consistency | `IndexRebuild_CancelledAfterReset_LeavesConsistentState` verifies reset leaves no chunks/pages and books recoverable as `NotIndexed` |
| Extraction cancellation recovery | `ExtractionPipeline_CancelledMidBook_ResetsBookForRecovery` |
| Search UI panel | `SearchViewModel`, `SearchPanelView`, shell toggle wiring, debounced query execution, stale-result protection, result selection, reader open action, Ctrl+F/Ctrl+K open, Escape close, and Enter open-selected handling |
| Index Manager UI panel | `IndexManagerViewModel`, `IndexManagerPanelView`, shell toggle wiring, status load, rebuild confirmation, progress indicator, cancel action, size/integrity/failed-page summaries, error list, and event subscription |
| Search/Index i18n | `InMemoryLocalizationService` adds en/fr keys for search and Index Manager panel labels, status text, confirmation text, and summaries |
| UI view-model tests | `SearchViewModel_QueryDebouncesAndOpenSelectedNavigates`, `SearchViewModel_StaleResults_DoNotOverwriteLatestQuery`, `IndexManagerViewModel_LoadAndRebuildExposeStatus`, `SearchBar_CtrlK_Opens`, and `IndexManager_RebuildButton_ShowsProgress` |
| Phase 10 icon catalog | 18 placeholder SVG assets under `src/OgmaLibrary.App/Assets/icons/search/`; `IconCatalog` registration; en/fr accessible labels; `IconCatalogPhase10Tests` |
| Pseudolocale render | `InMemoryLocalizationService.SetCulture("qps-ploc")`; `SearchIndexPanels_Pseudolocale_RenderWithoutBlankFrame`; screenshot artifact `artifacts/screenshots/search-index-pseudo.png` |
| Closeout evidence | `docs/qa/evidence/phase10-closeout-20260601.md` records local green gates and non-local blockers |

## Remaining Phase 10 Work

- WP2: core search view-model debounce, result-list interaction, and shell wiring are implemented locally.
- WP3: external TOC/scanned golden-corpus PDF fixtures and true crash-injection resume test remain; generated-PDF extraction smoke, core pipeline, chunking, failure isolation, and worker polling are implemented locally.
- WP4: external TOC/scanned golden-corpus PDF fixtures remain; generated-PDF FTS smoke, FTS service, snippets, integrity check, combined search, multi-source tests, and warm P95 benchmark are implemented locally.
- WP5: backend status service, rebuild/cancel flow, G7 reliability, and shell-mounted Index Manager UI are implemented locally.
- WP6: premium replacement icon assets and manual screen-reader signoff remain; placeholder SVGs, pseudolocale render coverage, keyboard paths, rebuild progress/confirmation, en/fr strings, and automation names are implemented locally.
- WP7: full phase test/benchmark/CI closeout.
