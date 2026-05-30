# Phase 20 — Tasks

> Work packages → tasks with IDs, descriptions, estimates, dependencies, and
> the requirement/NFR/CTRL IDs each task satisfies. Read `README.md` first.

---

## Work Package 1: Reference Hardware & CI Runner Setup

**Goal:** Finalize CON-1 and configure benchmark runners.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP1-T1 | Confirm or derive the reference hardware spec (Windows + macOS) per Phase 00 CON-1; if open, record in ADR-0020 and `docs/benchmarks/REFERENCE-HARDWARE.md`. Owner sign-off required. | 0.5 d | Phase 00 CON-1 answer | CON-1, ADR-0020 |
| P20-WP1-T2 | Register and configure self-hosted GitHub Actions runners on both reference machines; add runner labels (`ogma-ref-win`, `ogma-ref-macos`); verify runner connectivity and `dotnet` version. | 0.5 d | P20-WP1-T1 | NFR-OGMA-001..009 (runner prerequisite) |
| P20-WP1-T3 | Create `.github/workflows/benchmarks.yml`: nightly schedule on self-hosted runners; artifact upload of baseline JSON; PR check job (cloud runner, trend comparison only, no hard gate on non-ref hardware). | 0.5 d | P20-WP1-T2 | CI gate (all NFR-OGMA) |

---

## Work Package 2: Core NFR-OGMA Benchmarks (001–006)

**Goal:** BenchmarkDotNet harness and benchmarks for the first six NFR-OGMA budgets.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP2-T1 | Create `OgmaLibrary.Benchmarks` project; add BenchmarkDotNet package; configure `BenchmarkHostBuilder` using the main app DI composition root with in-memory SQLite and file-system seeded from perf corpus. | 1 d | Phase 02 scaffold | NFR-OGMA-001..006 (infrastructure) |
| P20-WP2-T2 | Seed the 2,000-book perf corpus from Phase 02 golden-corpus harness into a deterministic SQLite database; write a `PerfCorpusSeeder` that generates synthetic `Books` rows with realistic metadata distribution. | 0.5 d | P20-WP2-T1, Phase 02 | NFR-OGMA-002, NFR-OGMA-003 |
| P20-WP2-T3 | `Bench_ColdStart`: measure time from `IHostedService.StartAsync` invocation to the first UI frame rendered (using an `IApplicationLifetime` probe hook); assert P95 ≤ 3,000 ms. | 0.5 d | P20-WP2-T1 | NFR-OGMA-001, NFR-PROD-002 |
| P20-WP2-T4 | `Bench_CatalogueLoad_2000`: measure `ICatalogueService.LoadCatalogueAsync()` on 2,000-book corpus from SQLite cold (file not in OS page cache); assert P95 ≤ 2,000 ms. | 0.5 d | P20-WP2-T2 | NFR-OGMA-002 |
| P20-WP2-T5 | `Bench_MetadataSearch`: measure `ISearchService.SearchMetadataAsync(query)` over 2,000 books for 10 representative queries; assert P95 ≤ 150 ms. | 0.5 d | P20-WP2-T2 | NFR-OGMA-003, FR-SEARCH-001 |
| P20-WP2-T6 | `Bench_FtsSearch_Warm`: measure FTS5 query over extracted-text corpus (warm — index loaded); assert P95 ≤ 500 ms. Requires Phase 10 FTS5 index. | 0.5 d | Phase 10, P20-WP2-T2 | NFR-OGMA-004, FR-SEARCH-002 |
| P20-WP2-T7 | `Bench_PageTurn_Cached`: measure `IReaderService.RenderPageAsync(bookId, pageNumber)` with warm render cache; assert P95 ≤ 100 ms. Requires Phase 08 reader core. | 0.5 d | Phase 08, P20-WP2-T1 | NFR-OGMA-005, FR-READ-002 |
| P20-WP2-T8 | `E2e_3dFps_500`: drive the 3D bookshelf WebView with 500 spine textures; sample FPS from the JS bridge `fps` telemetry message at 1 Hz over 5 seconds; assert all samples ≥ 60 FPS. Requires Phase 14. | 0.5 d | Phase 14, P20-WP2-T1 | NFR-OGMA-006, FR-CAT-001 |
| P20-WP2-T9 | Persist benchmark results as `docs/benchmarks/baseline-windows.json` and `docs/benchmarks/baseline-macos.json`; write PR regression check script that fails if any metric regresses beyond 10% of baseline. | 0.5 d | P20-WP2-T3..T8 | All NFR-OGMA-001..006 CI gates |

---

## Work Package 3: NFR-OGMA-007 SMART-FAIL + NFR-OGMA-008

