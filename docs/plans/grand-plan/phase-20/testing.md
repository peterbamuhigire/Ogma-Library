# Phase 20 — Test Plan

> Which of the 9 test layers apply, the fixtures, deterministic oracles, and
> the Phase 20 slice of the golden-corpus / perf gates. Cross-reference
> `README.md` for context and `tasks.md` for task IDs.

---

## 1. Test layers in scope

| Layer | Applied | Notes |
| --- | --- | --- |
| 1. Domain | No | No new domain types in Phase 20 |
| 2. Infrastructure | Yes | Structured logging, telemetry writer, IPerformanceMeter |
| 3. PDF | No | Phase 08/09 owns PDF tests; Phase 20 reuses them as oracle |
| 4. Search | Partial | FTS5 and semantic benchmarks at 2K and 50K scale |
| 5. AI | Partial | NFR-OGMA-007 SMART-FAIL contract test; AI fallback fault test |
| 6. UI | Partial | Telemetry consent Settings view; developer diagnostics panel |
| 7. 3D | Partial | E2e_3dFps_500 FPS benchmark |
| 8. Performance | Primary | All NFR-OGMA-001..009 and NFR-PROD-003/004 benchmarks |
| 9. Packaging | No | Phase 22 |

---

## 2. Fixtures

### Perf corpora (seeded, deterministic)

| Fixture | Size | Format | Location | Generator |
| --- | --- | --- | --- | --- |
| `perf-2k.db` | 2,000 books | SQLite (EF Core schema) | `tests/fixtures/perf-2k.db` | `PerfCorpusSeeder --seed 42 --count 2000` |
| `perf-50k.db` | 50,000 books | SQLite | `tests/fixtures/perf-50k.db` | `PerfCorpusSeeder --seed 42 --count 50000` |
| `perf-fts-2k.db` | 2,000 books + FTS5 index | SQLite | `tests/fixtures/perf-fts-2k.db` | Seeder + `IndexManager.RebuildAsync()` |
| `perf-fts-50k.db` | 50,000 books + FTS5 index | SQLite | `tests/fixtures/perf-fts-50k.db` | Seeder + `IndexManager.RebuildAsync()` |
| `perf-emb-50k.db` | 50,000 books + embedding vectors | SQLite | `tests/fixtures/perf-emb-50k.db` | Seeder + `EmbeddingService.IndexAllAsync()` |
| `golden-corpus/` | 11 canonical PDFs | PDF | `tests/golden-corpus/` | Phase 02 harness (version-pinned, hash-verified) |

The seeder is deterministic (fixed seed = 42); the same corpus is produced on
every OS and every CI run. Fixtures are gitignored (binary); a `Makefile`
target `make perf-fixtures` regenerates them.

### Fault-injection test fixtures

| Fixture | Purpose |
| --- | --- |
| `fault-library-100.db` | 100-book SQLite DB used in fault-injection tests (small = fast setup/teardown) |
| `fault-pdf-sample.pdf` | A 10-page golden-corpus PDF used for write-back fault tests |
| `fault-job-state-30pct.json` | Pre-serialized job state at 30% completion for resumable-job test |
| `fault-job-state-60pct.json` | Same at 60% |
| `fault-job-state-90pct.json` | Same at 90% |

---

## 3. Benchmark tests (Layer 8 — Performance)

All benchmark tests live in `OgmaLibrary.Benchmarks/`.

### NFR-OGMA-001: Cold start ≤3,000 ms P95

- **Class:** `ColdStartBenchmarks`
- **Method:** `Bench_ColdStart`
- **Setup:** Fresh SQLite in temp dir; 2,000-book corpus from `perf-2k.db`.
- **Measurement:** Stopwatch from `IHostBuilder.Build()` call to
  `IApplicationLifetime.ApplicationStarted` event.
- **Oracle:** `BenchmarkDotNet.Attributes.MaxBudget(3000)` equivalent;
  CI gate fails if P95 > 3,000 ms.
- **Platform notes:** Measured on both `ogma-ref-win` and `ogma-ref-macos`
  runners. macOS cold-start includes WKWebView initialization; Windows includes
  WebView2 initialization.

### NFR-OGMA-002: Catalogue load ≤2,000 ms P95

