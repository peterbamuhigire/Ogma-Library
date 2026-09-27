# Sept-23 Kaizen: milestones

This is a running, owner-facing record of what has changed in the product, measured where
possible. Per-phase detail lives in the completion records under
`docs/implementation/execution/phase-sept23-NN-completion.md`, and every decision taken on the
owner's behalf is in [EXECUTION-LOG.md](EXECUTION-LOG.md).

Evidence classes: **MEASURED** means observed in the real application window or by a command
run; **CODE** means verified by tests only. Items that were not run are **NOT ASSESSED** and are
never counted as passes.

## Where we started (audit, 25 September 2026, commit `0ad3c0c`)

- Score **30/100**. The library was invisible, reading crashed the app, primary actions were
  off-screen, and covers, file validity, search and the Advisor were broken
  ([01-audit-report.md](01-audit-report.md)).
- The working tree did not build. CI had been red since at least 14 September.
- 1,161 tests passed while the main screen was blank.

## Milestone 1: the repository is healthy again (Phase 00)

MEASURED. Completion record: `phase-sept23-00-completion.md`.

| Before | After |
|---|---|
| The solution failed to build (210 test files deleted) | Tests restored; 0 errors, 0 warnings |
| Format gate red; CI red on Windows and macOS | Format green; **CI green on both OSs** (run 36104052111) |
| Per-commit tests took 18 min; flaky OCR test | 4.5 min fast suite; benchmarks run nightly; OCR test 10/10 |
| Test runs rewrote `docs/`, left 30 temp items, raised firewall prompts | Clean runs; LAN tests stay on loopback |
| No pinned SDK; 3.4 GB of stale build output | `global.json` pinned; tree cleaned |

## Milestone 2: the app works and does not crash (Wave A: Phases 01–04)

| Result | Evidence |
|---|---|
| **The library and first-run screen are visible.** A one-line layout error had hidden them since 4 September. A layout guard test now fails if it ever returns. | MEASURED, Phase 03 |
| **Reading no longer crashes the app.** 470 page turns in a 900-page book; recovery from a killed PDF engine in under 2 s; UI page-turn p95 dropped from an **11.4 s stall to 6 ms**; pages are about 3× sharper (render at device resolution). | MEASURED, Phase 04 |
| **Failures are caught and logged.** A process-wide safety net shows a localized message instead of crashing, and writes redacted rolling logs. The next launch shows a crash notice. The single-instance guard hands a second launch to the open window. Crash-prone `async void` handlers went from 126 to 1, and silent `catch` blocks from 21 to 0. | MEASURED, Phase 02 |
| **Shutdown is bounded; engine recovery is logged; a corrupt settings file is quarantined and reported.** | CODE, `936c587`; re-check in progress (see Open issues) |
| **A real-window test harness** (FlaUI UIA3, ADR-0017) drives the actual app through 8 golden journeys at 1280×800 and 1920×1080 on isolated data, with a desktop lock for parallel runs. It no longer pins its window above the owner's desktop. | MEASURED, Phase 01, `7e342a6` |

## Milestone 3: the library behaves correctly (Wave B: Phases 05–08)

| Result | Evidence |
|---|---|
| **Real covers** on every renderable book (13/13), stored in the app-data folder, never inside the user's library (ADR-0018). | MEASURED, Phase 05 |
| **Invalid files are handled honestly.** Empty, non-PDF and truncated files are set aside under a visible **"N files need attention"** notice. Password PDFs show as **Locked**. Journeys G2 and G6 **pass at both sizes**. | MEASURED, Phase 05, `49f1fe2` |
| **Several library folders.** Add, hide, rescan and remove; books return when their files come back; a watcher picked up a new PDF in 2.7 s. | MEASURED, Phase 05 |
| **Processing is fast and quiet.** Failed jobs **17 → 0**; attempts 269 → 65; all covers in **84 s → 27 s**; all processing **163 s → 64 s**; worker launches 129 → 34; the status counts truthfully to "finished". | MEASURED, Phase 06 |
| **Metadata and identity.** All **5 ISBNs** reach the books; a byte-identical duplicate is one book with two locations; editions are populated. | MEASURED, Phase 06 |
| **Navigation you can use.** A left rail (Library, Search, Reading, Advisor, Collections, Activity, Settings) shows one screen at a time, keeps active filters visible as chips, and has a 30-command palette (Ctrl+K). Dead entries are hidden. | MEASURED, Phase 07 |
| **A real Settings screen.** Library folders, theme and density, **English/French** switched live, online metadata with a disclosure, the 3D and classroom switches, AI and privacy status, text recognition, and diagnostics. Environment variables are now visible admin overrides. | CODE, Phase 08; real-window run in progress |

