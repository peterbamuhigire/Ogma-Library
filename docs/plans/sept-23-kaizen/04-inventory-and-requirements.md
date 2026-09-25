# Inventory and requirements coverage

**Evidence class: CODE.** This is a static read of `main` @ `0ad3c0c`, with tests read from the
clean HEAD worktree. Runtime observations from [02-test-report.md](02-test-report.md) are marked
**MEASURED** where they confirm or correct a code reading. Paths are relative to `src/` unless
stated.

**Verification:** 12 FR evidence citations were re-opened against the source on 2026-09-25: LIB-001,
CAT-003, CAT-006, READ-004, READ-006, READ-009, READ-012, AI-001, AI-003, AI-007, ADMIN-005 and
CLIENT-004. Every one matched. One line range was corrected: the ADMIN-005 proxy wiring is at
`SchoolAdminServiceExtensions.cs:46-52`. The AI-011 claim (Privacy Center unmounted) was confirmed:
`PrivacyCenterView` is referenced only by its own view model.

## 1. Project inventory

Counts are git-tracked `.cs/.axaml/.ts/.js/.html/.resx/.json` files, excluding EF migrations.

| Project | Files | LOC | Responsibility |
|---|---|---|---|
| `OgmaLibrary.App` | 93 | ~26.0k | Avalonia shell: composition root and 6 module registrars, startup coordinator, 18 views, about 30 view models, themes, 85 SVG icons |
| `OgmaLibrary.Application` | 151 | ~9.2k | Ports and use cases: AI, catalogue, classroom client, commands, ingestion, LAN host, metadata, navigation, OCR, reader, school admin, search, security |
| `OgmaLibrary.Domain` | 14 | ~2.4k | Entities and value objects: canonical identity, annotations, AI privacy and consent |
| `OgmaLibrary.Infrastructure` | 305 (+73 migration files) | ~48.2k | EF Core SQLite, discovery and ingestion, isolated PDFium worker client, metadata (ISBN, providers, merge, write-back), FTS5, embeddings, hybrid ranking, Tesseract OCR, AI gateway and providers, LAN Host (Kestrel, mDNS, TLS), classroom client, school admin, secrets, localisation |
| `OgmaLibrary.Reader` | 14 | ~2.1k | Session, render cache, text layer, in-document search, annotations, bookmarks, layers, citations, progress |
| `OgmaLibrary.Workers` | 11 | ~2.3k | Hosted workers (ingestion, search extraction, embeddings, OCR), job recovery, isolated PDF worker process |
| `OgmaLibrary.Bookshelf3D` | 21 | ~30.0k (29.1k is the bundled `shelf3d.js`) | WebView2/WKWebView bridge, message contracts, scheme handler, packaged bundle |
| `src/shelf3d` (TypeScript) | 6 | ~1.3k | Three.js scene source with npm build and performance-budget scripts |

Hygiene: there are about 77–79 untracked stale build directories under each `src/<project>/tmp/`
(K06).

## 2. Runtime composition

`App.axaml.cs:151-183` calls `CompositionRoot.AddOgmaLibrary(OgmaRuntimeOptions.FromEnvironment())`.
The modules run in order: CorePlatform, CatalogueProcessing, Classroom, Reader, Shell, Startup.
`MainShellViewModel` and its children are built by hand in `ShellModule.CreateMainShell`
(`ShellModule.cs:44-189`).

### 2.1 Fail-closed, disabled and in-memory bindings at runtime

| Service | Runtime binding | Effect |
|---|---|---|
| `IAiProvider` | `AiDisabledProvider` (`Infrastructure/AI/AiServiceExtensions.cs:66`) | No LLM call can succeed: Advisor, Reading plan and the school AI proxy (**MEASURED**: "AI features are disabled") |
| `IAiPrivacyService` | In-memory tier, default `Offline`, never persisted (`AiPrivacyService.cs:11,23-30`) | The Advisor throws before its offline fallback |
| `IAiProviderFactory`, `IAiProviderProfileService`, health registry | Registered, **no consumers** | The OpenAI-compatible, DeepSeek, Anthropic and Ollama adapters are unreachable |
| `IMetadataProvider` (Google Books, Open Library) | Only with `OGMA_ENABLE_METADATA_PROVIDERS` (`MetadataServiceExtensions.cs:45-82`) | "Enrich" is local-only; there are no online covers |
| `IClassroomHostConnectionService` | `InMemoryClassroomHostConnectionService` | The client connection is lost on restart |
| `IAiProxyEndpointHandler` | Built with `AiDisabledProvider` (`SchoolAdminServiceExtensions.cs:46-52`) | Student AI Smart Search always fails |
| `ILocalizationService` | `InMemoryLocalizationService`; en and fr each have 794 keys | `SetCulture` has no runtime caller, so the app is English only |
| `HostSharingViewModel` | `null` unless `OGMA_ENABLE_CLASSROOM_HOST` (`ShellModule.cs:144-165`) | The Sharing page is blank (**MEASURED**) |
| `MainWindowViewModel` | Registered, never resolved | Dead code with a duplicate scan path |

