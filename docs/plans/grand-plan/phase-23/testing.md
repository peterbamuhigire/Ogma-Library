# Phase 23 — Test Plan

> Which test layers apply, fixtures, oracles, and the Phase 23 slice of
> the beta-gate, SLO, Extension SDK, importer, and open-source readiness gates.

---

## 1. Test layers in scope

| Layer | Applied | Notes |
| --- | --- | --- |
| 1. Domain | No | No domain-model changes |
| 2. Infrastructure | Yes | `SloAggregator`, startup `integrity_check`, `ExtensionLoader`, `CatalogueReadApiAdapter`, `SearchReadApiAdapter`, importers |
| 3. PDF | No | Unchanged |
| 4. Search | Partial | `ISearchReadApi` adapter tested via integration test |
| 5. AI | No | `IAiProvider` unchanged; extension egress policy tested via architecture test |
| 6. UI | Partial | Import wizard UI flow; MCP listener Settings toggle; importer menu items |
| 7. 3D | No | Unchanged |
| 8. Performance | Partial | Extension loader performance (< 500 ms for 10 extensions) |
| 9. Packaging | Partial | Open-source release readiness check; `gitleaks`; XML docs build |

---

## 2. Beta-gate final verification (Layer 9)

The 8 public-beta gates are re-verified (not re-implemented — the test methods
exist from Phases 08–20). The test methods and their current pass status are
recorded in `docs/qa/BETA-GATES-STATUS.md`.

| Gate | Test method | Owner phase |
| --- | --- | --- |
| G1 WebView bridge | `Bridge_MessageRoundTrip_TypeSafe` | Phase 14 |
| G2 PDFium wrapper | `PdfiumWrapper_Benchmark_Baseline` | Phase 01/08 |
| G3 500-book responsiveness | `Bench_CatalogueLoad_500` | Phase 20 |
| G4 2,000-book responsiveness | `Bench_CatalogueLoad_2000` | Phase 20 |
| G5 Write-back backup/restore | `FaultInject_WriteBack_MidCrash` | Phase 20 |
| G6 AI payload preview | `AiPayloadPreview_ShowsPayloadBeforeCall` | Phase 12 |
| G7 Index rebuild | `FaultInject_IndexRebuild_FromSource` | Phase 20 |
| G8 Interrupted-job recovery | `FaultInject_ResumableJob_Checkpoint` | Phase 20 |

All eight must show ✅ in `docs/qa/BETA-GATES-STATUS.md` before WP4 proceeds.

---

## 3. SLO and reliability tests (Layer 2)

### 3a. SloAggregator tests

**Class:** `SloAggregatorTests`

| Test | Oracle |
| --- | --- |
| `ComputeUpdateSuccessRate_AllSuccess_Returns100` | Input: 100 `update_success=true` events; output: 1.0 (100%) |
| `ComputeUpdateSuccessRate_OneFailure_Returns99` | Input: 99 success + 1 failure; output: 0.99 |
| `ComputeCrashFreeRate_NoCrashes_Returns100` | Input: 200 sessions, 0 crashes; output: 1.0 |
| `ComputeMedianUpdateTime_Returns50thPercentile` | Input: sorted download times; output: exact median |
| `SloAggregator_NoData_ReturnsNullNotException` | Input: empty log file (telemetry off or no events); output: null for each SLO metric (not an exception) |
| `SloAggregator_7DayWindow_ExcludesOlderEvents` | Input: events spanning 10 days; output: only events within the last 7 days are counted |

### 3b. Startup integrity check test

**Class:** `StartupIntegrityCheckTests`

| Test | Oracle |
| --- | --- |
| `IntegrityCheck_ValidDb_Silent` | Start app with a valid database; no recovery dialog displayed |
| `IntegrityCheck_CorruptedDb_ShowsWizard` | Corrupt the catalogue (write random bytes to page 2); start app; recovery wizard is displayed |
| `IntegrityCheck_CorruptedDb_RestoreFromBackup` | Same as above; user selects "Restore from backup"; most recent migration backup is restored; all user data intact |

---

## 4. Extension SDK tests (Layer 2 — infrastructure)

### 4a. Architecture tests

**Class:** `ExtensionSdkArchitectureTests`

