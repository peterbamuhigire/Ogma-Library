# Sept-23 Phase 12 (reader experience), completion record

Status: **IMPLEMENTED** 2026-11-20 on branch `kaizen/phase-12`. Not COMPLETE: the Definition of Done (`08-master-plan.md` section 5) is not met. Open items are named in section 6.

Rollback point: `99eeff5e` (the branch point, before slice 1). Per-slice points are in section 7.

## 1. Gates (Release, this worktree, head `886bcc3b`)

| Gate | Result |
| --- | --- |
| `./scripts/Test-RequirementAccountability.ps1` | PASS: 101 FRs, 29 NFRs, 32 controls, all 162 IDs assigned |
| `dotnet restore OgmaLibrary.sln --locked-mode` | PASS |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | PASS (after `dotnet format` normalised four E2E files to CRLF in the working tree; git stores them normalised, so no commit was needed) |
| `dotnet build OgmaLibrary.sln -c Release --no-restore` | PASS: 0 warnings, 0 errors |
| `dotnet list ... package --vulnerable --include-transitive` | PASS: no vulnerable packages in any project |
| `dotnet format analyzers ... --severity warn` | PASS |
| `scripts/Test-Fast.ps1` | PASS: Architecture 53, Unit 1,470, Ui 514, E2E harness 5; 0 failed |
| `src/shelf3d` npm gates | NOT_ASSESSED: the 3D source was not changed by this phase |

## 2. Real-window journeys

Run `p12-reader` (`Invoke-GoldenJourneys.ps1 -Tag Reader -Sizes 1280x800,1920x1080 -Themes Light,Dark`) and run `p12-g6` (G6, both sizes, Light), each under `scripts/Use-DesktopLock.ps1 -Owner 'phase-12'`, with a fresh `OGMA_LIBRARY_DATA_DIR` in `%TEMP%` and the synthetic corpus. Summaries: `evidence/sept-23-kaizen/phase-12/results-reader.md` and `results-g6.md`.

| Journey | 1280x800 | 1920x1080 | Note |
| --- | --- | --- | --- |
| J-READ-1 soak (200 turns) | PASS, Light and Dark | PASS, Light and Dark | numbers in section 3 |
| J-READ-2 find, jump, history back | PASS | PASS | |
| J-READ-3 locked book, wrong then right password | PASS | PASS | closes K125 |
| J-READ-4 split view | NOT_ASSESSED | NOT_ASSESSED | the split-view button is unreachable while reading (K136) |
| J-READ-5 citation formats | PASS | PASS | author not asserted (K137) |
| J-READ-6 quit and reopen, exact page | PASS | PASS | |
| J-READ-N1 truncated PDF | NOT_ASSESSED | NOT_ASSESSED | the catalogue does not list the file; the refusal is covered by G6 |
| J-READ-N2 cancelled password prompt | PASS | PASS | |
| J-READ-AREA page-area share | PASS | NOT_ASSESSED | the 85 % threshold is defined at 1280x800 only |
| J-READ-SHARP sharpness at 100 % | PASS | PASS | rework run `p12-reader-rework2` (committed: `evidence/sept-23-kaizen/phase-12/results-rework2.md`, screenshots and timing JSON under `evidence/sept-23-kaizen/phase-12/{Light,Dark}/`): edge crispness 5.7 to 6.3 against a floor of 3.5 (K139 fixed; oracle replaced, section 3) |
| G3 turn fifty pages | PASS (baseline cleared) | PASS (baseline cleared) in Light; **FAIL once in Dark (p95 845 ms, K32/K138)** in the rework run | the 1920x1080 turn latency is still intermittent (K138) |
| G5 resume after relaunch | PASS | PASS | |
| G6 truncated PDF refused | PASS | PASS | Light only |
| G8 worker killed while reading | PASS | PASS | |

The runner lists three rows per journey and size (the Light and Dark runs plus a retry pass); every duplicate row for the PASS journeys above is PASS.

## 3. Measures, before and after