### 2.2 Library root: correction from runtime measurement

The static read assumed derived assets land under the data directory. **MEASURED:** after a scan,
covers and spines were written to `<chosen library>/.ogma/covers` and `.ogma/spines`, while
`CatalogueViewModel.LoadAsync` (`CatalogueViewModel.cs:268-276`) and `Shelf3DHostCoordinator.cs:26`
resolve cover paths against the **startup root** captured at composition. That root defaults to
the data directory (`OgmaRuntimeOptions.cs:19,45`). So the grid and the 3D shelf look for covers in
the wrong folder and show placeholders (K20). OCR queue, sidecar and LAN file resolution also keep
the startup root (`OcrJobQueueService.cs:28,130-147`, `SidecarService.cs:47`,
`LanBookFileResolver.cs:17,51`), while the reader uses the chosen folder. Phase 05 must resolve every
consumer through one per-book root lookup.

## 3. User-facing surfaces

| View | View model | Status |
|---|---|---|
| DesktopShellWindow | StartupShellViewModel | Wired. The configuration-failure message is hard-coded English (`App.axaml.cs:95`) |
| CatalogueShellView | MainShellViewModel | Wired, but the pager overlay hides the content (K10). Three host panels are hard-coded `IsVisible="False"` (`CatalogueShellView.axaml:558,658,734`), so their handlers are dead. There is no cancel-scan UI |
| Catalogue grid, list, directory | CatalogueViewModel | Wired; render once K10 is fixed (**MEASURED**) |
| BookDetailView | BookDetailViewModel | Wired: Read, Enrich, Run OCR, status, rating, favourite, tags, proposals, provenance, write-back. Enrich is local-only; OCR is English-only |
| ReaderView | ReaderViewModel | Wired; crashes under page turning (K30, **MEASURED**) |
| SplitViewView | SplitViewViewModel | Partial: a raw book ID must be typed (`SplitViewView.axaml:33-36`) |
| SearchPanelView | SearchViewModel | Wired to semantic search with an exact fallback only (**MEASURED**, K40) |
| IndexManager and ActivityCentre | IndexManagerViewModel, ActivityCentreViewModel | Wired (**MEASURED**) |
| ReconciliationReviewPanelView | ReconciliationReviewPanelViewModel | Wired |
| RecommendationPanelView | RecommendationPanelViewModel | Load always fails; Ask uses local evidence only |
| ReadingPlanView | ReadingPlanViewModel | Always fails |
| PayloadPreviewDialog | PayloadPreviewViewModel | Never reached |
| PrivacyCenterView | PrivacyCenterViewModel | **Unreachable** |
| StudentSmartSearchView | StudentSmartSearchViewModel | Needs a Host connection that cannot be made by default |
| SharingSettingsView | HostSharingViewModel | Only with the env flag; about 50 hard-coded English strings |
| Bookshelf3DView | Bookshelf3DViewModel | Always falls back, even with WebView2 installed (**MEASURED**, K60) |

All actions are code-behind `Click` handlers that call view-model methods. No handler is empty, but
many are `async void` without guards (K31).

## 4. Code-smell scan

