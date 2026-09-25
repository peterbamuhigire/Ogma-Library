# Real-window E2E harness

`tests/OgmaLibrary.Tests.E2E` drives the real `OgmaLibrary.App.exe` through Windows UI Automation
(FlaUI UIA3, [ADR-0017](../adrs/0017-real-window-e2e-harness.md)). It exists because green
headless tests once hid an invisible catalogue for three weeks (K05, K10). A behaviour change is
not done until the golden journeys pass in the real window at 1280×800 and 1920×1080
([AGENT_BRIEF](../plans/sept-23-kaizen/AGENT_BRIEF.md)).

## Requirements

- Windows 10/11 with an interactive desktop session (not a service or a locked screen). Keep
  other windows off the top-left of the primary screen; the harness puts the app there as topmost.
- .NET SDK from `global.json`, and Python 3 with `pip install pymupdf pypdf` for the corpus.
- Nothing touches your real library: every test gets `%TEMP%\ogma-e2e\<run-id>\<test>\{data,lib}`
  as `OGMA_LIBRARY_DATA_DIR` and library folder, deleted afterwards (`OGMA_E2E_KEEP=1` keeps it).

## Run journeys

Always use the runner; it builds the E2E copy of the app and applies the parameters.

```powershell
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -All -Sizes 1280x800,1920x1080
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey G1 -Sizes 1280x800
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Reader -NoBuild
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -ReportOnly -RunId <run-id>
```

| Parameter | Meaning |
|---|---|
| `-All` / `-Journey G1,G2` / `-Tag Reader` | What to run. Journeys are `G1`..`G12` or named scenarios (`A11yNames`, `Smoke`, `LaunchCycles`, `Harness`, `HostStartStop`). |
| `-Sizes` | Outer window sizes; below 860×560 is rejected. |
| `-Themes Light,Dark` | Dark is seeded through `user-preferences.json`. |
| `-Culture`, `-TextScale`, `-KeyboardOnly`, `-ClippingCheck`, `-UseMockAiServer` | Reported NOT ASSESSED until their owning phases (22, 21, 21, 22, 15–16) add support. |
| `-UiaAudit` | Fails a journey on unnamed controls or record-dump names on its final screen. |
| `-NoBuild`, `-Exe <path>` | Reuse the last build, or drive another build (for example an older commit). |
| `-Strict` | Exit 1 on BASELINE-FAIL and NOT ASSESSED too. |

The runner builds the app with `dotnet build src/OgmaLibrary.App -c Release -p:OgmaE2EHooks=true
-o artifacts/e2e/app`. That property defines `OGMA_E2E`, which compiles the folder-picker test
hook (`OGMA_E2E_PICK_FOLDER`). Shipped builds never set it; `Phase01E2EHookArchitectureTests`
enforces this. Plain `dotnet test tests/OgmaLibrary.Tests.E2E` also works against
`src/OgmaLibrary.App/bin/Release/net10.0` or `OGMA_E2E_EXE`; without the hook, journeys drive the
real folder dialog.

The fast suite (`scripts/Test-Fast.ps1`) and the CI `build-test` job exclude `Category=E2E`. The
`e2e-windows` CI job runs the suite at 1280×800 and uploads `artifacts/e2e/`; it is informational
(`continue-on-error`) until G1/G2 are green on the hosted runner.

## Read the results

Each run writes `artifacts/e2e/<run-id>/`:

- `results.md` and `results.json`: one row per journey × size, labelled **PASS**, **FAIL** (new),
  **BASELINE-FAIL** (a known defect in `baseline.json` whose message pattern matches),
  **PASS (baseline cleared)** (remove the baseline entry in your phase) or **NOT ASSESSED**.
- `<journey>/before-<size>.png`, `after-<size>.png`, `failure-<size>.png`, extra named shots.
- `<journey>/uia-<label>-<size>.json`: the UIA tree (type, name, id, enabled, offscreen, bounds).
- `<journey>/timings-<test>-<size>.json`: time-to-window, marks, measurements, close result.
- `<journey>/logs-failure-<size>/`: the app's JSON-lines log when a journey fails.
- `e2e.trx`: the raw xUnit results. NOT ASSESSED journeys fail in the raw runner on purpose, so
  they can never be mistaken for passes.

`AssertVisiblyPainted` failures print the measured facts: bounds against the client area, the five
hit-test points (`own`, `ancestor …`, or `COVERED by …`) and the painted-pixel share. `COVERED by`
names the element on top; `painted=0.0%` means nothing is drawn there.

## Add a journey or a locator

1. Give the control a stable id in AXAML: `AutomationProperties.AutomationId="Area.Kind.Name"`
   (`Shell.Nav.Library`, `Catalogue.Grid`, `Detail.Action.Read`, `Reader.Next`). Put readable names
   for list items on the `ListBoxItem` through a container style, never on an inner `Border`.
   Avalonia exposes buttons, text, list items, edits, images and user controls to UIA; plain
   `Border` and `Panel` elements are not in the tree, so do not put ids there.
2. Locate by id (`Uia.WaitFor(window, "Catalogue.Grid")`), never by text or coordinates.
3. Write a `[Theory]` with `[MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]`
   and the traits `Category=E2E`, `Journey=<id>` and one or more `Tag` values, in the
   `RealWindowTests` collection. Wrap the body in `Journey.Run(...)` so evidence and results are
   recorded.
4. Assert what the user sees: `Visibility.AssertVisiblyPainted`, `Visibility.AssertReachable`,
   counts and names from the UIA tree, and the app log. Tag failure messages with the defect id
   (`[K21]`) so the baseline can match them.
5. If the journey cannot honour a run parameter, declare it in `JourneySupport`; it is reported
   NOT ASSESSED.

## Flake policy

- Retries are allowed on **waits** only (`Uia.WaitFor`, `Uia.Poll`, `Shell.WaitForCatalogue`),
  never on assertions. Transient UIA errors (tree changed, busy provider) are retried inside
  waits and nowhere else.
- No fixed sleeps as synchronisation, except short settle delays after resizing and the documented
  pause before closing in G5.
- A journey that gives different results on the same build is a harness defect: open an issue with
  an owner and a deadline, and keep it in the baseline only with a message pattern that matches the
  observed failure.
- Run the suite five times before declaring a new journey stable.

## Known limits

- The native folder dialog exposes no *Select Folder* button to UIA. The harness brings the dialog
  to the foreground, types the path and clicks the button at its standard offset (G1b/G2). If the
  desktop is locked or another window holds the foreground, that step fails; the hook route
  (other journeys) does not need focus.
- Page-turn latency is measured to the page-number change through UIA, which adds round-trip time;
  it is an upper bound on what the user sees.
- macOS automation is NOT ASSESSED until Phase 27.
