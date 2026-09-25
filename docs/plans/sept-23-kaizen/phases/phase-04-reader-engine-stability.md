# Phase 04: Reader engine stability and page rendering

## 1. Header

| Field | Value |
|---|---|
| Wave | A: Stabilise |
| Size | 5–7 engineering days |
| Depends on | Phase 02 (safety net, logging); Phase 01 for the G3 soak |
| Owner decisions | none |
| Primary defects | K30 (crash while reading), K32 (UI stalls on page turns), K33 (blurry pages) |
| Requirement IDs | READ-001 (resume), READ-002 (navigation), READ-003 (zoom), NFR-OGMA-005 (page-turn budget), CTRL PDF containment (must stay intact) |

## 2. Why this phase exists

Reading is the product's core job, and it currently crashes. In the audit the reader opened a
120-page book and rendered page 1, but after about 20 *Next page* clicks the application
terminated. The captured stack (K30) shows the chain:

`ReaderView.NextButton_Click` (`async void`, `Views/Reader/ReaderView.axaml.cs:117`)
→ `ReaderViewModel.NavigateToAsync` → `ReaderSessionService.NavigateToAsync`
(`OgmaLibrary.Reader/Session/ReaderSessionService.cs:164-183`) → `GetPageRotationDegrees`
→ `PdfWorkerSession.SendSynchronously` (`OgmaLibrary.Infrastructure/Pdf/PdfWorkerClient.cs:1011-1022`,
`_requestGate.Wait()` on the UI thread) → `SendAsync` (line 1030) →
`IOException: The pipe is being closed`.

Root causes:
1. **The reader's long-lived worker is killed by a limit meant for one-shot jobs.** The Job
   Object sets `JobObjectLimitProcessTime` with `PerProcessUserTimeLimit` =
   `PdfWorkerClientOptions.CpuTimeLimit` (15 s, `PdfWorkerClient.cs:1138`;
   `WindowsChildProcessLimit.cs:20-50`). A reading session renders every page twice (preview
   and full) and prefetches neighbours, so it uses up 15 s of CPU after a few dozen pages.
   Windows then kills the worker.
2. **Synchronous IPC on the UI thread.** Rotation and geometry are fetched through
   `SendSynchronously`, queued behind up to six prefetch renders. One page turn measured
   **11.4 s** (K32).
3. **The protocol can desynchronise.** `SendAsync` applies `WaitAsync(Timeout)` to
   `ReadResponseAsync()` (line 1033). After a timeout, the pending `ReadLineAsync` still
   consumes the next reply, so the next request reads the previous request's response.
4. **No recovery.** When the worker dies, the session is dead until the book is reopened.
5. **Fixed raster width.** Pages render at `ReaderRenderDefaults.PageWidthPx`, whatever the
   zoom or monitor scaling, so text is blurry at 100 % on a 2560×1440 display (K33).

## 3. Objectives and exit criteria

1. **Soak:** 1,000 consecutive page turns (next, previous, jump, zoom) on the 900-page corpus
   book with zero process exits, zero unrecoverable sessions and zero error toasts.
2. **Responsiveness:** page-turn p95 ≤ 250 ms to a preview and ≤ 800 ms to a sharp page on the
   reference machine; the UI thread is never blocked for more than 50 ms by reader IPC
   (measured with a dispatcher-lag probe).
3. **Recovery:** if the worker is killed externally, the next page turn transparently respawns
   the session within 2 s and shows the same page. The user sees at most a subtle "Reconnecting
   the reader…" indicator.
4. **Sharpness:** rendered raster width = displayed width × zoom × monitor render scaling,
   capped at a memory budget. Text edges pass a sharpness check (Laplacian variance ≥ the
   threshold measured on a reference render).
5. PDF containment stays intact: memory limit, active-process limit, kill-on-close, no network
   and no child processes still apply to reader sessions.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`: UI-thread rules and bitmap handling.
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\reliability-engineering\SKILL.md`: supervision, retry and backoff, failure-mode tables.
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\frontend-performance\SKILL.md`: latency budgets and measurement.
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\systematic-bug-diagnosis\SKILL.md`: reproduce K30 deterministically first.
- `C:\wamp64\www\chwezi-dev-engine\skills\security\vibe-security-skill\SKILL.md`: keep the containment controls intact while changing limits.

## 5. Scope in / scope out

