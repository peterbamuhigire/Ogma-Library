# Benchmark Baseline — Phase 02

This records the performance-instrumentation baseline at the end of Phase 02.
Values measured on the developer box are **trend data only** — they are not gated
to the reference hardware (W-REF-01 / M-REF-01) until Phase 20, per Test Strategy
§4.3 and the reference-hardware context gap (CON-1).

## Instrumentation in place

- `IBenchmarkContext` (`OgmaLibrary.Application`) — the wall-clock timing contract
  injected into services so CI benchmarks can assert NFR-OGMA budgets without
  service code referencing `Stopwatch` directly.
- `StopwatchBenchmarkContext` (`OgmaLibrary.Infrastructure`) — the only place
  permitted to reference `System.Diagnostics.Stopwatch`.

## Phase 02 trend measurements (dev box)

| Metric | Value (dev box) | Budget (reference HW) | Status |
| --- | --- | --- | --- |
| Skeleton headless render (3 UI tests, incl. 2 Skia frame captures) | ~0.8–1.0 s total | n/a | informational |
| Full solution build (Release, warm) | a few seconds | n/a | informational |
| Cold-start to interactive window | not yet meaningful (no catalogue) | ≤ 3 s P95 (NFR-OGMA-001) | deferred to Phase 04+/20 |

Cold-start, catalogue-load, search, and page-turn budgets become measurable once
the catalogue (Phase 04), reader (Phase 08), and search (Phase 10) exist. The
Phase 01 spikes already produced early trend evidence for two budgets:

| Budget | Phase 01 spike result (dev box) |
| --- | --- |
| NFR-OGMA-004 full-text ≤ 500 ms P95 | **1.97 ms** P95 (FTS5, 252× headroom) — `spikes/s05-fts5` |
| NFR-OGMA-005 page-turn ≤ 100 ms cached | render P95 124–157 ms cold (cache makes turns fast) — `spikes/s02-pdfium` |

## Next

Phase 20 fixes the reference machines and converts these trend benchmarks into
hard CI gates; Phase 04+ adds the catalogue so cold-start and catalogue-load can
be measured against a 2,000-book corpus.
