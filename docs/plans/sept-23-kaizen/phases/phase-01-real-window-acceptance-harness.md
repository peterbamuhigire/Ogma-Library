# Phase 01: Real-window acceptance harness and golden journeys

## 1. Header

| Field | Value |
|---|---|
| Wave | A: Stabilise |
| Size | 5–7 engineering days |
| Depends on | Phase 00 |
| Owner decisions | none (the harness tooling choice is an engineering decision recorded as an ADR) |
| Primary defects | K05 (blind test oracle), K14 (accessible names), K04 (test hygiene, continued) |
| Requirement IDs | UX-007 (locate and resume in 60 s: first measurement), NFR test gates, CTRL evidence integrity |

## 2. Why this phase exists

For three weeks every user saw a blank catalogue and an invisible first-run call to action
(K10), while 1,160 automated tests passed (K05). The headless Avalonia tests check view models
and controls in isolation; nothing checks that the real window paints the content, that the
content is not covered by another element, or that a primary action is inside the window at a
normal size (K11). The 14 September audit found the same blind spot with the command palette.

During this audit a prototype driver
([`evidence/tools/Invoke-OgmaUia.ps1`](../evidence/tools/Invoke-OgmaUia.ps1)) and a synthetic
PDF corpus generator ([`evidence/tools/New-SyntheticCorpus.py`](../evidence/tools/New-SyntheticCorpus.py))
found K10, K11, K12, K13, K14, K20, K21, K30, K40, K41 and K50 in about two hours. This phase
turns that prototype into a maintained harness. Every later phase proves its work with it.

## 3. Objectives and exit criteria

1. A new test project `tests/OgmaLibrary.Tests.E2E` drives the **real Release build** of
   `OgmaLibrary.App.exe` through Windows UI Automation. It runs locally and in a Windows CI job.
2. Every test uses an isolated `OGMA_LIBRARY_DATA_DIR` under a per-test temp folder and a
   generated corpus. Nothing touches the developer's real library or app data.
3. An `AssertVisiblyPainted(element)` helper fails when an element that UIA reports as present
   is covered, clipped by the window edge or not painted. It **must fail on commit `0ad3c0c`**
   for the catalogue grid and the empty state (the K10 regression), and pass once Phase 03 lands.
4. The eight golden journeys (G1–G8 below) are automated, and each records timings and
   screenshots at 1280×800 and 1920×1080.
5. Primary interactive controls expose stable `AutomationProperties.AutomationId` values, and
   list items expose a human-readable `AutomationProperties.Name` (K14 detection; the fixes
   land in Phases 10 and 13).
6. A baseline run on `0ad3c0c` is stored in
   `docs/implementation/execution/evidence/sept-23-kaizen/phase-01/baseline/` and matches the
   audit's findings.

## 4. Skills to load before starting

- `C:\wamp64\www\windows-admin-engine-skills\skills\virtualization-containers-and-development\windows-desktop-e2e-testing\SKILL.md`: UIA, AutomationId-first locators, per-test APPDATA isolation, `windows-latest` CI. (Avalonia is not in its framework table, so validate each pattern against Avalonia's automation peers.)
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`: risk-based layers and flake policy.
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`: the testing section, AutomationProperties, and headless vs real-window boundaries.
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\click-path-audit\SKILL.md`: tracing each control's handler in call order.
- `C:\wamp64\www\design-system-skills\governance\design-quality-gate.md`: the state matrix the journeys must capture.

## 5. Scope in / scope out

**In:** the E2E project, driver library, corpus generator, visibility assertion, the eight
journeys, AutomationIds on the controls the journeys use, a test-only root seeding hook,
screenshot and timing capture, a CI job, an ADR for the tooling choice, and documentation.

**Out:** fixing the defects the journeys reveal (later phases); macOS automation (Phase 27
adds an XCUITest/AppleScript or Avalonia-accessibility variant); screen-reader output testing
(Phase 21).

## 6. Work breakdown

**T01.1: Tooling decision (ADR).** Evaluate FlaUI (UIA3, MIT licence, `FlaUI.UIA3` NuGet)
against the raw `System.Windows.Automation` client used in the prototype. Criteria: Avalonia
peer compatibility (Invoke, Toggle, SelectionItem, Value, ExpandCollapse, ScrollItem), speed of
tree walks on 1,000+ elements, access to owned native dialogs (the `#32770` folder picker is a
child of the main window), maintenance and licence. Recommended default: **FlaUI UIA3**, with a
fallback to raw UIA for anything FlaUI does not expose. Record the result in
`docs/adrs/ADR-00NN-real-window-e2e-harness.md`.
*Acceptance:* the ADR records measured tree-walk times for both options on the 17-book corpus.

