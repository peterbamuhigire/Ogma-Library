# Test report: build gates, automated suites and real-window journeys

Date: 25 September 2026. Machine: Windows 11 Pro 10.0.26200 on a 2560×1440 display, with .NET SDK
10.0.401 and runtime 10.0.12.

Two trees were tested:

- **As-is working tree:** `C:\wamp64\www\Ogma-Library`, on `main` @ `0ad3c0c`, with 210
  uncommitted deletions.
- **Clean HEAD worktree:** a disposable `git worktree add --detach <scratch>/head HEAD` in the
  session scratch directory. It was used for every build, test and runtime check, so the owner's
  working tree was never modified.

No repository source file was changed by this audit. To keep testing past the blank-catalogue
defect (K10), the pager-overlay fix was applied **only in the disposable scratch worktree**. The
diff is kept as evidence in
[`evidence/tools/root-cause-pager-overlay.patch`](evidence/tools/root-cause-pager-overlay.patch).
Results below say whether they came from the unmodified HEAD build or the patched scratch build.

## 1. Build and quality gates

| # | Command (run from the tree noted) | Result | Evidence |
|---|---|---|---|
| G1 | `dotnet build OgmaLibrary.sln -c Release --no-restore` (HEAD, first attempt) | **FAIL (environment).** `MSB4019: The imported project "...sdk\10.0.401\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Sdk.Common.targets" was not found.` The SDK folder was being written at 06:41–06:42. A retry minutes later passed with no source change (K02). | `evidence/logs/first-attempt-sdk-midupdate.log` |
| G2 | `powershell -File ./scripts/Test-RequirementAccountability.ps1` (HEAD) | **PASS.** "Requirement accountability verified: 101 FRs, 29 NFRs, 32 controls; all 162 IDs are assigned in the roadmap matrix." (`pwsh` is not installed; Windows PowerShell 5.1 was used.) | same log |
| G3 | `dotnet restore OgmaLibrary.sln --locked-mode` (HEAD) | **PASS** | same log |
| G4 | `dotnet build OgmaLibrary.sln -c Debug` (**as-is working tree**) | **FAIL.** `MSB3202: The project file "tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj" was not found`, and the same for `tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj` (K01) | build output (session) |
| G5 | `dotnet build OgmaLibrary.sln -c Release --no-restore` (HEAD, retry) | **PASS**: 0 errors, 0 warnings, 3 min 12 s | `evidence/logs/build-vuln-format-gates.log` |
| G6 | `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | **PASS.** All 10 projects have no vulnerable packages. | same |
| G7 | `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | **FAIL** (exit 2): `error IMPORTS: Fix imports ordering` in `src/OgmaLibrary.App/Startup/StartupTasks.cs` and `src/OgmaLibrary.Workers/Pdf/PdfWorkerCommand.cs` (K03) | same |
| G8 | `dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn` | **PASS** (exit 0) | same |
| G9 | `shelf3d` npm gates | **NOT RUN** this cycle (no `src/shelf3d` change) | — |

## 2. Automated test suites (HEAD, Release, `--no-build -m:1`, TRX loggers)

| Project | Passed | Failed | Skipped | Duration |
|---|---|---|---|---|
| OgmaLibrary.Tests.Architecture | 42 | 0 | 0 | 19 s |
| OgmaLibrary.Tests | 955 | **1** | 0 | **18 min 1 s** |
| OgmaLibrary.Tests.Ui (headless Avalonia) | 163 | 0 | 0 | 39 s |
| **Total** | **1,160** | **1** | 0 | about 19 min |

**Failure:** `OgmaLibrary.Tests.Ocr.Phase24RealOcrCorpusTests.PackagedTesseract_RecognizesExpectedWordsFromGeneratedScannedFixture`

> `System.IO.IOException : The process cannot access the file '\\?\C:\Users\…\AppData\Local\Temp\ogma-phase24-real-ocr-<guid>\<guid>' because it is being used by another process.`