**Goal:** AI app-only time gate and abnormal-termination annotation durability.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP3-T1 | Implement `MockAiProvider : IAiProvider` with configurable `SimulatedNetworkDelayMs = 0`; register via DI in benchmark host. Assert in a contract test that no `HttpClient` call is made when mock is active. | 0.5 d | Phase 12 AI gateway | NFR-OGMA-007 contract |
| P20-WP3-T2 | `Bench_AiAppOnlyTime`: call `IAiAdvisorService.GetRecommendationsAsync` with metadata-only payload (5 books) with `MockAiProvider`; assert P95 ≤ 10,000 ms. Document that this measures only app-side logic (serialization, routing, response parsing). | 0.5 d | P20-WP3-T1 | NFR-OGMA-007 |
| P20-WP3-T3 | `FaultInject_AbnormalTermination_Annotations`: write 5 annotations, inject `IFaultInjector.SimulateProcessKill` mid-write; restart process; assert all committed annotations are recovered. Classify R1. | 0.5 d | Phase 09, WP5 fault framework | NFR-OGMA-008, R1 |

---

## Work Package 4: 50,000-Item Scale Benchmarks (NFR-PROD-003/004)

**Goal:** Prove the system performs at V1-scale catalogue size.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP4-T1 | Extend `PerfCorpusSeeder` to generate a 50,000-book synthetic SQLite database (deterministic seed; realistic metadata; pre-seeded FTS5 index). Store as a binary fixture in `tests/fixtures/perf-50k.db` (gitignored; regenerated by seeder script). | 1 d | P20-WP2-T2 | NFR-PROD-003, NFR-PROD-004 |
| P20-WP4-T2 | `Bench_CatalogueLoad_50000`: measure first-screen render time on 50,000-book corpus; assert P95 ≤ 1,000 ms. | 0.5 d | P20-WP4-T1 | NFR-PROD-003 |
| P20-WP4-T3 | `Bench_PageNav_50000`: measure page-navigation latency (list scroll to item 40,000) on 50,000-book virtual list; assert P95 ≤ 200 ms. | 0.5 d | P20-WP4-T1, Phase 06 | NFR-PROD-003 |
| P20-WP4-T4 | `Bench_FtsSearch_50000`: FTS5 query on 50,000-book extracted-text index; assert P95 ≤ 500 ms warm. | 0.5 d | P20-WP4-T1, Phase 10 | NFR-PROD-004 |
| P20-WP4-T5 | `Bench_SemanticSearch_50000`: embedding cosine/ANN query on 50,000-book vector set; assert P95 ≤ 1,500 ms. Requires Phase 11 semantic search. | 0.5 d | P20-WP4-T1, Phase 11 | NFR-PROD-004 |

---

## Work Package 5: Fault-Injection Framework + Reliability Tests

