# Phase 17 Evidence

Date started: 2026-06-02

## Current Status

Phase 17 implementation is underway locally. The completed slices establish the
Client/Classroom mode decision record, inactive bounded-context scaffold, Host
client foundation, private-state storage, offline cache, and first reader-file
bridge:

- `docs/adrs/0012-classroom-identity-roles-private-state.md` records the
  proposed identity, role, per-profile private database, offline cache, and sync
  boundaries.
- `OgmaLibrary.Application.ClassroomClient` defines mode, profile, Host join,
  offline cache, sync, private-state repository, and Host-client contracts.
- `OgmaLibrary.Infrastructure.ClassroomClient` registers inactive default
  implementations. Standalone remains the default mode and no Host client
  network activity is active.
- `ClassroomJoinParser` parses and validates the current Phase 16
  `ogma-lan://<host>:<port>/join?...` QR/manual payload and the older
  plan-documented `ogma://host?addr=<host>:<port>&fp=<sha256>` shape.
- `MdnsResolver` scans for `_ogma-library._tcp` DNS-SD records, validates
  discovered records through the join parser, exposes an observable stream, and
  returns bounded discovery results for the onboarding UI.
- `HostTrustService` evaluates Client-mode TOFU trust decisions: first use
  requires explicit acceptance, matching pins are trusted, and mismatched
  presented fingerprints are rejected before persistence.
- `FileClassroomProfileService` persists student/teacher profile metadata and
  active selection, keeps guest sessions transient, creates/deletes per-profile
  private-state folders, and stores Host session tokens behind the classroom
  credential-store seam.
- `PlatformClassroomCredentialStore` maps classroom session-token and Host
  trust-pin secrets to Windows Credential Manager on Windows, macOS Keychain on
  macOS, and a restricted file fallback on unsupported platforms; tests use
  injected fake backends so the suite does not write to the real user credential
  store.
- `CredentialBackedHostTrustStore` persists TOFU Host certificate pins through
  the same classroom credential-store boundary instead of keeping accepted pins
  only in process memory.
- `FileClassroomModeService` persists Standalone/Connect-to-Host runtime mode
  settings under the sidecar classroom state while keeping Standalone as the
  missing-file default.
- `IClassroomModeService` now exposes runtime online/offline connectivity state
  and an observable status stream for future offline chips and fallback
  orchestration.
- `MainShellViewModel` subscribes to Client-mode connectivity changes and the
  shell footer renders an accessible offline chip when the classroom Host link
  is down while `ConnectToHost` mode is active.
- `IClassroomHostConnectionService` stores the active runtime Host connection
  for Client mode, and `ClassroomBookFileLocator` switches the existing reader
  file-location boundary from local catalogue files to materialized Host PDFs
  when the runtime mode is `ConnectToHost`.
- `ClassroomConnectionService` coordinates the onboarding connection flow:
  live Host health/fingerprint lookup when a caller has not already supplied a
  presented fingerprint, trust evaluation/acceptance, profile resolution or
  creation, Host session issuance, session-token persistence for non-guest
  profiles, Client-mode activation, online status publication, and active Host
  connection storage.
- `ClassroomCatalogueReadModel` keeps the existing catalogue UI on
  `ICatalogueReadModel` while switching to Host catalogue/detail/progress
  projections in Client mode when an active Host connection exists.
- The Sharing settings surface now includes a Client-mode "Connect to Host"
  control path that accepts a join link, profile/guest choice, and explicit
  first-use trust confirmation, then invokes the classroom connection service
  and reports connection status in-place.
- `MainShellViewModel` now listens for successful Client-mode Host connections,
  returns the student from Sharing settings to the catalogue, refreshes the
  mode-aware catalogue and shelf sidebar, and closes stale detail panels so Host
  books are immediately selectable for the existing detail/read flow.
- The shell has an app-level real-PDF proof: a generated PDF is selected through
  `OpenPdfPathAsync`, registered by production direct-open services, resolved by
  the production file locator, and opened into a reader session.
- `StudentPrivateRepository` derives per-profile private database paths under
  `classroom/profiles/<profileId>/private.db`, creates the private SQLite
  schema in code, and persists isolated reading progress, annotations,
  bookmarks, AI history, and sync state without touching the standalone
  catalogue database.
- `ClassroomSyncBlobCodec` serializes a private-state snapshot, compresses it
  with Brotli, encrypts it with AES-256-GCM using a session-token-derived
  AES-256 key, and rejects decryption with the wrong session token.