**T01.2: Project skeleton.** Create `tests/OgmaLibrary.Tests.E2E` (xUnit; Windows-only through
`[SkippableFact]` or an OS trait that **reports NOT ASSESSED rather than passing** on other
OSs). Reference no production assembly; the tests drive the built exe only. Build order: the
fixture resolves `src/OgmaLibrary.App/bin/Release/net10.0/OgmaLibrary.App.exe` or an
`OGMA_E2E_EXE` override.
*Acceptance:* `dotnet test tests/OgmaLibrary.Tests.E2E -c Release` runs one smoke test that
launches and closes the app.

**T01.3: App fixture.** `OgmaAppFixture` does the following:
- kills stray `OgmaLibrary.App`/`OgmaLibrary.Workers` processes that belong to earlier E2E runs
  (match on the data-dir argument only);
- creates `%TEMP%\ogma-e2e\<test-id>\{data,lib}`;
- launches with `OGMA_LIBRARY_DATA_DIR`, and optional feature environment variables until
  Phase 08 replaces them;
- waits for the main window with a 30 s timeout and records time-to-window;
- on dispose, closes with `WM_CLOSE`, waits ≤ 10 s, records exit time and orphaned workers, then
  kills if needed;
- collects `logs/` from the data dir (after Phase 02) into the test output.
*Acceptance:* 20 sequential launch/close cycles leave no stray processes.

**T01.4: Corpus generator.** Move `New-SyntheticCorpus.py` into `tests/fixtures/corpus/` as a
deterministic generator (fixed seed), or port it to C# using PdfSharp, which is already a
dependency, so CI does not need Python. It produces the audit corpus: 8 well-formed books with
embedded metadata, ISBNs and TOCs; an untitled file; a byte-identical duplicate; a
password-protected file; a truncated file; a non-PDF with a `.pdf` extension; a 0-byte file;
two image-only PDFs; a 900-page file; a deeply nested path; and a Unicode filename. Also
generate `expected.json` (titles, authors, valid ISBNs, words known to be on specific pages)
for the journey oracles.
*Acceptance:* two runs produce identical SHA-256 for every file.

**T01.5: Test-only root seeding hook.** The native folder picker exposes no "Select Folder"
button to UIA (measured), so journeys cannot reliably drive it. Add a **test hook that enters
the same production code path**: when the environment variable
`OGMA_E2E_PICK_FOLDER=<path>` is set **and** the build defines the `OGMA_E2E` symbol (Release
builds for CI E2E only, never shipped), `MainShellViewModel.ChooseFolderAsync`
(`src/OgmaLibrary.App/ViewModels/Catalogue/MainShellViewModel.cs:806`) uses a fake
`IStorageFolder` result instead of calling `OpenFolderPickerAsync`, and everything after the
picker runs unchanged. Keep one manual-assisted journey (G1b) that exercises the real dialog
through keyboard input, and document it as partially automated.
*Acceptance:* an architecture test asserts the symbol is absent from shipped configurations; G2
passes through the hook.

**T01.6: Visibility assertion.** `AssertVisiblyPainted(AutomationElement e)`:
1. `BoundingRectangle` is non-empty, within the window's client rectangle and `IsOffscreen == false`;
2. hit-test: `AutomationElement.FromPoint` at 5 sample points (centre and 4 inset corners)
   returns `e` or a descendant of `e`. This catches covering elements, as the K10 pager did;
3. pixel test: capture with `PrintWindow(PW_RENDERFULLCONTENT)` and require non-background
   variance inside the rectangle (for example at least 2 % of pixels differ by more than ΔE 10
   from the dominant colour). This catches unpainted content.

Add `AssertReachable(e)` (inside the window, enabled, invokable) for primary actions.
*Acceptance:* a unit test of the helper on a synthetic Avalonia window with an overlay fails as
expected.

