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

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed
  with 0 warnings and 0 errors.
- Classroom-client slice: 110 passed.
- Complete current Release solution suite: 1,109 passed (913 core, 41
  architecture, 155 UI), with 0 failures and 0 skips.
- Focused HostSharingViewModel suite: 18 passed, 0 failed, 0 skipped; the
  classroom-client slice remains 110 passed, 0 failed, 0 skipped.
- Added tests for cache tamper rejection, certificate-rotation cache isolation,
  oversized sync payload rejection, and host-scoped cache archive export.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-35-local-gate-reconciliation-2026-09-04.md`.

## Remaining phase gate

Physical Windows/macOS credential-store and host/client pairing evidence,
network-drop/reconnect with a renewed session, offline reader UX and
accessibility capture, two-user hostile isolation, and cross-machine load
evidence remain release gates.
