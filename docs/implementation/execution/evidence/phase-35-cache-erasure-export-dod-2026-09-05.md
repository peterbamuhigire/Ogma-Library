# Phase 35 Cache Erasure and Export — Delivery Handoff

Date: 2026-09-05

## Problem and success criteria

Classroom clients need a reversible export path for cached Host resources and
an explicit local-erasure primitive. Success means that export is limited to
the requested Host and valid cache records, while all-Host erasure removes
valid entries and service-owned orphan files without touching the local
catalogue or source PDFs.

## Implementation boundary

- `IOfflineCacheService.ExportHostAsync` writes a versioned ZIP containing a
  manifest and valid payloads for one Host only.
- `IOfflineCacheService.ClearAllAsync` removes all classroom cache metadata,
  payloads, and service temporary files under the cache root.
- Disk export validates recorded length and SHA-256 before streaming payloads;
  it does not materialize the full cached resource in memory.
- The existing `ClearHostAsync` behavior and Host/certificate scoping remain
  unchanged.

## Tests and evidence

- Build: `dotnet build tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj
  --configuration Release --no-restore` — 0 warnings, 0 errors.
- Focused cache suite: 10 passed, 0 failed, 0 skipped.
- Complete classroom-client slice: 109 passed, 0 failed, 0 skipped.
- Complete Release solution: 1,106 passed (910 core, 41 architecture, 155
  UI), 0 failed, 0 skipped.
- Evidence: [phase-35-local-gate-reconciliation-2026-09-04.md](phase-35-local-gate-reconciliation-2026-09-04.md).

## Release and rollback

The changes are committed on `main` at `c6635c0` (streaming export), `19c5855`
(all-Host erasure), and `7038dbb` (evidence). They are safe to release with
the existing cache format because the new operations are additive and do not
change cache reads or writes.

If the capability causes a regression, revert the runtime commits in reverse
order, then rebuild and run the focused cache suite:

```text
git revert --no-edit 19c5855 c6635c0
dotnet build tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~OfflineCacheServiceTests
```

Rollback does not restore files deliberately removed by a user’s confirmed
cache-clear action; the cache contains regenerable Host projections only.

## Operator runbook and ownership

The Classroom Client maintainer owns this boundary. For a failed export,
preserve the exception and Host identifier, confirm the destination stream is
writable, and retry after checking available disk space. For a failed clear,
close active client operations and retry against the same data directory; do
not delete the broader application data directory. A support bundle must not
include the ZIP payload unless the user explicitly supplied it.

## Security and privacy review

Export is Host-scoped and excludes other Hosts by construction. It exports
published Host resources, not student private-state database rows or
credentials. Clear-all cleanup is bounded to `<data>/classroom/cache/` and
does not follow metadata paths outside that root. Physical permission,
multi-user, and platform credential-store evidence remains `NOT ASSESSED`.

## Observability and maintenance

No new remote or durable telemetry is emitted by these local file operations;
this is intentional to avoid logging resource keys or payload content. The
existing cache integrity checks remain the first diagnostic point. Future
import support must validate archive schema, Host identity, path names,
payload lengths, and hashes before writing any cache files; it must not infer
trust from an archive filename.

## Explicit residual gates

The settings UI controls, physical Windows/macOS pairing and credential-store
proof, reconnect/offline-reader walkthroughs, hostile two-user isolation, and
cross-machine load evidence remain open in the Phase 35 progress record.
