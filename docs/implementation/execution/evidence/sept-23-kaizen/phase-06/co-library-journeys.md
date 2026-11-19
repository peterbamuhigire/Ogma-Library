# Phase CO slice 4: library and processing journeys (Phases 05 and 06)

Date: 2026-11-19. Branch `kaizen/phase-CO`, product head `06f8f7a1`, test commit `b8bc973a`.
Command (inside `scripts/Use-DesktopLock.ps1 -Owner phase-CO`; a fresh temporary
`OGMA_LIBRARY_DATA_DIR` per test; synthetic corpus only; Light theme):

```powershell
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey G2,G6,LaunchCycles -Sizes 1280x800,1920x1080 -RunId co-library-N
```

All values below are MEASURED from `artifacts/e2e/co-library-N/` (local); the run summaries are
committed beside this file.

## Runs

| Run | G2 1280 | G2 1920 | G6 1280 | G6 1920 | LaunchCycles | Note |
|---|---|---|---|---|---|---|
| `co-library-1` | FAIL | FAIL | PASS | PASS | PASS | New ISBN assertion: "(no ISBN line)". The ISBN is on the Bibliographic tab, and the File tab is the default. This was a test premise error, not a product failure. |
| `co-library-2` | PASS | PASS | PASS | PASS | PASS | The test selects the Bibliographic tab (`b8bc973a`) |

All other G2 oracles passed in both runs: 13 cards, 1 flagged (Locked) and 12 valid, as
expected.json requires; covers 100 %. G6 has now passed in 5 consecutive runs at both sizes
(`co-all-1..3`, `co-library-1..2`). G2's earlier oracles have passed in 6 consecutive runs
(`co-s1`, `co-all-1..3`, `co-library-1..2`). The extended G2, with the ISBN and status checks,
has passed once (`co-library-2`).

## Phase 06 pending items (run `co-library-2`)

| Item | 1280x800 | 1920x1080 |
|---|---|---|
| Inspector ISBN, "The Physics of Everyday Light" (expected 978-0-306-40615-7) | `ISBN: 9780306406157` | `ISBN: 9780306406157` |
| `Shell.Status` samples (1 s) / "Preparing books" samples | 19 / 18 | 19 / 18 |
| Samples with N > M | **0** | **0** |
| Largest M (tasks) | 73 | 72 |
| Finished summary reached (within 180 s) | yes | yes |
| Final status text | `Added 0 · Updated 0 · Needs attention 3 · Missing 0` | same |

The screenshots are `co-library-2-finished-1280x800.png` and `-1920x1080.png`. They show the
Bibliographic tab with the ISBN, the finished status bar and the Locked card.

Observations registered:
- **K111** (Phase 11, with copy in 22): the `ISBN:`, `DOI:` and `Size: … bytes` labels are
  hard-coded English, and an empty DOI shows as "DOI: " with nothing after it.
- **K112** (Phase 22): the finished summary says "Added 0" after the first scan, which added 13
  cards. The cause is NOT_ASSESSED.

## LaunchCycles (K71)

| Run | Cycles | Needed a kill | Orphaned workers | Exit after WM_CLOSE, median / max |
|---|---|---|---|---|
| `co-library-1` | 20 | 0 | 0 | 371 / 821 ms |
| `co-library-2` | 20 | 0 | 0 | 431 / 882 ms |

No cycle needed a kill, so K71 is not re-registered and no log was captured. K71's startup items
under load stay with Phase 24.