| Measure | Before | After | Label |
| --- | --- | --- | --- |
| Page area share of the client area, side panel closed, 1280x800 | about 55 % (plan) | **88.2 %** (target 85 %) | MEASURED (UIA bounds of `Reader.Viewport`) |
| Page area share, side panel open (its default) | n/a | **65.9 %** (below 85 %; the panel opens by default) | MEASURED |
| Page-turn p95, 1280x800 | 304 ms (Phase CO run `co-final`) | **61-62 ms** (G3, 50 turns), **69-71 ms** (J-READ-1, 200 turns); budget 250 ms | MEASURED |
| Page-turn p95, 1920x1080 | 267 ms | **173-175 ms** (G3), **206-231 ms** (J-READ-1) | MEASURED |
| Longest page turn | 3,725 ms | G3: 65-101 ms at 1280x800, 216 ms and 1,150 ms at 1920x1080. Slice 7 saw 989 ms and 8,954 ms in J-READ-1 (K138) | MEASURED |
| Sharpness, title page, 100 % | Phase 04: 853 to 2,605 (Laplacian variance, different page and method) | **Edge crispness** (Laplacian variance divided by the variance after a 3x3 blur; scale-independent): **Light 5.79 (1280x800) and 5.67 (1920x1080); Dark 6.27 and 6.18**, against a floor of **3.5**. The same crop stretched from a 75 % bitmap scores 2.8 and from 50 % scores 2.3, so the floor fails on blur. Raw Laplacian variance (564, 346, 307, 185) is recorded only: it falls with glyph size and with blank page area, so the old floor of 400 failed a crisp 1920x1080 page and could not tell blur from a large glyph | MEASURED (`J-READ-SHARP`, run `p12-reader-rework2`) |
| Sharpness at 150 % and 200 % display scaling | none | not run | NOT_ASSESSED |
| Toolbars and duplicate navigation sets in the reader | 3 toolbars, 2 navigation sets | 1 toolbar, 1 navigation set (keyboard and edge zones) | CODE (`Sept23Phase12ReaderFocusTests`) |
| READ requirements at M (missing) | 2 (spread and continuous, full screen) | 0 in code: spread and virtualised continuous layouts, F11 full screen | CODE |
| Split view | typed book ID, one shared session | picker with a session per pane, capped at 2 panes; real window not run | CODE |
| Locked PDF | cannot be opened from the inspector | opens with the right password; wrong password and cancel handled | MEASURED (J-READ-3, N2) |
| Citation formats | plain text | plain text, BibTeX, RIS, CSL-JSON, Markdown, with golden files | CODE (golden-file tests) |
| Reading position on exit | not flushed (nothing called `CloseAsync`) | flushed on close and on app exit, budget 1.5 s | MEASURED (J-READ-6) |

The 1920x1080 G3 and J-READ-1 p95 values are within the budget but with less margin than at 1280x800.

## 4. Typeface decision

Spectral for the reader title, Public Sans for the toolbar, tabular numerals (`tnum`) in the page field, all through the design tokens, per the work order. No new typeface and no hard-coded font sizes or colours were added. The rights decision for both families is the Phase 09 one; this phase did not change it.

## 5. Decisions

- **D-91**: proceed on Phase 09 IMPLEMENTED (its owner sign-off stays open).
- **D-92**: the unlock dialog is an in-app Avalonia dialog; the system keychain stores and reads passwords only if the user opts in.
- **D-93**: the Contents tab shows the outline or an empty state; the jump and back are tested with view-model and headless Ui tests, and history is proven in the real window with go-to-page and search jumps.
- **D-94**: the 150 % and 200 % sharpness checks are NOT_ASSESSED (owner Phase 24); 100 % is measured.
- **D-95**: the soak is journey J-READ-1 inside `Invoke-GoldenJourneys.ps1`.
- **D-96**: split view is capped at 2 panes.
- **D-97**: F4 toggles the side panel (Ctrl+B is the bookmark).
- **D-98**: Ctrl+F and Alt+Left/Right are shared by context (library or reader).
- **D-99**: shutdown flushes through the session and progress services, not `ReaderViewModel.CloseAsync`.
- **D-100**: CSL-JSON is checked for required keys and types; no schema is vendored.
- **D-101** (this slice): Phase 12 is recorded IMPLEMENTED, not COMPLETE, because J-READ-4 could not run (K136) and four NOT_ASSESSED items have no result yet.
- **D-102** (this slice): the working-tree line-ending dirt reported by `dotnet format` is normalised locally and not committed, because `.gitattributes` (`*.cs eol=crlf`) already stores these files correctly.

## 6. NOT_ASSESSED and open items, with owners

