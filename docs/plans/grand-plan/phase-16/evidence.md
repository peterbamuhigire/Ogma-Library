# Phase 16 Evidence

Date started: 2026-06-01

## Current Status

Phase 16 WP1/WP2/WP6/WP9/WP10 is underway. The first implementation slices establish
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
  starts. Windows stores the PFX through DPAPI, macOS stores the private-key
  material as a Keychain generic password scoped to the Ogma data directory,
  and non-MVP platforms retain the restricted-file fallback. Existing macOS
  fallback PFX files are migrated into Keychain on first load and then deleted
  best-effort.
- `MdnsAdvertiser` wraps `Makaretu.Dns.Multicast` and validates DNS-SD service
  type, instance name, port, and TXT record sizes before advertising
  `_ogma-library._tcp`.
- `LanBindAddressSelector` prefers active RFC1918 IPv4 LAN addresses and falls
  back to loopback when no private LAN adapter is available. The selected address
  is used by Kestrel and added to the mDNS TXT record as `addr`.
- `LanClientAddressPolicy` rejects non-loopback/non-RFC1918 remote client
  addresses at the Host middleware boundary before auth/session handling; denied
  attempts are still audited.
- `LibraryHostService` generates a fresh in-memory enrollment code on every Host
  start, clears it on stop, passes it to the HTTPS listener, and exposes it only
  to the local sharing UI. The mDNS record advertises the auth method without
  publishing the code.
- `KestrelHostModeListener` binds HTTPS on loopback only for the first endpoint
  fallback or on the selected private LAN address. It exposes `/api/v1/health`,
  `/api/v1/auth/session` guarded by the current enrollment code, authenticated
  `/api/v1/catalogue` with bounded page metadata, authenticated
  `/api/v1/catalogue/search` metadata search, and authenticated
  `/api/v1/catalogue/{bookId}` detail lookup; unauthenticated catalogue requests
  return `401`.
- LAN catalogue list and detail endpoints now return Host-specific DTOs with
  `/api/v1/assets/{cover|spine|thumb}/{sha256}` asset links derived from the
  book content hash. They do not expose `CoverRelativePath`, `RelativePath`, or
  local PDF file names in LAN catalogue JSON.
- Page-render mode now exposes authenticated
  `/api/v1/books/{bookId}/page/{pageNumber}`. The route resolves the PDF through
  the catalogue, treats the URL page number as 1-based, clamps render width, and
  returns PNG bytes. It returns `403` when the Host is switched to FileStream
  mode.
- Page renders are protected by `LanPageRenderLimiter`, a fail-fast concurrency
  guard that caps simultaneous Host render work at 10 requests and returns `429`
  when saturated.
- The catalogue shell now includes a compact Host sharing control strip with
  status, connected-client count, certificate fingerprint preview, and explicit
  start/stop controls bound through `HostSharingViewModel`. The strip also shows
  the active content-delivery mode and allows switching between Page Render and
  File Stream while the Host is stopped.
- A full Settings > Sharing route is now available from the catalogue toolbar.
  It reuses `HostSharingViewModel` for status refresh, content-mode changes,
  Host start/stop confirmations, QR/manual join details, enrollment code display,
  fingerprint display, and clipboard feedback.
- Risky Sharing actions are confirmation-gated in the catalogue shell: starting
  Host mode requires explicit confirmation before the listener opens, and
  switching to File Stream requires explicit confirmation before raw PDF streaming
  is enabled. Cancelling either flow leaves settings unchanged.
- The Host sharing strip now opens a sharing panel with a QR join payload, manual
  `ogma-lan://` join URI, full grouped certificate fingerprint, and copy
  confirmation states. `LibraryHostStatus` exposes the selected Host address and
  current enrollment code so QR/manual sharing uses the same address that Kestrel
  and mDNS advertise, while students can still type the short code if QR scanning
  is unavailable.
- `LanHostLoadSmokeTests` starts the real HTTPS Host on loopback and verifies 20
  concurrent authenticated catalogue clients and 10 concurrent authenticated
  page-render clients complete successfully with local P95 guards under 2
  seconds.
