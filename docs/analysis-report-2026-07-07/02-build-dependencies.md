# Build and Dependency Hygiene

Score: **25 / 100**. Weight: 12%.

Coverage reviewed: `OgmaLibrary.sln`, `Directory.Build.props`, package references in `src/*/*.csproj` and `tests/*/*.csproj`, Development Standards, Test Completion Report, and ADRs.

| ID | Location | Rule violated | Severity | Evidence | Consequence |
| --- | --- | --- | --- | --- | --- |
| F-BLD-001 | `OgmaLibrary.sln`, `Directory.Build.props:14`, `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj:36` | NuGet advisories must be fixed without weakening warnings-as-errors. | Critical | `dotnet restore OgmaLibrary.sln` fails with NU1903 for `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 / GHSA-2m69-gcr7-jv3q. | No clean restore, no canonical build, no valid release candidate. |
| F-BLD-002 | `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj:36`, `tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj:12`, `artifacts/reference-extracts-2026-07-07/Ogma-Library_ADRs.txt:1` | ADR/runtime alignment must be ratified before release. | High | EF Core SQLite is pinned to 9.0.16 while ADR-0014 is Proposed and names EF Core 9.x on net10.0 as an open decision. | Runtime/package support risk remains open and feeds the restore blocker. |
| F-BLD-003 | Repo-wide package policy | Reproducible build gates require locked dependency graph and auditable dependency evidence. | Medium | Restore failure shows transitive package drift is not fully controlled; no successful canonical lock/audit evidence was produced in this session. | Future dependency changes can silently reopen security or compatibility failures. |

90%+ means restore succeeds with NuGet audit enabled and warnings-as-errors preserved, ADR-0014 is ratified, package versions are aligned, and dependency lock/audit evidence is generated in CI.
