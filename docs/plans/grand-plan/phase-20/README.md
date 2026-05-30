# Phase 20 — Performance Engineering & Reliability

One sentence: Turn every NFR-OGMA and NFR-PROD performance budget into a hard
CI gate anchored to the confirmed reference hardware, and harden the system
against every failure mode identified in the Test Strategy reliability matrix.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (performance gates) + V1 (50,000-item scale, LAN concurrency) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 7 (polish/hardening spine) |
| **Platforms** | Windows 10 1903+ (WebView2) + macOS 13 Ventura+ (WKWebView); both runners required for benchmarks |
| **Baseline date** | 2026-05-30 |

---

## 2. Objectives

1. Every NFR-OGMA budget (001-009) has a passing, deterministic CI benchmark
   on both Windows and macOS reference hardware; any budget miss is a build
   failure, not a warning.
2. The reference hardware specification from Phase 00 (CON-1) is formally
   reconciled: if Phase 00 deferred the machine spec, it is finalized here as
   the anchor for all NFR-OGMA measurements, and recorded in ADR-0020.
3. NFR-PROD-003 and NFR-PROD-004 at 50,000-item scale pass: first screen ≤1 s,
   page navigation ≤200 ms, full-text search ≤500 ms, semantic search ≤1.5 s.
4. LAN Host concurrency baseline is established: the Phase 16 host sustains
   20 simultaneous client readers at the defined throughput budget without
   degrading single-client latency beyond 20%.
5. All seven reliability scenarios from Test Strategy §6 have fault-injection
   tests: per-file scan isolation, transactional write-back restore, resumable
   jobs (G8), index repair from source (G7), missing-file metadata preservation,
   AI degradation to local search, abnormal-termination annotation durability.
6. Structured logging and opt-in device-local telemetry are operational and
   tested; no log line leaks user-authored content or off-device query payloads.
7. NFR-OGMA-007 SMART-FAIL handling is implemented and gated: the AI
   metadata-only budget (≤10 s P95) is measured for app-only time only;
   provider latency is excluded and the separation is enforced by test harness
   contract.

---

## 3. Scope

### In scope

- Finalizing the reference hardware specification (CON-1 resolution) and
  recording it in ADR-0020 if Phase 00 left it open; if Phase 00 already
  ratified CON-1, this phase re-confirms and anchors the benchmark runners.
- Implementing and running CI benchmark jobs for all NFR-OGMA-001..009:
  cold start, catalogue load, metadata search, full-text search, page turn,
  3D FPS, AI app-only time, annotation durability, background-job recovery.
- NFR-PROD-003 and NFR-PROD-004 at 50,000 items: load time, screen render,
  search latency. Synthetic 50,000-item perf corpus (seeded from the Phase 02
  golden-corpus harness).
- LAN Host (Phase 16) concurrency benchmark: 20 simultaneous clients,
  measure P50/P95 latency per operation type (catalogue load, page stream,
  search), establish the Phase 16 concurrency budget in a recorded baseline.
- Fault-injection tests:
  - Per-file scan isolation: one corrupt/inaccessible PDF does not abort the
    scan of remaining files (FR-LIB-004, NFR-OGMA-009).
  - Transactional write-back restore: simulated mid-write crash; catalogue and
    PDF file are intact and match pre-write state (FR-META-005, R1).
  - Resumable jobs (G8): job interrupted at 30%/60%/90%; resumes from
    checkpoint without re-processing completed items.
  - Index repair from source (G7): FTS5 / embedding index deleted; rebuild
    from extracted-text store completes correctly.
  - Missing-file metadata preservation: file removed from disk; catalogue
    record, annotations, reading progress, and shelves survive intact
    (FR-LIB-004, R1).
  - AI degradation to local search: AI provider returns 5xx or is unreachable;
    app falls back to hybrid metadata+FTS5 search with a visible, localized
    status message; no crash, no data loss (FR-AI-001).
  - Abnormal-termination annotation durability (NFR-OGMA-008): process
    killed mid-write; reopen recovers all committed annotations.