The failure happened while cleaning up the temporary directory, so an OCR worker or the test still
held a file handle. This is either a flaky test or a real handle leak (K04, Phase 17).

**Slowest tests.** These are performance benchmarks running inside the unit suite (K04, Phase 24).

| Seconds | Test |
|---|---|
| 116.2 | `MetadataSearchServiceTests.PerfBenchmark_MetadataSearch_P95_LessThan150ms` |
| 110.3 | `SemanticSearchServiceTests.PerfBenchmark_SemanticSearch_P95_LessThan1500ms` |
| 89.5 | `DiscoveryServiceTests.DiscoveryService_EnumeratesFiftyThousandFilesWithBoundedChannel` |
| 76.6 | `FtsIndexServiceTests.PerfBenchmark_FtsSearch_P95_LessThan500ms` |
| 68.9 | `Phase16VisualAssetDiskBudgetTests.GeneratedVariants_StayWithinDiskBudgetAcrossPdfCorpus` |
| 54.9 | `Phase16VisualAssetTests.VisualAssetService_PreferredLookupStaysBoundedAt50kBooks` |
| 48.8 | `CatalogueReadModelTests.GetBookSummaries_50kServerSidePage_CompletesWithinTwoSeconds` |
| 38.7 | `OcrGoldenCorpusTests.OcrJob_VeryLargePdf_NoOutOfMemory` |

**Side effect:** during the core run Windows Defender Firewall asked whether to allow `testhost`
on public and private networks (`evidence/screens/j2-screen.png`). At least one test binds a
non-loopback listener (K04). The prompt was left for the owner and not answered.

**Oracle gap (K05):** all three suites were green while the real catalogue was invisible (K10).

## 3. Real-window test method

- **Driver:** a Windows UI Automation script, now kept at
  [`evidence/tools/Invoke-OgmaUia.ps1`](evidence/tools/Invoke-OgmaUia.ps1). It launches the
  Release exe, dumps the automation tree (type, name, id, bounds), invokes, selects or sets
  values on controls, resizes the window and captures it with `PrintWindow`
  (`PW_RENDERFULLCONTENT`). No window focus is needed.
- **True screen capture:** to rule out capture artefacts, one on-screen `CopyFromScreen` capture
  confirmed the blank catalogue (`j2-screen.png`).
- **Isolation:** each run used its own `OGMA_LIBRARY_DATA_DIR` in the scratch directory
  (`data-fresh`, `data-lib`). The owner's real `%LOCALAPPDATA%` library was never touched.
- **Folder picker:** the Windows folder dialog did not expose its *Select Folder* button to UI
  Automation. The run used keyboard address entry (Alt+D, path, Enter) and then a synthetic mouse
  click on *Select Folder* after bringing the dialog to the foreground.
- **Synthetic corpus:** 17 files, about 11 MB. Sixteen were generated by
  [`evidence/tools/New-SyntheticCorpus.py`](evidence/tools/New-SyntheticCorpus.py) with PyMuPDF
  and pypdf. The generator takes an output directory argument (default `%TEMP%/ogma-corpus`). The
  seventeenth file, `scanned-image-only.pdf`, was copied from `tests/golden-corpus/ocr-pipeline/`.
  All content is original test text; no private books were used.