- `ClassroomSyncService` implements the explicit manual sync action for
  persistent student/teacher profiles: it exports host-scoped private reading
  progress, annotation/bookmark tombstones, and AI history, encrypts the
  snapshot, uploads it to the Host, downloads the stored blob for integrity
  confirmation, and records the resulting sync hash/status in the private DB.
- `ClassroomSyncService` also downloads an existing Host sync blob before
  upload, decrypts it, merges remote private state into the local private DB
  with last-write-wins by timestamp, keeps local rows when same-timestamp
  content conflicts require future student choice, and persists the conflict
  count in `StudentSyncState`.
- The Sharing settings Client-mode section now includes a private-sync status
  row with last-synced/conflict text and a manual `Sync now` action wired to
  `ISyncService`; the button is disabled until an active persistent classroom
  profile and Host connection make sync available.
- The LAN Host now exposes authenticated `PUT /api/v1/profile/sync` and
  `GET /api/v1/profile/sync` endpoints backed by `FileProfileSyncBlobStore`,
  storing the encrypted client payload opaquely by enrolled profile/client id.
- `DiskOfflineCacheService` stores Host resources under `classroom/cache`,
  preserves eTags and content across app restarts, scopes entries by Host and
  resource, clears one Host at a time, and enforces size-limit eviction with LRU
  last-access metadata.
- `CachingLibraryHostClient` wraps Host page-render, file-stream, and sidecar
  asset reads with cache-aside storage and cache-hit fallback so reader resource
  calls can be served from disk without touching the network when already cached.
- `ClassroomBookFileMaterializer` turns Host file-stream PDF resources into
  stable local `.pdf` files under `classroom/files`, using atomic writes and
  sidecar metadata so the existing path-based PDF renderer can open Host books.
- `LibraryHostHttpClient` maps the Phase 16 Host health endpoint, enrollment
  session endpoint, authenticated catalogue page endpoint, page-render PNG
  endpoint, raw PDF file-stream endpoint, projected asset URLs, book-detail
  endpoint, metadata-search endpoint, and profile sync upload/download
  endpoints into Client-mode application records, including bearer-token
  handling.
