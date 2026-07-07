# ADR-0014: Align EF Core and Microsoft Extensions Packages to .NET 10

## Status

Accepted

> Ratified in remediation Phase 02, 2026-07-07.

## Date

2026-07-07

## Context

ADR-0001 made .NET 10 LTS the application runtime. Before remediation Phase 02,
the production SQLite data layer and direct SQLite tests still used EF Core
9.0.x packages while the desktop shell and workers used Microsoft.Extensions
10.0.x packages. Phase 01 restored the build by overriding the vulnerable
SQLite native dependency, but the runtime/package alignment decision remained
open in the extracted ADR baseline.

The release programme needs one supported package policy for the net10.0
solution, because migrations, SQLite FTS5 behaviour, architecture tests, and
CI restore all depend on a reproducible dependency graph.

## Decision Drivers

- Keep the data stack on the same major platform line as the target runtime.
- Preserve the SQLite native dependency fix from Phase 01.
- Make dependency restore reproducible and auditable before later security,
  data-integrity, and packaging phases.
- Avoid silent transitive package drift in CI.

## Considered Options

### Option A - Move EF Core and Microsoft.Extensions packages to 10.0.x now

- **Pros:** aligns the data stack with net10.0; removes the open supportability
  question; keeps the SQLite native package on the remediated 3.0.x line; lets
  CI enforce a committed lock file.
- **Cons:** requires a full restore/build/test pass and migration coverage
  before later remediation phases proceed.

### Option B - Pin EF Core 9.0.x through launch

- **Pros:** smallest package delta after Phase 01; avoids immediate retesting
  of EF Core 10 behaviour.
- **Cons:** leaves the major-version mismatch in place and keeps runtime
  supportability as an explicit launch risk.

### Option C - Stay on EF Core 9.x indefinitely

- **Pros:** no immediate change.
- **Cons:** conflicts with the net10.0 runtime policy and leaves dependency
  drift unresolved.

## Decision Outcome

Adopt Option A. The production SQLite-owning project and the direct SQLite test
project use `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9. The infrastructure
project uses `Microsoft.EntityFrameworkCore.Design` 10.0.9. App and worker
projects use Microsoft.Extensions packages on 10.0.9 where they carry explicit
package references.

`SQLitePCLRaw.bundle_e_sqlite3` remains explicitly pinned to 3.0.3 to preserve
the Phase 01 vulnerability remediation. The solution commits NuGet lock files
and CI restore runs in locked mode.

## Consequences

### Positive

- Runtime and EF Core major versions now align with ADR-0001.
- The SQLite native dependency remains outside the vulnerable 2.1.x line.
- CI can prove the dependency graph from committed lock files.

### Negative

- Future package changes must update lock files deliberately.
- The cross-platform lock graph must include both Windows and macOS native
  asset packages even when restoring on one operating system.

### Affects

- ADR-0001 (.NET 10 runtime baseline).
- ADR-0005 (SQLite catalogue of record).
- CI restore policy in `.github/workflows/ci.yml`.
- Package references in `src/OgmaLibrary.Infrastructure`,
  `src/OgmaLibrary.App`, `src/OgmaLibrary.Workers`, and direct SQLite tests.