**T01.7: AutomationIds.** Add `AutomationProperties.AutomationId` to the controls the journeys
use. Naming convention: `Shell.Nav.Library`, `Shell.Action.ChooseFolder`, `Catalogue.Grid`,
`Catalogue.Item.<BookId>`, `Detail.Action.Read`, `Reader.Next`, `Search.Box`,
`Search.Results`, `Advisor.Query`, `Advisor.Recommend`, and so on. Start with
`CatalogueShellView.axaml`, `CatalogueGridView.axaml`, `BookDetailView.axaml`,
`ReaderView.axaml`, `SearchPanelView.axaml` and `RecommendationPanelView.axaml`. Put the
readable `AutomationProperties.Name` on the `ListBoxItem` through an
`ItemContainerTheme`/style setter. Detecting K14 is in scope; changing wording is not.
*Acceptance:* a UIA dump shows every journey control with an id; a test fails if any
`ListItem` name starts with a C# record type name (`BookSummaryProjection {`, `SearchResultItem {`).

**T01.8: Golden journeys.** Each journey asserts visible outcomes, not just tree presence.

| ID | Journey | Oracle |
|---|---|---|
| G1 | First run: empty state visible, call to action reachable at both sizes | `AssertVisiblyPainted` on the heading and the Choose-folder button |
| G2 | Add library → scan → catalogue shows valid books with covers | Count equals the valid books in `expected.json`; ≥ 80 % of cards show a non-placeholder cover image |
| G3 | Select → detail inspector → Read → turn 50 pages → back to library | No crash; p95 page-turn under the Phase 04 budget; the page region is painted |
| G4 | Search: title, author, body phrase, typo, Unicode | Expected book in the top 3 for each `expected.json` query |
| G5 | Resume: close the app mid-book, relaunch, resume within 60 s (UX-007) | Same page restored; elapsed time recorded |
| G6 | Invalid files: 0-byte, non-PDF, truncated, password-protected | Not shown as normal "Indexed" books; a needs-attention indicator exists |
| G7 | Advisor: ask a question with AI unconfigured | A helpful, localised state with a route to Settings; no dead end |
| G8 | Resilience: kill the PDF worker during reading; corrupt `library-settings.json` | The app survives, shows a recoverable message and logs the event |

On `0ad3c0c`, G1, G2, G3, G6, G7 and G8 are **expected to fail**. Record them as the baseline;
never mark a baseline failure as skipped or expected-to-fail in code. The journey suite reports
"BASELINE-FAIL" through a documented results file, not through test attributes.

**T01.9: Evidence capture.** On every run, write
`artifacts/e2e/<run-id>/<journey>/{before,after,failure}-<size>.png`, the UIA dump JSON and
`timings.json`. On failure, also copy the app log folder.
*Acceptance:* artefacts are uploaded in CI.

**T01.10: CI job.** Add an `e2e-windows` job to `.github/workflows/ci.yml` on `windows-latest`:
build Release, generate the corpus, and run E2E at 1280×800 (set through the fixture with
`SetWindowPos`). Mark the job required once Phase 03 turns G1/G2 green.
*Acceptance:* the job runs on pull requests and publishes artefacts.

**T01.11: Documentation.** Add `docs/developer-guide/e2e-harness.md`: how to run one journey,
how to add a locator, how to read failures, and the flake policy.

**T01.12: Runner wrapper contract.** Later phases call one script, so its interface is fixed here.
Add `tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1`, which runs under Windows PowerShell 5.1
and under PowerShell 7 (macOS in Phase 27). It builds the `dotnet test` filter and run settings from
these parameters:

| Parameter | Meaning |
|---|---|
| `-All` | every journey and tag |
| `-Journey <id>` | `G1`..`G12`, or a named scenario trait (for example `HostStartStop`) |
| `-Tag <name>` | an xUnit `Trait("Tag", …)`, for example `Reader`, `Search`, `Catalogue`, `Detail`, `Settings`, `Visual`, `Ai`, `SemanticSearch`, `Shelf3D` |
| `-Sizes 1280x800,1920x1080` | window sizes (default both); values below the 860 px `MinWidth` are rejected |
| `-Themes Light,Dark` | theme matrix (default Light) |
| `-Culture en\|fr\|qps-ploc` | UI culture, including pseudo-localisation |
| `-KeyboardOnly` | drive through keyboard input only; fail on any pointer action |
| `-UiaAudit` | fail on unnamed or record-dump accessible names (T01.7 rule) |
| `-TextScale 100\|200` | OS text-scaling scenario |
| `-ClippingCheck` | fail when localised text is truncated or clipped |
| `-UseMockAiServer` | start the local mock AI provider used by Phases 15–16 |

