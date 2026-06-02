# Phase 17 Evidence

Date started: 2026-06-02

## Current Status

Phase 17 WP1 is underway locally. The first slice establishes the
Client/Classroom mode decision record and inactive bounded-context scaffold:

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
- `FileClassroomModeService` persists Standalone/Connect-to-Host runtime mode
  settings under the sidecar classroom state while keeping Standalone as the
  missing-file default.
- `StudentPrivateRepository` derives per-profile private database paths under
  `classroom/profiles/<profileId>/private.db`, creates the private SQLite
  schema in code, and persists isolated reading progress, annotations,
  bookmarks, AI history, and sync state without touching the standalone
  catalogue database.
- `DiskOfflineCacheService` stores Host resources under `classroom/cache`,
  preserves eTags and content across app restarts, scopes entries by Host and
  resource, clears one Host at a time, and enforces size-limit eviction with LRU
  last-access metadata.
- `LibraryHostHttpClient` maps the Phase 16 Host health endpoint, enrollment
  session endpoint, authenticated catalogue page endpoint, page-render PNG
  endpoint, raw PDF file-stream endpoint, and projected asset URLs into
  Client-mode application records, including bearer-token handling.
- Architecture tests guard the Classroom Client context from depending on the
  Phase 16 Host server implementation or the standalone catalogue
  infrastructure.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ClassroomClientScaffoldTests\|FullyQualifiedName~ClassroomJoinParserTests\|FullyQualifiedName~MdnsResolverTests\|FullyQualifiedName~HostTrustServiceTests\|FullyQualifiedName~ProfileServiceTests\|FullyQualifiedName~ClassroomModeServiceTests\|FullyQualifiedName~StudentPrivateRepositoryTests\|FullyQualifiedName~OfflineCacheServiceTests\|FullyQualifiedName~LibraryHostHttpClientTests" --logger "console;verbosity=minimal"` | Passed: 42 Classroom Client tests for default Standalone mode, persistent vs guest profile behavior, per-profile private DB paths, Host/resource-scoped offline cache entries, Phase 16 `ogma-lan://` join parsing, legacy plan URI parsing, chunked fingerprint normalization, malformed payload rejection, parser DI registration, mDNS Host record projection, observable discovery emissions, invalid fingerprint filtering, resolver DI registration, first-use TOFU evaluation, explicit accept pinning, trusted matching pins, mismatch rejection, trust-service DI registration, file-backed profile persistence, transient guest sessions, credential-store session token keys, delete cleanup, mode persistence across restart, private SQLite DB persistence, cross-profile annotation isolation, sync tombstones, bookmarks, AI history, sync state, disk cache persistence, per-Host cache clearing, LRU eviction, cache DI registration, Host health mapping, enrollment session issuance, authenticated catalogue page mapping, page-render resource reads, file-stream reads, and projected asset reads |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_ClassroomClient\|FullyQualifiedName~ArchTests_StandaloneMode_HasClassroomClientInactiveByDefault" --logger "console;verbosity=minimal"` | Passed: 3 Classroom Client architecture/default-mode guardrails |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed: 30 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 513 tests |
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
| TOFU trust-pin logic | `IHostTrustService`, `IHostTrustStore`, `HostTrustService`, and `InMemoryHostTrustStore` |
| TOFU trust-pin tests | `HostTrustServiceTests` |
| Profile/session management | `FileClassroomProfileService` and `IClassroomCredentialStore` |
| Profile/session tests | `ProfileServiceTests` |
| Runtime mode persistence | `FileClassroomModeService` |
| Runtime mode tests | `ClassroomModeServiceTests` |
| Private student DB schema/CRUD | `StudentDbContext` and `StudentPrivateRepository` |
| Private student DB tests | `StudentPrivateRepositoryTests` |
| Offline cache foundation | `DiskOfflineCacheService` |
| Offline cache tests | `OfflineCacheServiceTests` |
| Host API client foundation | `LibraryHostHttpClient` health, session, catalogue-page, page-render, file-stream, and asset methods |
| Host API client tests | `LibraryHostHttpClientTests` |
| Architecture guardrails | `ArchTests_ClassroomClient_*` and default-Standalone guard |

## Remaining Phase 17 Work

- Owner ratification for ADR-0012.
- OS-backed credential storage for Host trust pins and session tokens, live
  certificate fetch integration, Host API detail/search methods,
  reader/cache integration, sync, UI, offline fallback orchestration,
  profile-management polish, and cross-platform real-LAN verification.
