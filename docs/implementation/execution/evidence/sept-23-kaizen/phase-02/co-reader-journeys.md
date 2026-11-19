# Phase CO slice 3: reader journeys (Phase 02 proof), 2026-11-19

All values MEASURED. Each run used a fresh temporary `OGMA_LIBRARY_DATA_DIR`, the synthetic corpus,
Light theme and `en`, inside `scripts/Use-DesktopLock.ps1`. Commands are in the summaries
`results-co-*.md`.

## Root cause of the G3 stall (K94)

| Run | Head | Condition | G3 1280x800 | G3 1920x1080 |
|---|---|---|---|---|
| co-diag-1 | `20c11309` | as shipped, CPU sampled every 0.3 s | FAIL: 1 turn, 4 re-presses, 15 s `request_timeout` | not run |
| co-diag-2 | `20c11309` | `DOTNET_ThreadPool_ForceMinWorkerThreads=64` (diagnostic only) | PASS: 50 turns, 0 re-presses, p95 216 ms | PASS: 50 turns, 0 re-presses, p95 205 ms |

- In co-all-1 to -3 and co-diag-1, every stall began about 30 s after launch, whatever the turn
  count (1 to 17 turns). It coincided with `ocr.auto.queued`. Four of the 6 failing logs show
  `reader.engine.recovered ... (request_timeout)`. The other two (co-all-1 and co-all-2 at
  1280x800) closed the app before the 15 s timeout could fire.
- In co-diag-1 every PDF worker process was idle (no CPU) from 06:15:23 until the timeout, while
  the app used about 1.6 cores. The worker had no work to do; the app was not consuming its reply.
- Raising the thread-pool minimum removed the stall without any code change. That confirms
  starvation of the thread pool.
- Source: the OCR and extraction page loops ran `IPdfRenderer.ExtractTextLayer`, which is
  sync-over-async (`GetAwaiter().GetResult()`), inside `Task.Run`. Fixed in `fc153ba6`.

## After the fix (`fc153ba6`)

| Run | G3 1280x800 | G3 1920x1080 | G5 both | G8 both (2 tests) |
|---|---|---|---|---|
| co-reader-1 | PASS: 50 turns, p95 228 ms, max 816 ms | PASS: 50 turns, p95 205 ms | PASS | PASS |
| co-reader-2 | FAIL p95 only: 50 turns, p95 252 ms | FAIL stall: 21 turns, 4 re-presses, no engine restart | PASS | PASS |
| co-reader-3 | PASS: 50 turns, p95 242 ms, max 3,167 ms (1 re-press) | FAIL p95 only: 50 turns, p95 422 ms | PASS | PASS |

- `request_timeout` / `reader.engine.recovered`: 0 in all 6 G3 runs (before the fix: every
  failing run had them).
- G3 is still not stable (3 of 6 rows). The baseline entry stays, with corrected reasons.

## T02.3 log check (kept app data of all 24 sessions in co-reader-1 to -3)

| Check | Result |
|---|---|
| Synthetic corpus titles (9, from `tests/fixtures/corpus/expected.json`) in the logs | 0 hits |
| Corpus folder names in the logs | 0 hits |
| `ui.thread.violation` lines | 0 |
| Unhandled-exception or Critical lines | 0 |
| Error lines | 6, all EF Core `TransactionError` after `app.exit.requested`, only in G8 at 1920x1080 (K110) |