- Architecture tests guard the Classroom Client context from depending on the
  Phase 16 Host server implementation or the standalone catalogue
  infrastructure.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ClassroomSyncServiceTests\|FullyQualifiedName~ClassroomSyncBlobCodecTests\|FullyQualifiedName~StudentPrivateRepositoryTests" --logger "console;verbosity=minimal"` | Passed: 13 focused merge/sync tests for encrypted sync blob round-trip, wrong-token rejection, host-wide private-state listing, explicit sync upload/download, remote-newer last-write-wins merge, same-timestamp annotation conflict counting, sync hash persistence, saved sync status projection, guest sync rejection, and disconnected sync rejection. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ClassroomClientScaffoldTests\|FullyQualifiedName~ClassroomJoinParserTests\|FullyQualifiedName~MdnsResolverTests\|FullyQualifiedName~HostTrustServiceTests\|FullyQualifiedName~ClassroomCredentialStoreTests\|FullyQualifiedName~ClassroomSyncBlobCodecTests\|FullyQualifiedName~ClassroomSyncServiceTests\|FullyQualifiedName~ProfileServiceTests\|FullyQualifiedName~ClassroomModeServiceTests\|FullyQualifiedName~StudentPrivateRepositoryTests\|FullyQualifiedName~OfflineCacheServiceTests\|FullyQualifiedName~LibraryHostHttpClientTests\|FullyQualifiedName~CachingLibraryHostClientTests\|FullyQualifiedName~ClassroomBookFileMaterializerTests\|FullyQualifiedName~ClassroomBookFileLocatorTests\|FullyQualifiedName~ClassroomConnectionServiceTests\|FullyQualifiedName~ClassroomCatalogueReadModelTests\|FullyQualifiedName~HostSharingViewModelTests" --logger "console;verbosity=minimal"` | Passed: 97 Classroom Client tests for default Standalone mode, persistent vs guest profile behavior, platform-scoped classroom credential keys, fake Windows Credential Manager and macOS Keychain adapters, restricted file fallback, credential-backed TOFU pin persistence, encrypted private-state sync blob round-trip, AES-GCM wrong-token rejection, sync codec DI registration, explicit sync service DI registration, per-profile private DB paths, host-wide private-state export, Host/resource-scoped offline cache entries, Phase 16 `ogma-lan://` join parsing, legacy plan URI parsing, chunked fingerprint normalization, malformed payload rejection, parser DI registration, mDNS Host record projection, observable discovery emissions, invalid fingerprint filtering, resolver DI registration, first-use TOFU evaluation, explicit accept pinning, trusted matching pins, mismatch rejection, live Host health/fingerprint lookup before connection trust evaluation, connection-service trust gating, profile creation/selection, guest connection behavior, Host session issuance, active connection storage, online status publication, trust-service DI registration, file-backed profile persistence, transient guest sessions, credential-store session token keys, delete cleanup, mode persistence across restart, online/offline status defaults, observable connectivity emissions, connectivity unsubscribe, active Host connection registration, Host catalogue projection mapping, Host detail/progress mapping, Standalone catalogue delegation, mode-aware catalogue DI shape, Sharing settings connection controls, sync status/settings controls, invalid join status reporting, private SQLite DB persistence, cross-profile annotation isolation, sync tombstones, bookmarks, AI history, sync state, manual sync upload/download orchestration, remote sync download/merge, conflict-count persistence, disk cache persistence, per-Host cache clearing, LRU eviction, cache DI registration, Host health mapping, enrollment session issuance, authenticated catalogue page mapping, page-render resource reads, file-stream reads, projected asset reads, profile sync blob upload/download, book-detail mapping, catalogue search mapping, cache-aside page/file/asset storage, cache-hit network bypass, cached Host-client DI registration, Host PDF materialization to local reader paths, stable file reuse, non-PDF rejection, materializer DI registration, Standalone-vs-Client reader file-location switching, no-active-Host null resolution, and existing reader-session open through the classroom locator |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests" --logger "console;verbosity=minimal"` | Passed: 10 shell/settings UI tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --filter "FullyQualifiedName~LanHostEndpointTests" --logger "console;verbosity=minimal"` | Passed: 1 LAN Host endpoint integration test proving unauthenticated sync upload rejection, authenticated opaque sync blob upload/download, media type preservation, and audit event classification |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_ClassroomClient\|FullyQualifiedName~ArchTests_StandaloneMode_HasClassroomClientInactiveByDefault" --logger "console;verbosity=minimal"` | Passed: 3 Classroom Client architecture/default-mode guardrails |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed: 30 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 565 tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 117 UI tests. Earlier long runs exposed transient Dispatcher/timer starvation in shell refresh and search debounce waits; the tests passed alone, wait helpers were hardened to 10 seconds, and subsequent full serialized UI suites passed. |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| ADR-0012 draft | `docs/adrs/0012-classroom-identity-roles-private-state.md` |