| Item | Owner |
| --- | --- |
| J-READ-4 split view in the real window; K136 must be fixed first | next Phase 12 follow-up (before COMPLETE) |
| J-READ-N1 truncated PDF as a reader journey (G6 covers the refusal) | Phase 12 follow-up |
| Sharpness at 150 % and 200 % display scaling | Phase 24 |
| Screen-reader access to page text and reader controls | Phase 21 |
| macOS keychain unlock and Cmd gestures | Phase 27 |
| Reference-machine page-turn budget | Phase 24 |
| Arabic RTL, 200 % text and the longest declared language for the new toolbar and panel | Phase 21 and Phase 24 |
| Owner walkthrough of the reader | Peter Bamuhigire |
| Advanced-testing skill: not found through the router | recorded NOT_ASSESSED |
| 1920x1080 page-area share (threshold defined at 1280x800 only) | Phase 24 |
| Keyboard focus inside the annotate flyout; real-window side panel resize | Phase 21 |

Open defects: K135, K136, K137, K138 (K139 fixed in rework: `SinglePageSurfaceWidth` was never re-notified, so the page image kept its initial 720 px width at any viewport; test `ReaderPage_At1920x1080_FillsFrameTopAligned`) (K138 also tracks the 1,150 ms turn at 1920x1080). Reader toolbar title now shows the open book's title (localised label as fallback).

## 7. Rollback points (revert the slice's commit; each is independent of the later ones except where noted)

| Slice | Commit | Roll back to |
| --- | --- | --- |
| 1 focus layout, one toolbar, full screen | `1be09f53` | `99eeff5e` |
| 2 to 3 spread and virtualised continuous layouts (the sharp-rendering code predates the phase; the check is in slice 7) | `af961b96` | `1be09f53` |
| 4 find bar, history, contents | `b43a8370` | `af961b96` |
| 5 password unlock, keychain opt-in | `43fe7ebe` | `b43a8370` |
| 6a split view | `ebd72f3f` | `43fe7ebe` |
| 6b citation formats, side panel, commands | `04b49428` | `ebd72f3f` |
| 7 reader journeys and metrics | `886bcc3b` | `04b49428` |
| 8 this record | the commit that adds this file | `886bcc3b` |

Earlier slice records: `phase-sept23-12-slice1-completion.md`, `-slice6-`, `-slice7-`.

## Rework 2026-11-20 (reviewer findings)

- **K139 fixed.** Root cause: `ReaderViewModel` raised `PageSurfaceWidth` but never `SinglePageSurfaceWidth`, which the page `Image` binds. The image kept its first width (720 px, the unscaled base) while the fit-width surface grew to 1,560 px, so a 720 px page sat centred in a tall beige surface (MEASURED from the old screenshot: image 720 px wide, about 580 px down). All eight notification sites now raise both. Test `ReaderPage_At1920x1080_FillsFrameTopAligned` fails on the old code (expected 1,560, actual 720) and passes now.
- **Real window re-run** (`p12-reader-rework2`, Reader tag, both sizes, Light and Dark, under the desktop lock): J-READ-1, 2, 3, 5, 6, N2, SHARP, G5, G8 PASS at both sizes in both themes; J-READ-AREA PASS at 1280x800 (1920x1080 NOT_ASSESSED, threshold defined at 1280x800); J-READ-4 and N1 NOT_ASSESSED as before (K136, catalogue). J-READ-1 p95 at 1920x1080: 223 ms (Light), 200 ms (Dark). The 1920x1080 reader page now fills the frame top-aligned and the toolbar shows the book title (screenshots committed at `evidence/sept-23-kaizen/phase-12/{Light,Dark}/J-READ-SHARP/after-1920x1080.png`; run summary `evidence/sept-23-kaizen/phase-12/results-rework2.md`).
- **G3 at 1920x1080, Dark: FAIL once** (p95 845 ms, budget 250 ms; the Light run and the earlier run passed). K32/K138 stays open for 1920x1080 page-turn latency; not hidden.
- **Blur.** The J-READ-1 and J-READ-3 `after` screenshots are taken right after a turn and show the low-resolution preview (visibly soft at 1920x1080); the full-resolution bitmap replaces it within the 1.5 s the SHARP journey waits, and the SHARP screenshot is crisp. A settled-render shot for J-READ-1 and J-READ-3 is not added (NOT_ASSESSED).
- **Evidence now committed.** The screenshots and timing JSON under `evidence/sept-23-kaizen/phase-12/` were replaced by the `p12-reader-rework2` files (the earlier ones predated the K139 fix). `results-rework2.md` and `.json` are the run summaries. Dark G3 at 1920x1080 failed in that run (p95 845 ms, K138), so `Dark/G3/after-1920x1080.png` was removed and `failure-1920x1080.png` plus `uia-failure-1920x1080.json` are kept instead.
