# Defect and gap register (Kaizen cycle 2026-09-23)

Audit date: 25 September 2026. Audited commit: `0ad3c0c` (HEAD of `main`), executed from a
clean HEAD worktree, plus the owner's uncommitted working tree for the build check.

Every entry has a stable ID (`K..`) that the phase documents cite. The **Class** column uses
the engine evidence classes:

- **MEASURED**: observed in the running application, a command result, a database query or a
  crash stack captured during this audit.
- **CODE**: confirmed by reading the source at the cited location. Not yet observed at runtime.
- **HEURISTIC**: design or process judgement.

Severity: **P0** stops a core journey, loses data or crashes the process. **P1** makes a journey
wrong, misleading or unusable for normal users. **P2** is degraded quality, polish or hygiene.
A gate-failing finding is always *Must* regardless of effort (design engine
`ux-remediation-and-redesign`).

Screenshots are in [`evidence/screens/`](evidence/screens/). The command logs are in
[`evidence/logs/`](evidence/logs/).

## Engineering baseline

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K01 | P0 | MEASURED | All 210 files of `tests/OgmaLibrary.Tests` and `tests/OgmaLibrary.Tests.Ui` are deleted in the working tree (uncommitted). `OgmaLibrary.sln:26,28` still references both projects, so `dotnet build OgmaLibrary.sln` fails with `MSB3202 The project file ... was not found`. | `git status`; working-tree build output | 00 |
| K02 | P1 | MEASURED | No `global.json` pins the SDK. During the audit the machine updated SDK 10.0.401 in place (folder written 06:41–06:42). The first build failed with `MSB4019 ... Microsoft.NET.Sdk.Common.targets was not found` and passed later with no source change. | `evidence/logs/first-attempt-sdk-midupdate.log` | 00 |
| K03 | P1 | MEASURED | The format gate fails. `dotnet format --verify-no-changes` reports `IMPORTS` ordering errors in `src/OgmaLibrary.App/Startup/StartupTasks.cs` and `src/OgmaLibrary.Workers/Pdf/PdfWorkerCommand.cs`. The analyzer gate passes. | `evidence/logs/build-vuln-format-gates.log` | 00 |
| K04 | P1 | MEASURED | Test-suite hygiene. 1 of 1,161 tests fails: `Phase24RealOcrCorpusTests.PackagedTesseract_RecognizesExpectedWordsFromGeneratedScannedFixture`, with `IOException ... being used by another process` on temp cleanup. The core suite takes **18 min**, dominated by 50k-scale performance benchmarks (the longest is 116 s). `testhost` opened a non-loopback listener and raised a Windows Firewall prompt during the run. One full run left 30 `ogma-*` databases and folders (14 MB) plus `OgmaLibraryPdfWorker` sandboxes in `%TEMP%`. | `evidence/logs/dotnet-test-summary.log`; TRX durations; screenshot `j2-screen.png` | 00, 01 |
| K05 | P0 | MEASURED | **The test oracle is blind to the real window.** 42/42 architecture, 955/956 core and 163/163 UI tests passed while the catalogue, the empty state and the first-run call to action were invisible to every user for three weeks (K10). No test asserts that content is visibly painted, unobstructed and reachable at supported window sizes. | Test summary vs `j2-scanned.png` | 01 |
| K06 | P2 | MEASURED | The Release output ships `Avalonia.Diagnostics.dll` and native runtime folders for about 40 RIDs (android, ios, linux-musl, s390x and others). `Avalonia.Diagnostics` is conditioned to Debug in `OgmaLibrary.App.csproj:29` yet its DLL is in the Release output (it arrives transitively). There are about 77 untracked `src/*/tmp/` build directories. | `ls src/OgmaLibrary.App/bin/Release/net10.0/runtimes` | 00, 26 |
| K07 | P1 | CODE | There is no application logging. No `ILogger` is used anywhere in `src`, and 24 of 82 broad `catch` blocks swallow errors silently. Field failures cannot be diagnosed. | Inventory scan | 02 |

