# Phase 20 — Skills & Slash Commands

> Phase-scoped detail. The bird's-eye map is `SKILLS-INDEX.md`. Convention:
> every skill entry states the task it informs and the artifact it produces.

---

## Always-on (every phase)

| Skill / command | Task | Artifact |
| --- | --- | --- |
| `superpowers:writing-plans` → `superpowers:executing-plans` | Before WP1 | Execution plan breaking WPs into daily tasks |
| `superpowers:test-driven-development` | All WPs | Tests written before each benchmark/fault-injection implementation |
| `superpowers:verification-before-completion` | End of each WP | Verification checklist confirming CI gate is green before WP is closed |
| `superpowers:systematic-debugging` | Any benchmark regression | Root-cause analysis before any budget widen proposal |
| `superpowers:requesting-code-review` + `/code-review` | End of phase | Code review on `OgmaLibrary.Benchmarks`, fault-injection framework, diagnostics infrastructure |
| `superpowers:using-git-worktrees` | WP1–WP9 | `feature/P20-performance-benchmarks` and `feature/P20-fault-injection` worktrees |

---

## Phase-specific skills

### WP1 — Reference hardware & CI runners

**`documentation-generation:architecture-decision-records`**
- Task: P20-WP1-T1
- Produce: `docs/adrs/ADR-0020.md` — reference hardware specification, rationale
  for the chosen machine specs, and the implications for NFR-OGMA budget anchoring.
- Invocation: Use the ADR template from `docs/adrs/ADR-0000-template.md`; fill
  Context (CON-1 gap from Phase 00), Decision (the two machine specs), and
  Consequences (benchmark runners must match; any future hardware change requires
  a new ADR amendment and baseline re-run).

---

### WP2–WP3 — NFR-OGMA benchmarks

**`full-stack-orchestration:performance-engineer`**
- Tasks: P20-WP2-T1 through P20-WP3-T2
- Produce: `OgmaLibrary.Benchmarks` project with all BenchmarkDotNet jobs;
  `docs/benchmarks/BUDGETS.md` with current measured values vs. NFR budgets.
- Invocation: Use this skill to structure the benchmark job hierarchy (Cold,
  Warm, Stress tiers), select the appropriate BenchmarkDotNet `Job` configurations
  (ShortRun for PRs, MediumRun for nightly), and interpret P95 percentile
  measurement methodology.