**In:** reader-session resource policy, async IPC with request correlation, session
supervision and respawn, cancellation of stale renders, the DPI-aware render scale, preview
vs full render policy, soak and latency tests.

**Out:** reader UI and chrome (Phase 12), password unlock UI (Phase 12), split-view session
isolation (Phase 12), and ingestion-side worker throughput (Phase 06).

## 6. Work breakdown

**T04.1: Reproduce deterministically.** Add an integration test in `tests/OgmaLibrary.Tests`
(category `Integration`) that opens a `PdfWorkerSession` on a 900-page generated PDF and
requests 300 renders with rotation and geometry calls. On `0ad3c0c` it must fail with the
pipe-closed error. Record the CPU time consumed per render, so the limit design is based on data.
*Acceptance:* the failure reproduces in ≤ 3 minutes.

**T04.2: Separate resource policies.** Split `PdfWorkerClientOptions` into
`OneShotLimits` (the current 15 s CPU and 15 s wall clock, for ingestion jobs) and
`SessionLimits` (memory limit unchanged; **no cumulative process CPU limit**; per-request wall
clock of 10 s; an idle timeout of 10 minutes that closes the worker). In
`WindowsChildProcessLimit.TryAssign`, allow `cpuTimeLimit = null`, which omits
`JobObjectLimitProcessTime` but keeps `KillOnJobClose`, `ActiveProcess` and `ProcessMemory`.
A per-request watchdog in the client enforces runaway protection: if one request exceeds its
wall clock, kill and respawn the worker.
*Acceptance:* T04.1 passes; a unit test proves the session Job Object still has the memory,
active-process and kill-on-close flags.

**T04.3: Correlated async protocol.** Add `RequestId` (a monotonically increasing long) to
`ServerRequest` and to the worker's response (in `OgmaLibrary.Workers/Pdf/PdfWorkerCommand.cs`
and the session server). Replace the single `ReadResponseAsync` per call with one background
reader loop per session that routes responses to `TaskCompletionSource`s keyed by id. Discard
late responses for cancelled or timed-out requests. Remove `SendSynchronously` and make
`GetPageRotationDegrees`, `GetPageGeometry`, metadata, outline and text-layer calls
`Task`-returning through `IPdfRenderer`. Update `ReaderSessionService` (lines 119 and 176) and
`ReaderViewModel.ApplyPageGeometry` (line ~1723) to await them.
*Acceptance:* a test injects a delayed response; the next request still gets its own response;
no call site blocks (architecture test: no `.GetAwaiter().GetResult()` or `.Wait()` in
`OgmaLibrary.Reader` or `OgmaLibrary.App`).

**T04.4: Prioritised scheduling.** Give the visible page priority over prefetch. When the user
turns a page, cancel queued prefetches that are no longer within ±2 of the target, and send
geometry and rotation requests ahead of render requests. Cache geometry and rotation per page
for the session (they never change).
*Acceptance:* with 6 prefetches queued, a geometry request completes in ≤ 100 ms (was 11.4 s).

**T04.5: Supervision and respawn.** Wrap the session in a `ReaderSessionSupervisor`: detect
process exit or pipe failure, dispose, respawn with the same document (and password, held in
the existing bounded `char[]` copy), replay the current page request, then emit a
`reader.session.respawned` log event. After 3 respawns within 60 s, stop and show a localised
error with *Reopen* and *Export diagnostics*. Drain the worker's stderr into the log (it is
never read today).
*Acceptance:* E2E G8 kills `OgmaLibrary.Workers` during reading; the page is back within 2 s.

**T04.6: DPI-aware render scale (K33).** Replace the fixed `ReaderRenderDefaults.PageWidthPx`
request with `RenderRequest(widthPx = ceil(displayWidthDip × zoom × TopLevel.RenderScaling))`,
bucketed to 128 px steps for cache reuse. Cap it at 4,096 px or at the memory budget. Render a
quick low-resolution preview first, then the sharp page. Prefetch neighbours at the current
bucket. Re-render when monitor scaling changes (`TopLevel.ScalingChanged`).
*Acceptance:* the sharpness metric on page 1 at 100 % zoom improves from the audit baseline
image to at least the reference threshold; memory stays under the budget during the soak.

**T04.7: Cancellation and disposal.** When a book is closed or another one opened, cancel all
pending requests, dispose the session and wait for worker exit (≤ 2 s) so no worker is leaked.
Handle a double open racing in `ReaderSessionService`.
*Acceptance:* 50 open/close cycles leave 0 `OgmaLibrary.Workers` processes.

