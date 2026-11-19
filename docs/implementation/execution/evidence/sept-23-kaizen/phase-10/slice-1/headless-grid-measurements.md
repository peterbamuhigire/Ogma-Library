# Phase 10 slice 1: headless grid measurements (2026-11-19)

Source: `tests/OgmaLibrary.Tests.Ui/Sept23Phase10GridTests.cs`, Avalonia headless, Release,
1280x800 window, synthetic in-memory projections (no PDFs, no private content).

| Measure | Result |
|---|---|
| Containers realised with 2,000 books (grid) | 42 (budget < 200); before this slice, all 2,000 |
| Scroll through 2,000 books in steps of 1/3 viewport (386 steps), layout + dispatcher per step | median 3.1-4.7 ms; p95 86-161 ms; max 181-417 ms (across 4 runs) |
| Same, before the template-reuse fix (containers cleared on recycle) | median not taken; p95 183 ms; mean 92 ms |

The benchmark is `Category=Benchmark` (excluded from the fast suite) and asserts the median
step is under 50 ms. Headless figures are a proxy: they include headless text measurement and
GC pauses and are not frame times.

Real-window frame times on the reference hardware: **NOT_ASSESSED**. The G2 journey records no
frame timings and no 2,000-book real-window journey exists yet (the corpus script now takes
`--count N` for one).
