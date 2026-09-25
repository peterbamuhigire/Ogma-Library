# ADR 0017: Drive the real window with FlaUI UIA3 for end-to-end journeys

## Status

Accepted on 2026-09-25 (Sept-23 Kaizen Phase 01, task T01.1). Engineering decision under the
owner's delegated authority; no owner decision was required by the plan.

## Date

2026-09-25

## Context

For three weeks every user saw a blank catalogue and an invisible first-run call to action (K10)
while 1,160 automated tests passed (K05). The headless Avalonia tests check view models and
controls in isolation; nothing checked that the real window paints its content. The Sept-23 audit
found K10–K14, K20, K21, K30, K40, K41 and K50 with a prototype PowerShell driver built on the
raw `System.Windows.Automation` client. Phase 01 turns that prototype into a maintained harness,
so the automation library must be chosen.

The harness drives `OgmaLibrary.App.exe` (Avalonia 11.3 on Windows) from outside the process. It
has to walk the UIA tree, activate controls, read values, hit-test points, and reach the native
`#32770` folder dialog the app opens.

## Decision drivers

- Compatibility with Avalonia's automation peers (Invoke, Toggle, SelectionItem, Value,
  ExpandCollapse, ScrollItem).
- Tree-walk speed on the real window with the 17-file audit corpus.
- Access to owned native dialogs (the folder picker).
- Maintenance and licence: open source, compatible with the repository's licence, current on
  .NET 10.
- Honest reporting in CI (`windows-latest`) and a fallback where the library falls short.

## Considered options

1. **Raw `System.Windows.Automation` (UIA2 managed client)**, as in the prototype.
   - Pro: ships with the Windows Desktop framework; no package.
   - Con: the managed UIA2 client is slower (measured below) and in maintenance mode; no
     helpers for waits, input or screenshots; needs `UseWPF` in the test project.
2. **FlaUI UIA3 (`FlaUI.UIA3` 5.0.0, MIT)** over the native COM UIA3 API.
   - Pro: faster tree walks (measured), typed pattern access, keyboard and mouse input helpers,
     condition factories, `FromPoint`, raw-view tree walkers, configurable connection and
     transaction timeouts. MIT licence; maintained; targets `net8.0-windows` and runs on .NET 10.
   - Con: a new test-only dependency (FlaUI.Core, Interop.UIAutomationClient, System.Drawing.Common,
     System.Management and related Windows packages, recorded in the E2E project's lock file only).
3. **Appium / WinAppDriver.** Rejected: WinAppDriver is unmaintained and needs Developer Mode and a
   separate server; nothing it offers is needed here.

## Measurements (MEASURED, 2026-09-25)

Windows 11 Pro 10.0.26200, 2560×1440 at 96 DPI, Release build of the app with the E2E hook, the
17-file synthetic corpus scanned, catalogue grid showing 17 cards, window 1180×760. Both clients
ran in the same process against the same window; median of 7 runs.

| Operation | FlaUI UIA3 | Raw System.Windows.Automation |
|---|---|---|
| `FindAllDescendants` on the main window (141 elements) | **333.5 ms** | 453.8 ms |
| First descendant by AutomationId (`Shell.Status`) | **324.8 ms** | 357.8 ms |
| Patterns on a catalogue `ListItem` | SelectionItem, ScrollItem (+ LegacyIAccessible) | SelectionItem, ScrollItem |
| Patterns on a toolbar `Button` / the More `DropDownButton` | Invoke, ScrollItem | — (not measured) |

Both clients see the same 141 elements and the same patterns, so Avalonia peer compatibility is
equal. The Avalonia provider itself costs about 2–3 ms per element; the harness therefore scopes
searches to container ids and never walks the tree in a tight loop. Neither client exposes a
*Select Folder* button in the native folder dialog; the harness uses a keyboard-and-offset
technique (see below). The measurement program is described in the Phase 01 completion record.

## Decision outcome

Use **FlaUI UIA3** in a new test project, `tests/OgmaLibrary.Tests.E2E` (xUnit, `net10.0-windows`,
no reference to any production assembly). Fall back to Win32 calls for what UIA does not expose:
`PrintWindow(PW_RENDERFULLCONTENT)` for captures, `SetWindowPos` for sizing, `WM_CLOSE` for
shutdown, and foreground, keyboard and a bottom-right offset click for the folder dialog's
*Select Folder* button.

Because that dialog cannot be driven through UIA alone, E2E builds (`-p:OgmaE2EHooks=true`,
defining `OGMA_E2E`) also compile a test-only hook: with `OGMA_E2E_PICK_FOLDER` set, the folder is
passed to the same `ChooseFolderAsync` code that runs after the dialog. G2 always uses the real
dialog; other journeys use the hook when the build has it. Architecture tests keep the symbol out
of shipped configurations.

## Consequences

### Positive

- Every later phase proves behaviour in the real window through one runner,
  `tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1`.
- `AssertVisiblyPainted` combines bounds, hit tests and pixel variance, and was shown to fail on
  the K10 nesting and pass on the fixed shell.

### Negative

- New test-only packages (FlaUI and its Windows dependencies) must be kept current and
  vulnerability-scanned with the rest of the solution.
- Journeys need an interactive Windows desktop; macOS remains NOT ASSESSED until Phase 27.
- The folder-dialog technique depends on foreground rights and the dialog's standard layout.

### Affects

- Sept-23 Phase 01 (T01.1–T01.12), K05, K10, K14; every later phase's real-window proof.
- `src/OgmaLibrary.App/OgmaLibrary.App.csproj` (the `OgmaE2EHooks` opt-in),
  `MainShellViewModel.ChooseFolderAsync`, `OgmaLibrary.Tests.Architecture`
  (`Phase01E2EHookArchitectureTests`), `.github/workflows/ci.yml` (`e2e-windows`).
- ADR-0002 (Avalonia shell): automation peers are part of the shell's testable surface.