- Structured logging via `Microsoft.Extensions.Logging` with JSON formatter:
  every significant operation emits a structured log event with correlation ID,
  bounded-context tag, and duration_ms; no user-authored data in log payloads.
- Opt-in device-local telemetry: feature-usage counters (no PII, no content)
  written to a local rotating log file; telemetry is off by default; user
  can opt in/out in Settings; CTRL-OGMA-025 (telemetry consent gate).
- Observability surfaces: a developer diagnostics panel (toggled by a build
  flag / settings key) displaying live NFR-OGMA meter readings.
- CI benchmark infrastructure: a `Benchmarks` project using BenchmarkDotNet;
  benchmark jobs run on tagged runners matching the reference hardware spec;
  results persisted to `docs/benchmarks/` as JSON baselines; PR checks compare
  against baseline and fail on regression beyond the defined tolerance.

### Explicitly out of scope

- Cloud / remote telemetry (no data leaves the device in this phase; cloud
  telemetry pipeline is a post-V1 consideration gated by a fresh DPIA).
- New UI features or icon surfaces beyond the developer diagnostics panel.
- Network performance outside the LAN (internet latency is provider-dependent
  and excluded from NFR-OGMA-007 by the SMART-FAIL contract).
- Database schema migrations (handled in Phase 04; this phase only benchmarks
  the existing schema at scale).
- LAN Host capacity beyond 20 clients (classroom target per
  LAN-CLASSROOM-ARCHITECTURE.md §3).

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| NFR-OGMA-001 | MVP | Cold start ≤3 s P95 | `Bench_ColdStart` BenchmarkDotNet test; CI gate |
| NFR-OGMA-002 | MVP | Catalogue load ≤2 s P95 (2,000 books) | `Bench_CatalogueLoad_2000` on perf corpus; CI gate |
| NFR-OGMA-003 | MVP | Metadata search ≤150 ms P95 | `Bench_MetadataSearch` on 2,000-book corpus; CI gate |
| NFR-OGMA-004 | MVP | Full-text search ≤500 ms P95 warm | `Bench_FtsSearch_Warm` on extracted-text corpus; CI gate |
| NFR-OGMA-005 | MVP | Page turn ≤100 ms P95 cached | `Bench_PageTurn_Cached` with warm render cache; CI gate |
| NFR-OGMA-006 | MVP | 3D shelf ≥60 FPS (500 books) | `E2e_3dFps_500` using WebView JS bridge metrics; CI gate |
| NFR-OGMA-007 | MVP | AI app-only time ≤10 s P95 (SMART-FAIL) | `Bench_AiAppOnlyTime` with mock provider (0 ms latency); CI gate; contract test asserts provider latency excluded |
| NFR-OGMA-008 | MVP | Annotation durable across abnormal termination | `FaultInject_AbnormalTermination_Annotations`; R1 |
| NFR-OGMA-009 | MVP | Background job recoverable without duplicate work | `FaultInject_ResumableJob_Checkpoint` (G8); CI gate |
| NFR-PROD-003 | V1 | First screen ≤1 s / page ≤200 ms at 50,000 items | `Bench_CatalogueLoad_50000`; CI gate |
| NFR-PROD-004 | V1 | Full-text ≤500 ms / semantic ≤1.5 s at 50,000 items | `Bench_FtsSearch_50000`, `Bench_SemanticSearch_50000`; CI gate |
| NFR-PROD-005 | MVP | No UI stall >100 ms | `Bench_UiResponsiveness` dispatcher-thread monitor; CI gate |
| NFR-PROD-006 | MVP | Crash-free ≥99.5% | Automated crash-injection suite; crash rate < 0.5% |
| FR-LIB-004 | MVP | Flag unavailable files without deleting user data | `FaultInject_MissingFile_MetadataPreserved`; R1 |
| FR-META-005 | V1 | Write-back backup + restore on failure | `FaultInject_WriteBack_MidCrash`; R1 |
| G7 | All | Index rebuild from source | `FaultInject_IndexRebuild_FromSource` |
| G8 | All | Interrupted-job recovery | `FaultInject_ResumableJob_Checkpoint` |
| CON-1 | MVP | Reference hardware spec resolved | ADR-0020; benchmark runner config matches spec |
| CTRL-OGMA-025 | V1 | Telemetry consent gate | `Settings_Telemetry_DefaultOff`; opt-in integration test |