**`frontend-ux:frontend-performance`**
- Tasks: P20-WP2-T7 (page-turn), P20-WP2-T8 (3D FPS)
- Produce: `Bench_PageTurn_Cached` and `E2e_3dFps_500` benchmark implementations.
- Invocation: Consult this skill for render pipeline profiling — specifically how
  to measure frame delivery time in a WebView (JS `requestAnimationFrame` →
  bridge message → C# timestamp delta) and how to warm the PDFium render cache
  deterministically before sampling.

---

### WP4 — 50,000-item scale

**`full-stack-orchestration:performance-engineer`** (continued)
- Tasks: P20-WP4-T1 through P20-WP4-T5
- Produce: 50,000-book perf corpus seeder and the five scale benchmarks.
- Invocation: Use this skill to design the `PerfCorpusSeeder` so it produces
  a realistic metadata distribution (title length, author count, shelf membership)
  that exercises the FTS5 tokenizer and the embedding ANN index at realistic
  cardinality.

**`backend-databases:database-reliability`**
- Tasks: P20-WP4-T4, P20-WP4-T5
- Produce: FTS5 and semantic search benchmarks at scale; SQLite page-cache
  tuning recommendations (`PRAGMA cache_size`, WAL mode, mmap_size).
- Invocation: Use this skill to review the SQLite configuration for the
  50,000-item corpus and recommend WAL + mmap settings that keep the
  full-text search budget within 500 ms on SATA SSD.

---

### WP5 — Fault-injection framework

**`devops-cloud:reliability-engineering`**
- Tasks: P20-WP5-T1 through P20-WP5-T7
- Produce: `IFaultInjector` abstraction, `InMemoryFaultInjector`, and all seven
  reliability scenario tests.
- Invocation: Use this skill to design the fault-injection framework architecture
  (deterministic, zero-production-overhead, DEBUG-only guard pattern) and to
  verify that each test exercises a real failure mode (not a mock of a mock).

**`sdlc-meta:advanced-testing-strategy`**
- Tasks: P20-WP5-T2 through P20-WP5-T7
- Produce: Test classification table (R1/R2/R3/R4) for each fault-injection
  test; oracle definitions (what "pass" means for each scenario).
- Invocation: Use this skill before writing each fault test to define: (1) the
  exact failure injection point, (2) the observable postcondition (oracle),
  (3) the classification (R1 data-loss, R4 recoverability, etc.), and
  (4) how to make the test deterministic.

---

### WP6 — LAN concurrency

**`devops-cloud:reliability-engineering`** (continued)
- Tasks: P20-WP6-T1, P20-WP6-T2
- Produce: `Bench_LanHost_Concurrency_20` and the LAN baseline JSON.
- Invocation: Use this skill to design the concurrency test harness: `HttpClient`
  worker coordination, request interleaving pattern, P95 measurement with
  concurrent request streams, and the 1.5× degradation tolerance rationale.

---

### WP7 — Structured logging & analyzer

**`devops-cloud:observability-monitoring`**
- Tasks: P20-WP7-T1 through P20-WP7-T4
- Produce: `IPerformanceMeter`, `StopwatchPerformanceMeter`, JSON log schema,
  `LoggingDiagnosticsExtensions`.
- Invocation: Use this skill to define the structured log schema (field names,
  cardinality, data types) and the log verbosity strategy (Information for
  normal operations, Warning for budget misses, Error for fault-injection
  recovery events).

**`security-scanning:security-sast`**
- Tasks: P20-WP7-T3
- Produce: `LogPayloadAnalyzer` Roslyn analyzer source.
- Invocation: Use this skill to review the analyzer implementation for
  false-negative cases (e.g., multi-step string builds that evade simple
  interpolation detection) and to confirm the analyzer is active in the
  warnings-as-errors build pipeline.

---

### WP8 — Telemetry UI

**`frontend-ux:practical-ui-design`**
- Tasks: P20-WP8-T2, P20-WP8-T3
- Produce: `TelemetrySettingsView.axaml` and `PerformanceDiagnosticsView.axaml`.
- Invocation: Use this skill to ensure the telemetry consent toggle and the
  developer diagnostics panel follow the Phase 03 design-token system (spacing,
  typography, color) and are consistent with the Settings surface established
  in earlier phases.

---

### WP9 — CI integration

**`devops-cloud:deployment-release-engineering`**
- Tasks: P20-WP9-T1
- Produce: `.github/workflows/benchmarks.yml`.
- Invocation: Use this skill to design the two-tier benchmark CI strategy
  (nightly self-hosted for hard gates, PR cloud-runner for trend checks) and
  to ensure the artifact upload/download between jobs is reliable and
  deterministic.

**`documentation-generation:changelog-automation`**
- Tasks: P20-WP9-T2
- Produce: `docs/benchmarks/BUDGETS.md` with the complete budget table and a
  generated changelog entry for Phase 20 in `CHANGELOG.md`.
- Invocation: Use this skill to generate the `CHANGELOG.md` entry for Phase 20
  that summarizes all performance gate additions and reliability test additions.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` (medium effort) | End of WP2, WP5, WP7 | Review benchmark harness correctness, fault-injection determinism, log-payload analyzer |
| `/code-review --fix` | After review findings | Apply mechanical fixes (formatting, naming) |
| `/security-review` | WP7-T3 | Confirm the log-payload analyzer covers all PII leak vectors |
| `/verify` | After WP2-T9, WP5-T7, WP8-T2 | Run the benchmark suite and fault-injection suite on both platforms; confirm telemetry opt-in behavior |
| `/run` | WP8-T3 | Launch the app in debug mode; open developer diagnostics panel; confirm live meter readings update |
| `superpowers:finishing-a-development-branch` | Phase gate | Decide merge/PR/cleanup strategy before closing Phase 20 |
