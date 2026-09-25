# Phase 02: Crash safety, logging and threading foundation

## 1. Header

| Field | Value |
|---|---|
| Wave | A: Stabilise |
| Size | 4–6 engineering days |
| Depends on | Phase 00 (Phase 01 is recommended so G8 can prove the result) |
| Owner decisions | none |
| Primary defects | K31 (no safety net), K07 (no logging), K72 (off-UI-thread mutation), K70 (no single-instance guard) |
| Requirement IDs | NFR reliability and recoverability; CTRL audit and diagnostics controls; UX-002 (error states) |

## 2. Why this phase exists

During the audit, turning pages made the whole application vanish (K30). An `IOException` from
the PDF worker pipe escaped an `async void` click handler, and nothing in the process caught it.
There are 126 `async void` methods in `src`. Nothing handles `AppDomain.UnhandledException`,
`TaskScheduler.UnobservedTaskException` or dispatcher exceptions (K31). No component uses
`ILogger`, and 24 catch blocks swallow errors silently (K07). So when something fails in the
field, there is nothing to read afterwards.

View models are also changed from thread-pool threads after `ConfigureAwait(false)` (K72), and a
second copy of the app can start and run migrations and workers against the same database (K70).

This phase does not fix individual features. It makes the process survive failures, report
them, and leave a trail. Every later phase builds on it.

## 3. Objectives and exit criteria

1. No exception raised from a UI event handler, command or fire-and-forget task terminates the
   process. Each one is logged, and the user sees a localised, dismissible message where
   appropriate. G8 passes.
2. Structured logs are written to `<DataDirectory>/logs/ogma-YYYYMMDD.log` (rolling, 7 files,
   10 MB each), with redaction of paths inside the library, book titles, provider keys and
   personal data. *Export diagnostics* includes the logs.
3. No `async void` remains in `src/OgmaLibrary.App` except framework-mandated overrides that
   are wrapped by the safe-invoke helper. An analyzer or architecture test enforces this.
4. Every view-model property and bound collection mutation runs on the UI thread. A debug-build
   guard throws on violations, and the E2E suite runs with the guard on.
5. A second launch activates the existing window and exits within 2 s, without touching the
   database.
