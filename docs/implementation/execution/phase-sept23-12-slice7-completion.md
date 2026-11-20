# Sept-23 Phase 12, slice 7 completion record

Status: slice 7 of 8 IMPLEMENTED; Phase 12 stays open.

Scope: reader journeys in the real-window harness (`Journeys/ReaderJourneyTests.cs`, Trait `Tag=Reader`), harness arithmetic with unit tests (`Harness/ReaderMetrics.cs`, `HarnessTests/ReaderMetricsTests.cs`: nearest-rank percentile, page-area share, Laplacian variance), a `Reader.Citation.Capture` AutomationId, and a `Shell.TurnPage` fix.

Decisions applied: **D-94** (150 % and 200 % sharpness NOT_ASSESSED, owner Phase 24), **D-95** (soak is J-READ-1; turns set by `OGMA_E2E_READ_PAGES`, default 200).

Measured at 1280x800, Light, synthetic corpus, under the desktop lock (runs `20261120-091753`, `-092843`, `-093336`):
- J-READ-1: 200 of 200 turns, p50 44-46 ms, **p95 92 ms and 105 ms** in two runs, budget 250 ms. **K32 is resolved on the p95 budget.** The longest turn was 989 ms and 8,954 ms (new defect K138).
- J-READ-2 (find "caravan", jump, history back), J-READ-3 (wrong then right password), J-READ-6 (page 137 resumes after relaunch), J-READ-N2 (cancelled prompt): PASS.
- J-READ-5: all five formats copy with their marker (title, `@`, `TY  -`, `"title"`, title) after a real mouse drag selection: PASS. The author is not asserted (K137).
- Page area: viewport is **88.2 %** of the client area with the side panel closed, **65.9 %** with it open (it opens by default). The 85 % oracle is asserted with the panel closed and both values are recorded.
- Sharpness at 100 %: Laplacian variance 529-530 on the sparse title page. The floor (400) is a regression guard only; the plan's 2,605 figure used a different page and method.

Premises corrected: `Shell.TurnPage` required the page to be exactly `before+1`, so a late-applied retry press made it report a stall at page 17 while the reader was on page 22; it now accepts any forward move. J-READ-4 split view is NOT_ASSESSED because the button is unreachable while reading (K136). J-READ-N1 (truncated PDF) is NOT_ASSESSED because the catalogue does not list it; the refusal is covered by G6.

NOT_ASSESSED: 1920x1080 run, Dark theme, RTL, 200 % text, 150 %/200 % display scaling, macOS, screen reader, the advanced-testing skill (not found through the router).