| File | What it probes |
|---|---|
| `Science/The Physics of Everyday Light.pdf` (40 pp, ISBN 978-0-306-40615-7) | Embedded metadata, ISBN on the copyright page, TOC, body text |
| `Science/Introduction to Tropical Ecology.pdf` (60 pp, ISBN) | Same, larger |
| `Science/Algorithms Explained.pdf` (120 pp, ISBN) | Reader navigation depth (the crash test book) |
| `Science/Big Reference Handbook.pdf` (900 pp, 5.5 MB, ISBN) | Large-file extraction, timeouts, paging |
| `History/A Short History of the Great Lakes Kingdoms.pdf` (80 pp, ISBN with invalid check digit) | ISBN validation, local-context vocabulary (`Chwezi dynasty`) |
| `History/Trade Routes of East Africa.pdf` (30 pp, no ISBN) | Full-text phrases (`dhow Zanzibar`, `caravan`) |
| `Fiction/The Lantern Keeper.pdf` (25 pp, ISBN) | Title search, duplicate source |
| `Fiction/Ngũgĩ-style Stories — Unicode Title.pdf` (metadata "Hadithi za Jioni: Évening Tales", author "Wanjirũ Kamau") | Unicode file names, titles and authors |
| `Edge Cases/untitled_scan_0042.pdf` (no metadata) | Filename-derived title, "Unknown author" |
| `Edge Cases/The Lantern Keeper (copy).pdf` | Byte-identical duplicate detection |
| `Edge Cases/Locked Ledger (password secret).pdf` | Password-protected handling |
| `Edge Cases/Truncated Download.pdf` (first third of a valid PDF) | Corrupt-file rejection |
| `Edge Cases/not-really-a.pdf` (HTML bytes) | File-type validation |
| `Edge Cases/empty.pdf` (0 bytes) | Empty-file rejection |
| `Edge Cases/Scanned Pamphlet (image only).pdf` | Image-only detection and OCR prompt |
| `Edge Cases/scanned-image-only.pdf` (repo golden-corpus copy) | OCR fixture |
| `Edge Cases/aaa…/bbb…/ccc…/Deeply Nested.pdf` (3 × 40-character folders) | Long, deep paths |

## 4. Journey results

Result key: **PASS** means it worked as a user expects; **PARTIAL** means it worked with
significant defects; **FAIL** means the journey could not be completed or the app crashed;
**NOT ASSESSED** means the journey was not run.