Parameters a journey does not support are reported as `NOT ASSESSED` for that journey, never as
passes. G9–G12 are registered here as placeholders that report `NOT ASSESSED` until their
owning phase (11, 10, 19–20, 18) implements them.
*Acceptance:* `Invoke-GoldenJourneys.ps1 -Journey G1 -Sizes 1280x800` and `-Tag Reader` each run the
expected subset; `-Sizes 800x600` is rejected with a clear message.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Green tests, broken window (K05) | No real-window oracle | E2E harness with visibility assertion | Rendering regressions are caught before merge | K10 undetected → detected by G1/G2 | baseline run | Flaky UIA timing | Retry policy on waits only, never on assertions |
| Unreadable accessible names (K14) | Name set on inner Border | Name on container; test guard | Screen readers hear titles | 17 record dumps → 0 | UIA dump | Duplicate names | Keep HelpText for author |
| Picker not automatable | Native dialog lacks UIA button | Compile-time test hook after the picker | Journeys stay on production path | manual only → automated G2 | hook test | Hook ships by accident | Architecture test on symbol |

## 8. Test plan

- **Helper unit tests:** `AssertVisiblyPainted` against a synthetic overlay, a clipped element
  and an unpainted panel.
- **Baseline:** run G1–G8 on `0ad3c0c` with the pager fix reverted; confirm the failures match
  K10, K11, K13, K14, K20, K21, K30, K50.
- **Patched check:** apply `evidence/tools/root-cause-pager-overlay.patch` locally; G1 and G2
  visibility assertions must flip to pass. This proves the oracle detects K10.
- **Flake:** run the suite 5 times; any non-deterministic result gets an issue, an owner and a
  deadline.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln -c Release
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --logger "trx;LogFileName=e2e.trx"
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G1|Journey=G2"
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| macOS real-window automation | Engineering (Phase 27) | Mac journeys remain NOT ASSESSED until then |
| Hosted-runner GPU/DPI differences for pixel tests | Engineering | Pixel thresholds are tuned on CI; a failure there is a harness defect |
| Real folder picker (G1b) | Engineering | Partially automated; manual confirmation recorded per wave |

## 11. Risks and mitigations

- *Pixel assertions are brittle across themes and DPI.* Use relative variance, not golden
  images, for "painted" checks; keep golden-image comparison for Phase 09 only.
- *UIA tree walks are slow on large catalogues.* Scope searches to container ids; cap corpus
  size in E2E (large-scale work belongs to Phase 24).
- *Test hook abuse.* Guard with the compile-time symbol and an architecture test.

## 12. Execution prompt

```
## Prompt 01 - Build the real-window acceptance harness
You are building a Windows UI Automation end-to-end harness for Ogma Library in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K05, K10, K11, K14)
5. docs/plans/sept-23-kaizen/phases/phase-01-real-window-acceptance-harness.md
6. docs/plans/sept-23-kaizen/evidence/tools/Invoke-OgmaUia.ps1 and New-SyntheticCorpus.py (the prototype)
Load skills (read SKILL.md): windows-desktop-e2e-testing, advanced-testing-strategy, avalonia-desktop-development,
click-path-audit, and design-system-skills/governance/design-quality-gate.md.
Work plan: A. T01.1 ADR, T01.2 skeleton, T01.3 fixture (serial). B. T01.4 corpus, T01.6 assertion, T01.7 ids in parallel.
C. T01.5 hook, T01.8 journeys, T01.9 evidence, T01.10 CI, T01.11 docs.
File scope: tests/OgmaLibrary.Tests.E2E/**, tests/fixtures/corpus/**, AutomationId/Name attributes in the listed .axaml
files, the guarded hook in MainShellViewModel.ChooseFolderAsync, .github/workflows/ci.yml, docs/adrs, docs/developer-guide.
Do not fix product defects here. Record baseline failures as data, not as skipped tests.
Acceptance: section 9 commands; baseline evidence saved; G1/G2 proven to detect K10 with the patch applied locally.
Recovery point: the last Phase 00 commit.
```