---

## 5. Dependencies

### Depends on

- **Phase 00**: CON-1 reference hardware spec (if still open, must be resolved
  here as a Phase 20 prerequisite; see ADR-0020).
- **Phase 02**: BenchmarkDotNet harness scaffolding, golden-corpus seeded perf
  corpora (500-book and 2,000-book sets), `dotnet test` baseline.
- **Phase 04-05**: Catalogue + Ingestion — all data paths under benchmark.
- **Phase 08-09**: Reader + Annotations — page-turn budget and annotation
  durability tests target these components.
- **Phase 10-11**: Search (FTS5 + semantic) — all search benchmarks.
- **Phase 12**: AI gateway — NFR-OGMA-007 SMART-FAIL test requires the mock
  provider injection point.
- **Phase 14**: 3D bookshelf — NFR-OGMA-006 3D FPS gate.
- **Phase 16**: LAN Host — concurrency benchmark.
- **Phase 19**: Security hardening — structured logs must not leak secrets or
  PII (reviewed jointly); CTRL-OGMA-025 telemetry consent aligns with
  CTRL-OGMA-018 audit trail.

### Unblocks

- **Phase 21**: Comprehensive QA and the golden-corpus E2E pass require all
  performance gates to be green so the suite runs within CI time budgets.
- **Phase 22**: Store submission readiness; App Store and Windows Store reviewers
  check launch-time and responsiveness; hard CI gates here prevent regression.
- **Phase 23**: SLO baseline (crash-free, update-success) is derived from the
  reliability test results here.

---

## 6. Architecture & approach

### Bounded contexts touched

- **All contexts** (performance is cross-cutting): benchmarks exercise the
  composition root paths of Library Catalogue, Ingestion Pipeline, Metadata
  Enrichment, Reader, Search Index, AI Advisor, Bookshelf Presentation, and
  (for LAN) Library Sharing / Host.
- **Settings & Security**: telemetry consent gate (CTRL-OGMA-025) added to the
  Settings context.
- **Observability** is not a new bounded context; it is a cross-cutting
  infrastructure concern owned by `OgmaLibrary.Infrastructure.Diagnostics`.

### New project / components

- `OgmaLibrary.Benchmarks` — BenchmarkDotNet project; one benchmark class per
  NFR; shares the same DI composition root as the main app via a
  `BenchmarkHostBuilder` that wires real implementations with an in-memory
  SQLite database seeded from the perf corpus.
- `OgmaLibrary.Infrastructure.Diagnostics` — structured logging abstractions,
  the `IPerformanceMeter` interface, and the opt-in telemetry writer. Depends
  only on `Application` and `Domain`; no UI coupling.
- `OgmaLibrary.Tests.FaultInjection` — the fault-injection test project; uses
  a `IFaultInjector` abstraction (see below) to simulate crashes, missing files,
  mid-write failures, and provider outages.

### IFaultInjector abstraction

```csharp
/// <summary>
/// Injects deterministic faults into infrastructure operations for reliability testing.
/// Implementations are test-only; production DI never registers a real fault injector.
/// </summary>
public interface IFaultInjector
{
    void ThrowBefore(FaultPoint point);
    void ThrowAfter(FaultPoint point, int afterCallCount = 1);
    void SimulateProcessKill();
}

public enum FaultPoint
{
    WriteBackBeforeFlush,
    WriteBackAfterDbCommit,
    ScanFileRead,
    JobCheckpointWrite,
    AiProviderCall,
    EmbeddingStoreWrite,
}
```

All fault injection points are `DEBUG`-only compiler-guarded; the production
build has zero overhead.

### NFR-OGMA-007 SMART-FAIL contract

The AI app-only time budget excludes provider network latency. The benchmark
contract is enforced as follows:
1. A mock `IAiProvider` implementation returns a canned response with
   configurable `SimulatedNetworkDelay = TimeSpan.Zero`.