- The listener uses a short-lived server certificate issued from the persisted
  Host CA. The leaf certificate keeps `localhost` and `127.0.0.1` SANs for
  loopback fallback and now also includes the selected bind IP address, so the
  certificate matches the LAN address advertised through mDNS and the QR/manual
  sharing panel. The advertised fingerprint remains the Host CA SHA-256
  fingerprint.
- Every Host request currently writes `LanHostRequestServed` to `AuditEvents`
  with normalized action/resource fields, method, path, status code, remote IP,
  elapsed time, content mode, client id/role when a session is active or newly
  issued, and a short one-way session fingerprint. Raw bearer tokens are not
  written to the audit row.
- Host status now reports active connected clients from unexpired, non-revoked
  client sessions instead of returning a fixed zero.
- Authenticated sidecar assets are available at
  `/api/v1/assets/{cover|spine|thumb}/{sha256}` with optional safe variant
  suffixes. The endpoint serves only cover, spine, and thumbnail sidecars, uses
  SHA-256 hash validation, and rejects malformed asset IDs before file I/O.
- `/api/v1/books/{bookId}/file` returns `403` while the Host is in the default
  page-render mode, ensuring raw PDF bytes do not leave the Host by default.
  When an admin explicitly switches Host content delivery to FileStream, the
  endpoint resolves the book through the catalogue, rejects rooted/traversal
  paths, ignores missing/unavailable files, and streams the PDF with HTTP range
  support.
- LAN request audit payloads include the active Host content delivery mode and
  route-level action/resource classification, so FileStream-mode access,
  page-render requests, catalogue reads, session issuance, and authorization
  rejections are distinguishable in persisted audit rows.
- The scaffold can start/stop its coordinator, advertise the planned mDNS record
  shape, and revoke persisted sessions on stop without binding a network port.
- Architecture tests guard the LanHost boundary from credential-store,
  worker, and AI-provider dependencies, and assert standalone infrastructure
  has no listener references.
