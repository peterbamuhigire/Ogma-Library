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
| **Shutdown is bounded; engine recovery is logged; a corrupt settings file is quarantined and reported.** | MEASURED: G8 and LaunchCycles stable in the close-out runs (`co-final`) |
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
| **A real Settings screen.** Library folders, theme and density, **English/French** switched live, online metadata with a disclosure, the 3D and classroom switches, AI and privacy status, text recognition, and diagnostics. Environment variables are now visible admin overrides. | MEASURED, Phase 08 (COMPLETE; Settings journeys stable, `co-final`) |

## Milestone 4: scanned books become searchable (Phase 17)

MEASURED: the ScanOcrSearch journey passes in the real window at both sizes (`co-final`).
Phase 17 is COMPLETE.

- Each book shows an honest text status: Searchable, Image only, OCR in progress, OCR text with
  confidence, or Failed.
- Image-only books are recognised automatically on the device, one at a time, pausing on
  battery. Failures are typed, logged and not retried pointlessly.
- Only installed, checksum-verified language packs are offered.
- Search hits from recognised text are labelled "From OCR text". A re-index bug that would
  have undone OCR was found and fixed.

## Milestone 5: close-out of Phases 02, 05–08, 13 and 17 (19 November 2026)

MEASURED, run `co-final` on `ebb7b718`. [Record](../../implementation/execution/phase-sept23-CO-completion.md).

- **Phases 05, 08 and 17 are COMPLETE.** Their journeys passed at least 3 runs in a row at both
  sizes (rule D-17), and the full gate set passes.
- **Phases 02, 06, 07 and 13 stay IMPLEMENTED.** Each has a named open item: G3 (02), K109 (06),
  the owner walkthrough (07), and the K40 miss cause and J-SRCH-1 (13).
- **Search finds scanned books.** OCR text reaches the index, and hits are labelled "From OCR text"
  under the real title (K97, K99).
- **The Settings screen reads in Dark mode.** Text contrast was 2.0:1 and is now above AA (K113).
- **A second library folder can be added from Settings.** Proven by journey J-SET-3.
- **The thread-pool starvation behind most reader stalls is fixed** (`fc153ba6`). G3 still stalls
  occasionally (K94, K109).

## Golden journeys (close-out head `ebb7b718`, run `co-final`)

"Streak" counts the consecutive passing runs at 1280×800 / 1920×1080.

| Journey | Status | Streak | Notes |
|---|---|---|---|
| G1 first run | **PASS** | 5 / 5 | |
| G2 add library, covers, ISBN, status counts | **PASS** | 3 / 3 | |
| G3 read 50 pages (p95 ≤ 250 ms) | BASELINE-FAIL | 0 / 0 | p95 304 ms at 1280; a stall at page 12 at 1920 (K32, K94, K109) |
| G4 search | **PASS** | 6 / 6 | |
| G5 close and resume | **PASS** | 7 / 7 | |
| G6 invalid files | **PASS** | 6 / 6 | |
| G7 Advisor without AI | **PASS** | 5 / 5 | |
| G8 worker kill, corrupt settings | **PASS** | 7 / 7 | |
| Navigation (3 tests) | **PASS** | 4 / 5 | |
| Settings (4 tests, including J-SET-3) | **PASS** | 3 / 3 or more | |
| A11yNames | **PASS** | 6 / 6 | |
| ScanOcrSearch | **PASS** | 5 / 6 | baseline removed |
| LaunchCycles (20 launch/close) | **PASS** | 6 runs | 0 kills; slowest exit 1,072 ms |
| G9–G12 | NOT ASSESSED | — | owned by Phases 11, 10, 18 and 19–20 |

## Test and quality totals

| | Audit | Now (`ebb7b718`) |
|---|---:|---:|
| Automated tests (per commit) | 1,161 (one failing, 18 min) | **1,677 passing** (Architecture 52, core 1,248, UI 377; about 6 min) |
| Real-window journeys | 0 | `co-final`: 54 rows, 44 PASS, 2 BASELINE-FAIL (G3), 0 FAIL, 8 NOT ASSESSED placeholders |
| CI on `main` | red | NOT ASSESSED for this head (the driver pushes) |
| Defects registered | 46 | K1–K113 |

## In progress

- **Phase 09, "The Lamplit Library" (D-14): IMPLEMENTED 2026-11-19 and merged; awaiting the owner
  walkthrough.** A vivid, jewel-toned visual identity in response to the
  owner's feedback ("too greyish and boring"): one type scale on Spectral, Public Sans and
  JetBrains Mono with a 12 px floor; Lamplit colour roles and a Night Library dark theme; one
  button-variant set; amber selection; licensed duotone icons; cloth-bound generated covers; a
  vector QR; a TabStrip overflow menu (D-15); and a design-token lint in CI. No literal font size,
  `White` or hex colour remains in the views. Light and Dark screenshots at both sizes are in the
  [completion record](../../implementation/execution/phase-sept23-09-completion.md). Not COMPLETE:
  the accountability script fails on `main`'s archived `aug-39` matrix (K100), and the owner
  walkthrough is pending.
- **Phase 10, catalogue experience: IMPLEMENTED 2026-11-19 (branch `kaizen/phase-10`); not
  COMPLETE.** The catalogue is no longer a thin shell. Changes:
  - Readable item names (K14 fixed for the catalogue) and a virtualised grid.
  - Full filters with chips, an explained no-results state and smart shelves.
  - Multi-select and shelf membership.
  - Bulk edit with preview and undo.
  - "2 copies" with Compare, Merge and Split with undo (K23 partly fixed).
  - A library health dashboard.

  The static gates and the fast suite pass. J-CAT-1 to J-CAT-4 pass at 1280x800. Open:
  - No journey has run at 1920x1080.
  - G10 and J-CAT-5 were never executed, because the owner's desktop was in use (D-48).
  - The real-window 2,000-book scroll timing (K120).
  - Keep separate (K118) and localised job rows (K121).

  [Record](../../implementation/execution/phase-sept23-10-completion.md).
- **Phase 13, unified search: IMPLEMENTED and merged.** Phrases, typos, Unicode names and field
  queries; honest status; no race. G4 is stable. Open: the K40 miss cause and the J-SRCH-1
  journey.

## Stabilisation lane (done, merged `b55b8387`)

The real-window run of 25 September (`artifacts/e2e/20260925-145558`) found integration defects
between phases. A dedicated lane fixed all seven, and the `-All` runs `co-all-1..3` and `co-final`
confirm them (0 UI-thread violations, LaunchCycles 0 kills, G8 and G6 stable, ScanOcrSearch passes):

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
| G3 reader stall (K94, K108, K109) and p95 over 250 ms (K32) | Phase 04 (stall), Phases 06/24 (K109), Phase 12 (budget) |
| K40: cause of one phrase miss in `co-all-1`; J-SRCH-1 journey | Phase 13 |
| K113: nine accent-as-text usages still allow-listed | Phase 09 |
| K111 hard-coded inspector labels; K112 "Added 0" summary | Phases 11 and 22 |
| K71 cold start under load; K96 load-sensitive timing; K105 fast-suite time under load; K110 shutdown `TransactionError` | Phase 24 (LaunchCycles close hang not reproduced in 120 cycles) |
| K107 foreign window hit in a Navigation visibility check | Phase 01 harness |
| Owner walkthrough of the new navigation (D-13) and visual concept (D-14) | Owner |
| macOS, screen readers and accessibility variants, two-machine classroom, signing, hosted CI | NOT ASSESSED (Phases 27, 21, 19–20, 26; driver) |