- **Class:** `CatalogueLoadBenchmarks`
- **Method:** `Bench_CatalogueLoad_2000`
- **Setup:** `perf-2k.db` copied to a temp location; OS page cache cleared
  via `FlushFileBuffers` (Windows) / `purge` (macOS).
- **Oracle:** P95 ≤ 2,000 ms; baseline delta ≤ 10%.

### NFR-OGMA-003: Metadata search ≤150 ms P95

- **Class:** `SearchBenchmarks`
- **Method:** `Bench_MetadataSearch`
- **Setup:** `perf-2k.db`; 10 representative queries (title partial, author
  exact, tag multi-value, year range, rating filter — drawn from the query
  corpus fixture `tests/fixtures/query-corpus.json`).
- **Oracle:** P95 ≤ 150 ms across all 10 queries.

### NFR-OGMA-004: Full-text search ≤500 ms P95 warm

- **Class:** `SearchBenchmarks`
- **Method:** `Bench_FtsSearch_Warm`
- **Setup:** `perf-fts-2k.db`; FTS5 index pre-warmed by one prior identical
  query (warm = SQLite WAL in page cache).
- **Oracle:** P95 ≤ 500 ms.

### NFR-OGMA-005: Page turn ≤100 ms P95 cached

- **Class:** `ReaderBenchmarks`
- **Method:** `Bench_PageTurn_Cached`
- **Setup:** Open the 1,000-page golden-corpus PDF; render pages 1–5 to warm
  the `PageRenderCache`; then measure sequential page turns 6–20.
- **Oracle:** P95 ≤ 100 ms.

### NFR-OGMA-006: 3D shelf ≥60 FPS (500 books)

- **Class:** `ThreeDShelfBenchmarks`
- **Method:** `E2e_3dFps_500`
- **Setup:** Launch the WebView with 500 spine-texture books; wait for
  `ogma://ready` bridge message; inject a JS `setInterval` that posts FPS
  samples back via the bridge at 1 Hz.
- **Oracle:** All 5 samples over 5 seconds ≥ 60.0 FPS.
- **Platform note:** This test requires a display or virtual framebuffer;
  on macOS CI runner use a virtual display; on Windows CI use WARP software
  renderer as a fallback (flag test as `[Trait("Category","RequiresDisplay")]`).

### NFR-OGMA-007: AI app-only time ≤10,000 ms P95 (SMART-FAIL)

- **Class:** `AiBenchmarks`
- **Method:** `Bench_AiAppOnlyTime`
- **Setup:** `MockAiProvider` with `SimulatedNetworkDelayMs = 0`; 5-book
  metadata payload.
- **Oracle:** P95 ≤ 10,000 ms; contract test asserts `HttpMessageHandler`
  not invoked.

### NFR-OGMA-008: Annotation durability across abnormal termination

- **Class:** `FaultAnnotationBenchmarks` (in `OgmaLibrary.Tests.FaultInjection`)
- **Method:** `FaultInject_AbnormalTermination_Annotations`
- **Setup:** 5 highlights + 2 notes written; `IFaultInjector.SimulateProcessKill()`
  called after the DB transaction is committed but before the WAL checkpoint.
- **Oracle:** After process restart, `IAnnotationService.GetAnnotationsAsync(bookId)`
  returns all 7 annotations with identical content-hashes.

### NFR-OGMA-009: Background job recovery

- **See:** WP5-T4 (`FaultInject_ResumableJob_Checkpoint`)

### NFR-PROD-003/004 at scale

- **Methods:** `Bench_CatalogueLoad_50000`, `Bench_PageNav_50000`,
  `Bench_FtsSearch_50000`, `Bench_SemanticSearch_50000`
- **Setup:** `perf-50k.db` / `perf-fts-50k.db` / `perf-emb-50k.db`.
- **Oracles:** As per Requirements table in `README.md`.

---

## 4. Fault-injection tests (Layer 2 — Infrastructure / cross-layer)

All tests in `OgmaLibrary.Tests.FaultInjection/`. All tests are sequential
(parallelism disabled). Each test is classified by R-tier.