| Application contracts | `src/OgmaLibrary.Application/ClassroomClient/` |
| Infrastructure scaffold | `src/OgmaLibrary.Infrastructure/ClassroomClient/` |
| DI registration | `CompositionRoot.AddOgmaLibrary()` calls `AddClassroomClientServices()` |
| Scaffold tests | `ClassroomClientScaffoldTests` |
| QR/manual join parser | `IClassroomJoinParser` and `ClassroomJoinParser` |
| Join parser tests | `ClassroomJoinParserTests` |
| mDNS resolver | `IMdnsResolver` and `MdnsResolver` |
| mDNS resolver tests | `MdnsResolverTests` |
| TOFU trust-pin logic | `IHostTrustService`, `IHostTrustStore`, `HostTrustService`, and `CredentialBackedHostTrustStore` |
| TOFU trust-pin tests | `HostTrustServiceTests` |
| Platform classroom credential store | `PlatformClassroomCredentialStore`, Windows Credential Manager adapter, macOS Keychain adapter, and restricted file fallback |
| Platform classroom credential tests | `ClassroomCredentialStoreTests` |
| Profile/session management | `FileClassroomProfileService` and `IClassroomCredentialStore` |
| Profile/session tests | `ProfileServiceTests` |
| Runtime mode persistence | `FileClassroomModeService` |
| Runtime mode tests | `ClassroomModeServiceTests` |
| Online/offline state signalling | `IClassroomModeService.Connectivity`, `GetConnectivityAsync`, `SetConnectivityAsync` |
| Online/offline state tests | `ClassroomModeServiceTests` |
| Client-mode offline shell chip | `MainShellViewModel` connectivity subscription and `CatalogueShellView` footer chip |
| Client-mode offline shell chip tests | `ShellReaderNavigationTests.MainShell_ClassroomOfflineChip_VisibleInClientModeAndClearsOnReconnect` and `MainShell_ClassroomOfflineChip_HiddenInStandaloneMode` |
| Active Host connection runtime store | `IClassroomHostConnectionService` and `InMemoryClassroomHostConnectionService` |
| Host connection orchestration | `IClassroomConnectionService` and `ClassroomConnectionService`, including live Host health/fingerprint lookup before trust evaluation |
| Host connection orchestration tests | `ClassroomConnectionServiceTests` |
| Client-mode catalogue source | `ClassroomCatalogueReadModel` |
| Client-mode catalogue source tests | `ClassroomCatalogueReadModelTests` |
| Classroom connection settings UI | `HostSharingViewModel`, `SharingSettingsView`, and `CompositionRoot` connection-service wiring |
| Classroom connection settings UI tests | `HostSharingViewModelTests` and focused `ShellReaderNavigationTests` |
| Connect-to-browse shell bridge | `MainShellViewModel` successful Host connection event handling reloads catalogue/shelves and returns to `ShellView.Catalogue` |
| Connect-to-browse shell bridge tests | `ShellReaderNavigationTests.MainShell_HostConnectionSucceeded_ReloadsCatalogueAndReturnsToCatalogue` |
| Real-PDF shell open proof | `ShellReaderNavigationTests.MainShell_OpenPdfPathAsync_WithRealPdf_RegistersAndOpensReaderSession` uses a generated PDF plus production direct-open, catalogue, and file-locator services |
| Client-mode reader file locator | `ClassroomBookFileLocator` |
| Client-mode reader locator tests | `ClassroomBookFileLocatorTests` |
| Private student DB schema/CRUD | `StudentDbContext` and `StudentPrivateRepository` |
| Private student DB tests | `StudentPrivateRepositoryTests` |
| Encrypted private-state sync blob codec | `IClassroomSyncBlobCodec`, `ClassroomSyncSnapshot`, and `ClassroomSyncBlobCodec` |
| Encrypted private-state sync blob tests | `ClassroomSyncBlobCodecTests` |
| Client sync upload/download orchestration | `ClassroomSyncService`, host-wide `IStudentPrivateRepository` listing methods, and `ILibraryHostClient` profile sync methods |
| Client sync orchestration tests | `ClassroomSyncServiceTests`, `StudentPrivateRepositoryTests`, and `LibraryHostHttpClientTests` |
| Client sync download/merge backend | `ClassroomSyncService` last-write-wins merge and conflict-count persistence |
| Client sync merge tests | `ClassroomSyncServiceTests` |
| Client sync settings controls | `HostSharingViewModel`, `SharingSettingsView`, and `CompositionRoot` sync-service wiring |
| Client sync settings tests | `HostSharingViewModelTests.HostSharingViewModel_SyncNow_ReportsStatusAndCallsSyncService` and focused `ShellReaderNavigationTests` |
| Host opaque sync blob endpoints | `IProfileSyncBlobStore`, `FileProfileSyncBlobStore`, and authenticated `PUT`/`GET /api/v1/profile/sync` routes |
| Host opaque sync blob endpoint tests | `LanHostEndpointTests.HostListener_HealthAuthAndCatalogueProjection_WorkOverHttps` |
| Offline cache foundation | `DiskOfflineCacheService` |
| Offline cache tests | `OfflineCacheServiceTests` |
| Host resource cache-aside | `CachingLibraryHostClient` |
| Host resource cache-aside tests | `CachingLibraryHostClientTests` |
| Host API client foundation | `LibraryHostHttpClient` health, session, catalogue-page, book-detail, search, page-render, file-stream, and asset methods |
| Host API client tests | `LibraryHostHttpClientTests` |
| Host PDF reader-file materializer | `IClassroomBookFileMaterializer` and `ClassroomBookFileMaterializer` |
| Host PDF reader-file tests | `ClassroomBookFileMaterializerTests` |
| Architecture guardrails | `ArchTests_ClassroomClient_*` and default-Standalone guard |

## Remaining Phase 17 Work

- Owner ratification for ADR-0012.
- TLS certificate fingerprint extraction beyond the current Host health
  fingerprint handoff, mDNS picker/QR onboarding polish, sync conflict
  resolution UI, durable sync opt-in preference/sync-on-reconnect,
  profile-management polish, live Windows/macOS credential-store verification,
  Linux secret-service hardening beyond the restricted fallback, and
  cross-platform real-LAN verification.
