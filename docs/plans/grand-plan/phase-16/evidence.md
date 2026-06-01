# Phase 16 Evidence

Date started: 2026-06-01

## Current Status

Phase 16 WP1/WP2/WP10 is underway. The first implementation slices establish
the LAN Host bounded-context contract, keep the default standalone product
listener-free, add durable local Host settings/session persistence, replace
the scaffold certificate fingerprint with a real local X.509 Host CA, wire the
discovery boundary to a real DNS-SD adapter, and add the first opt-in HTTPS Host
listener endpoints:

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
- `LocalCertificateProvisioner` creates a self-signed X.509 root CA, returns a
  SHA-256 certificate fingerprint, and reloads the same certificate across Host
  starts. On Windows the PFX is DPAPI-protected on disk; non-Windows currently
  uses a restricted-file fallback until the macOS Keychain adapter lands.
- `MdnsAdvertiser` wraps `Makaretu.Dns.Multicast` and validates DNS-SD service
  type, instance name, port, and TXT record sizes before advertising
  `_ogma-library._tcp`.
- `KestrelHostModeListener` binds HTTPS on loopback only for the first endpoint
  slice. It exposes `/api/v1/health`, `/api/v1/auth/session`, and authenticated
  `/api/v1/catalogue`; unauthenticated catalogue requests return `401`.
- The listener uses a short-lived server certificate with `localhost` and
  `127.0.0.1` SANs issued from the persisted Host CA; the advertised fingerprint
  remains the Host CA SHA-256 fingerprint.
- Every Host request currently writes `LanHostRequestServed` to `AuditEvents`
  with method, path, status code, remote IP, elapsed time, and a hashed session
  actor when authenticated. Raw bearer tokens are not written to the audit row.
- The scaffold can start/stop its coordinator, advertise the planned mDNS record
  shape, and revoke persisted sessions on stop without binding a network port.
- Architecture tests guard the LanHost boundary from credential-store,
  worker, and AI-provider dependencies, and assert standalone infrastructure
  has no listener references.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LanHostScaffoldTests\|FullyQualifiedName~LanHostPersistenceTests\|FullyQualifiedName~LanHostCertificateProvisionerTests\|FullyQualifiedName~MdnsAdvertiserTests\|FullyQualifiedName~LanHostEndpointTests" --logger "console;verbosity=minimal"` | Passed: 13 scaffold/persistence/certificate/mDNS/listener tests for standalone-safe defaults, migration creation, settings round-trip, token hashing, session revocation, valid X.509 root generation, stable fingerprint reload, no certificate material in catalogue DB, mDNS registration lifecycle, TXT fingerprint, DNS-SD size validation, HTTPS health, session issue, catalogue 401, authenticated catalogue projection, and LAN request audit rows |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ArchTests_LanHost\|FullyQualifiedName~ArchTests_StandaloneMode" --logger "console;verbosity=detailed"` | Passed: 3 architecture tests for credential/worker/AI isolation and no standalone listener references |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after migration and formatting: 10 projects, 0 warnings, 0 errors |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| LanHost application contracts | `src/OgmaLibrary.Application/LanHost/` |
| LanHost infrastructure scaffold | `src/OgmaLibrary.Infrastructure/LanHost/` |
| LanHost certificate provisioner | `LocalCertificateProvisioner` creates/reloads the Host CA and exposes stable SHA-256 fingerprint |
| LanHost mDNS advertiser | `MdnsAdvertiser` wraps `Makaretu.Dns.Multicast` behind `IMdnsAdvertiser` |
| LanHost HTTPS listener | `KestrelHostModeListener` exposes loopback health/auth/catalogue endpoints behind bearer session validation |
| LanHost request audit | `LanHostRequestServed` rows in `AuditEvents` for health, session, unauthorized, and authenticated catalogue requests |
| LanHost EF entities/configurations | `HostModeSettingsRow`, `HostClientSessionRow`, and matching EF configurations |
| LanHost persistence migration | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/20260601184330_Phase16LanHostTables.cs` |
| DI registration | `CompositionRoot.AddOgmaLibrary()` calls `AddLanHostServices()` |
| Architecture guardrails | `ArchTests_LanHost_*` and `ArchTests_StandaloneMode_HasNoOpenListener` |

## Remaining Phase 16 Work

- WP1 macOS Keychain-specific CA private-key storage and real same-subnet
  mDNS discovery verification on macOS/Windows runners.
- WP3+ LAN-bound listener interface selection, catalogue pagination contract
  hardening, asset/page-render/file-stream endpoints, UI, and load tests.
