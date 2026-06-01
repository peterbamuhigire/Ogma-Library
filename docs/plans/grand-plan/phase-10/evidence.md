# Phase 10 Verification Evidence

Last updated: 2026-06-01

This file tracks implementation evidence for Phase 10 Search & Indexing.

## Current Position

Phase 10 has started with WP1 and the backend portion of WP2. The schema and
repository foundation is in place: `Books.IndexStatus`, Phase 10
extraction/chunk columns, the FTS5 external-content virtual table, trigger
maintenance, Application-layer search contracts, Infrastructure repositories,
migration repair for non-model FTS5 objects, and focused tests. Metadata search
now has an Application contract, Infrastructure implementation, relevance
scoring, a covering index, and a 2,000-book P95 test.

The phase is not complete. Search UI debounce/results, extraction pipeline, FTS
query service, Index Manager UI, full benchmarks, G7 rebuild reliability, icons,
i18n, and manual accessibility signoff remain pending.

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

## Remaining Phase 10 Work

- WP2: search view-model debounce, result-list interaction, and UI wiring.
- WP3: extraction/chunking pipeline, page quality flags, resumability.
- WP4: `FtsIndexService`, snippets, warm P95 benchmark, multi-source FTS tests.
- WP5: Index Manager service/UI, rebuild/cancel flow, G7 reliability.
- WP6: search icons, en/fr strings, keyboard and screen-reader coverage.
- WP7: full phase test/benchmark/CI closeout.
