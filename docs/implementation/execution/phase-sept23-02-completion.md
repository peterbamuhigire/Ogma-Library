# Sept-23 Phase 02 completion: crash safety, logging and threading foundation

Status: **IMPLEMENTED; journey proof pending Phase 01** (2026-09-25). The code, unit,
architecture and real-window checks below pass. G3 and G8 cannot run until the Phase 01 journey
harness exists, so the Definition of Done is not yet met (see NOT ASSESSED).
Rollback point: `81f178f` (the last commit before any Phase 02 change).
Plan: [phase-02](../../plans/sept-23-kaizen/phases/phase-02-crash-safety-logging-threading.md).
Defects: K07, K30 (safety-net half; the reader fix is Phase 04), K31, K70, K72; K81 in part.

## Tasks

| Task | Result | Commit |
|---|---|---|
| Gate repair | Five LAN host tests from `ec61054` failed `dotnet format --verify-no-changes`; whitespace fixed | `6546729` |
| T02.3 Logging | In-repo JSON-lines rolling sink (`ogma-YYYYMMDD[.N].log`, 10 MB × 7), `LogRedactor`, crash marker, diagnostics zip, `InfrastructureLog`. Logging comes from the ASP.NET Core shared framework Infrastructure already references: **no package or lock-file change** (adding `Microsoft.Extensions.Logging.Abstractions` fails restore with NU1510). Events are source-generated (`area.action.outcome`). Framework `Microsoft.*`/`System.*` categories are filtered to Warning. | `63bf84a` |
| T02.1 Safety net | `AppDomain.UnhandledException` (Critical + `last-crash.json` + flush), `TaskScheduler.UnobservedTaskException` (Error + `SetObserved`), `Dispatcher.UIThread.UnhandledException` (log, toast, `Handled` unless OOM/SOE/AV). One-time crash notice with *Export diagnostics*. Hidden commands: `OGMA_E2E=1` + `OGMA_E2E_INJECT_UI_FAULT=1` or `OGMA_E2E_INJECT_CRASH=1`. | `b6ea941`, `ea9f1c6` |
| T02.6 Single instance | `Local\OgmaLibrary-<sha256(dataDir)[..16]>` mutex (lock file off Windows) acquired in `Program.Main` before Avalonia; second launch sends `OGMA-ACTIVATE/1` (+ optional PDF path) over a current-user-only named pipe and exits 0; primary restores, activates and pulses `Topmost`. | `b6ea941` |
| T02.7 Error surface | Toast stack (max 3, repeat-coalescing, en/fr, Retry / Open settings / Export diagnostics) in `DesktopShellWindow`, bound to `NotificationCenterViewModel : IUserNotifier`. Tokens only (`Brush.Accent.Clay` error, `Brush.Accent.Slate` other; Public Sans body via the theme). | `b6ea941` |
| T02.8 Startup failures | `StartupFailureClassifier`: configuration, database locked, database corrupt, migration, storage unavailable, unknown; localised en/fr messages; Retry reruns the whole composition (not offered for a corrupt database). Replaces the hard-coded English "Correct the application settings" message. | `b6ea941` |
| T02.2 Safe-invoke | `UiActions.Run`/`RunAsync`, `AsyncRelayCommand` (re-entrancy guard, `IsRunning`), `RelayCommand`. All **126** `async void` methods in 19 files migrated (reader 35, catalogue 36, search 12, sharing 21, advisor/classroom/3D 19, shell window 3). `ReaderViewModel.OpenAsync` and `IndexManagerViewModel` rebuild/erase no longer rethrow into handlers. | `5c07950`, `6b6d86c`, `594857d`, `2338122` |
| T02.4 Silent catches | All 21 empty broad catches now log (`InfrastructureLog`/`AppLog`) or carry `// Intentionally ignored:`; includes `PdfWriteBackService` audit save, `MetadataExtractionService.ExtractFields`, `OcrWorker` (logs `job.retry.scheduled`). The PDF-worker adapter and Reader `CitationService` have no logger, so they carry the marker only. | `51f387a` |
| T02.5 UI thread | `IUiDispatcher` (Application) + `AvaloniaUiDispatcher`; `CatalogueViewModel.LoadAsync` single-flight (cancel superseded, coalesce queued, build off-thread, swap on UI thread); `MainShellViewModel.InitializeAsync` stays on the UI context and no longer shows raw `ex.Message`; Bookshelf3D, Recommendation, ReadingPlan and HostSharing continue on the UI context before bound updates. `UiThreadGuard.Verify` in every view model's `OnPropertyChanged` (Debug throws, `OGMA_E2E=1` logs `ui.thread.violation`, otherwise off). | `4b28757` |
| Tests | Architecture: no `async void` outside `UiActions.Run`, no silent broad catch, logger-free Domain, sink composed only in the root, `Program.Main` order. Unit: redaction, sink, crash marker, bundle, UiActions, commands, handlers, classifier (en/fr), toasts, single instance. UI: 20 concurrent refreshes. | `ffa8276` |