| Test | Oracle |
| --- | --- |
| `Extension_SdkDoesNotDependOnInfrastructure` | `OgmaLibrary.Extensions.Sdk` has no transitive dependency on `OgmaLibrary.Infrastructure` or `OgmaLibrary.Application` |
| `Extension_SdkOnlyDependsOnDomainAndPrimitives` | SDK's only references are `OgmaLibrary.Domain`, `System.*`, and `Microsoft.Extensions.Logging.Abstractions` |
| `Extension_CannotCallHttpDirectly` | The extension DI container built by `ExtensionLoader` does not contain a registration for `HttpClient`, `IHttpClientFactory`, or any `System.Net.Http.*` type |
| `Extension_CannotAccessCredentialStore` | Extension DI container does not expose `ICredentialStore` |
| `Extension_CannotAccessAuditTrail` | Extension DI container does not expose `IAuditTrailService` |

### 4b. ExtensionLoader tests

**Class:** `ExtensionLoaderTests`

| Test | Oracle |
| --- | --- |
| `ExtensionLoader_ValidAssembly_LoadsExtension` | An assembly with one `[OgmaExtension]`-attributed `IMetadataProvider` is loaded; one `IMetadataProvider` is returned by the loader |
| `ExtensionLoader_NoExtensions_EmptyResult` | Extensions directory is empty; loader returns empty list without exception |
| `ExtensionLoader_InvalidAssembly_LogsAndSkips` | Assembly in extensions dir throws `BadImageFormatException` on load; loader logs a warning and continues loading other assemblies |
| `ExtensionLoader_10Extensions_LoadsInUnder500ms` | Load 10 test extension assemblies from disk; total `ExtensionLoader.LoadAllAsync()` time < 500 ms |
| `ExtensionLoader_ExtensionCanQueryCatalogue` | Loaded `IMetadataProvider` calls `ICatalogueReadApi.SearchAsync`; returns books from the test catalogue |

### 4c. ReadApi adapter tests

**Class:** `CatalogueReadApiAdapterTests`, `SearchReadApiAdapterTests`

| Test | Oracle |
| --- | --- |
| `CatalogueReadApi_SearchAsync_Returns2000Books` | Call `SearchAsync` on the 2,000-book perf corpus; result count = 2,000 |
| `CatalogueReadApi_GetBook_ReturnsAllFields` | Call `GetBookAsync(knownId)`; all `BookDetail` fields match the catalogue record |
| `SearchReadApi_SearchMetadata_MatchesCatalogueResult` | Compare `ISearchReadApi.SearchMetadataAsync("query")` with `ICatalogueService.SearchAsync("query")`; same book IDs in top 10 |

---

## 5. Importer tests (Layer 2)

**Project:** `OgmaLibrary.Tests.Importers`

### Import test fixtures (golden corpus, committed to `tests/fixtures/importers/`)

| Fixture | Description |
| --- | --- |
| `zotero-export-sample.rdf` | 20-item Zotero RDF export with multi-author books, tags, notes |
| `zotero-export-sample.json` | Same 20 items as Better BibTeX JSON |
| `calibre-metadata-sample.opf` | Calibre `metadata.opf` for 5 books with custom columns |
| `goodreads-export-sample.csv` | Goodreads export CSV with 15 books, mixed shelves, ratings |

### Importer test cases

| Test | Oracle |
| --- | --- |
| `ZoteroRdfImporter_Parses20Books` | Produces exactly 20 `BookImportRecord` instances; each has `Title`, `Authors` (non-empty), `Isbn` (where present in fixture) |
| `ZoteroRdfImporter_MultiAuthor_PreservesOrder` | "Smith, J.; Jones, A." → `Authors = ["Smith, J.", "Jones, A."]` in order |
| `ZoteroJsonImporter_Parses20Books` | Same oracle as RDF importer for the same data set |
| `CalibreImporter_ParsesToBookImportRecord` | 5 books parsed; Dublin Core `dc:title`, `dc:creator`, `dc:identifier` (ISBN) mapped correctly |
| `GoodreadsImporter_Parses15Books` | 15 records parsed; `Rating` field = integer 1-5 or null; `DateRead` parsed as `DateOnly` |
| `GoodreadsImporter_EmptyShelf_HandledGracefully` | A book with `Bookshelves` = empty string → `Shelves = []` (not an exception) |
| `GoodreadsImporter_MissingIsbn_HandledGracefully` | A book with no ISBN13 → `Isbn = null` (not an exception) |