| Journey | Build | Result | Measured observations | Evidence |
|---|---|---|---|---|
| **J1 First run** | HEAD | **FAIL** | The window appeared after **23.9 s**, with the test suite loading the machine. The empty-state heading and *Choose library folder* / *Open PDF* buttons exist in the automation tree but are **not painted** (K10). At the default 1180×760 window, the toolbar's *Choose library folder*, *Open PDF*, *Advisor*, *Reading plan* and *Relocation reviews* sit off the right edge (x≈1320–1741) (K11). The status reads "Ready — choose a library folder to begin". Working set 252 MB, 54 threads. | `j1-first-run.png`, `j1-wide.png` |
| J1b Library root by env var | HEAD | **FAIL** | Launching with `OGMA_LIBRARY_ROOT=<lib>` did not scan: 0 books after 45 s (K22). | UIA dumps |
| **J2 Choose folder → scan → ingest** | HEAD | **PARTIAL** | The empty-state button opened the folder picker. 17 of 17 files were catalogued in about **15 s**. Two PDF worker processes ran. But the catalogue stayed **blank** (K10). The status bar showed "59 / 17 files" and later "Scanned 77 files" (K13). The toolbar copy of *Choose library folder* did nothing when invoked while off-screen. | `j2-scanned.png`, `j2-screen.png` |
| J2 data check | HEAD | **PARTIAL** | Database: 17 `Books`, 17 `BookFiles`, 1,302 `ExtractedPages`, 1,432 FTS chunks, 26 `VisualAssetManifests`, 102 `Jobs`, 319 `AuditEvents`. The 0-byte, HTML and truncated files each became a book (K21). The duplicate has the same SHA-256 but is a separate book; `EditionId` is null for all 17 (K23). Six ISBNs were found with `IsBest=1`, but `Books.IsbnNormalized` is null for all (K24). The app created `<library>/.ogma/covers` and `.ogma/spines` with 12 JPGs each (K27). | DB queries below |
| **J3 Catalogue browse** | Patched | **PARTIAL** | Grid, list and directory views render once K10 is fixed. Every cover is a brown placeholder even though 12 covers exist (K20). Invalid files are badged *Indexed* (K21). The duplicate shows twice. Accessible names are record dumps (K14). The status bar is stale. Warm launch took 5.0 s. | `j3-grid.png` |
| J3 Detail panel | Patched | **PARTIAL** | The panel opens on selection. Seven tab headers render at display size and wrap onto three rows. *Read* appears twice. The raw SHA-256 is shown. The selection highlight is salmon (K15, K81). | `j3-detail.png` |
| **J4 Reader** | Patched | **FAIL** | *Read* opened the 120-page book and rendered page 1, but the text is blurry (K33). There are three stacked toolbars and two sets of navigation (K34). During a 110-page-turn stress run, one invoke **stalled 11.4 s** and the **process crashed after about 20 turns**: `System.IO.IOException: The pipe is being closed` from `PdfWorkerSession.SendAsync` (`PdfWorkerClient.cs:1030`) ← `SendSynchronously` (`:1017`) ← `GetPageRotationDegrees` ← `ReaderSessionService.NavigateToAsync` (`:173`) ← `ReaderView.NextButton_Click` (`ReaderView.axaml.cs:121`). This was captured in the Windows Application log (.NET Runtime 1026, Application Error 1000, WER CLR20r3) at 07:10:48 (K30–K32). | `j4-reader.png`; event log |
| **J5 Search** | Patched | **PARTIAL** | The panel labels itself "Semantic search active" although no provider exists (K41). Results are below. | `j5-search.png` |
| **J6 AI Advisor / Reading plan** | Patched | **FAIL** | *Recommend* returned "AI advisor unavailable: AI features are disabled". *Ask locally* is disabled and has no Invoke pattern. The Reading plan shows the same state (K50). | `sheet1.png` |
| **J7 3D shelf** | Patched | **FAIL** | "3D view is not available on this device — showing the accessible bookshelf list", although WebView2 153.0.4234.48 is installed (K60). | `sheet1.png` |
| **J8 Classroom / Sharing** | Patched | **FAIL** | Without the environment flag the Sharing page is mostly empty, with unlabeled *Start*/*Stop* buttons. *AI Smart Search* says to connect to a classroom Host (K61, K17). | `sheet1.png`, `sheet2.png` |
| J8 Other panels | Patched | **PARTIAL** | Index Manager and Activity Centre work and report "Failed: 26 · Attempts: 300" for 17 books (K25). Relocation reviews: "No pending relocation reviews." Split view: two empty readers ("/ 0") that need a typed book ID. **Panels stack**, so four were open at once (K12). | `sheet2.png` |
| **J9 Settings / theme / language / responsive** | Patched | **PARTIAL** | There is no settings screen. At 800×600 only 8 of about 20 toolbar controls are reachable, and *Rename* overflows the sidebar (K11, K15). Theme and density through the command palette: **NOT ASSESSED** (keystrokes not injected). | `j9-800.png` |
| **J10 Shutdown / restart** | Patched | **PASS** | A WM_CLOSE exit took **3.75 s** with no orphaned `OgmaLibrary.Workers`. Three cold relaunches reached the window in 3.73, 3.76 and 4.14 s. Working set 182 MB. On restart, processing resumed and badges changed (for example *Locked Ledger*: *Indexing*, then *Index failed*). | launch log |

### 4.1 Jobs table after scan and one restart (HEAD data directory)

Status codes: 2 = completed, 3 = failed. The ingestion worker also recorded `ExtractionFailed`
entries.

| JobType | Status | Count | Max RetryCount | FailureCode / message |
|---|---|---|---|---|
| Enrich | 2 | 17 | 1 | — |
| MetadataExtraction | 2 | 17 | 1 | — |
| SearchExtraction | 2 | 16 | 1 | — |
| SpineGeneration | 2 | 13 | 1 | — |
| ThumbnailGeneration | 2 | 13 | 1 | — |
| EmbeddingJob | 3 | 16 | 3 | `embedding_provider_unavailable` |
| ExtractionFailed | 3 | 1 | **48** | "Search page extraction failed." |
| SearchExtraction | 3 | 1 | 3 | `search_index_failed` |
| SpineGeneration | 3 | 4 | 3 | `job_failed` |
| ThumbnailGeneration | 3 | 4 | 3 | `job_failed` ("Cover generation failed.") |

### 4.2 Search battery

**Run 1** was taken immediately after opening the panel, waiting about 2.5 s per query. Every query
returned **0 results**, including exact titles, and the status stayed "Search is ready". An
isolated retry of "Lantern Keeper" returned 1 result within 2 s, so run 1 is recorded as an
intermittent race or silent failure (K41).

**Run 2** cleared the box between queries and waited up to 12 s for results or status.

| Query | Present in corpus as | Results | Status text |
|---|---|---|---|
| `Lantern` | Title ×2 | 2 | "Semantic search is unavailable. Showing 2 exact matches." |
| `Namutebi` | Author | 1 | … 1 exact match |
| `caravan` | Body text | 1 | … 1 |
| `dhow Zanzibar` | Body text (both words) | **0** | … 0 |
| `algoritms` | Typo of a title word | **0** | … 0 |
| `Wanjirũ` | Unicode author | **0** | … 0 |
| `amber library lantern` | Body words (not adjacent) | **0** | … 0 |
| `Chwezi dynasty` | Body text (both words) | **0** | … 0 |
| `books about light and colour` | Conceptual | **0** | … 0 (no semantic provider) |
| `author:Okello` | Field query | **0** | … 0 |
| `zzqxnotaword` | Negative control | 0 | … 0 (correct) |

Result rows expose `SearchResultItem { BookId = … }` as their accessible names (K14).

## 5. Screenshots

All are in [`evidence/screens/`](evidence/screens/).

| File | Content |
|---|---|
| `j1-first-run.png` | First run at the default size: blank content, clipped toolbar |
| `j1-wide.png` | First run at 2200 px: still blank |
| `j2-scanned.png` | After scanning 17 books: blank catalogue; "59 / 17 files" |
| `j2-screen.png` | True screen capture confirming the blank catalogue; firewall prompt from `testhost` |
| `j3-grid.png` | Patched build: grid renders with placeholder covers |
| `j3-detail.png` | Detail panel |
| `j4-reader.png` | Reader, page 1 |
| `j5-search.png` | Search panel |
| `sheet1.png` | 3D shelf fallback, AI Smart Search, Advisor, Reading plan |
| `sheet2.png` | Index Manager, Filter, Sharing, Relocation reviews, Split view (stacking panels) |
| `j9-800.png` | 800×600 window |

## 6. Limitations and NOT ASSESSED

| Item | Status | Owner phase |
|---|---|---|
| Command palette, keyboard shortcuts, dark theme, density | NOT ASSESSED (no keystroke injection) | 01, 09 |
| Narrator, NVDA, VoiceOver | NOT ASSESSED | 21 |
| macOS build and run | NOT ASSESSED | 27 |
| Two-machine classroom LAN (Host/Client, mDNS, TLS trust on first use) | NOT ASSESSED | 19, 20 |
| Online metadata providers (`OGMA_ENABLE_METADATA_PROVIDERS`) | NOT ASSESSED | 08 |
| Semantic search with Ollama present | NOT ASSESSED (port 11434 closed) | 14 |
| AI Advisor with a real provider | NOT ASSESSED (impossible at HEAD) | 15, 16 |
| OCR run from Book Detail | NOT ASSESSED | 17 |
| Password unlock, metadata write-back apply and undo, annotations persistence across restart | NOT ASSESSED | 12, 11 |
| Performance at 2k or 50k books on reference hardware | NOT ASSESSED (only 17-book corpus) | 24 |
| Signed installer, clean install, upgrade, rollback | NOT ASSESSED | 26 |
| `shelf3d` npm gates | NOT RUN | 18 |

The first launch time (23.9 s) was measured while the core test suite loaded the CPU, so it is not
a clean cold-start figure. The clean warm-start figures are 3.7–4.1 s.
