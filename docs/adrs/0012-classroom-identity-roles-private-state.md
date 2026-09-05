# ADR-0012: Classroom Identity, Roles, and Private State

## Status

Accepted

> Owner-ratified on 2026-09-05. Client/Classroom mode remains subject to the
> physical network, credential-store, privacy, and release gates recorded in
> the execution ledger.

> Drafted at the start of Phase 17 and owner-ratified on 2026-09-05. Exposure
> outside development builds remains subject to the physical and release gates
> recorded in the execution ledger.

## Date

2026-06-02

## Context

Phase 16 added an opt-in Library Host mode that can serve a school catalogue
over the LAN. Phase 17 adds the matching Client/Classroom mode: a student
installation discovers or manually joins a Host, verifies the Host certificate
fingerprint, selects a profile, and reads from the school library while keeping
private reading state local by default.

The design must preserve three invariants:

- Standalone mode remains the default and must not open network connections or
  alter the local catalogue.
- Student annotations, bookmarks, reading progress, and AI history are private
  per profile and are not passively visible to peers or the Host.
- The client architecture must be forward-compatible with optional sync and the
  later school-admin track without requiring a second product.

## Decision Drivers

- Minimise privacy risk for students, including minors.
- Keep the existing single-user catalogue and reader flows stable.
- Allow offline reading from cache after a LAN drop.
- Give Phase 18 a role/profile model to build admin enrollment on.
- Avoid coupling the client context to the Phase 16 Host server implementation.

## Considered Options

### Option A - Per-profile private client databases

Each client profile owns a private SQLite database under the Ogma sidecar root:
`classroom/profiles/<profileId>/private.db`. The Host knows profile metadata and
session role but not the contents of private annotations or reading history
unless the student explicitly opts into sync.

- **Pros:** strong local privacy boundary, simple backup/erasure story, offline
  reads and writes are natural, and the main catalogue DB is untouched.
- **Cons:** profile switching needs careful path handling and file-permission
  checks; sync requires conflict handling.

### Option B - Shared client database with profile columns

All classroom users on a machine share one SQLite database with `ProfileId`
columns on private-state tables.

- **Pros:** simpler schema management and fewer files.
- **Cons:** accidental cross-profile reads become easier, erasure is riskier,
  and file permissions cannot isolate profiles.

### Option C - Store private state on the Host by default

All student state is written directly to the Host under profile IDs.

- **Pros:** students can roam between devices without sync setup.
- **Cons:** privacy-by-default is lost, offline mode becomes fragile, and the
  Host becomes a much higher-value student-data target.

## Decision Outcome

Choose **Option A: per-profile private client databases**.

The Classroom Client bounded context owns:

- Runtime mode state: `Standalone` or `ConnectToHost`. Standalone is the default.
- Profile records: `ProfileId` as UUID v4, display name, and role.
- Roles: `Student`, `Teacher`, and `Guest`.
- Private profile DB path: `<sidecar>/classroom/profiles/<profileId>/private.db`.
- Offline cache path: `<sidecar>/classroom/cache/`.
- Host join metadata: host address, port, and pinned Host CA fingerprint.
- Optional sync state and conflict metadata.

The client consumes the Phase 16 Host API through a typed `ILibraryHostClient`
contract. It must not depend on `OgmaLibrary.Infrastructure.LanHost`, and it
must not write to the standalone catalogue database. Network calls, profile
credential storage, and sync remain behind Application-layer contracts.

Guest sessions are transient and never create a private database. Teacher roles
are represented in the client token/profile model, but curation and admin powers
remain out of scope until Phase 18.

## Consequences

### Positive

- Student privacy is structurally enforced by a file and repository boundary.
- Standalone catalogue state is untouched by Client mode.
- Offline operation can be implemented incrementally without changing reader UI
  surfaces.
- Phase 18 can add managed enrollment and curation on top of the same profile
  and role model.

### Negative

- The product must manage profile DB lifecycle, permissions, deletion, and
  migration for many small databases.
- Sync requires explicit merge/conflict design because the private database can
  change offline.
- A second local storage root for cache/profile data needs clear diagnostics and
  support tooling.

### Affects

- Phase 17 Client/Classroom mode.
- Phase 18 school admin and managed AI.
- Phase 19 threat model and data-protection controls.
- CTRL-OGMA-001 and CTRL-OGMA-016.

## Amendment Log

| Date | Change |
| --- | --- |
| 2026-06-02 | Initial Phase 17 draft. |
| 2026-09-05 | Accepted by owner direction after implementation and local verification; physical release gates remain open. |