6. None of the 24 silent catch blocks remains silent: each logs at the correct level or is
   documented as intentionally ignored.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`: async work off the UI thread, dispatcher rules, compiled bindings.
- `C:\wamp64\www\chwezi-dev-engine\skills\languages\csharp-dotnet-development\SKILL.md`: async and cancellation conventions.
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\observability-monitoring\SKILL.md`: structured logging, event naming, redaction.
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\reliability-engineering\SKILL.md`: failure-mode tables and graceful degradation.
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\systematic-bug-diagnosis\SKILL.md`: reproduce, isolate and prove.
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`: the wording of the crash and recovery messages.

## 5. Scope in / scope out

**In:** global handlers; the safe-invoke pattern and its migration across views; logging
infrastructure and redaction; the UI-thread guard; the single-instance guard; converting silent
catches; a user-facing error surface (toast or banner) and a crash-recovery notice on next launch.

**Out:** feature-level fixes such as the reader engine (Phase 04), scan state (Phase 05) and
the worker loop (Phase 06), except where a handler rethrows into `async void` today.

## 6. Work breakdown

**T02.1: Global safety net.** In `src/OgmaLibrary.App/Program.cs` (before
`BuildAvaloniaApp()`) and `App.axaml.cs` (`OnFrameworkInitializationCompleted`, line 28):
- `AppDomain.CurrentDomain.UnhandledException`: log `Critical`, write a crash marker file
  `<data>/logs/last-crash.json` (timestamp, exception type, redacted stack, app version), and
  flush the logs;
- `TaskScheduler.UnobservedTaskException`: log `Error`, then `e.SetObserved()`;
- `Dispatcher.UIThread.UnhandledException` (Avalonia 11): log, show the error surface, and set
  `e.Handled = true` unless the exception is fatal (`OutOfMemoryException`,
  `StackOverflowException`, `AccessViolationException`);
- on the next launch, if `last-crash.json` exists, show a one-time localised notice: "Ogma
  closed unexpectedly last time. Diagnostics were saved." with actions *Export diagnostics* and
  *Dismiss*.

*Acceptance:* a hidden test command (`OGMA_E2E` builds only) that throws on the UI thread leaves
the app alive and writes a log entry.

**T02.2: Safe-invoke pattern.** Add `src/OgmaLibrary.App/Infrastructure/UiActions.cs`:

```csharp
public static class UiActions
{
    // Wraps an async UI action: runs on the UI thread, catches, logs, reports to IUserNotifier.
    public static async void Run(Func<Task> action, string operation,
        [CallerMemberName] string caller = "") { ... }
}
```

Also add an `AsyncRelayCommand` equivalent with `CanExecute`, re-entrancy protection
(`IsRunning`) and the same error policy. Migrate every `async void` in the 19 files listed by
`grep -rn "async void" src/OgmaLibrary.App` (for example
`Views/Reader/ReaderView.axaml.cs:117` `NextButton_Click`,
`Views/Catalogue/CatalogueGridView.axaml.cs:22`, `Views/DesktopShellWindow.axaml.cs:28,36,71`,
`Views/Catalogue/CatalogueShellView.axaml.cs`, `Views/Search/*`,
`Views/Settings/SharingSettingsView.axaml.cs`) to either commands bound in XAML or
`UiActions.Run(() => vm.XAsync(), "reader.next")`. Remove the `throw;` rethrows that feed
`async void` today (`ViewModels/Reader/ReaderViewModel.cs:951,956,987`,
`ViewModels/Search/IndexManagerViewModel.cs:435,480`) in favour of a returned result or a
reported error state.
*Acceptance:* an architecture test fails if any `async void` method in `OgmaLibrary.App` is not
an event handler whose body is a single `UiActions.Run(...)` call.

**T02.3: Logging infrastructure.** Add `Microsoft.Extensions.Logging` (version aligned with the
existing `Microsoft.Extensions.DependencyInjection` 10.0.x) and a small in-repo rolling file
provider in `OgmaLibrary.Infrastructure/Diagnostics/` (JSON lines; no new third-party sink
unless an ADR approves it). Register it in the composition root only. Event names follow
`area.action.outcome` (for example `reader.render.failed`, `scan.completed`, `job.retry.scheduled`).
Keep a redaction policy class with unit tests: library-relative paths become `<lib>/…/<hash8>.pdf`,
titles are omitted, secrets are never logged, and user home paths become `~`. Library
assemblies receive `ILogger<T>` through DI. Domain stays logger-free.
*Acceptance:* one scan plus one reader session on the corpus produces a log with no title
strings (grep test against `expected.json` titles).

**T02.4: Convert silent catches.** Visit the 24 silent `catch` blocks listed in the inventory
report (for example `MetadataExtractionService.ExtractFields`, `OcrWorker.cs:42-45` and
`PdfWriteBackService.cs:668`). Log with context, or add a comment
`// Intentionally ignored: <reason>` plus a `Debug`-level log.
*Acceptance:* a grep-based test finds no `catch { }` or `catch (Exception) { }` without a log call or the marker.

**T02.5: UI-thread discipline (K72).** Introduce `IUiDispatcher` (Application layer interface,
App implementation over `Dispatcher.UIThread`) and marshal mutations in the known offenders:
- `CatalogueViewModel.LoadAsync` (`ViewModels/Catalogue/CatalogueViewModel.cs:260-295`):
  `LibraryRootPath`, `IsLoading` and `_allItems` are mutated after `ConfigureAwait(false)`;
- `Bookshelf3DViewModel` `Books.Add`;
- `RecommendationPanelViewModel` and `ReadingPlanViewModel` result updates;
- `HostSharingViewModel` status updates;
- `MainShellViewModel.InitializeAsync` catalogue load (line ~214).

Make `CatalogueViewModel` refresh single-flight: coalesce concurrent refreshes, cancel the
superseded one, and build the list off-thread then swap it on the UI thread. In Debug and
`OGMA_E2E` builds, `ObservableObject.OnPropertyChanged` asserts `Dispatcher.UIThread.CheckAccess()`.
*Acceptance:* the E2E suite runs clean with the assertion enabled; a unit test runs 20
concurrent refreshes and checks the result has no duplicates.

**T02.6: Single-instance guard (K70).** In `Program.Main`, before Avalonia starts, create a
named `Mutex` scoped to the user and the data directory:
`Local\OgmaLibrary-<sha256(DataDirectory)[..16]>`. If it is already held, send an activation
message over a named pipe of the same name (the payload may carry a PDF path for future "Open
with") and exit with code 0. The first instance listens on the pipe and calls
`Activate()`/`Topmost` pulse on the main window. On macOS, use a lock file with
`FileShare.None` in the data directory.
*Acceptance:* E2E launches the app twice; the second process exits in under 2 s; the database
file shows no second writer; the first window comes forward.

**T02.7: Error surface.** Add a non-modal notification area (a stack of up to 3 dismissible
toasts) in `DesktopShellWindow.axaml`, bound to `IUserNotifier`. Messages are localised (en/fr)
with an optional action (Retry, Open settings, Export diagnostics). Raw `ex.Message` is never
shown (see K81 and `MainShellViewModel.cs:220`).
*Acceptance:* the resilience journey G8 shows the toast.

**T02.8: Startup failure screen.** `App.axaml.cs:83-107` shows "Correct the application
settings" for any failure. Classify failures (configuration, database locked or corrupt,
migration, unknown) and offer Retry where it is safe. Retry reruns composition, not just the
startup tasks.
*Acceptance:* a unit test for each class; E2E with a read-only data dir shows the right message.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| App terminates on handler error (K31) | `async void` + no global handlers | Safety net + UiActions | Failures become recoverable | 1 crash per ~20 page turns → 0 process exits in G3/G8 | E2E logs, WER events | Masking fatal state | Fatal types still terminate |
| No field diagnostics (K07) | No logging | Rolling redacted log | Faults diagnosable | 0 log lines → every journey logged | log files | Privacy leak | Redaction tests; disable per category |
| Cross-thread mutation (K72) | ConfigureAwait(false) then bind | IUiDispatcher + guard | No cross-thread exceptions | unknown → 0 guard trips in E2E | guard logs | Deadlocks | Post, never Invoke-wait |
| Double instance (K70) | No guard | Mutex + pipe activation | Single writer | 2 writers → 1 | E2E | Stale mutex after crash | Mutex is released by the OS on process death |

## 8. Test plan

- **Unit:** redaction policy; `UiActions` error routing; single-flight refresh; failure
  classification.
- **Architecture:** no unwrapped `async void`; no `ILogger` in Domain; the logging provider is
  registered only in the composition root.
- **E2E (Phase 01):** G8 (kill the worker mid-read; corrupt settings); double launch; the
  injected UI exception.
- **Negative:** a disk full or read-only log folder must not crash the app (the logger falls
  back to trace).

## 9. Acceptance commands

```powershell
./scripts/Test-Fast.ps1
dotnet test tests/OgmaLibrary.Tests.Architecture -c Release --no-build
dotnet test tests/OgmaLibrary.Tests.E2E -c Release --no-build --filter "Journey=G3|Journey=G8|Category=SingleInstance"
Get-ChildItem "$env:TEMP\ogma-e2e\*\data\logs\*.log" | Select-String -Pattern (Get-Content tests/fixtures/corpus/expected-titles.txt) # expect no matches
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| macOS lock-file single-instance behaviour | Engineering (Phase 27) | NOT ASSESSED until run on a Mac |
| Crash-dump collection (WER LocalDumps) policy | Owner/Engineering | Optional; not required for this phase |

## 11. Risks and mitigations

- *Swallowing exceptions hides bugs.* Every caught exception is logged at Error with an event
  id, and the E2E runner fails a journey when an Error-level entry appears that the journey did
  not expect.
- *Large mechanical migration.* Migrate one view per commit, and run the E2E suite after each.
- *Logging slows hot paths.* Use `LoggerMessage` source generators for high-frequency events.

## 12. Execution prompt

```
## Prompt 02 - Make the process survive and report failures
You are adding crash safety, structured logging, UI-thread discipline and a single-instance guard to Ogma Library
in C:\wamp64\www\Ogma-Library.
Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md
4. docs/plans/sept-23-kaizen/03-defect-register.md (K07, K30, K31, K70, K72)
5. docs/plans/sept-23-kaizen/phases/phase-02-crash-safety-logging-threading.md
Load skills (read SKILL.md): avalonia-desktop-development, csharp-dotnet-development, observability-monitoring,
reliability-engineering, systematic-bug-diagnosis, error-empty-and-system-messaging.
Work plan: A. T02.1, T02.3 (serial foundation). B. T02.2 migration, one view per commit; T02.4; T02.5.
C. T02.6, T02.7, T02.8.
File scope: src/OgmaLibrary.App/**, src/OgmaLibrary.Infrastructure/Diagnostics/**, silent-catch sites listed by the
inventory, localization resources, tests. Domain must stay free of logging.
Acceptance: section 9 commands pass; G3 and G8 show no process exit; record in
docs/implementation/execution/phase-sept23-02-completion.md.
Never hide a failure without logging it. Recovery point: the last Phase 01 commit.
```