## Milestone 4: scanned books become searchable (Phase 17)

CODE (real engines in tests); real-window run in progress.

- Each book shows an honest text status: Searchable, Image only, OCR in progress, OCR text with
  confidence, or Failed.
- Image-only books are recognised automatically on the device, one at a time, pausing on
  battery. Failures are typed, logged and not retried pointlessly.
- Only installed, checksum-verified language packs are offered.
- Search hits from recognised text are labelled "From OCR text". A re-index bug that would
  have undone OCR was found and fixed.

## Golden journeys on `main`

| Journey | Status | Notes |
|---|---|---|
| G1 first run | **PASS** | both sizes |
| G2 add library, covers | **PASS** | both sizes (was failing) |
| G5 close and resume | **PASS** | |
| G6 invalid files | **PASS** | both sizes (was failing) |
| G7 Advisor without AI | **PASS** | routes to Settings instead of a dead end |
| Navigation | **PASS** | four sizes, 860–2560 px |
| Settings | **PASS** | both sizes: theme and language persist; env-managed switch shown locked |
| ScanOcrSearch | failing | OCR runs automatically, but one fixture yields no text and search misses depend on Phase 13 |
| G3 read 50 pages (p95 ≤ 250 ms from click to page number) | failing | measured 500–632 ms end to end under load; the geometry wait is the known remaining cost |
| G4 search | failing | Phase 13 in progress |
| G8 worker killed while reading | **PASS** | both sizes; baseline cleared |
| G8 corrupt settings file | failing | the notice is not yet visible; stabilisation lane |
| LaunchCycles (20 launch/close) | **failing** | 3 of 20 closes still needed a kill after 10 s; under investigation |
| G9–G12 | NOT ASSESSED | owned by Phases 10, 11, 18, 19–20 |

## Test and quality totals

| | Audit | Now |
|---|---:|---:|
| Automated tests (per commit) | 1,161 (one failing, 18 min) | **1,404 passing** (about 6 min) |
| Real-window journeys | 0 | 11 journeys × 2 sizes, plus harness tests |
| CI on `main` | red | **green** (Windows and macOS) |
| Defects registered | 46 | 51 (K93–K96 found by the new harness and load) |

## In progress

- **Phase 09, "The Lamplit Library" (D-14).** A vivid, jewel-toned visual identity in response to
  the owner's feedback ("too greyish and boring"). Concept screenshots first, then the full
  design system.
- **Phase 13, unified search.** Phrases, typos, Unicode names and field queries; honest status;
  no race.

## Stabilisation lane (in progress)

The real-window run of 25 September (`artifacts/e2e/20260925-145558`) found integration defects between phases, now owned by a dedicated lane:

- the collections sidebar updates state off the UI thread (K93; 194 violations per run);
- database queries run before the startup migration has created the tables;
- closes hang when they arrive during startup (LaunchCycles, K71);
- the corrupt-settings notice is not shown (K95);
- one scanned fixture yields no OCR text;
- the locked-PDF error message is garbled;
- a G6 harness flake.

## Open issues

| Issue | Owner |
|---|---|
| LaunchCycles: 3/20 closes hang >10 s (suspected startup work blocking the UI thread, K71) | Coordinator, now; Phase 24 |
| G3 page-turn latency over its 250 ms budget | Phase 06 follow-up / Phase 12 |
| Load-sensitive timing test (K96) | Phase 24 |
| Owner walkthrough of the new navigation (D-13) and visual concept (D-14) | Owner |
| macOS, screen readers, two-machine classroom, signing | NOT ASSESSED (Phases 21, 26, 27) |