- `scripts/Invoke-Phase16LanVerification.ps1` generates a dated Windows/macOS
  LAN verification evidence record with OS/runtime/network context, focused
  Phase 16 automated gates, macOS Keychain service probe, and same-subnet
  mDNS/HTTPS observation fields. `scripts/Test-Phase16VerificationTooling.ps1`
  smoke-tests the evidence generator in draft mode.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LanHostScaffoldTests\|FullyQualifiedName~LanHostPersistenceTests\|FullyQualifiedName~LanHostCertificateProvisionerTests\|FullyQualifiedName~MdnsAdvertiserTests\|FullyQualifiedName~LanBindAddressSelectorTests\|FullyQualifiedName~LanClientAddressPolicyTests\|FullyQualifiedName~LanBookFileResolverTests\|FullyQualifiedName~LanPageRenderLimiterTests\|FullyQualifiedName~LanHostEndpointTests\|FullyQualifiedName~LanHostLoadSmokeTests\|FullyQualifiedName~HostSharingViewModelTests" --logger "console;verbosity=minimal"` | Passed: 48 scaffold/persistence/certificate/mDNS/bind-selector/client-address-policy/resolver/limiter/listener/load/UI tests for standalone-safe defaults, migration creation, settings round-trip, token hashing, session revocation, active client counting, host address status exposure, enrollment code generation/clearing, valid X.509 root generation, injected Host CA-store persistence, macOS Keychain save/load command contract, data-directory-scoped Keychain account names, migration from legacy macOS fallback PFX, selected LAN address SAN generation, stable fingerprint reload, no certificate material in catalogue DB, mDNS registration lifecycle, TXT fingerprint, DNS-SD size validation, RFC1918 bind-address classification, client IP loopback/private allow and public/APIPA deny policy, catalogue-backed FileStream path resolution, traversal/rooted-path rejection, missing/unavailable file rejection, page-render concurrency saturation and lease release, Host sharing start/stop UI state, Host start/File Stream confirmation gates, Host content-mode toggle persistence and running-mode lockout, QR join URI/fingerprint copy and enrollment-code display state, 20 concurrent authenticated catalogue clients, 10 concurrent authenticated page-render clients, HTTPS health, session issue with enrollment code, invalid enrollment code rejection, catalogue 401, authenticated LAN catalogue DTOs with asset URLs and no local paths, paged catalogue metadata, metadata search projection, authenticated book detail lookup, authenticated cover asset serving, malformed asset rejection, page-render-mode PNG serving, FileStream-mode page-render 403, page-render-mode file-stream 403, FileStream-mode PDF streaming, FileStream content-mode audit payloads, and LAN request audit rows |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LanHostCertificateProvisionerTests" --logger "console;verbosity=minimal"` | Passed: 9 certificate provisioning tests, including injected CA-store persistence, macOS Keychain generic-password save/load command contract, data-directory-scoped Keychain account names, and migration from the legacy fallback PFX file into Keychain |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellReaderNavigationTests\|FullyQualifiedName~SearchViewModelTests" --logger "console;verbosity=minimal"` | Passed: 15 shell/UI tests, including the direct PDF reader path, Sharing settings route activation, `SharingSettingsView` headless render, and catalogue-shell construction with the Host sharing panel present. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BookFileLocator_ProductionDi_RepairsMissingBookFilesTableBeforeQuerying\|FullyQualifiedName~DirectPdfOpen_ProductionDi_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf\|FullyQualifiedName~DirectPdfOpen_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf\|FullyQualifiedName~IngestionPipeline_ProductionDi_RepairsMissingBookFilesTableBeforeScanning\|FullyQualifiedName~Migration_RepairsMissingModelTable_WhenHistorySaysCurrent\|FullyQualifiedName~GetBookSummaries_RepairsMissingBookFilesTableBeforeProjectingAvailability" --logger "console;verbosity=minimal"` | Passed: 6 migration repair regression tests after making the Host-mode settings seed insert idempotent during generated create-script repair |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 471 tests. Two earlier default-parallel full-suite attempts exposed timing-sensitive performance-smoke contention (`MetadataSearchServiceTests.PerfBenchmark_MetadataSearch_P95_LessThan150ms` once, then `LanHostLoadSmokeTests.CatalogueEndpoint_HandlesTwentyConcurrentAuthenticatedClients` once); each affected class passed when rerun in isolation, and the full project passed with collection parallelization disabled. |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ArchTests_LanHost\|FullyQualifiedName~ArchTests_StandaloneMode" --logger "console;verbosity=detailed"` | Passed: 3 architecture tests for credential/worker/AI isolation and no standalone listener references |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` | Passed: 27 architecture tests |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-Phase16VerificationTooling.ps1` | Passed: draft-mode evidence generation smoke for Phase 16 LAN verification tooling |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Invoke-Phase16LanVerification.ps1 ...` | Passed on Windows development machine; generated `docs/qa/evidence/phase16-lan-verification-20260602-023001.md` with OS/runtime/network context, format/build/LAN/architecture command outputs, and pending real same-subnet/macOS observations |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after migration and formatting: 10 projects, 0 warnings, 0 errors |

Note: one parallel verification attempt collided on shared Release output files
(`OgmaLibrary.App.pdb`) while build/tests were running concurrently. Sequential
reruns passed.

## Implemented Locally