2. The `Bench_AiAppOnlyTime` benchmark measures wall time from
   `IAiAdvisorService.GetRecommendationsAsync` call entry to result returned,
   with the mock provider at zero delay.
3. A contract test asserts that `IAiAdvisorService` does not call any HTTP
   client when the mock provider is active (verifying the budget measurement
   boundary).

### Reference hardware resolution (CON-1 / ADR-0020)

If Phase 00 left CON-1 open, this phase resolves it as:
- **Windows reference**: Intel Core i5 (10th gen or equivalent), 8 GB RAM,
  512 GB SATA SSD, 1920x1080 display, Windows 10 22H2, WebView2 installed.
- **macOS reference**: Apple M1, 8 GB unified memory, 256 GB NVMe SSD,
  2560x1600 Retina display, macOS 13 Ventura, WKWebView (built-in).

These specs are the minimum for which the NFR-OGMA budgets are guaranteed.
Higher-spec machines will meet them by wider margins. This resolution is
recorded in ADR-0020 and in `docs/benchmarks/REFERENCE-HARDWARE.md`.

### Structured logging contract

```
{
  "timestamp": "ISO-8601",
  "level": "Information|Warning|Error",
  "context": "LibraryCatalogue|IngestionPipeline|...",
  "correlationId": "UUID",
  "operation": "CatalogueLoad|ScanFile|...",
  "duration_ms": 142,
  "bookCount": 2000
}
```

Fields prohibited in log payloads: `filePath` (only hashed book ID allowed),
`queryText`, `annotationContent`, `aiPayload`. A Roslyn analyzer (introduced
here) warns on `LogInformation` calls that pass potentially-sensitive string
interpolations.

### LAN concurrency baseline

The `Bench_LanHost_Concurrency_20` benchmark:
1. Starts the LAN Host in-process with a real HTTP listener on loopback.
2. Spawns 20 `HttpClient` instances (simulating students), each performing:
   - Catalogue load (cold).
   - 5 search queries.
   - 10 page-render requests.
3. Measures per-client P95 latency. Pass criterion: P95 per client ≤ 1.5×
   single-client P95 (≤20% degradation factor under 20 clients).

### Cross-platform approach

- BenchmarkDotNet benchmark jobs run on both Windows and macOS CI runners;
  baseline JSON files are OS-tagged (`baseline-windows.json`,
  `baseline-macos.json`).
- Fault-injection tests are cross-platform; `IFaultInjector.SimulateProcessKill`
  uses `Environment.FailFast` (portable) rather than OS signals.
- The `IPerformanceMeter` uses `Stopwatch.GetTimestamp()` (high-resolution,
  cross-platform).
- The 3D FPS benchmark reads the `fps` field from the WebView JS bridge
  message bus (same bridge used by the app); the test verifies ≥60 FPS by
  sampling over a 5-second window after a 500-spine render.

---

## 7. Work breakdown (summary)

| WP | Work package | Estimate |
| --- | --- | --- |
| P20-WP1 | Reference hardware finalization + ADR-0020; benchmark runner configuration on Win + macOS CI | 1 d |
| P20-WP2 | `OgmaLibrary.Benchmarks` project: BenchmarkDotNet harness, perf corpora, all NFR-OGMA-001..006 benchmarks | 3 d |
| P20-WP3 | NFR-OGMA-007 SMART-FAIL benchmark + contract test; NFR-OGMA-008 abnormal-termination test | 1.5 d |
| P20-WP4 | 50,000-item scale benchmarks (NFR-PROD-003/004); synthetic 50,000-book corpus seeding | 2 d |
| P20-WP5 | Fault-injection framework + all seven reliability scenario tests (G7, G8, R1 paths) | 3 d |
| P20-WP6 | LAN Host concurrency benchmark (Phase 16 integration) | 1.5 d |
| P20-WP7 | `OgmaLibrary.Infrastructure.Diagnostics`: structured logging, IPerformanceMeter, log-payload analyzer | 2 d |
| P20-WP8 | Opt-in telemetry writer, Settings UI hook (consent gate CTRL-OGMA-025), developer diagnostics panel | 1.5 d |
| P20-WP9 | CI integration: baseline JSON persistence, regression-check PR gate, documentation | 1 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** `icons.md` contains a minimal manifest for
  the developer diagnostics panel (performance meters) and the telemetry
  consent toggle in Settings. Both require colorful icons — see `icons.md`.
