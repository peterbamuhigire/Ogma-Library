# Sept-23 Phase 04 completion: reader engine stability and page rendering

Status: **COMPLETE, with hand-offs** (2026-09-25). Rollback point: `c5100d3` (last Phase 03 commit).
Plan: [phase-04](../../plans/sept-23-kaizen/phases/phase-04-reader-engine-stability.md).
Defects: K30 (crash while reading), K32 (page-turn stalls), K33 (blurry pages).
Branch: `worktree-agent-af6897a4375e4e1a0` (not merged). The real-window proof used the prototype
UIA driver because the Phase 01 harness is not built yet. Phase 02 (global handlers, logging) was
being built at the same time, so this phase adds no logger and does not touch view code-behind.

## Commits

| Commit | Subject |
|---|---|
| `305e884` | feat(reader): add async renderer contract and DPI render width |
| `4c48755` | fix(reader): await page geometry and serialise session lifecycle |
| `45f2cd0` | fix(pdf): supervise reader worker sessions without a CPU cap |
| `cce0d7b` | fix(reader): render at device resolution and survive engine loss |
| `85fd86f` | perf(reader): warm neighbour page geometry during prefetch |

## Tasks

| Task | Result |
|---|---|
| T04.1 Reproduce | `PdfWorkerSession_LongReadingSession_IsNotKilledByOneShotCpuLimit`. It sets the one-shot CPU limit to 1 s and renders until the worker has used more than 2.5 s of CPU. **Before the fix it failed in 6 s** with `InvalidOperationException: The PDF worker session ended without a response` from `SendSynchronously ← GetPageRotationDegrees` (the K30 stack). It passes now. Measured CPU per page turn (rotation, geometry and a 1,440 px render): **≈ 365–390 ms**. At that rate the old 15 s cap is used up in about 40 renders, about 20 page turns once previews and prefetches are counted. |
| T04.2 Separate resource policies | `PdfWorkerOptions.Timeout`/`CpuTimeLimit` stay as the one-shot limits. The new `PdfWorkerOptions.Session` (`PdfWorkerSessionLimits`) has a 10 s per-request wall clock, a 30 s startup limit, a 10 min idle close and a budget of 3 respawns in 60 s. `WindowsChildProcessLimit.TryAssign(cpuTimeLimit: null)` leaves out `JobObjectLimitProcessTime` and keeps kill-on-close, active-process = 1 and the memory limit. Tests read the flags back through `QueryInformationJobObject` for both policies. A request that runs past its wall clock kills the worker (test). |
| T04.3 Correlated async protocol | Protocol v2: `RequestId` on every request and response, and `ProtocolVersion` in the ready line. One background read loop routes responses by id through `WorkerResponseRouter`, and late replies are counted and discarded (unit test with a delayed reply). `SendSynchronously` is removed. `IPdfRenderer` gains `…Async` geometry, rotation, metadata, outline and text-layer members. `ReaderSessionService` and `ReaderViewModel` await them. The architecture test `Architecture_ReaderPaths_DoNotBlockOnAsyncWork` bans `.GetAwaiter().GetResult()`, `.Wait()` and `SendSynchronously` in `OgmaLibrary.Reader` and in the App's reader view model and views. |
| T04.4 Prioritised scheduling | `PriorityRequestGate`: control requests first, then previews, then full renders. Cancelling a queued waiter removes it. Renders more than 2 pages from the target are cancelled. Geometry is cached per document and warmed for the neighbour pages. One geometry request queued behind 6 renders took **376 ms**, which is the time left on the render already running (the worker is single-threaded). The target was ≤ 100 ms; see hand-offs. |
| T04.5 Supervision and respawn | `ReaderSessionSupervisor` sits inside `IsolatedPdfRenderer`. It detects a lost worker (exit, broken pipe or timeout), starts a new one with the retained password copy, and replays the request once. After 3 respawns in 60 s it throws `PdfRendererUnavailableException`, and the reader shows a localised message (en/fr) whose action reopens the book at the current page. Recoveries are published as `ReaderEvent.EngineRecovered` (`reader.session.respawned`) for Phase 02's logger. Worker stderr is drained into a bounded buffer that is counted but not exported. |
| T04.6 DPI-aware render scale | Width = displayed width (DIP, including zoom) × `TopLevel.RenderScaling`. It is rounded up to a 128 px bucket and capped at 4,096 px and 16 MP (`ReaderRenderDefaults.ComputePageWidthPx`). The `ReaderRenderScaling` attached behaviour re-renders when `ScalingChanged` fires. Neighbours are prefetched at the same width. The page `Image` uses high-quality interpolation. |
| T04.7 Cancellation and disposal | Open and close are serialised. A racing-opens test leaves exactly one live renderer. Renderer teardown runs off the UI thread, and a session waits at most 2 s for its worker to exit. 50 open/close cycles leave **0** worker processes (test). |
| T04.8 Telemetry | Partial. `IPdfRendererHealth.GetHealthSnapshot()` reports state, respawns, idle closes, queue depth, discarded responses and stderr line count, and `EngineRecovered` events are published. Debug logging and the Activity Centre export wait for Phase 02's logging abstraction (see hand-offs). |
| Extra (found by the soak) | `PdfiumAdapter` parsed **every** page with PdfPig on the first geometry or render call. On the 900-page book that took longer than the 10 s request limit, so the worker was killed repeatedly. Geometry is now read per page on demand. PNG encoding took about 200 ms of each render; unfiltered zlib level 1 is about 40 % faster and gives smaller files (lossless). |