| Pattern | Count | Notes |
|---|---|---|
| `TODO` / `FIXME` / `HACK` / "stub" | 0 | |
| `NotImplementedException` | 2 | Only in catch filters |
| Stale "placeholder" comments | several | `Icons/IconCatalog.cs:16-17`, `CatalogueGridView.axaml:46` |
| Hard-coded English in axaml | about 50 | Mostly `SharingSettingsView.axaml`; also `CatalogueShellView.axaml:767-779`, `DesktopShellWindow.axaml:47` |
| Hard-coded English in C# | many | `HostSharingViewModel.cs:38-99`, `MainShellViewModel.cs:220,1207`, `App.axaml.cs:95` |
| Broad `catch` without a filter | 82 | 24 appear to swallow, including the **audit save** in `PdfWriteBackService.cs:668` |
| `ILogger` usage | **0 files** | No structured logging (K07) |

## 5. Functional requirement coverage (SRS v2.1, 101 FRs)

**Legend:**

- **W**: wired to the UI.
- **W\***: wired only with `OGMA_ENABLE_CLASSROOM_HOST=true`.
- **P**: partial.
- **B**: backend only (no UI reaches it).
- **M**: missing.

Sources: `docs/plans/astra-audit-sept/evidence/reference-extract.txt` (SRS text) and
`docs/plans/aug-39/appendices/01-requirement-phase-matrix.md`. The Phase column gives the
Sept-23 phase that owns closing the gap.