- [x] **i18n (en/fr strings externalized):** The telemetry consent UI copy and
  structured-log-based user-facing status messages (AI degradation notice,
  job recovery notice) are externalized in `en` and `fr` in this phase.
  `es/it/de` keys are present (empty → pseudolocale-flagged) per I18N-STRATEGY.
- [x] **Accessibility (keyboard + SR):** The developer diagnostics panel and
  the telemetry opt-in control in Settings are keyboard-operable and have
  accessible names/roles. Automated axe-style check included in test run.
- [x] **Privacy/egress:** Structured logs explicitly forbid user-authored
  content (log-payload analyzer). Telemetry is device-local by default;
  opt-in before any data leaves the device (CTRL-OGMA-025). NFR-OGMA-007
  SMART-FAIL contract ensures AI provider call boundaries are tested.
- [x] **Reversibility:** Fault-injection tests verify every destructive
  operation (write-back, index rebuild, job state) is fully reversible;
  all R1 paths are re-validated.
- [x] **Performance budgets:** This phase is the primary performance gate.
  All NFR-OGMA-001..009 and NFR-PROD-003/004/005 budgets are measured and
  enforced as hard CI gates.
- [x] **Bounded-context tests:** The `OgmaLibrary.Benchmarks` project runs
  the existing architecture tests as a pre-benchmark smoke check; no new
  bounded-context violations are introduced.
- [x] **Documentation:** ADR-0020 (reference hardware), `docs/benchmarks/`
  baseline JSON files, and the performance budget table in
  `docs/benchmarks/BUDGETS.md` are committed and kept current.

---

## 9. Definition of Done

### Global DoD (Phase 20 slice)

- [ ] Every NFR-OGMA-001..009 ID has a passing BenchmarkDotNet test asserting
  the budget; CI fails the build on regression.
- [ ] NFR-PROD-003 and NFR-PROD-004 at 50,000 items pass on both CI runners.
- [ ] NFR-OGMA-007 SMART-FAIL: the benchmark measures app-only time with the
  mock provider; a contract test asserts provider latency is excluded.
- [ ] All seven reliability/fault-injection scenarios have passing tests; no
  open R1 defect.
- [ ] G7 (index rebuild) and G8 (interrupted job recovery) gates are green.
- [ ] LAN Host concurrency baseline is recorded in `docs/benchmarks/`.
- [ ] Structured logs emit no user-authored content; log-payload Roslyn
  analyzer is active and warns on violations.
- [ ] Telemetry is off by default; opt-in stores consent in Settings; no
  telemetry data leaves the device in any test path.