| Test method | R-tier | Oracle |
| --- | --- | --- |
| `FaultInject_PerFileIsolation` | R1 | 7 of 10 files scanned; 3 flagged as `ScanFailed`; no exception thrown at scan level |
| `FaultInject_WriteBack_BeforeCommit` | R1 | PDF file byte-for-byte identical to pre-write state; catalogue record unchanged |
| `FaultInject_WriteBack_AfterCommit` | R1 | PDF backup restored on startup; catalogue record matches backup; no user data lost |
| `FaultInject_ResumableJob_30pct` | R4 | Resume completes; Items processed = total; no item processed twice (checked via `AuditEvents` table) |
| `FaultInject_ResumableJob_60pct` | R4 | Same oracle at 60% checkpoint |
| `FaultInject_ResumableJob_90pct` | R4 | Same oracle at 90% checkpoint |
| `FaultInject_IndexRebuild_FromSource` | R4 | FTS5 result set for 5 test queries matches pre-deletion result set (same doc IDs, same rank order) |
| `FaultInject_MissingFile_MetadataPreserved` | R1 | Book record, 7 annotations, 3 bookmarks, reading progress page 42, shelf membership all intact; `AvailabilityStatus = Missing` |
| `FaultInject_AiProviderOutage_FallsBackToLocalSearch` | R5 | No exception; search returns local results; UI status message key = `Search.AiFallback.Unavailable` |
| `FaultInject_AbnormalTermination_Annotations` | R1 | All 7 annotations recoverable post-restart |

---

## 5. Infrastructure tests (Layer 2)

| Test class | What it tests |
| --- | --- |
| `TelemetryServiceTests` | Opt-in off → no events written; opt-in on → events written; no PII fields present in written JSON |
| `LogPayloadAnalyzerTests` | Analyzer fires on `LogInformation($"Query: {query}")` pattern; does not fire on `LogInformation("CatalogueLoaded {Count}", count)` |
| `PerformanceMeterTests` | `StopwatchPerformanceMeter.MeasureAsync` returns elapsed time within 1 ms of `Stopwatch` measurement; thread-safe across 10 concurrent callers |

---

## 6. UI tests (Layer 6)

| Test | Platform | Oracle |
| --- | --- | --- |
| `TelemetryConsentView_DefaultOff` | Win + macOS | Settings page opens; telemetry toggle is in the Off position; label reads `Telemetry.OptIn.Label` (localized) |
| `TelemetryConsentView_OptIn_PersistsAcrossRestart` | Win + macOS | Toggle on → restart → toggle still on; telemetry events present in sidecar log |
| `DiagnosticsPanel_ShowsLiveMeters` | Debug build only | Panel opens; `CatalogueLoadTime_ms` updates after a catalogue load operation |

---

## 7. Golden-corpus integration

The Phase 20 benchmark suite runs the full golden-corpus E2E as a smoke check
before any benchmark job starts. If the golden-corpus suite fails, all benchmarks
are skipped and the CI job fails with `PreconditionFailed`. This ensures benchmark
results are only recorded when the software is functionally correct.

Golden-corpus documents exercised specifically by Phase 20:
- `gc-large-1000pp.pdf` — page-turn benchmark (NFR-OGMA-005).
- `gc-scanned-imageonly.pdf` — FTS5 index rebuild test (G7); post-OCR FTS5.
- `gc-bad-metadata.pdf` — write-back fault injection (R1); tests metadata
  restore on a book whose original metadata was malformed.

---

## 8. Performance budget CI gate

The CI gate operates as follows:

1. **Nightly (self-hosted runners):** Full BenchmarkDotNet `MediumRun`
   (15 iterations); results uploaded to `docs/benchmarks/baseline-*.json`.
2. **PR check (cloud runner):** BenchmarkDotNet `ShortRun` (5 iterations);
   compare against latest committed baseline; fail PR if any metric > 110%
   of baseline.
3. **Hard gate (nightly only):** If any NFR-OGMA budget is breached on the
   reference hardware runner, the nightly run fails and a GitHub Issue is
   filed automatically via the CI script.

Tolerance: 10% regression tolerance for PR checks; 0% tolerance (hard gate)
for nightly on reference hardware.

---

## 9. Test data governance

- All perf fixtures are regenerated by `make perf-fixtures`; gitignored binaries.
- The seeder is deterministic; the same corpus is produced on any machine.
- No real user data is used in any test; all metadata is synthetic.
- The golden-corpus PDF files are version-pinned (SHA-256 in
  `tests/golden-corpus/MANIFEST.json`); any change to a corpus file fails
  the hash check and blocks the test run.