| FR | Requirement | Status | Evidence | Phase |
|---|---|---|---|---|
| LIB-001 | Choose and persist library root | W (single root; relinks) | `MainShellViewModel.cs:806`; multi-root `AddAsync` unreachable | 05 |
| LIB-002 | Recursive discovery with exclusions | P | Exclusions have no UI | 05 |
| LIB-003 | Re-match renamed or moved files | W | Reconciliation panel | 05 |
| LIB-004 | Mark missing files unavailable | W (never cleared, K22) | `UnavailableFileFlagService` | 05 |
| LIB-005 | Background jobs | W (retry storm, K25) | Hosted workers, Activity Centre | 06 |
| LIB-006 | Incremental skip | W | `IncrementalDiscoveryService` | 05 |
| LIB-007 | Scan health report | P | `IScanHealthService` has no App caller | 05 |
| CAT-001 | Grid, list, directory and 3D views | W (hidden by K10; 3D falls back) | `CatalogueShellView.axaml:397-407,535` | 03, 10, 18 |
| CAT-002 | Combined sort and filter | P | Only title, author and sort in the UI | 10 |
| CAT-003 | Virtual shelves | P | `AddBookToShelfAsync` has no UI caller | 10 |
| CAT-004 | Detail groups | W | `BookDetailView.axaml` | 11 |
| CAT-005 | Previewed, undoable bulk edit | B | `BulkEditAsync` has no App caller | 10 |
| CAT-006 | Smart shelves | B | `ShelfSidebarViewModel.cs:183` uses `isSmart:false` | 10 |
| CAT-007 | Work and edition merge/split | B | `IIdentityGroupingService` has no App reference; `EditionId` null after a real scan (**MEASURED**) | 06, 10 |
| META-001 | ISBN detection | W (not promoted, K24) | `BookDetailViewModel.cs:1554` | 06 |
| META-002 | Google Books / Open Library | P (env flag) | `MetadataServiceExtensions.cs:48` | 08 |
| META-003 | Merge policy, accept and reject | W | `BookDetailViewModel.cs:1481,1489` | 11 |
| META-004 | Field provenance | W | `BookDetailViewModel.cs:1337` | 11 |
| META-005 | Write-back with backup and restore | W | `BookDetailViewModel.cs:1103-1245` | 11 |
| META-006 | Batch enrichment | B | `IBatchEnrichmentOrchestrator` unreferenced | 10 |
| META-007 | Quality score and missing-field filters | P | Badge only | 10 |
| META-008 | Library health dashboard | B | `ILibraryHealthService` unreferenced | 10 |
| READ-001 | Resume position | W | `ReadingProgressService` | 04, 12 |
| READ-002 | Navigation and history | P | `NavigationHistory` not exposed | 12 |
| READ-003 | Zoom modes | W | Reader buttons | 12 |
| READ-004 | Single, spread and continuous layout | M | Enum only (`IReaderSessionReadModel.cs:83-87`) | 12 |
| READ-005 | Full screen and Esc | M | Icon entry only | 12 |
| READ-006 | In-document search | B | Registered (`ReaderModule.cs:50`), never consumed | 12 |
| READ-007 | Bookmarks | W | Reader | 12 |
| READ-008 | Highlights and notes | W | Reader | 12 |
| READ-009 | Password-protected PDFs | B | `PasswordUnlockViewModel` registered (`ShellModule.cs:35`), no view | 12 |
| READ-010 | Background OCR | P (English only) | `BookDetailViewModel.cs:1616` | 17 |
| READ-011 | Citation capture | W | Reader | 12 |
| READ-012 | Split view | P | Typed book ID; shared session | 12 |
| READ-013 | Export BibTeX, RIS, CSL-JSON, Markdown | P | Plain text only (`CitationService.cs:83-103`) | 12 |
| READ-014 | Reading memory | W | Reader and detail | 12 |
| READ-015 | Annotation layers | W | Reader | 12 |
| SEARCH-001 | Metadata search | W | Search panel | 13 |
| SEARCH-002 | FTS5 full-text search | W (panel misses phrases, K40) | `FtsIndexService`, `CombinedSearchService` | 13 |
| SEARCH-003 | Match-location labels | W | `SearchViewModel.cs:284-315` | 13 |
| SEARCH-004 | Semantic search | P (needs Ollama) | `SemanticSearchService.cs:89` | 14 |
| SEARCH-005 | Hybrid ranking | P | Only when semantic is available | 14 |
| SEARCH-006 | Index manager | W | `CatalogueShellView.axaml:343` | 13 |
| AI-001 | AI disabled by default | W | `AiServiceExtensions.cs:63-69` | 15 |
| AI-002 | Provider selection through the gateway | B | Factory and profile services unused | 15 |
| AI-003 | Catalogue recommendations | P (always fails) | `AdvisorService.cs:31,60-66`; `RecommendationPanelViewModel.cs:376` | 16 |
| AI-004 | Metadata-only payload | B | `AiPayloadBuilder` unreachable | 15 |
| AI-005 | Content-aware opt-in and preview | B | Preview gate never reached | 15 |
| AI-006 | Local embeddings | P (needs Ollama) | No setup UI | 14 |
| AI-007 | Reading plans | P (always fails) | `ReadingPlanViewModel.cs:155` | 16 |
| AI-008 | Cited answer mode | W (extractive; consent-gated) | `LocalEvidenceAnswerPipeline` | 16 |
| AI-009 | History deletion and retention | B | Only in the unmounted Privacy Center | 15 |
| AI-010 | Cost and usage display | B | Only in the unmounted Privacy Center | 15 |
| AI-011 | Privacy Center | B | `PrivacyCenterView.axaml` unreferenced | 15 |
| LAN-001 | Explicit, audited Host start/stop | W\* | `HostSharingViewModel` | 19 |
| LAN-002 | TLS on the LAN | W\* | `KestrelHostModeListener` | 19 |
| LAN-003 | mDNS | W\* | `MdnsAdvertiser` | 19 |
| LAN-004 | Certificate and TOFU | W\* | `HostTrustService` | 19 |
| LAN-005 | Join codes | W\* | `CatalogueShellView.axaml:629-638` | 19 |
| LAN-006 | Range streams and path validation | W\* | `LanBookFileResolver.cs:43-51` | 19 |
| LAN-007 | Session list, limit, revoke | B | `IClientSessionService` unreferenced | 19 |
| LAN-008 | Graceful degrade | W\* | Offline chip | 20 |
| LAN-009 | Only published content visible | P | No publish UI | 19 |
| LAN-010 | Audit export | W\* | `HostSharingViewModel.cs:1147` | 19 |
| CLIENT-001 | Connect and browse | W\* | `ConnectToHostAsync` | 20 |
| CLIENT-002 | Private per-profile state | W\* | `StudentPrivateRepository` | 20 |
| CLIENT-003 | Roles | P | No role picker | 20 |
| CLIENT-004 | Stream without a full copy | P | `ClassroomBookFileMaterializer.cs:69` writes full copies | 20 |
| CLIENT-005 | Cache limit and purge | W\* | `DiskOfflineCacheService` | 20 |
| CLIENT-006 | Sync and conflicts | W\* | `SyncNowAsync` | 20 |
| CLIENT-007 | Host-index search | P | AI-only | 20 |
| CLIENT-008 | Cross-student isolation | W\* | Encrypted per-profile stores | 20 |
| CLIENT-009 | Revocation | W\* | `RevokeSelectedProfileAsync` | 20 |
| CLIENT-010 | No client mutation of the Host | W\* | Read-only client | 20 |
| CLIENT-011 | Connection state | W | `CatalogueShellView.axaml:711` | 20 |
| CLIENT-012 | Teacher dashboard | M | None | 20 |
| CLIENT-013 | Host entitlements | B | Unreferenced | 20 |
| ADMIN-001 | Publish folders | B | `ILibraryPublishingService` unreferenced | 19 |
| ADMIN-002 | Shared shelves | B | `ISharedShelfService` unreferenced | 19 |
| ADMIN-003 | Enrol and revoke profiles | W\* | `EnrollProfileAsync` | 19 |
| ADMIN-004 | Key held in the OS store | W\* | "Test key" checks presence only | 19 |
| ADMIN-005 | Student AI through the Host gateway | P | Proxy gets `AiDisabledProvider` (`SchoolAdminServiceExtensions.cs:46-52`) | 19 |
| ADMIN-006 | Class policy | W\* | `SaveSchoolAiPolicyAsync` | 19 |
| ADMIN-007 | Tier label and preview for students | P | Preview works; the call fails | 19 |
| ADMIN-008 | Quota | B | Proxy only | 19 |
| ADMIN-009 | Rate limit | B | `AiProxyEndpointHandler.cs:71-77` | 19 |
| ADMIN-010 | Usage dashboard | W\* | `IUsageDashboardService` | 19 |
| ADMIN-011 | Answers cite the Host catalogue | B | `ClassroomAnswerGrounder` unreachable | 19 |
| ADMIN-012 | Students delete their own AI history | W\* | `StudentSmartSearchViewModel.cs:327` | 19 |
| ADMIN-013 | Institutional purge | W\* | `PurgeAiHistoryAsync` | 19 |
| EXT-001 | Plugin contracts | P | Attributes only | 25 |
| EXT-002 | Local read API | M | None | 25 |
| EXT-003 | Zotero, Calibre, Goodreads import; theme packs | M | None | 25 |
| UX-001 | First-run flow | W (**hidden**, K10) | `CatalogueShellView.axaml:434-442` | 03 |
| UX-002 | Loading, empty and error states | P | Partial | 03, 09 |
| UX-003 | Command palette covers commands | P | 7 commands | 07 |
| UX-004 | Runtime English/French switch | P | No runtime caller | 08, 22 |
| UX-005 | Keyboard-only flows | P | Reader and palette only | 21 |
| UX-006 | Offline help | M | None | 28 |
| UX-007 | Locate and resume in 60 s | P | Not measured | 01, 28 |
| UX-008 | Themes | P | Palette only | 08, 09 |