- [ ] ADR-0020 committed and owner-reviewed.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format --verify-no-changes`, `dotnet build` (warnings = errors),
  `dotnet test`, architecture tests all pass on Windows + macOS CI runners.
- [ ] New user strings (telemetry consent, AI-fallback notice) externalized
  in `en + fr`; pseudolocale check passes.
- [ ] `icons.md` complete; developer diagnostics and telemetry consent icons
  procured or placeholders flagged.
- [ ] `/code-review` completed; findings resolved.

### Phase-specific exit criteria

- Benchmark baseline JSON files for Windows and macOS are committed to
  `docs/benchmarks/` and the PR check is wired in CI.
- The `FaultInject_WriteBack_MidCrash` test is classified R1 and passes;
  a comment in the test references FR-META-005.
- The AI-fallback test (`FaultInject_AiProviderOutage_FallsBackToLocalSearch`)
  passes and produces a visible, localized status message in the UI.

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `full-stack-orchestration:performance-engineer` — structure the benchmark
  strategy and interpret results against the NFR budgets.
- `frontend-ux:frontend-performance` — page-turn and UI-responsiveness
  benchmarks (NFR-OGMA-005, NFR-PROD-005).
- `devops-cloud:reliability-engineering` — fault-injection framework design,
  resumable-job pattern, observability.
- `devops-cloud:observability-monitoring` — structured logging schema, telemetry
  architecture, IPerformanceMeter design.
- `sdlc-meta:advanced-testing-strategy` — fault-injection test design,
  golden-corpus integration, BenchmarkDotNet harness.
- `backend-databases:database-reliability` — write-back transactional guarantee
  tests, index rebuild from source.
- `superpowers:test-driven-development` — all fault-injection and benchmark
  tests written before implementation adjustments.
- `superpowers:verification-before-completion` — no "done" without running the
  full benchmark suite on both CI runners.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| `OgmaLibrary.Benchmarks` project | `src/OgmaLibrary.Benchmarks/` |
| `OgmaLibrary.Tests.FaultInjection` project | `tests/OgmaLibrary.Tests.FaultInjection/` |
| `OgmaLibrary.Infrastructure.Diagnostics` | `src/OgmaLibrary.Infrastructure/Diagnostics/` |
| BenchmarkDotNet baseline files | `docs/benchmarks/baseline-windows.json`, `docs/benchmarks/baseline-macos.json` |
| Performance budget table | `docs/benchmarks/BUDGETS.md` |
| Reference hardware doc | `docs/benchmarks/REFERENCE-HARDWARE.md` |
| ADR-0020 | `docs/adrs/ADR-0020.md` |
| Telemetry consent Settings UI | `src/OgmaLibrary.App/Views/Settings/TelemetrySettingsView.axaml` |
| Developer diagnostics panel | `src/OgmaLibrary.App/Views/Diagnostics/PerformanceDiagnosticsView.axaml` |
| Log-payload Roslyn analyzer | `src/OgmaLibrary.Analyzers/LogPayloadAnalyzer.cs` |
| CI benchmark regression check | `.github/workflows/benchmarks.yml` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Reference hardware not physically available to CI runners | R3 | Use GitHub Actions self-hosted runners on the two confirmed reference machines; document runner registration in `docs/benchmarks/RUNNER-SETUP.md`. Cloud runners are a fallback for PRs (trend data only); gates run on self-hosted runners nightly. |
| NFR-OGMA-002 catalogue load fails on SATA SSD reference hardware | R3 | Pre-warm the SQLite WAL on startup; use memory-mapped I/O; if budget is still missed, record as a tracked defect and escalate to owner — do not widen the budget without owner sign-off. |
| 3D FPS benchmark flaky on macOS WKWebView headless | R3 | Use the JS bridge FPS sampling with a 5-second window; retry up to 3 times before failing; if consistently flaky, gate on P50 rather than P95 for the headless case and add a manual verification step. |
| LAN concurrency benchmark requires real network stack | R5 | Use loopback HTTPS; the test is representative of LAN round-trips. Document that real LAN testing (with physical switches) is a manual verification step in Phase 23 beta soak. |
| Fault-injection tests introduce non-determinism | R5 | Use deterministic seed data; lock `IFaultInjector` to exact call-count triggers; run in a dedicated test project with `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. |

---

## 13. Owner asks

1. **Reference hardware confirmation (CON-1 / ADR-0020):** If not resolved in
   Phase 00, confirm the two reference machine specifications (Windows + macOS)
   so CI runner procurement can proceed. Deadline: before WP1 completes.
2. **Telemetry consent UX approval:** Review the opt-in telemetry consent
   dialog copy (in `en` and `fr`) and the data-minimization statement before
   the Settings UI is built. Deadline: before WP8.
3. **Icon procurement (see `icons.md`):** Approve and procure the colorful
   premium PNG icons for the developer diagnostics panel and the telemetry
   consent toggle in Settings.
4. **LAN concurrency budget sign-off:** Confirm the 20-client concurrency
   target and the ≤20% degradation tolerance as binding commitments for the
   Phase 16 LAN Host.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