---

## 6. MCP extension scaffold test (Layer 2)

**Class:** `McpListenerScaffoldTests`

| Test | Oracle |
| --- | --- |
| `McpListener_DefaultOff_DoesNotListen` | On app start with `EnableMcpListener = false`, `System.Net.HttpListener` is not started; no port is bound |
| `McpListener_OptedIn_ListensOnLoopback` | With `EnableMcpListener = true`, listener starts on `http://127.0.0.1:<port>/mcp`; `HttpClient` GET to that URL returns 200 with MCP protocol response |
| `McpListener_RequestOutsideLoopback_Rejected` | Listener does not bind to `0.0.0.0` or any non-loopback address |

---

## 7. UI tests (Layer 6 — partial)

| Test | Platform | Oracle |
| --- | --- | --- |
| `ImporterMenu_ZoteroItem_Visible` | Win + macOS | Library menu contains "Import from Zotero" item; accessible name correct |
| `ImporterMenu_CalibreItem_Visible` | Win + macOS | Library menu contains "Import from Calibre" item |
| `ImporterMenu_GoodreadsItem_Visible` | Win + macOS | Library menu contains "Import from Goodreads" item |
| `ImportWizard_ShowsPreviewBeforeCommit` | Win + macOS | Import wizard shows a preview list of `BookImportRecord` items before writing to catalogue |
| `McpListenerToggle_DefaultOff` | Win + macOS | Settings > Advanced page shows MCP listener toggle in Off state by default |
| `McpListenerToggle_Accessible` | Win + macOS | Toggle has accessible name; keyboard-operable; screen-reader announces state change |

---

## 8. Open-source release readiness checks (Layer 9)

| Check | Command / Oracle |
| --- | --- |
| `gitleaks` scan | `gitleaks detect --source . --report-format json --report-path /tmp/gitleaks.json`; report `findings` array is empty |
| XML doc coverage | `dotnet build --configuration Release` exits 0 with no `CS1591` warnings (missing XML comment) on any public project |
| `GenerateDocumentationFile` | All projects in `src/` have `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in their `.csproj` or `Directory.Build.props` |
| LICENSE present | `git show HEAD:LICENSE` exits 0 and contains the owner-selected license text |
| CONTRIBUTING.md present | `git show HEAD:CONTRIBUTING.md` exits 0 |
| CODE_OF_CONDUCT.md present | `git show HEAD:CODE_OF_CONDUCT.md` exits 0 |

---

## 9. Beta soak metrics (runtime, not automated tests)

During the 14-day soak, the following metrics are recorded daily in
`docs/ops/BETA-SOAK-REPORT.md`:

| Metric | Source | Target |
| --- | --- | --- |
| Update-success rate (7-day rolling) | `SloAggregator` from opt-in telemetry | ≥ 99.0% |
| Crash-free session rate (7-day rolling) | `SloAggregator` | ≥ 99.5% |
| GitHub Releases download count | GitHub API aggregate | Monitored (no target; baseline for V1) |
| Open `beta-feedback` issues | GitHub Issues API | Triaged within 48 hours |
| Open `bug` + `P0` issues | GitHub Issues API | = 0 before soak exit |

Soak exit: all metrics at target for 7 consecutive days; no open SEV-1 or
SEV-2 incident; owner approves soak exit in `BETA-SOAK-REPORT.md`.

---

## 10. Test artifacts committed by Phase 23

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.Tests.Importers` project | `tests/OgmaLibrary.Tests.Importers/` |
| Importer golden fixtures | `tests/fixtures/importers/` |
| Extension SDK architecture tests | `tests/OgmaLibrary.Tests.Architecture/ExtensionSdkArchitectureTests.cs` |
| `ExtensionLoaderTests` | `tests/OgmaLibrary.Tests.Infrastructure/ExtensionLoaderTests.cs` |
| `SloAggregatorTests` | `tests/OgmaLibrary.Tests.Infrastructure/SloAggregatorTests.cs` |
| `StartupIntegrityCheckTests` | `tests/OgmaLibrary.Tests.Infrastructure/StartupIntegrityCheckTests.cs` |
| `McpListenerScaffoldTests` | `tests/OgmaLibrary.Tests.Infrastructure/McpListenerScaffoldTests.cs` |
| Beta soak report | `docs/ops/BETA-SOAK-REPORT.md` |