| Area | Evidence |
| --- | --- |
| LanHost application contracts | `src/OgmaLibrary.Application/LanHost/` |
| LanHost infrastructure scaffold | `src/OgmaLibrary.Infrastructure/LanHost/` |
| LanHost certificate provisioner | `LocalCertificateProvisioner` creates/reloads the Host CA and exposes stable SHA-256 fingerprint |
| LanHost CA private-key storage | Windows DPAPI store, macOS Keychain generic-password store, and restricted-file fallback for non-MVP platforms are selected behind `IHostCaStore`; macOS Keychain account names are scoped to the Ogma data directory, and legacy fallback PFX files migrate into Keychain on load |
| LanHost server certificate SANs | `LocalCertificateProvisioner` issues listener leaf certificates with loopback SANs plus the selected LAN bind IP address |
| LanHost mDNS advertiser | `MdnsAdvertiser` wraps `Makaretu.Dns.Multicast` behind `IMdnsAdvertiser` |
| LanHost bind-address selector | Prefers active RFC1918 IPv4 LAN addresses, falls back to loopback, and advertises the selected address in mDNS TXT |
| LanHost client-address policy | Allows loopback fallback and RFC1918 private IPv4 clients; rejects public/APIPA/IPv6 internet addresses before auth |
| LanHost session bootstrap | Host start creates an in-memory enrollment code; `/api/v1/auth/session` rejects missing/invalid codes before issuing bearer sessions |
| LanHost HTTPS listener | `KestrelHostModeListener` exposes selected-address health/auth/catalogue endpoints behind bearer session validation |
| LanHost catalogue contract | Authenticated catalogue list supports bounded `page`/`pageSize` metadata, metadata search projection, single-book detail lookup, LAN-safe asset URL DTOs, and no local path exposure |
| LanHost asset endpoint | Authenticated cover/spine/thumbnail sidecar endpoint with SHA-256 hash validation |
| LanHost page-render endpoint | Authenticated page-render endpoint returns PNG bytes in PageRender mode and rejects requests in FileStream mode |
| LanHost page-render limiter | Caps simultaneous page renders at 10 and fails fast with `429` when saturated |
| LanHost file-stream endpoint | Default page-render mode returns `403`; explicit FileStream mode streams catalogue-resolved PDFs with path traversal/rooted-path protection and range support |
| LanHost request audit | `LanHostRequestServed` rows in `AuditEvents` for health, session, unauthorized, authenticated catalogue, asset, page-render, and FileStream requests; payloads include action, resource type/id, status code, active content mode, client id/role when resolved, and a token fingerprint without raw bearer tokens |
| Host sharing UI scaffold | Catalogue shell status strip and Settings > Sharing route include Host status, active client count, fingerprint preview, content-mode toggle, explicit start/stop controls, Host/FileStream confirmation panels, QR join panel, manual join URI, full fingerprint display, enrollment code display, and copy confirmation state |
| LanHost catalogue/page-render load smoke | Live HTTPS loopback Host handles 20 concurrent authenticated catalogue requests and 10 concurrent authenticated page renders with local P95 under 2 seconds |
| LanHost EF entities/configurations | `HostModeSettingsRow`, `HostClientSessionRow`, and matching EF configurations |
| LanHost persistence migration | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/20260601184330_Phase16LanHostTables.cs` |
| DI registration | `CompositionRoot.AddOgmaLibrary()` calls `AddLanHostServices()` |
| Architecture guardrails | `ArchTests_LanHost_*` and `ArchTests_StandaloneMode_HasNoOpenListener` |
| Phase 16 verification tooling | `scripts/Invoke-Phase16LanVerification.ps1` and `scripts/Test-Phase16VerificationTooling.ps1` |

## Remaining Phase 16 Work

ADR-0010 is accepted, its CI-2 scope amendment is explicit, and the canonical
source summary now links that decision to Standalone and classroom behavior.
The existing Windows/macOS integration, migration, and architecture evidence
also closes the HTTPS start/stop, deterministic catalogue projection,
page-render containment, explicit file-stream opt-in, bounded-context
isolation, migration round-trip, and Standalone no-listener checklist entries.
This reconciliation does not close same-subnet mDNS, the stricter 800 ms
performance target, exhaustive request-audit coverage, or independent review.

Dedicated schema proof: `Phase16Migration_AddsLanHostTablesAndRoundTrips`
migrates to Phase 16, verifies both Host tables, both indexes, and the disabled
default row, downgrades to the Phase 15 boundary, and remigrates successfully.

The real HTTPS endpoint flow now requires authenticated audit events for its
catalogue, search, detail, sync, asset, page/file delivery, managed-AI, and
Host-local admin route set. Token values remain explicitly absent from every
captured audit payload.

The isolated, warmed 20-client authenticated catalogue smoke now enforces the
phase's original P95 budget directly (`<= 800 ms`) and emits the measured P95
to the test log. Three consecutive local runs measured 149 ms, 146 ms, and
149 ms. The prior 2,000 ms interim ceiling is no longer accepted.

- Real same-subnet mDNS discovery and HTTPS binding verification on Windows and
  macOS runners.
- Real macOS Keychain execution evidence for the Host CA store on a macOS runner
  or reference machine.
