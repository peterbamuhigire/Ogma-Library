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
| Host-local admin route guard | `KestrelHostModeListener` blocks `/admin/*` unless the request is loopback and the active session role is `admin` |
| Admin enrollment hardening | `/api/v1/auth/session` refuses LAN enrollment requests that ask for `admin` role |
| Disabled admin AI test endpoint | `POST /admin/ai/test-connection` is guarded and returns key status without exposing token or secret material |
| Admin route tests | `LanHostEndpointTests` covers rejected admin enrollment, student 403, Host-minted admin access, and audit redaction |
| Phase 18 EF row model | `LibraryPublishSettingsRow`, `SharedShelfRow`, `SharedShelfBookRow`, `EnrolledProfileRow`, `SchoolAiEntitlementRow`, `AiUsageLedgerRow` |
| Phase 18 table configurations | `LibraryPublishSettingsConfiguration`, `SharedShelfConfiguration`, `SharedShelfBookConfiguration`, `EnrolledProfileConfiguration`, `SchoolAiEntitlementConfiguration`, `AiUsageLedgerConfiguration` |
| Phase 18 migration | `20260602072445_Phase18SchoolAdminTables` creates six additive Host catalogue tables and reverses to Phase 16 |
| Migration isolation test | `Phase18Migration_AddsSchoolAdminTablesAndRoundTrips` verifies UP, DOWN to `20260601184330_Phase16LanHostTables`, and re-UP |
| Library publishing service | `SchoolAdminCatalogueService` implements `ILibraryPublishingService` with publish, unpublish, and list persistence |
| Shared shelf service | `SchoolAdminCatalogueService` implements `ISharedShelfService` with save, list, soft-delete, book assignment, and group visibility persistence |
| Profile enrollment service | `SchoolProfileEnrollmentService` enrolls student/teacher profiles, stores one-time token hashes, creates default AI entitlements, lists profiles, and revokes profiles |
| Service registration | `AddSchoolAdminServices()` registers data-backed publishing, shared-shelf, and profile-enrollment services while non-implemented AI services remain fail-closed |

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
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal"` | Passed: 8 SchoolAdmin scaffold/authorization tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LanHostEndpointTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 1 real HTTPS Host endpoint test with admin route enforcement |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_AdminRoutes" --logger "console;verbosity=minimal"` | Passed: 1 admin route architecture guard |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after admin route guard: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after admin route guard: 585 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after admin route guard: 34 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after admin route guard |
| `git diff --check` | Passed after admin route guard: no whitespace errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Phase18Migration" --logger "console;verbosity=minimal"` | Passed: 1 Phase 18 migration UP/DOWN/re-UP isolation test |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MigrationTests" --logger "console;verbosity=minimal"` | Passed: 5 migration tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LanHostPersistenceTests" --logger "console;verbosity=minimal"` | Passed: 4 LAN Host persistence tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after Phase 18 schema: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LanHostLoadSmokeTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed when isolated: 2 load smoke tests; earlier parallel gate contention produced a transient P95 miss |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after Phase 18 schema: 586 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after Phase 18 schema: 34 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after Phase 18 schema |
| `git diff --check` | Passed after Phase 18 schema: no whitespace errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminCatalogueServiceTests" --logger "console;verbosity=minimal"` | Passed: 2 data-backed publishing/shared-shelf tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal"` | Passed after data-backed service registration: 8 scaffold/authorization tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after publishing/shared-shelf services: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after publishing/shared-shelf services: 588 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after publishing/shared-shelf services: 34 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after publishing/shared-shelf services |
| `git diff --check` | Passed after publishing/shared-shelf services: no whitespace errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolProfileEnrollmentServiceTests" --logger "console;verbosity=minimal"` | Passed: 3 profile enrollment, hashed token, entitlement, and admin-role rejection tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests\|FullyQualifiedName~SchoolAdminCatalogueServiceTests" --logger "console;verbosity=minimal"` | Passed after enrollment registration: 10 SchoolAdmin scaffold/catalogue tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after profile enrollment service: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after profile enrollment service: 591 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after profile enrollment service: 34 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after profile enrollment service |
| `git diff --check` | Passed after profile enrollment service: no whitespace errors |

## Remaining Phase 18 Work

- Owner ratification for ADR-0013.
- Host-local admin sign-in UI/session creation beyond the internal Host-issued admin
  session path; admin-route enforcement is implemented and tested.
- Enrollment-token exchange with Host sessions; key storage, DPIA, quota,
  AI-proxy, dashboard, audit viewer, and student smart-search implementation.
- Windows/macOS live credential-store verification for school AI key storage.
- Red-team, security review, code review, and secret-scan gates.
