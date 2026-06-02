# Phase 18 Evidence

Phase 18 implementation is underway locally. The first slice establishes the
School Administration and managed-AI decision record plus disabled contracts so
later admin, key, DPIA, quota, and AI-proxy work has a bounded context to build
on.

## Implemented Locally

| Area | Evidence |
| --- | --- |
| School-managed AI ADR | `docs/adrs/0013-school-managed-ai-host-gateway.md` |
| ADR index update | `docs/adrs/README.md` |
| Phase 18 plan ADR renumbering | `docs/plans/grand-plan/phase-18/README.md`, `tasks.md`, and `skills.md` |
| Source summary FR-ADMIN requirements | `docs/plans/grand-plan/SOURCE-SUMMARY.md` |
| Application contracts | `src/OgmaLibrary.Application/SchoolAdmin/SchoolAdminInterfaces.cs` |
| Application records | `src/OgmaLibrary.Application/SchoolAdmin/SchoolAdminRecords.cs` |
| Disabled infrastructure scaffold | `src/OgmaLibrary.Infrastructure/SchoolAdmin/UnavailableSchoolAdminService.cs` |
| DI registration | `SchoolAdminServiceExtensions.AddSchoolAdminServices()` and `CompositionRoot.AddOgmaLibrary()` |
| Scaffold tests | `SchoolAdminScaffoldTests` |
| Architecture guardrails | `ArchTests_SchoolAdmin_*` |

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal"` | Passed: 2 disabled School Administration scaffold tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_SchoolAdmin" --logger "console;verbosity=minimal"` | Passed: 2 focused SchoolAdmin architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 579 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 117 UI tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed: 33 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after repository formatting normalized new C# file endings |
| `git diff --check` | Passed: no whitespace errors |

## Remaining Phase 18 Work

- Owner ratification for ADR-0013.
- Host-local admin authentication and admin-route enforcement.
- Library publishing, shared shelves, enrollment, key storage, DPIA, quota,
  AI-proxy, dashboard, audit viewer, and student smart-search implementation.
- Windows/macOS live credential-store verification for school AI key storage.
- Red-team, security review, code review, and secret-scan gates.
