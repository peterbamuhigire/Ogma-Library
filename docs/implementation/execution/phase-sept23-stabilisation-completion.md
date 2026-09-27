# Sept-23 stabilisation lane completion

Status: **7 of 7 items done; the full-suite regression run is PENDING** (2026-09-27).
Branch `worktree-agent-a705173e608609aec`, not merged. Rollback point: `bd76e8c` (the lane's base).
Scope: the seven defects from the real-window run `20260925-145558` on merged `main`. No new
scope was taken on. The owner stopped work for the day before the `-All` regression run could
finish, so that run is pending (see *Pending*).

## Items

| # | Item | Status | Commit | Evidence |
|---|---|---|---|---|
| 1 | K93: collections sidebar changed state off the UI thread | Done | `efea424` | MEASURED: 0 `ui.thread.violation` events in every run `stab-final-2` journey (G6, G8, LaunchCycles, ScanOcrSearch; was 194 per run). CODE: fail-first `ShelfSidebarTests`. |
| 2 | Catalogue queries before the migration ("no such table: Jobs/LibraryRoots") | Done | `7f0eae1` | MEASURED: the kept ScanOcrSearch app logs of `stab-final-2` contain no `no such table` line. CODE: fail-first `ApplicationStartupTests`. |
| 3 | K71: LaunchCycles close hangs during startup | Done | `515251e` | MEASURED: LaunchCycles PASS in `stab-final-2`. 20 of 20 closes exited cleanly with no kill. Exit took 238–702 ms, with 0 orphaned workers, 0 warnings and 0 UI-thread violations. It was 3 of 20 hung at 10 s in `20260925-145558`. It also passed in `stab-all-1` (partial run). CODE: `StartupCoordinatorShutdownTests`. |
| 4 | K95: G8 corrupt-settings notice not seen | Done | `13dabdc` | MEASURED: G8 corrupt settings PASS at 1280×800 and 1920×1080 (`stab-final-2`). Root cause: the product already showed the notice (from `936c587`), with the text "Library settings were damaged and have been reset…". The journey looked for the `Shell.Toast` id, which is on a `Border` that Avalonia leaves out of the UIA tree. `Shell.FindToast` now falls back to the `Shell.Toasts` list when it contains readable text. The fail-first evidence is the BASELINE-FAIL in `20260925-145558`. |
| 5 | Scanned OCR fixture yielded no text | Done (OCR part) | `9366c56` | MEASURED: in `stab-final-2`, both "Scanned Pamphlet" and "Scanned Handout" reach the **OCR text** badge at both sizes. The log shows `ocr.job.completed pages=1 chunks=1` for both books. Before the fix, the handout showed NoText. The search step still fails; Phase 13 owns that (see *Remaining failures*). |
| 6 | Garbled locked-PDF message | Done | `27e7b12` | CODE: `PdfiumAdapterPasswordTests`, `ReaderViewRenderTests` (localized "password-protected" state, en/fr). G6 PASS at both sizes. No journey opens a locked book in the reader, so the reader message itself is not proven in the real window. |
| 7 | G6 harness flake (bare COMException) | Done | `f1b7b5b` | MEASURED: G6 PASS at both sizes (`stab-final-2`). A card replaced during a poll now counts as "not settled" instead of failing, as in G2. |

Baseline (`tests/OgmaLibrary.Tests.E2E/baseline.json`, commit `5264aa5`):

- Removed G8 corrupt settings (K95/K28), which passed.
- Removed G8 worker kill (K30), which passed in `20260925-145558`, `stab-after-2` and `stab-final-2`.
- Added a ScanOcrSearch entry for Phase 13 (K97, K40). It matches only search-miss messages; a
  missing OCR badge still reports FAIL.

## Gates (worktree, 2026-09-27)

| Gate | Result |
|---|---|
| Requirement accountability | PASS (101 FRs, 29 NFRs, 32 controls) |
| `restore --locked-mode` | PASS |
| `format --verify-no-changes` | PASS |
| Release build | PASS, 0 warnings, 0 errors |
| Vulnerable packages | PASS (none reported) |
| Analyzers (warn) | PASS |
| `dotnet test … --filter "Category!=Performance&Category!=E2E"` | PASS: **1,426** tests (Architecture 52, Tests 1,156, Ui 218) |

Notes:

- The brief's filter `Category!=Performance` also runs the E2E project, which drives real
  windows **outside** the desktop lock. The first attempt did this, while other lanes were also
  running, so the lane stopped its own E2E testhost. E2E runs only through the lock runner.
  Stray run `20260927-124559` came from this and is not evidence.
- Under concurrent load from other lanes, three timing tests failed once. They pass in isolation
  and passed in the final full run, which makes them the K96 class (Phase 24). The tests:
  - `IsolatedPdfRenderer_FiftyOpenCloseCycles_LeaveNoWorkers`: passed 3 of 3 in isolation.
  - `PerfBenchmark_MetadataSearch_P95_LessThan150ms`: passed 3 of 3 in isolation.
  - `DiscoveryService_EnumeratesFiftyThousandFilesWithBoundedChannel` (`Category=Benchmark`):
    passed 2 of 3 in isolation.

## Real-window journeys: run `stab-final-2` (desktop lock, isolated data)

| Journey | 1280×800 | 1920×1080 |
|---|---|---|
| G6 invalid files | PASS | PASS |
| G8 corrupt settings | PASS | PASS |
| G8 worker kill | PASS | PASS |
| LaunchCycles (20 cycles) | PASS | n/a (single size) |
| ScanOcrSearch | BASELINE-FAIL (K97, K40; Phase 13) | BASELINE-FAIL (K97, K40; Phase 13) |

Evidence: `artifacts/e2e/stab-final-2/` (`results.md`, screenshots, timings).

## Pending

- **`-All` regression run (`stab-all-1`): PENDING, NOT ASSESSED.** The run was stopped when the
  owner ended the day, before G1–G8 ran. Partial results:
  - PASS: Smoke, LaunchCycles, Settings ×4, Navigation 1280×800 (1 of 2 tests).
  - FAIL: Harness `AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements` (its probe
    window was covered by an unnamed Pane).
  - FAIL: Navigation 1920×1080, where `Shell.Nav.Library` was covered by an unnamed button.
  - FAIL: Navigation palette 1280×800, where the palette stayed open.
  - FAIL: ScanOcrSearch 1280×800, a stale-element `InvalidOperationException` on the search box.
    At 1920×1080 ScanOcrSearch was a BASELINE-FAIL.

  These failures came while the owner was at the desk. The "covered by" hits point to foreground
  interference, but that is not proven, so they are not claimed as either harness or product
  failures.
  **Next step:** re-run `Use-DesktopLock.ps1 … Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080`
  with the desk idle, and compare it with `20260925-145558`. Owner: coordinator, before merging.
- **ScanOcrSearch search-box stale element** (1280×800, `stab-all-1` only): possibly the same
  harness class as item 7, but it was not reproduced. Next step: see whether the re-run repeats
  it.

## Remaining failures

| Failure | Owner |
|---|---|
| ScanOcrSearch search step: the OCR hit shows as "Untitled" (**K97, new**), and the multi-word phrase misses (K40) | Phase 13 |
| G3 page-turn latency (K32, K94), which stays in the baseline | Phase 04 / 12 |
| G4 and A11yNames search misses (K40, K41), which stay in the baseline | Phase 13 |
| Load-sensitive timing tests (K96) | Phase 24 |