## Measured results

Machine: 4 cores, Windows 11. Isolated data directory, synthetic corpus. Class: MEASURED.

| Check | Before (audit, `0ad3c0c`) | After |
|---|---|---|
| Consecutive page turns in the real window | App terminated after about 20 turns (K30) | **470 turns on the 900-page book** (220 + 250, with resume in between) and 119 on the 120-page book (60 turns, then 59 more to the last page after resuming), with 0 exits and 0 `.NET Runtime` crash events for this build |
| Worst page-turn stall | 11.4 s (K32) | Invoke call (UI thread): p50 2.3 ms, p95 6.1 ms, max 21 ms. Page label updated: p50 14.6 ms, p95 57.6 ms; max 1,764 ms, which was the respawn turn ([csv](evidence/sept-23-kaizen/phase-04/real-window-900p-250-turns-kill-at-150.csv)) |
| First 220 turns, 900-page book | — | Page label p50 44.8 ms, p95 260 ms, max 373 ms. This run was before the geometry warm-up ([csv](evidence/sept-23-kaizen/phase-04/real-window-900p-220-turns.csv)) |
| Kill `OgmaLibrary.Workers` while reading (G8) | Reader stayed dead until the book was reopened | Next page shown after **1.40 s** (120-page book), **1.76 s** (900-page book) and **1.88 s** (first run). Integration test: 1.31 s |
| Sharpness at 100 % (Laplacian variance of the title text, 1600×1000 window) | 853 ([j4-reader](../../plans/sept-23-kaizen/evidence/screens/j4-reader.png)) | **2,605** ([after](evidence/sept-23-kaizen/phase-04/after-algorithms-p1-1600x1000.png)) |
| Soak through the reader session, 1,000 turns (`Category=Benchmark`) | — | 0 respawns, 0 discarded replies. p50 85 ms, p95 620 ms, max 2.4 s (next, random jump, 3 widths). Cache 38.6 MB, under the 256 MB budget. This run was before the PNG and geometry changes; the nightly run repeats it |
| Soak, 100 turns (fast suite) | — | p50 142 ms, p95 606 ms, 0 respawns |
| Fast suite | 1,159 passed | **1,190 passed** (Architecture 43, Tests 974, UI 173) |
| Restore (locked) / format / Release build / requirement accountability | green | green; build has 0 warnings |

Screenshots: [after 220 turns](evidence/sept-23-kaizen/phase-04/after-220-turns-1920x1080.png),
[after 470 turns and a worker kill](evidence/sept-23-kaizen/phase-04/after-470-turns-and-worker-kill-1920x1080.png),
[120-page book, page 1](evidence/sept-23-kaizen/phase-04/after-algorithms-p1-1600x1000.png).
The first real-window attempt stopped because another worktree's UIA `launch` action force-kills
every `OgmaLibrary.App` process. That kill was external, not a crash: no crash event was logged
for this build. The runs above used a driver that targets this build's process only.

## Hand-offs and NOT ASSESSED

| Item | Owner |
|---|---|
| Geometry ≤ 100 ms behind 6 queued prefetches was not met: 376 ms, bounded by the render already running in the single-threaded worker. Page turns avoid it through the geometry cache and warm-up. Meeting it needs a second worker or cancellable renders inside the worker | Phase 06 (worker throughput) |
| p95 ≤ 250 ms preview / ≤ 800 ms sharp on the reference machine; UI-thread block ≤ 50 ms needs a dispatcher-lag probe | Phase 24 (reference machine); Phase 01 harness |
| Debug logging of latency buckets, queue depth and respawns; Activity Centre diagnostics export; the "Reconnecting the reader…" indicator; an *Export diagnostics* action | Phase 02 (logger), Phase 12 (reader chrome) |
| `App.axaml.cs:197` blocks on `StopAsync(...).GetAwaiter().GetResult()` during shutdown. It is outside the reader architecture test | Phase 02 |
| Moving the window between 100 % and 150 % monitors | NOT ASSESSED (single monitor); Engineering |
| macOS session limits (no Job Objects) | NOT ASSESSED; Phase 27 |
| Removing the CPU cap must be reviewed in the threat model (the compensating controls are the wall clock, memory cap, idle close and respawn budget) | Phase 23 |
| Sync `IPdfRenderer` members remain for background ingestion callers (metadata, ISBN, OCR, search extraction) and block a pool thread | Phase 06 |
| Truncated or 0-byte file opened in the reader, and a file deleted while open (respawn then fails and shows the reopen error), have no E2E test yet | Phase 01 (G6) |