## Shell, navigation and visual system

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K10 | P0 | MEASURED | **The catalogue and the first-run empty state are never painted.** The pager `Border` (opaque `Brush.Surface.Footer`, default stretch) is nested inside the `Grid.Row="4"` content grid instead of being its sibling (`CatalogueShellView.axaml:450-482`). That grid has no row definitions, so `Grid.Row="5"` collapses into the single cell and the pager covers every catalogue view and the empty state. Its buttons appear mid-screen. Introduced by `822e760` (2026-09-04, "persist catalogue view state and add paging"). **Root cause verified:** moving the block out of the cell (`evidence/tools/root-cause-pager-overlay.patch`) makes the catalogue render (`j3-grid.png`). | `j1-first-run.png`, `j1-wide.png`, `j2-scanned.png`, `j2-screen.png` (real screen, not a capture artefact) | 03 |
| K11 | P0 | MEASURED | **Primary actions are unreachable at normal window sizes.** About 20 toolbar controls sit in one fixed row. At the default 1180×760 window, *Choose library folder*, *Open PDF*, *Advisor*, *Reading plan* and *Relocation reviews* are laid out beyond the right edge (x≈1320–1741). At the enforced minimum width (`MinWidth="860"`; an 800 px request is clamped) only 8 controls are reachable. A horizontal scroll bar exists but is not discoverable. | UIA bounds in `j1-first-run-uia`; `j9-800.png` | 03 (stopgap), 07 |
| K12 | P1 | MEASURED | Panels stack instead of switching. Search, Index Manager, Filter, Sharing, Split view and Relocation reviews remain open together and squeeze the catalogue into a strip. *Library* does not close them. A value typed into an earlier panel's field (for example *Filter by title*) stays live under later screens and can silently filter the catalogue. | `sheet2.png` | 07 |
| K13 | P1 | MEASURED | The status bar is wrong. It says "Ready — choose a library folder to begin" with 17 books loaded, and progress reads "59 / 17 files" and then "Scanned 77 files" for 17 files. Scan progress and asset-job progress share one counter (`BookIngestionWorker.cs:79-91`). | `j2-scanned.png`, `j2-screen.png`, `j3-grid.png` | 03, 06 |
| K14 | P1 | MEASURED | Screen readers hear C# record dumps. Catalogue items expose `BookSummaryProjection { BookId = 01M3…, Title = … }` and search results expose `SearchResultItem { BookId = … }` as their accessible names. `AutomationProperties.Name` is set on the inner `Border`, not on the `ListBoxItem`. | UIA dumps | 10, 13, 21 |
| K15 | P1 | MEASURED/HEURISTIC | The visual system is incoherent. There are four unrelated button styles in one toolbar. The selection highlight is an off-palette salmon. Detail and reader side-panel tab headers render at display size and wrap onto three rows. Disabled buttons have low contrast. *Rename* overflows the sidebar. Split view uses a literal `||`. Icons are tiny and inconsistent. The reader page raster is blurry. The Caption token is 11 px (below the design engine's 12 px floor). There are 21 hard-coded `FontSize` values and 5 hard-coded `Foreground="White"`. The fonts themselves (Spectral, Public Sans, JetBrains Mono) are licensed and compliant. | `j3-detail.png`, `j4-reader.png`; `Themes/Tokens.axaml:134-136` | 09, 11, 12 |
| K16 | P1 | CODE+MEASURED | There is no settings screen. Theme and density are reachable only through the command palette. Language can neither be chosen nor persisted, and French is unreachable. Metadata providers, the classroom Host and the 3D shelf can be enabled **only by environment variables** (`OgmaRuntimeOptions.cs:33-64`), which normal users cannot use. | Code read; no settings route in UIA tree | 08 |
| K17 | P1 | MEASURED | Entries are dead or misleading. *AI Smart Search* appears in standalone mode but works only with a classroom Host. *Sharing* opens an almost empty page without the env flag. `OGMA_ENABLE_3D_SHELF` changes only a diagnostic string. Three host panels are hard-coded `IsVisible="False"` (`CatalogueShellView.axaml:558,658,734`). The dead `MainWindow`/`MainWindowViewModel` still contain a second copy of the scan logic. | `sheet1.png`, `sheet2.png`; code | 07, 08 |

## Library, scanning and processing

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K20 | P0 | MEASURED | **Covers are never shown.** Twelve cover and spine JPGs were generated into `<library>/.ogma/covers` and `.ogma/spines`. `CatalogueViewModel.LoadAsync` (`CatalogueViewModel.cs:268-276`) prefers the injected startup root (`_assetRootPath`, which defaults to the app-data folder) over the folder the user chose, so every card falls back to a brown placeholder. The 3D host has the same wrong root (`Shelf3DHostCoordinator.cs:26`). | `j3-grid.png`; `find lib/.ogma`; `library-settings.json` | 05 |
| K21 | P1 | MEASURED | Invalid files become "books". The 0-byte `empty.pdf`, the HTML file `not-really-a.pdf` and the one-third-truncated PDF were each catalogued as a book and badged **Indexed**. There is no quarantine or "needs attention" state. | DB query; `j3-grid.png` | 05 |
| K22 | P1 | CODE | Only one library root is supported. *Choose library folder* **relinks** the existing root (`MainShellViewModel.cs:861-872`), so every book from the previous folder is flagged missing (`UnavailableFileFlagService.cs:39-100`). A flag is never cleared on rescan (`IngestionOrchestrator.cs:187-208`). There is no Rescan command, no startup scan and no watcher, and setting `OGMA_LIBRARY_ROOT` does not start a scan (measured). | Code; launch with env var showed 0 books | 05 |
| K23 | P1 | MEASURED | A byte-identical duplicate is listed twice with no duplicate indication. `EditionId` is null for all 17 books, so the work/edition grouping the roadmap reports as complete is not populated by a real scan. | DB: 16 distinct hashes / 17 books | 06, 10 |
| K24 | P1 | MEASURED | Six ISBNs were extracted into `ExtractedIsbnEvidence` with `IsBest=1`, but none reached `Books.IsbnNormalized` or the metadata the user sees. | DB query | 06 |
| K25 | P1 | MEASURED | The job system retries too much and says too little. The Activity Centre reports **26 failed, 300 attempts** for 17 books. One `ExtractionFailed` job has `RetryCount` 48. All 16 embedding jobs fail and retry for a provider that is absent. The password-protected PDF shows *Indexing* and later *Index failed*, with no unlock path. Badges change between launches because processing resumes. Root cause of the 48 (CODE): `ExtractionPipelineService` stores its failure record as a job and increments it per failed page; crash recovery, thumbnail repair and re-queue each add retries too. | DB `Jobs`; `sheet2.png`; `j3-detail.png` | 06, 14 |
| K26 | P1 | CODE | Pipeline robustness and throughput are weak. `BookIngestionWorker.ExecuteAsync` has no loop-level error guard (`BookIngestionWorker.cs:66-93`), so one exception stops covers, spines and metadata until restart. Each PDF operation copies the whole file, hashes it three times and spawns a process (`PdfWorkerClient.cs:485-527`), about 4–5 times per book, with 15 s CPU and wall-clock caps (`:1132-1138`). | Code | 06 |
| K27 | P2 | MEASURED | The app writes a hidden `.ogma/` cache into the user's library folder. That is a risk for read-only, network, removable and cloud-synchronised folders, and it changes the user's files without disclosure. This is an owner policy decision (D-04). | `find lib/.ogma` | 05 |
| K28 | P2 | CODE | Assorted scan-state defects. The scan stays in "Scanning" after cancel or failure. Stale leases from a crashed process block a resource group for up to 5 minutes. Shutdown mid-job consumes a retry. `library-settings.json` is written in place, not atomically, and a torn file crashes *Choose folder*. *Open PDF* silently sets the library root. | Code (`MainShellViewModel.cs:904-918`, `JobRecoveryService.cs:57-59`, `LibrarySettingsService.cs:119-128`, `DirectPdfOpenService.cs:96-101`) | 05, 06 |

## Reader

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K30 | P0 | MEASURED | **The app crashes while reading.** After about 20 consecutive page turns the process terminated with an unhandled `System.IO.IOException: The pipe is being closed`. The stack is `PdfWorkerSession.SendAsync` (`PdfWorkerClient.cs:1030`) ← `SendSynchronously` (`:1017`) ← `GetPageRotationDegrees` ← `ReaderSessionService.NavigateToAsync` (`:173`) ← `ReaderView.NextButton_Click` (`ReaderView.axaml.cs:121`, `async void`). The persistent reader worker is subject to the one-shot 15 s CPU Job Object limit (`WindowsChildProcessLimit.cs:43-48`). Once it dies, the next synchronous IPC throws on the UI thread and nothing catches it. | Windows Application log, .NET Runtime event at 07:10:48 | 04 |
| K31 | P0 | CODE+MEASURED | There is no process-wide exception safety net. There are zero handlers for `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` or dispatcher unhandled exceptions. Dozens of `async void` handlers await code that rethrows (catalogue double-click, choose folder, search, index manager, sharing, retry). K30 is one measured instance. | Code; K30 stack | 02 |
| K32 | P1 | MEASURED | Page turns block the UI. One page-turn invoke took **11.4 s** before the crash. Geometry and rotation are fetched through synchronous IPC on the UI thread, queued behind up to six prefetch renders (`PdfWorkerClient.cs:1011-1022`). One timeout desynchronises the session protocol (`:1033`). | Timing in reader stress run | 04 |
| K33 | P1 | MEASURED | Page text is visibly blurry at 100 % on a 2560×1440 display. Root cause (CODE): every page is rendered at a fixed `ReaderRenderDefaults.PageWidthPx = 1440` (`ReaderSessionService.cs:278`), whatever the zoom or display scaling. | `j4-reader.png` | 04, 12 |
| K34 | P1 | MEASURED/CODE | The reader experience is incomplete. There are three stacked toolbars and two duplicate sets of page navigation, and the library sidebar and toolbar stay on screen (no focus or full-screen mode, READ-005). There is no spread or continuous layout (READ-004). In-document search is registered but not wired (READ-006). There is no password unlock view (READ-009). Split view needs a book ID typed by hand, and both panes share one session. Citations export as plain text only (READ-013). *Read* appears twice in the detail panel. | `j4-reader.png`, `sheet2.png`; code | 12 |

## Search and AI

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K40 | P1 | MEASURED | Search misses obvious matches. The search panel runs semantic search with an exact-match fallback only. Single words work (`Lantern` 2, `Namutebi` 1, `caravan` 1). Multi-word phrases present in the text (`dhow Zanzibar`, `Chwezi dynasty`), a typo (`algoritms`), a Unicode author (`Wanjirũ`) and a field query (`author:Okello`) all return **0**. The structured, fuzzy and FTS services that exist in the backend are not what the panel uses. Root cause for phrases (CODE): `FtsIndexService.BuildMatchQuery` (`FtsIndexService.cs:204-215`) wraps any multi-word query in quotes, so FTS5 demands the words be adjacent. | Query battery, Kaizen run 2 | 13 |
| K41 | P1 | MEASURED | Search status is misleading or flaky. It shows "Semantic search active" when no embedding provider exists (it later says "unavailable"). The first battery after opening the panel returned **0 for every query, including exact titles**, while a later identical run succeeded, so there is an intermittent race. `RunSearchAsync` exceptions are lost inside a fire-and-forget `Task.Run` (`SearchViewModel.cs:196-214`). | Query battery, run 1 vs run 2 | 13 |
| K42 | P2 | MEASURED | Semantic search depends on an Ollama server at `localhost:11434` with `nomic-embed-text`, which is neither shipped nor explained. The port was closed on the test machine and embedding jobs failed ×16. | TCP probe; `Jobs` | 14 |
| K50 | P0 (product) | MEASURED+CODE | **The AI Reading Advisor and Reading Plan can never work.** *Recommend* returns "AI advisor unavailable: AI features are disabled". *Ask locally* stays disabled. The privacy tier is in memory and fixed at Offline (`AiPrivacyService.cs:11-30`). The Privacy Center view is never mounted. Only `AiDisabledProvider` is registered (`AiServiceExtensions.cs:66`). The view models hard-code `"openai"`/`"gpt-test"` (`RecommendationPanelViewModel.cs:376`, `ReadingPlanViewModel.cs:155`). The offline deterministic fallback is unreachable (`AdvisorService.cs:31-67`). | Advisor run; code | 15, 16 |
| K51 | P1 | CODE | Student AI Smart Search and the school AI proxy also receive the disabled provider (`SchoolAdminServiceExtensions.cs:43-50`). | Code | 19 |

## Advanced surfaces

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K60 | P1 | MEASURED+CODE | The 3D shelf says "3D view is not available on this device" on Windows 11 with Edge WebView2 present, and falls back to a plain list. `SetScene` is posted before navigation completes and is never re-sent. The WebView is hidden until a WebGL message that may never arrive. The fallback list is filled off the UI thread. | `sheet1.png`; code (`Bookshelf3DView.axaml:44-51`, `NativeWebViewHostAdapter.cs:~92-105`) | 18 |
| K61 | P1 | MEASURED+CODE | Classroom Host and client join are unreachable without `OGMA_ENABLE_CLASSROOM_HOST`. The Sharing page is a skeleton with unlabeled *Start*/*Stop*. About 50 English strings are hard-coded in `SharingSettingsView.axaml`. A Host start failure throws into `async void`. The listener is never stopped at exit. Publishing folders and shared shelves have no UI (ADMIN-001/002). | `sheet2.png`; code | 19, 20 |
| K62 | P2 | CODE | Classroom clients write full local copies of PDFs (`ClassroomBookFileMaterializer.cs:69`, CLIENT-004). There is no teacher dashboard (CLIENT-012). The Host connection is kept in memory only. | Code | 20 |

## Platform, security and process

| ID | Sev | Class | Finding | Evidence | Phase |
|---|---|---|---|---|---|
| K70 | P1 | CODE | There is no single-instance guard. A second launch runs migrations (including backup-restore over an open DB), starts a second set of workers on the same `catalogue.db` and opens a second LAN listener. | Code | 02, 23 |
| K71 | P2 | MEASURED+CODE | Startup and shutdown. Warm cold-start is 3.7–4.1 s to window (good), but it was 23.9 s under test-suite load. Startup DB work (migration, backfill) likely runs on the UI thread. Exit blocks without a timeout. Graceful close measured 3.75 s with no orphaned workers. Working set is 182–252 MB. | Launch timings; shutdown test | 24 |
| K72 | P1 | CODE | View-model state is mutated off the UI thread after `ConfigureAwait(false)` (catalogue load, 3D shelf, advisor, reading plan, host sharing). This risks cross-thread exceptions and corrupt lists during concurrent refreshes. | Code (`CatalogueViewModel.cs:266-293` and others) | 02 |
| K80 | P1 | HEURISTIC | The P0 security gate from 30 August is still open: an independent review of PDF containment and escape, plus a hostile-PDF corpus beyond synthetic fixtures. DPIA covering minors and school-controller approval are still open. | Prior audit records | 23 |
| K81 | P2 | MEASURED | Internal data is shown to users. The detail panel shows the raw SHA-256. Failures show raw `ex.Message` in English (`MainShellViewModel.cs:219-221`, folder picker `FailedFormat`). | `j3-detail.png` | 11, 22 |
| K82 | P1 | CODE | PDF worker containment is Windows-only. `RequireWindowsProcessLimit` (`PdfWorkerClient.cs:460-470`) fails closed only on Windows; on macOS the untrusted-PDF worker runs with no CPU or memory limit. | Code | 23, 27 |
| K90 | P1 | CODE | **54 of 101 functional requirements are not usable by a normal user**: 28 partial, 20 backend-only and 6 missing. Of the 47 wired, 21 work only with an environment flag. The AI group is weakest (2 wired of 11). | [04-inventory-and-requirements.md](04-inventory-and-requirements.md) | all |
| K91 | P1 | HEURISTIC | There are four layered plans (24-phase grand plan, Aug-39, astra 21-phase, Sept-10 Kaizen) with conflicting status claims. Of the 30 astra findings, 2 are fixed, 8 partial, 5 token-fixed and 15 open. There has been no product commit since 14 September. | [06-prior-plans-reconciliation.md](06-prior-plans-reconciliation.md) | 00 |
| K92 | P1 | NOT ASSESSED | macOS has never been built or run in this cycle. There is no signed installer, clean install, upgrade or rollback on either OS. | — | 26, 27 |

## Items verified as working (do not regress)

| Area | Measured result |
|---|---|
| Build | HEAD Release build: 0 errors, 0 warnings. No vulnerable packages. Analyzer gate passes. 162-ID accountability script passes. |
| Tests | Architecture 42/42, UI 163/163, core 955/956. |
| Discovery | 17 of 17 files discovered in about 15 s, including Unicode names and a 3-level, 120-character nested path. |
| Extraction | 1,302 pages extracted, 1,432 FTS chunks, embedded PDF metadata (title, author, subject) captured for every valid file. |
| Reader | Opens, renders page 1 of a 120-page PDF, and shows page count, zoom, highlight colours, notes, bookmarks and layers. |
| Detail panel | Opens on selection and shows the file path, size, availability and the metadata write-back preview action. |
| Grid, list, directory | Render correctly once K10 is fixed (`j3-grid.png`). |
| Shutdown | A normal close exits in 3.75 s and leaves no orphaned worker processes. |
| Advisor privacy | Fails closed: nothing leaves the device while AI is disabled. |
