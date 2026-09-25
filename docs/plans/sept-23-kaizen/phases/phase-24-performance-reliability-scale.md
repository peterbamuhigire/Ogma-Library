# Phase 24 — Performance, reliability and scale

## 1. Header

| Field | Value |
|---|---|
| Wave | F. Quality attributes |
| Size | 6–9 engineering days, plus soak wall-clock time |
| Depends on | 06 (pipeline and jobs), 13 (unified search); benefits from 04 (reader engine) |
| Owner decisions | Reference-machine specification (section 3); no D-register item |
| Primary defects | K71, K26, K04, K32 (regression guard), K25 (retry storms at scale) |
| Requirements | NFR-PROD-001..014 (performance and reliability), NFR-OGMA reliability set, LIB-005/006, SEARCH-001..006 budgets |

## 2. Why this phase exists

Phases 02, 04 and 06 fix the defects that make Ogma crash or stall. This phase proves the product
stays fast and dependable at the library sizes the SRS promises, on a defined machine, over long
sessions. The baselines measured on 25 September 2026:

| Signal | Baseline | Source |
|---|---|---|
| Cold start to window (unloaded) | 3.7–4.1 s | three launches |
| Cold start under concurrent load | 23.9 s | first launch while the test suite ran |
| Single page-turn invoke, worst case | **11.4 s**, then a crash after about 20 turns | reader stress run (K30, K32) |
| Scan of 17 files (11 MB) to catalogue rows | about 15 s | Kaizen run |
| Jobs for 17 books | 102 jobs, 300 attempts, 26 failed | Activity Centre and DB (K25) |
| Graceful shutdown | 3.75 s, no orphaned workers | close test |
| Working set after load | 182–252 MB | process counters |
| Core test suite duration | **18 min**, the longest test 116 s | TRX (K04) |

The 18-minute suite exists because 50k-scale benchmarks such as
`PerfBenchmark_MetadataSearch_P95_LessThan150ms` and
`DiscoveryService_EnumeratesFiftyThousandFilesWithBoundedChannel` are plain `[Fact]` tests. Only 5
tests carry `Trait("Category", "Benchmark")`, and `.github/workflows/ci.yml:97` runs the whole
solution on every push. Benchmark results on shared CI runners are also noisy, so they neither gate
reliably nor run fast.

Startup database work (migration, identity backfill, job recovery, thumbnail repair) most likely
runs on the UI thread, because Microsoft.Data.Sqlite async calls complete synchronously (journeys
audit X5, `ApplicationStartupCoordinator.cs:36-51`). Exit blocks the UI thread on
`ApplicationStartup.StopAsync(...).GetAwaiter().GetResult()` with no timeout
(`App.axaml.cs:180-206`).

## 3. Objectives and exit criteria

**Reference machines.** Define and record them in `docs/performance/reference-machines.md`:

- **R-Win-Low:** 4-core x64 CPU, 8 GB RAM, SATA SSD, 1920×1080 at 100 %, Windows 11 23H2 or later.
- **R-Win-Std:** 8-core x64 CPU, 16 GB RAM, NVMe, 2560×1440 at 125–150 %.
- **R-Mac:** Apple silicon, 8 GB RAM (measured in Phase 27).

**Budgets.** These are defaults the owner confirms; they are measured on R-Win-Low unless noted.

| Budget | Target |
|---|---|
| Cold start to interactive catalogue (2k books) | ≤ 3.0 s p50, ≤ 5.0 s p95 |
| First page render after *Read* | ≤ 800 ms p95 for a 120-page text PDF |
| Page turn to sharp page | ≤ 150 ms p95 with prefetch hit; ≤ 600 ms p95 with a miss; the UI thread is never blocked over 50 ms |
| Scan and registration | ≥ 1,000 PDFs discovered and registered in ≤ 60 s (warm disk) |
| Full processing throughput | ≥ 250 PDFs/hour to covers, metadata and FTS on R-Win-Low (target set with Phase 06 data) |
| Search p95 (metadata, FTS, fuzzy) | ≤ 150 ms at 2k books; ≤ 500 ms at 50k books |
| Catalogue page switch | ≤ 200 ms at 50k books |
| Memory | ≤ 400 MB working set with a 2k library and one open book; no growth over 5 % per hour in soak |
| Graceful shutdown | ≤ 5 s worst case, bounded by timeouts |

**Exit criteria.**

1. All budgets are measured on R-Win-Low (and R-Win-Std where noted) and pass, or have an
   owner-accepted exception with a follow-up.
2. The default PR test suite finishes in ≤ 6 min. Benchmarks and soak tests run in a nightly
   workflow with stored history.
