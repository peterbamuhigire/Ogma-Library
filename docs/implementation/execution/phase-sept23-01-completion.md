# Sept-23 Phase 01 completion: real-window acceptance harness and golden journeys

Status: **IMPLEMENTED; harness complete, journeys baselined** (2026-09-25). The harness, runner,
corpus, visibility oracle, AutomationIds, hook, CI job, ADR and guide are in place and were run
end to end. Most golden journeys fail today because of known product defects owned by later
phases; they are recorded as **BASELINE-FAIL** data, never as skipped tests.
Branch: `worktree-agent-a88fae4c2798e7386` (lane worktree, not pushed), from `38f895a`.
Rollback point: `38f895a` (the last commit before any Phase 01 change).
Plan: [phase-01](../../plans/sept-23-kaizen/phases/phase-01-real-window-acceptance-harness.md).
Defects: K05, K14 (detected and fixed for list items), K10 (oracle proven); new K93–K95.

## Tasks

| Task | Result | Commit |
|---|---|---|
| T01.1 ADR | [ADR-0017](../../adrs/0017-real-window-e2e-harness.md): FlaUI UIA3 5.0.0 (MIT), Win32 fallbacks. Measured on the 17-file corpus (141 elements): full walk 333.5 ms FlaUI vs 453.8 ms raw `System.Windows.Automation`; AutomationId lookup 324.8 vs 357.8 ms; identical Avalonia patterns. | `24c0ea5` |
| T01.2 Skeleton | `tests/OgmaLibrary.Tests.E2E` (`net10.0-windows`, xUnit, no production reference, `IsTestProject=false` off Windows so macOS is NOT ASSESSED, not passed). Lock file for this project only; `restore --locked-mode` passes. | `ed83f62` |
| T01.3 Fixture | `OgmaApp`/`E2ESession`: per-test `%TEMP%\ogma-e2e\<run>\<test>\{data,lib}`, `--ogma-e2e-session=` marker, 30 s window wait, topmost `SetWindowPos` sizing, `WM_CLOSE` with exit time and orphaned-worker check, stray cleanup limited to marked processes of the exe under test (never another lane's app). **20 launch/close cycles: all exited cleanly, 0 orphans, 0 strays** (253 s). | `ed83f62` |
| T01.4 Corpus | `tests/fixtures/corpus/New-SyntheticCorpus.py` (seeded per file, fixed dates and IDs, fixed RC4 ID); `MANIFEST.sha256.json`; `expected.json` oracle (12 catalogue works, 4 invalid, 5 search probes, reader probe). **Two runs: identical SHA-256 for all 17 files.** Python kept (PdfSharp port not needed; CI installs pymupdf/pypdf). | `6ca2ffa` |
| T01.5 Hook | `-p:OgmaE2EHooks=true` defines `OGMA_E2E`; `OGMA_E2E_PICK_FOLDER` replaces only `OpenFolderPickerAsync` in `ChooseFolderAsync`. 4 architecture tests guard it (symbol only behind the opt-in, no packaging/release use, source fenced, shipped assembly has no hook method). G2 uses the **real dialog** (G1b technique, keyboard + offset click); other journeys use the hook. | `ce36f0d` |
| T01.6 Visibility | `AssertVisiblyPainted` (client-area bounds, 5 hit-test points, CIE76 painted share) and `AssertReachable`. Self-test on a synthetic WinForms window: covered, clipped and unpainted elements fail; a plain button passes. | `ed83f62` |
| T01.7 Ids and names | 60+ `AutomationId`s (`Shell.*`, `Catalogue.*`, `Detail.*`, `Reader.*`, `Search.*`, `Advisor.*`). K14 fixed where trivial: catalogue grid/list and search `ListBoxItem`s carry the title via a container style. `A11yNames` test: catalogue names readable at both sizes (PASS). | `56204df` |
| T01.8 Journeys | G1–G8 at 1280×800 and 1920×1080 (G8 has worker-kill and corrupt-settings tests), `A11yNames`, `Smoke`, `LaunchCycles`, `Harness` self-checks, G9–G12 placeholders (NOT ASSESSED). | `ed83f62`, `fcbb958`, `64e9483`, `bc0fab5` |
| T01.9 Evidence | `artifacts/e2e/<run>/<journey>/`: before/after/failure PNGs, UIA dumps, `timings-*.json` (with log warning and `ui.thread.violation` counts), app logs on failure, `results.jsonl`, `e2e.trx`. | `ed83f62` |
| T01.10 CI | `e2e-windows` job on `windows-latest` (corpus, runner at 1280×800, uploads `artifacts/e2e`), **`continue-on-error: true`** until G1/G2 are green on the hosted runner. `build-test` and `Test-Fast.ps1` exclude `Category=E2E`. | `6bc81a5` |
| T01.11 Docs | [e2e-harness.md](../../developer-guide/e2e-harness.md): run, read failures, add a locator, flake policy. | `18c9450` |
| T01.12 Runner | `Invoke-GoldenJourneys.ps1` (PS 5.1 and 7): `-All/-Journey/-Tag/-Sizes/-Themes/-Culture/-KeyboardOnly/-UiaAudit/-TextScale/-ClippingCheck/-UseMockAiServer`, plus `-NoBuild/-Exe/-Strict/-ReportOnly`. `-Sizes 800x600` is rejected (exit 2, clear message); `-Journey G1 -Sizes 1280x800` ran G1 only; `-Tag Reader` ran exactly G3, G5 and G8 worker-kill. `baseline.json` maps a failure to BASELINE-FAIL only when its message matches the entry's pattern. | `ed83f62`, `fcbb958` |
| Prototype fix | `Invoke-OgmaUia.ps1` `launch`/`kill` no longer stop every Ogma process on the machine. | `7874cfc` |

## K10 oracle proof (MEASURED)

A scratch build of `ed83f62` with the pre-Phase-03 nesting re-created (pager `Border` inside the
`Grid.Row="4"` cell) was driven with `-Exe`
([results](evidence/sept-23-kaizen/phase-01/baseline/results-k10-regression.md),
[screenshot](evidence/sept-23-kaizen/phase-01/baseline/k10-g1-failure-1280x800.png)):
G1 **FAIL** at both sizes (heading: no hit point reaches it, **0.0 %** painted); G2 **FAIL** at
both sizes (grid centre *COVERED by* the pager text, 0.4–0.8 % painted). On the fixed shell G1
passes at both sizes. The 0ad3c0c tree itself has no AutomationIds, so the regression was
re-created on the Phase 01 tree instead of building `0ad3c0c` directly.

## Journey baseline (full run `phase01-baseline-r2`, Windows 11, 2560×1440 at 96 DPI)

Command: `Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080` →
[results-full-run.md](evidence/sept-23-kaizen/phase-01/results-full-run.md). G8 worker-kill was
re-run after a harness fix in `-Tag Reader` run `phase01-reader-r3`
([results](evidence/sept-23-kaizen/phase-01/results-reader-tag-run.md)).

| Journey | 1280×800 | 1920×1080 | Cause / measurement | Owner |
|---|---|---|---|---|
| G1 first run | PASS | PASS | Heading and both Choose-folder buttons painted and reachable ([shot](evidence/sept-23-kaizen/phase-01/g1-first-run-1280x800.png)) | — |
| G2 add library (real dialog) | BASELINE-FAIL | BASELINE-FAIL | 17 cards for 12 works (K21); 0 % covers after 90 s (K20) ([shot](evidence/sept-23-kaizen/phase-01/g2-placeholders-invalid-cards-1920x1080.png)) | 05 |
| G3 read 50 pages | BASELINE-FAIL | BASELINE-FAIL | p50/p95 567/681 ms, stall after 48 turns (1280); 1,204/1,525 ms, stall after 14 (1920); 4 ignored presses (K32, K94). Phase 04 not merged in this branch. | 04 |
| G4 search | BASELINE-FAIL | BASELINE-FAIL | Title, author, phrase, typo, Unicode probes all miss (K40, K41) | 13 |
| G5 resume | PASS | PASS | Page 7 restored; 20.9 s / 22.1 s from relaunch (UX-007 ≤ 60 s) | — |
| G6 invalid files | BASELINE-FAIL | BASELINE-FAIL | 0-byte, HTML, truncated, locked shown as books, no attention badge (K21) | 05 |
| G7 Advisor unconfigured | BASELINE-FAIL | BASELINE-FAIL | Ask enabled; no route to Settings (dead end) | 15–16 |
| G8 worker killed | BASELINE-FAIL | BASELINE-FAIL | App survives and logs; the reader never renders again, no message, 9 presses ignored (K30) | 04 |
| G8 corrupt settings | BASELINE-FAIL | BASELINE-FAIL | Logged, but no message; normal first-run screen (K95, K28) | 05 |
| A11yNames catalogue | PASS | PASS | Titles, no record dumps (K14 fixed) | — |
| A11yNames search | PASS (baseline cleared) | BASELINE-FAIL | Search returned items at 1280 but none at 1920 in the same run: K41 flakiness | 13 |
| Smoke, LaunchCycles, Harness ×3 | PASS | — | Size-independent | — |
| G9–G12 | NOT ASSESSED | NOT ASSESSED | Placeholders for Phases 11, 10, 19–20, 18 | later |

Not run (NOT ASSESSED): `-Themes Dark` (supported by seeding, not run this phase), `-Culture`
other than `en`, `-TextScale 200`, `-KeyboardOnly`, `-ClippingCheck`, `-UseMockAiServer`, macOS.
The first full run ([results](evidence/sept-23-kaizen/phase-01/results-first-full-run.md)) found
two harness defects, both fixed and re-run: a 3 s dialog wait race in G2 (`64e9483`) and a
short-lived worker PID in G8 (`bc0fab5`).

## New findings (added to the register)

- **K93** (P2): `ShelfSidebarViewModel` mutated off the UI thread after a scan; 16–32
  `ui.thread.violation` events per library journey (0 in G1/G7).
- **K94** (P1): Next presses ignored while a turn is in flight; the reader stalls.
- **K95** (P1): a corrupt `library-settings.json` is silent to the user.
- The native folder dialog exposes no `cmb13` folder box to Win32 either; only the
  keyboard-and-offset technique works (needs foreground).

## Gates (this branch)

`restore --locked-mode` pass; `format --verify-no-changes` 0; Release build 0 warnings 0 errors;
`Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls); `Test-Fast.ps1`:
Architecture 51/51, core 988/988, UI 175/175 (1,214 tests, 0 failures; E2E excluded).

## Deviations and NOT ASSESSED

| Item | Owner | Note |
|---|---|---|
| CI `e2e-windows` on a hosted runner | Coordinator / Engineering | Not pushed from this lane; DPI and foreground behaviour on `windows-latest` unverified. |
| Five-run flake check (plan §8) | Engineering | Two full runs and one Reader run done; search (K41) is nondeterministic. |
| Build of `0ad3c0c` itself | — | Replaced by the re-created K10 nesting on the Phase 01 tree (ids needed). |
| macOS journeys | Phase 27 | Project is not a test project off Windows. |
| Typeface | — | No UI styling changed (ids and accessible names only); Spectral / Public Sans unchanged. |
