# Phase 35 Progress - Classroom Client Offline and Sync

Date: 2026-09-04

## Delivered in this increment

- Added tamper-evident disk-cache metadata with SHA-256 content hashes,
  canonical cache filenames, host/resource metadata matching, and atomic
  content replacement before metadata publication.
- Scoped offline cache entries to the host address/port plus pinned certificate
  fingerprint, preventing stale data reuse after certificate rotation.
- Added bounded compressed-sync payload and decompression limits to resist
  oversized or compression-bomb inputs; temporary plaintext/key material is
  still cleared on codec paths.
- Made classroom sync single-flight so reconnect/manual actions cannot race and
  overwrite private-state snapshots or conflict counters.
- Preserved per-profile encrypted private storage, guest no-sync behavior,
  host-scoped cache eviction, TOFU certificate pinning, and explicit conflict
  resolution semantics.
- Added a versioned, host-scoped ZIP export contract for valid offline-cache
  resources, preserving resource keys, validators, timestamps, content types,
  and payload lengths in a manifest while excluding other Hosts.
- Disk export hashes and copies through asynchronous streams, keeping export
  memory bounded by the I/O buffer rather than the full cache payload.
- Added explicit all-Host cache erasure, including orphaned payload and
  temporary-file cleanup, so local cache deletion does not depend on valid
  metadata being present.
- Added localized Client settings controls for Host-scoped ZIP export and
  explicit all-cache erasure confirmation, with native save-file selection and
  status feedback for unavailable storage or operation failures.
- Made transport failure during connection/session renewal publish an explicit
  offline state while retaining the last Host connection as the cache scope;
  successful reconnect renews and persists the selected profile's session.
- Made first-use private-state key creation single-flight per profile and the
  in-memory credential adapter concurrency-safe, preventing simultaneous
  operations from encrypting rows under competing keys. A concurrent eight-
  annotation write remains decryptable after repository recreation and cannot
  be read through another profile. Evidence:
  `evidence/phase-35-concurrent-profile-key-isolation-2026-09-06.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors.
- Classroom-client slice: 112 passed.
- Complete current Release solution suite: 1,110 passed (914 core, 41
  architecture, 155 UI), with 0 failures and 0 skips.
- Focused HostSharingViewModel suite: 18 passed, 0 failed, 0 skipped; the
  classroom-client slice remains 110 passed, 0 failed, 0 skipped.
- Added tests for cache tamper rejection, certificate-rotation cache isolation,
  oversized sync payload rejection, and host-scoped cache archive export.
- Added a deterministic connected -> network drop -> offline -> reconnect
  regression proving cache-context retention and session-token renewal; the
  focused classroom slice passed 111 tests and the offline-chip UI passed 2.
- Network-drop/reconnect evidence is recorded in
  `evidence/phase-35-network-drop-reconnect-2026-09-06.md`.
- Concurrent private-repository first-use and profile-isolation tests passed
  8/8; the complete classroom-client slice passed 112/112.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-35-local-gate-reconciliation-2026-09-04.md`.
- Current focused classroom-client/sync/private-state/cache verification:
  **114 passed, 0 failed, 0 skipped** on 2026-09-06.

## Remaining phase gate

Physical Windows/macOS credential-store and host/client pairing evidence, a
physical two-machine network interruption and offline-reader UX/accessibility
capture, physical two-user hostile isolation, and cross-machine load evidence
remain release gates.

The Aug-39 Definition of Done now records the locally executable isolation and
cache-management gates as closed. This does not close physical credential
custody, two-machine offline/reconnect, accessibility, or load acceptance.