**T04.8: Telemetry.** Log per-turn latency buckets (preview and sharp), queue depth and respawn
count at Debug level, and expose them to the Activity Centre diagnostics export.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Crash after ~20 turns (K30) | Cumulative 15 s CPU limit on persistent worker + sync IPC + no handler | Session limits, async IPC, supervisor | Reading never kills the app | exit at ~20 turns → 0 exits in 1,000 | soak log | Runaway render without CPU cap | Per-request watchdog kills and respawns |
| 11.4 s stall (K32) | Sync IPC behind prefetch queue | Priority queue + async + geometry cache | Turns feel instant | 11.4 s max → p95 ≤ 250 ms preview | latency log | Complexity in the scheduler | Feature flag to disable prefetch |
| Reply mismatch after timeout | No correlation ids | RequestId routing | No desync | desync possible → impossible by design | protocol test | Worker/app version skew | Version handshake field |
| Blurry text (K33) | Fixed raster width | DPI and zoom-aware buckets | Sharp text | blurry → sharpness ≥ reference | image metric | Memory growth | Width cap and cache LRU |

## 8. Test plan

- **Unit:** limit flags per policy; request-id routing; stale-response discard; width
  bucketing; supervisor state machine (healthy → respawning → failed).
- **Integration:** T04.1 regression; a 300-render session; a killed worker mid-render; a
  password-protected session respawn.
- **E2E:** G3 extended to 1,000 turns (nightly; 100 turns in the pull-request suite); G8 worker
  kill; zoom in and out 20 times; move the window between a 100 % and a 150 % scaled monitor
  (NOT ASSESSED on single-monitor CI).
- **Negative:** truncated PDF opened in the reader (clear error, no crash); 0-byte file;
  a file deleted while open; worker exe missing.

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests -c Release --no-build --filter "FullyQualifiedName~ReaderSession|FullyQualifiedName~PdfWorkerSession"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G3|Journey=G8"
$env:OGMA_E2E_SOAK_TURNS=1000; dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Category=ReaderSoak"
Get-WinEvent -FilterHashtable @{LogName='Application';ProviderName='.NET Runtime';StartTime=(Get-Date).AddHours(-1)} -ErrorAction SilentlyContinue | Where-Object Message -match 'OgmaLibrary'   # expect none
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| macOS reader session limits (no Job Objects; uses rlimits or a supervisor) | Phase 27 | NOT ASSESSED on macOS |
| Reference-machine latency budget | Phase 24 | Local measurements only until then |
| Mixed-DPI multi-monitor behaviour | Engineering | Manual check recorded; not automated |

## 11. Risks and mitigations

- *Removing the CPU cap weakens containment.* Compensate with the per-request wall-clock
  watchdog, the unchanged memory cap, the idle timeout and a documented review in the Phase 23
  threat model.
- *Protocol change breaks ingestion jobs that share the worker binary.* Version the protocol,
  and have one-shot commands ignore the `RequestId` field.
- *Higher-resolution rasters increase memory.* Use the LRU byte-budget cache and cap width.

## 12. Execution prompt

```
## Prompt 04 - Make reading stable, responsive and sharp
You are fixing the PDF reader engine of Ogma Library in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K30, K32, K33)
5. docs/plans/sept-23-kaizen/phases/phase-04-reader-engine-stability.md
Load skills (read SKILL.md): avalonia-desktop-development, reliability-engineering, frontend-performance,
systematic-bug-diagnosis, vibe-security-skill.
Work plan: A. T04.1 (failing reproduction) first. B. T04.2 and T04.3 (serial; protocol change touches app and worker).
C. T04.4-T04.8.
File scope: src/OgmaLibrary.Infrastructure/Pdf/**, src/OgmaLibrary.Workers/Pdf/**, src/OgmaLibrary.Reader/**,
src/OgmaLibrary.App/ViewModels/Reader/ReaderViewModel.cs, src/OgmaLibrary.App/Views/Reader/ReaderView.axaml(.cs), tests.
Never weaken memory, process-count, network or kill-on-close containment. Record CPU-time data before choosing limits.
Acceptance: section 9 commands; 1,000-turn soak log and latency histogram in
docs/implementation/execution/evidence/sept-23-kaizen/phase-04/.
Recovery point: the last Phase 03 commit.
```