3. Startup database work runs off the UI thread. The loading shell repaints during a migration of a
   50k-book database (verified by frame timing).
4. Shutdown completes within 5 s even with an OCR job and a cover render in flight. No job loses a
   retry attempt because of a normal shutdown.
5. An 8-hour soak (continuous reading plus a rescan every 30 min on a 2k library) completes with no
   crash, no leak beyond budget and no stuck jobs.
6. Crash-recovery drills pass: kill the app during scan, render, OCR and migration; the next launch
   recovers within 30 s without manual steps.
7. *Export diagnostics* produces a local, redacted bundle (logs, settings without secrets, job
   summary, versions). There is no telemetry.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\reliability-engineering\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\observability-monitoring\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\frontend-performance\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\backend-databases\database-reliability\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md` (UI-thread rules, virtualisation)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md` (flake policy, test layering)
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\cicd-pipelines\SKILL.md` (nightly workflow)
- `C:\wamp64\www\windows-admin-engine-skills\skills\observability-performance-and-troubleshooting\windows-troubleshooting\SKILL.md` (Windows counters, event logs, crash dumps)

## 5. Scope

**In:** reference-machine definition; budget suite; test-suite split; startup and shutdown
threading; soak and crash drills; diagnostics export; regressions found by the measurements.

**Out:** algorithmic redesign of search (Phase 13) or of the pipeline copy-and-hash model (Phase 06),
unless measurement proves a budget cannot be met otherwise; macOS measurement (Phase 27); GPU and 3D
budgets (Phase 18).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 24.1 | Write `docs/performance/reference-machines.md` and the budget table. Obtain the owner's confirmation of the budgets. | `docs/performance/` | Owner-confirmed budgets recorded |
| 24.2 | Split the suites: tag every benchmark and 50k-scale test `[Trait("Category","Benchmark")]` or `"Soak"`; the PR workflow runs `--filter "Category!=Benchmark&Category!=Soak"`; add `.github/workflows/nightly-performance.yml` that runs the benchmarks on a fixed runner and uploads the results. | `tests/OgmaLibrary.Tests/**`, `.github/workflows/ci.yml` | PR suite ≤ 6 min on the local machine; nightly job green |
| 24.3 | Budget harness: a `scripts/Measure-OgmaBudgets.ps1` that uses the Phase 01 real-window harness and synthetic corpora (2k and 50k generated with the Kaizen corpus generator) to measure start, first render, page turn, scan, search and memory. Output JSON plus a Markdown summary. | `scripts/`, Phase 01 harness | The same command reproduces the table on any reference machine |
| 24.4 | Move startup database work off the UI thread: run the coordinator on a background thread and marshal progress to the dispatcher; confirm with a frame-time probe. | `src/OgmaLibrary.App/App.axaml.cs`, `ApplicationStartupCoordinator`, `StartupShellViewModel` | Migration of a 50k DB: loading shell repaints at ≥ 10 fps |
| 24.5 | Bounded shutdown: a `Closing` handler that flushes reader position and reading memory; `StopAsync` with a linked 4 s timeout; link the synchronous cover-render CTS to the stopping token; treat shutdown cancellation as "requeue without penalty", not as a failure. | `App.axaml.cs`, `BookIngestionWorker`, `PdfWorkerClient.cs:368-372, 419-429` | Shutdown ≤ 5 s under load; `RetryCount` unchanged across 5 shutdowns |
| 24.6 | Lease recovery after a crash: recover jobs whose lease owner PID no longer exists immediately, not after 5 minutes (`JobRecoveryService.cs:57-59`). | Job runtime | Kill during processing, relaunch: processing resumes ≤ 30 s |
| 24.7 | Page-turn regression guard: an automated 1,000-turn run on the 120-page and 900-page fixtures that asserts no UI-thread block over 50 ms and no worker death. | Phase 01 harness, reader | Guard runs nightly and passes |
| 24.8 | Soak: 8 h on R-Win-Low with a 2k library; record working set, handles, threads, worker count, job states and log errors every 5 min. | `scripts/Invoke-OgmaSoak.ps1` | No crash; memory growth ≤ 5 %/h; zero stuck jobs |
| 24.9 | Crash drills: kill during scan, render, OCR, migration and settings save; power-loss simulation with a torn `library-settings.json` (K28). | Scripted drills | Each drill recovers automatically; results table in evidence |
| 24.10 | Diagnostics export: extend the Activity Centre *Export diagnostics* into a zipped bundle with the Phase 02 logs, redacted settings, job summary and versions. | App, Infrastructure | Bundle contains no secrets, titles or paths beyond policy (reviewed with Phase 23) |
| 24.11 | Fix what measurement finds; each fix is its own slice with a before/after number. | As found | Budget table all green or owner-excepted |
| 24.12 | Catalogue scale check: confirm the full in-memory reload (`CatalogueViewModel.LoadAsync`) meets the 50k page-switch budget; if not, page from the read model. | `CatalogueViewModel.cs` | 50k budget met |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| 18-min PR suite (K04) | Benchmarks untagged; whole solution on every push | Trait split plus nightly job | Fast feedback without losing perf history | 18 min → ≤ 6 min | CI timings | Benchmarks rot unobserved | Nightly failure opens an issue; weekly review |
| Page-turn stall (K32) | Sync IPC on the UI thread | Phase 04 fix plus 1,000-turn guard | Stalls cannot return unnoticed | 11.4 s worst → ≤ 50 ms UI block | Guard output | Flaky timing on CI | Guard runs on a fixed machine |
| UI freeze during migration (K71) | SQLite async runs synchronously on the UI thread | Background coordinator | Loading shell stays live | Unmeasured → ≥ 10 fps | Frame probe | Startup races | Retain the ordered task list; integration test |
| Shutdown burns retries | Cancellation mapped to failure | Requeue-without-penalty | Jobs survive normal exits | Unmeasured → 0 lost attempts | DB check | Genuine timeouts hidden | Only the stopping token triggers requeue |
| No long-run evidence | No soak or drills existed | Soak and crash drills | Leaks and stuck jobs surface before users do | None → 8 h clean | Soak log | Machine time | Run overnight on R-Win-Low |

## 8. Test plan

- **Unit:** requeue-on-shutdown semantics; lease recovery by dead PID; timeout linking.
- **Integration:** migration on 50k synthetic DB off the UI thread; diagnostics bundle redaction.
- **Real window:** budget harness journeys; 1,000-turn reader guard; shutdown under load.
- **Nightly:** benchmarks, the 50k scale tests, the soak-lite (1 h) variant.
- **Negative:** disk full during scan; DB locked by an external reader; a 900-page PDF with huge images; clock change during soak.

## 9. Acceptance commands

```powershell
dotnet test OgmaLibrary.sln --configuration Release --no-build -m:1 --filter "Category!=Benchmark&Category!=Soak"
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "Category=Benchmark"
./scripts/Measure-OgmaBudgets.ps1 -Machine R-Win-Low -Library 2k
./scripts/Measure-OgmaBudgets.ps1 -Machine R-Win-Low -Library 50k
./scripts/Invoke-OgmaSoak.ps1 -Hours 8
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| Physical R-Win-Low machine | Owner provides or approves purchase | Budgets measured only on the development machine; recorded as MEASURED-dev, not reference |
| macOS budgets | Phase 27 | macOS release blocked |
| Real (non-synthetic) 2k library | Owner, using their own collection locally; no content leaves the machine | Throughput for real-world PDFs unverified |

## 11. Risks and mitigations

- **Budgets fail on low-end hardware.** Adjust prefetch depth and render resolution adaptively; get
  the owner to accept a revised budget rather than silently relaxing it.
- **Nightly runner variance.** Compare against the rolling median; alert only on sustained regression.
- **Soak finds rare crashes late.** Start a 1 h soak-lite as soon as Phase 04 closes.

## 12. Execution prompt

```
## Prompt 24 - Performance, reliability and scale
You are proving and improving performance and reliability in C:\wamp64\www\Ogma-Library.
Read first, in order: C:\wamp64\www\Ogma-Library\CLAUDE.md; docs/plans/sept-23-kaizen/README.md;
docs/plans/sept-23-kaizen/AGENT_BRIEF.md; docs/plans/sept-23-kaizen/03-defect-register.md (K04, K26, K32, K71);
docs/plans/sept-23-kaizen/phases/phase-24-performance-reliability-scale.md.
Read the SKILL.md files in section 4 directly.
A. Serial: 24.1 budgets (owner confirmation) -> 24.2 suite split -> 24.3 budget harness.
B. Independent after 24.3: 24.4 startup, 24.5 shutdown, 24.6 leases, 24.7 reader guard, 24.10 diagnostics.
C. Long-running: 24.8 soak and 24.9 drills on R-Win-Low (or record MEASURED-dev with NOT ASSESSED reference).
Every change carries a before/after number from the harness. Do not relax a budget without the owner's written
acceptance. Record completion at docs/implementation/execution/phase-sept23-24-completion.md.
```