**Goal:** All seven reliability scenarios have passing fault-injection tests.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP5-T1 | Create `OgmaLibrary.Tests.FaultInjection` project; implement `IFaultInjector` and `InMemoryFaultInjector` (DEBUG-only); register via DI in test host only; add `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. | 0.5 d | Phase 02 scaffold | Fault framework |
| P20-WP5-T2 | `FaultInject_PerFileIsolation`: inject `ScanFileRead` fault on 3 of 10 files; assert the other 7 are scanned successfully and the 3 failed files are flagged (not aborted) in the catalogue. Covers FR-LIB-004. | 0.5 d | P20-WP5-T1, Phase 05 | NFR-OGMA-009, FR-LIB-004, R1 |
| P20-WP5-T3 | `FaultInject_WriteBack_MidCrash` (two sub-cases): (a) crash before DB commit — original PDF and catalogue record unchanged; (b) crash after DB commit but before PDF flush — backup restored automatically on next start. Both pass → R1 cleared. | 1 d | P20-WP5-T1, Phase 07 | FR-META-005, R1, NFR-PROD-010 |
| P20-WP5-T4 | `FaultInject_ResumableJob_Checkpoint` (G8): start a 1,000-item enrichment job; kill at 30%, 60%, 90% checkpoints; restart; assert completion without re-processing already-processed items (idempotency check via audit log). | 1 d | P20-WP5-T1, Phase 07 | NFR-OGMA-009, G8, FR-META-006 |
| P20-WP5-T5 | `FaultInject_IndexRebuild_FromSource` (G7): delete FTS5 index and embedding store; trigger rebuild; assert all extracted-text chunks are re-indexed and semantic search returns the same top-3 results as pre-deletion. | 0.5 d | P20-WP5-T1, Phase 10/11 | G7, FR-SEARCH-002 |
| P20-WP5-T6 | `FaultInject_MissingFile_MetadataPreserved`: remove a PDF from disk; trigger rescan; assert catalogue record, all annotations, reading progress, shelf memberships, and metadata fields survive intact. Covers FR-LIB-004. | 0.5 d | P20-WP5-T1, Phase 05 | FR-LIB-004, R1, NFR-PROD-010 |
| P20-WP5-T7 | `FaultInject_AiProviderOutage_FallsBackToLocalSearch`: inject `AiProviderCall` fault returning HTTP 503; assert (a) no crash, (b) search falls back to hybrid metadata+FTS5, (c) UI shows a localized "AI unavailable, showing local results" status message. | 0.5 d | P20-WP5-T1, Phase 12 | FR-AI-001, NFR-PROD-001 |

---

## Work Package 6: LAN Host Concurrency Benchmark

**Goal:** Establish and record the LAN Host throughput baseline for 20 clients.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP6-T1 | `Bench_LanHost_Concurrency_20`: start LAN Host in-process on loopback HTTPS; spawn 20 `HttpClient` workers; each performs catalogue load + 5 search queries + 10 page requests; measure P50/P95 per client; assert P95 ≤ 1.5× single-client P95. | 1 d | Phase 16 LAN Host | LAN concurrency baseline |
| P20-WP6-T2 | Persist concurrency baseline to `docs/benchmarks/lan-concurrency-baseline.json`; document in `docs/benchmarks/BUDGETS.md` under "LAN Host". | 0.5 d | P20-WP6-T1 | Documentation |

---

## Work Package 7: Structured Logging & IPerformanceMeter

**Goal:** Operational observability without leaking private data.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP7-T1 | Create `IPerformanceMeter` interface in `OgmaLibrary.Application.Diagnostics`; implement `StopwatchPerformanceMeter` in `Infrastructure.Diagnostics`; inject into all bounded-context service implementations. | 0.5 d | Phase 02 scaffold | NFR-PROD-005 instrumentation |
| P20-WP7-T2 | Configure `Microsoft.Extensions.Logging` JSON formatter with the structured schema defined in README §6; add `LoggingDiagnosticsExtensions` startup extension that wires the formatter. | 0.5 d | P20-WP7-T1 | Observability |
| P20-WP7-T3 | Implement `LogPayloadAnalyzer` Roslyn analyzer: warn (IDE0300-class) on `LogInformation` / `LogWarning` / `LogError` calls whose format string argument contains string interpolations of fields matching `query`, `annotation`, `payload`, `path`, `content`. Add to `OgmaLibrary.Analyzers`. | 0.5 d | P20-WP7-T1 | Privacy, CTRL-OGMA-018 |
| P20-WP7-T4 | Write integration tests: (a) assert no log line from the benchmark run contains any of the prohibited field names; (b) assert `LogPayloadAnalyzer` fires on a synthetic violating call and is suppressed by a `#pragma` with a documented reason. | 0.5 d | P20-WP7-T1..T3 | Privacy verification |

---

## Work Package 8: Telemetry Consent + Developer Diagnostics UI

**Goal:** Opt-in telemetry and a developer diagnostics panel.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP8-T1 | Implement `ITelemetryService` in `Infrastructure.Diagnostics`: feature-usage counter writer; writes to a rotating local JSON log in the sidecar folder (`telemetry/`); no PII, no content fields. | 0.5 d | P20-WP7-T1 | CTRL-OGMA-025 |
| P20-WP8-T2 | Add telemetry consent setting to `AppSettings`; add `TelemetrySettingsView.axaml` in Settings; externalize all copy in `en` + `fr`; default = off; assert no telemetry events are written when opt-in is false. | 0.5 d | P20-WP8-T1 | CTRL-OGMA-025, NFR-PROD-001 |
| P20-WP8-T3 | Implement developer diagnostics panel: `PerformanceDiagnosticsView.axaml` (debug builds only, toggled via `AppSettings.ShowDiagnosticsPanel`); displays live `IPerformanceMeter` readings for: cold-start time, catalogue load time, last search latency, FPS sample, last job duration. | 0.5 d | P20-WP7-T1 | Developer observability |

---

## Work Package 9: CI Integration & Documentation

**Goal:** Benchmark gate in CI; all benchmark docs committed.

| Task ID | Description | Est. | Depends on | Satisfies |
| --- | --- | --- | --- | --- |
| P20-WP9-T1 | Wire `.github/workflows/benchmarks.yml`: nightly self-hosted run → upload JSON artifacts; PR job on cloud runner reads last baseline and posts a comment with trend delta; fails PR if any metric > 110% of baseline. | 0.5 d | P20-WP1-T3, P20-WP2-T9 | All NFR CI gates |
| P20-WP9-T2 | Write `docs/benchmarks/BUDGETS.md`: table of all NFR-OGMA-001..009 and NFR-PROD-003/004 budgets, current baseline values, tolerance, and CI gate status. | 0.5 d | P20-WP2-T9, P20-WP4-T5 | Documentation |