## Measured results (Windows 11, SDK 10.0.401, Release)

| Check | Before (`81f178f`) | After |
|---|---|---|
| `async void` in `src/OgmaLibrary.App` | 126 (CODE) | 1 (`UiActions.Run`), enforced by an architecture test |
| Empty broad catches without log or marker | 21 (CODE scan) | 0, enforced by an architecture test |
| `ILogger` usage / log files | none | `<data>/logs/ogma-YYYYMMDD.log`, JSON lines |
| 20 concurrent `CatalogueViewModel.LoadAsync` on 60 books | **1,192** entries (test red) | 60, no duplicates (test green) |
| Injected UI-thread fault (real window, 1280×800) | process terminates (K30 class) | app alive; `app.dispatcher.recovered` logged; toast shown with *Export diagnostics* / *Dismiss* |
| Injected background-thread crash | exit, no trace | `app.crash.unhandled` logged; `last-crash.json` written; next launch shows the one-time notice (1920×1080); *Export diagnostics* wrote `diagnostics/ogma-diagnostics-*.zip`; marker consumed |
| Second launch, same data directory | second full app, migrations, workers | exit 0 in **1,111 ms**; one writer pid in the log; primary logged `app.instance.activation.received` |
| Close | — | `remaining=` (no App or Worker process left) |
| User home path in the real-window log | — | absent |
| `ui.thread.violation` in the real-window runs (guard in Log mode) | — | 0 (startup, fault, crash, notice paths only) |
| Build / format / analyzers / vulnerable / accountability | — | 0 warnings / 0 changes / 0 / none / pass |
| Fast suite | 1,149 | see *Gates* below |

Evidence: [`evidence/sept-23-kaizen/phase-02/`](evidence/sept-23-kaizen/phase-02/) — toast and
crash-notice screenshots and the redacted real-window log.

Finding for K71 (MEASURED): on a first run the UI-thread fault posted 3 s after launch executed
~11 s later, because catalogue migration runs synchronously on the UI thread through
`StartupShellViewModel.StartAsync`. K71 (Phase 24) already owns this; no new K ID.

## Gates

`dotnet restore --locked-mode` pass; `dotnet format --verify-no-changes` 0; Release build 0
warnings; `dotnet format analyzers --severity warn` 0; no vulnerable packages;
`Test-RequirementAccountability.ps1` pass (101 FRs, 29 NFRs, 32 controls); `Test-Fast.ps1`:
Architecture 47/47, core 988/988, UI 165/165 (1,200 tests, 0 failures).

## Deviations from the plan

- The plan asked for `Microsoft.Extensions.Logging` as a new package. It is already supplied by
  the ASP.NET Core shared framework referenced by Infrastructure; NuGet rejects the explicit
  reference (NU1510), so none was added and the lock files are unchanged.
- The async-void rule is stricter than written: no `async void` at all except `UiActions.Run`;
  handlers are plain `void` methods delegating to `UiActions.Run(() => XAsync(...), "area.op")`.
- There is no shared `ObservableObject`; the guard is a one-line `UiThreadGuard.Verify` call in
  each view model's `OnPropertyChanged`, and only checks notifications that have subscribers.
- Migration was committed one area per commit (reader, catalogue, search, others), not one view
  per commit, because the conversion was mechanical and identical per handler.
- Other raw `ex.Message` sites (book detail, shelves, advisor) remain for K81's owners (11, 22).

## NOT ASSESSED

| Item | Owner | Why |
|---|---|---|
| G3 and G8 journeys; E2E suite with the guard on; `Category=SingleInstance` E2E | Phase 01 harness | The journey harness does not exist yet. The prototype UIA driver could not drive the Windows file and folder pickers on this machine, so scan, reader page turns and the corpus-title grep (T02.3 acceptance) were not run. The unit redaction tests cover titles in paths. |
| Killing the PDF worker mid-read (G8) | Phase 01 / Phase 04 | Needs an open book (see above). |
| macOS lock-file single-instance and pipe activation | Engineering (Phase 27) | No Mac available. |
| WER LocalDumps crash-dump policy | Owner / Engineering | Optional in the plan. |
| Debug-build guard (Throw mode) against a full interactive session | Phase 01 | Release runs used Log mode. |