### 5.1 Counts

| Status | Count |
|---|---|
| W | 47 (26 always available; 21 only with the Host env flag) |
| P | 28 |
| B | 20 |
| M | 6 |

| Group | W | P | B | M |
|---|---|---|---|---|
| LIB | 5 | 2 | 0 | 0 |
| CAT | 2 | 2 | 3 | 0 |
| META | 4 | 2 | 2 | 0 |
| READ | 7 | 4 | 2 | 2 |
| SEARCH | 4 | 2 | 0 | 0 |
| AI | 2 | 3 | 6 | 0 |
| LAN | 8 | 1 | 1 | 0 |
| CLIENT | 8 | 3 | 1 | 1 |
| ADMIN | 6 | 2 | 5 | 0 |
| EXT | 0 | 1 | 0 | 2 |
| UX | 1 | 6 | 0 | 1 |

"W" means the code path is reachable from a control. It does **not** mean the requirement works
for a user. For example, CAT-001 and UX-001 are W but were invisible at runtime (K10). Phase 01's
real-window journeys are the acceptance oracle.

### 5.2 NFRs and controls

- NFR-OGMA-001..009 are largely designed into the code.
- NFR-PROD-001..014, NFR-LAN and NFR-CLIENT need physical measurement, so they are
  **NOT ASSESSED**.
- CTRL-001..032 exist in code. The AI privacy controls hold only because AI can never be enabled.
  They must be re-verified once Phase 15 enables providers.

## 6. Tests inventory (HEAD)

