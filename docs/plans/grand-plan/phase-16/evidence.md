# Phase 16 Evidence

Date started: 2026-06-01

## Current Status

Phase 16 WP1/WP2/WP10 is underway. The first implementation slices establish
the LAN Host bounded-context contract, keep the default standalone product
listener-free, and add durable local Host settings/session persistence:

- `OgmaLibrary.Application.LanHost` defines Host-mode status/settings records
  and contracts for host control, client sessions, certificate provisioning,
  mDNS advertising, and settings persistence.
- `OgmaLibrary.Infrastructure.LanHost` provides no-listener scaffold
  implementations registered through DI.
- Host mode defaults to disabled, port `7473`, and page-render content delivery.
- `HostModeSettings` and `HostClientSessions` are now EF-backed SQLite tables
  created by `20260601184330_Phase16LanHostTables`.
- Client bearer tokens are generated as random 32-byte values and only SHA-256
  hashes are persisted; Host stop revokes active sessions by setting
  `RevokedUtc`.
- The scaffold can start/stop its coordinator, advertise the planned mDNS record
  shape, and revoke persisted sessions on stop without binding a network port.
- Architecture tests guard the LanHost boundary from credential-store,
  worker, and AI-provider dependencies, and assert standalone infrastructure
  has no listener references.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LanHostScaffoldTests\|FullyQualifiedName~LanHostPersistenceTests" --logger "console;verbosity=detailed"` | Passed: 5 scaffold/persistence tests for standalone-safe defaults, migration creation, settings round-trip, token hashing, and session revocation |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ArchTests_LanHost\|FullyQualifiedName~ArchTests_StandaloneMode" --logger "console;verbosity=detailed"` | Passed: 3 architecture tests for credential/worker/AI isolation and no standalone listener references |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after migration and formatting: 10 projects, 0 warnings, 0 errors |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| LanHost application contracts | `src/OgmaLibrary.Application/LanHost/` |
| LanHost infrastructure scaffold | `src/OgmaLibrary.Infrastructure/LanHost/` |
| LanHost EF entities/configurations | `HostModeSettingsRow`, `HostClientSessionRow`, and matching EF configurations |
| LanHost persistence migration | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/20260601184330_Phase16LanHostTables.cs` |
| DI registration | `CompositionRoot.AddOgmaLibrary()` calls `AddLanHostServices()` |
| Architecture guardrails | `ArchTests_LanHost_*` and `ArchTests_StandaloneMode_HasNoOpenListener` |

## Remaining Phase 16 Work

- WP1 certificate provisioner and mDNS real adapters.
- WP3+ HTTPS endpoints, auth, audit, UI, and load tests.
