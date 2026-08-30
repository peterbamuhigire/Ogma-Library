# Phase 05 Completion - Library Roots and Path Security

Date: 2026-08-30

## Delivered

- Added `LibraryRootStatus` and `LibraryRootPermissionStatus` domain contracts.
- Added application contracts for root descriptors, bounded probes, platform
  adapters, add/list/relink/enable/health/scan-success operations.
- Extended `LibraryRoots` with canonical locator, volume hint, permission state,
  enablement, explicit symlink policy, health timestamp and successful-scan
  timestamp fields.
- Added `Phase05LibraryRoots` EF migration with a nullable locator for safe
  compatibility-root upgrades and a unique locator index.
- Added the filesystem platform adapter. It resolves existing link segments,
  applies boundary-aware path comparison through `PathGuard`, and probes no
  more than one directory entry.
- Added the durable `LibraryRootService` and registered it in the application
  ingestion module.
- Updated PDF discovery to canonicalize every emitted path and reject escaped
  symlink/reparse-point paths.

## Acceptance evidence

`Phase05LibraryRootTests` covers:

1. canonical root persistence and initial health/permission state;
2. identity-preserving relink and disable-without-delete semantics;
3. compatibility roots without locators requesting relink;
4. duplicate canonical-locator rejection.

Existing `PathGuardTests` covers encoded traversal, rooted/UNC escapes, and
symlink escape. The phase 4 migration suite exercises application of the full
migration chain including this migration.

## Boundary for following phases

Legacy scan records are still represented by `BookFiles` and the legacy settings
compatibility service. Binding discovery, processing jobs and file occurrences
to root IDs is intentionally delivered in phases 6-8; this phase supplies the
durable root contract and safe path authority those phases consume.