| Project | Files | Approx. cases | Measured result |
|---|---|---|---|
| OgmaLibrary.Tests | 179 | ~956 | 955/956 |
| OgmaLibrary.Tests.Ui | 27 | ~163 | 163/163 |
| OgmaLibrary.Tests.Architecture | 1 | 42 | 42/42 |

**Tests by area** (OgmaLibrary.Tests, facts):

| Area | Facts | Area | Facts |
|---|---|---|---|
| Ai | 98 | Metadata | 81 |
| App | 11 | Ocr | 23 |
| Catalogue | 97 | Pdf | 3 |
| ClassroomClient | 106 | Privacy | 2 |
| Domain | 17 | Reader | 80 |
| GoldenCorpus | 3 | SchoolAdmin | 38 |
| Ingestion | 71 | Search | 101 |
| LanHost | 50 | Security | 29 |
|  |  | Shelf3D | 34 |

**Skips and gating:**

- No `Skip=` anywhere.
- `Category=Benchmark` is not filtered by CI (`.github/workflows/ci.yml:97`).
- Early-return "passes" hide platform gaps: real OCR on macOS, Windows-only write-back, and the
  Keychain path.

**Blind spots:**

- **No coverage at all:** `AvaloniaPreviewGate`, `NativeWebViewHostAdapter`,
  `AiProxyEndpointHandler`, and the shelf3d TypeScript.
- **Thin coverage:** `ReaderViewModel` (2 files for 2.7k LOC), `HostSharingViewModel`,
  `SearchViewModel`, `SplitViewViewModel`, `PasswordUnlockViewModel`, and the workers.

**Tests that lock in current gaps:**

- `App/Phase02CompositionTests.cs:32,67` and `ArchitectureTests.cs:178,207` assert that
  `IAiProvider` is `AiDisabledProvider` and that no metadata provider is registered. Phases 08 and
  15 must change these deliberately.
- AI gateway tests use fakes with a non-Offline tier, so they prove a path the runtime cannot reach.

**Working tree:** both test projects are deleted (uncommitted) and still referenced by the solution
(K01).

## 7. Settings and configuration

### 7.1 What a user can configure in the UI

| Setting | Where | Persisted |
|---|---|---|
| Library root (single) | Choose folder | `library-settings.json` (non-atomic write, K28) |
| Theme and density | Command palette only | `user-preferences.json` (atomic) |
| View mode, sort, filters, page | Catalogue toolbar | View-state store |
| Shelves, tags, rating, favourite, status | Sidebar and detail | SQLite |
| Reader annotations | Reader | SQLite |
| Index, embeddings, OCR queue | Index Manager | SQLite |
| Host, client, school admin | Sharing (env flag only) | Files and OS credential store |
| AI tier, provider, key | **Nowhere** | Tier in memory only |
| Excluded folders | **Nowhere** | — |
| Language | **Nowhere** | — |

### 7.2 Environment variables (`App/Configuration/OgmaRuntimeOptions.cs:33-64`)

| Variable | Default | Effect | Target (Phase 08) |
|---|---|---|---|
| `OGMA_LIBRARY_DATA_DIR` | `%LOCALAPPDATA%\Ogma Library Data` | Catalogue DB and settings location | Keep as an admin/test override |
| `OGMA_LIBRARY_ROOT` | The data directory | Startup root for assets, sidecars and OCR; does **not** start a scan (**MEASURED**) | Remove; roots come from Settings |
| `OGMA_ENABLE_METADATA_PROVIDERS` | false | Online metadata and covers | Settings toggle with disclosure |
| `OGMA_ENABLE_CLASSROOM_HOST` | false | Whole Sharing/Host/Client UI | Settings mode selector |
| `OGMA_ENABLE_3D_SHELF` | false | Diagnostics text only | Remove; use capability detection |
| `OGMA_PDF_WORKER_PATH` | Auto-discovered | Worker location | Keep as a diagnostic override |

### 7.3 Defaults that make features look broken

1. The Advisor and Reading plan are always disabled; fixing that needs code, not configuration.
2. The first-run empty state is hidden (K10), and the root is the data directory until a folder is
   chosen.
3. "Enrich" is local-only.
4. Sharing is blank.
5. Semantic search needs Ollama, and says "active" when it is not (K41).
6. OCR supports English only.
7. 3D falls back even with WebView2 present.
8. The client connection is lost on restart.
9. French is implemented but unreachable.
