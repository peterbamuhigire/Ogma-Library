# Phase 18 Evidence

Phase 18 implementation is underway locally. The first slice establishes the
School Administration and managed-AI decision record plus disabled contracts so
later admin, key, DPIA, quota, and AI-proxy work has a bounded context to build
on.

## Implemented Locally

| Area | Evidence |
| --- | --- |
| School-managed AI ADR | `docs/adrs/0013-school-managed-ai-host-gateway.md` is accepted by owner direction during Phase 18 completion work |
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
| Service registration | `AddSchoolAdminServices()` registers data-backed publishing, shared-shelf, profile-enrollment, and credential-backed AI key services while non-implemented AI policy/proxy/DPIA/dashboard services remain fail-closed |
| School AI key provider | `SchoolAiKeyProvider` stores school provider keys under `ogma.school.ai.key.<providerId>` through `IClassroomCredentialStore`, clears the mutable input buffer, reports status only, and deletes configured keys |
| AI key API guard | `ISchoolAiKeyProvider` exposes only save/status/delete methods; no public method returns the plaintext key |
| Credential-store activation | School AI key storage activates when the Host composition registers `IClassroomCredentialStore`; standalone SchoolAdmin-only registration remains disabled/fail-closed |
| School AI policy service | `SchoolAiPolicyService` reads and writes per-student/class token budgets and query-rate settings through `SchoolAiEntitlementRow` |
| School AI quota reservation | `SchoolAiPolicyService.CheckAndReserveQuotaAsync()` transactionally writes `AiUsageLedger` rows, blocks student/class daily token exhaustion, and serializes in-process reservations to prevent budget overrun |
| Conservative tier policy | `SchoolAiPolicyService.SavePolicyAsync()` rejects ContentAware and answer-mode elevation until the Phase 18 AI proxy/tier persistence path is implemented |
| Usage dashboard service | `SchoolUsageDashboardService.GetSummaryAsync()` aggregates `AiUsageLedger` by enrolled profile, filters UTC date ranges, includes zero-usage enrolled profiles, and reports quota percentage and last activity |
| DPIA screening service | `SchoolDpiaScreeningService` approves no-egress and metadata-only tiers, blocks ContentAware requests until explicit school DPIA approval exists, treats unknown-age profiles conservatively, and writes local audit events |
| Enrollment-token redemption | `IProfileEnrollmentService.RedeemTokenAsync()` validates hashed one-time tokens, rejects revoked/expired/replayed tokens, consumes valid tokens, and returns enrolled profile metadata |
| Managed profile Host sessions | `/api/v1/auth/session` accepts `profileId` + `enrollmentToken` for school-managed profiles, issues sessions using the enrolled server-side role, and keeps admin role unenrollable from LAN |
| Classroom AI answer grounding | `ClassroomAnswerGrounder` accepts only `[[book:BOOK_ID]]` citations verified against Host-local catalogue candidates, strips fabricated citation markers, and returns `No local evidence found.` when no cited local evidence survives |
| Host AI proxy handler | `AiProxyEndpointHandler` builds metadata-only payload previews, requires preview confirmation, verifies active managed profiles, enforces per-profile minute limits, reserves quota before provider calls, performs DPIA screening, estimates token/cost usage, and grounds provider answers |
| Student smart-search API | `POST /api/v1/ai/search/preview` and `POST /api/v1/ai/search` are authenticated LAN endpoints bound to the managed profile id in the session, reject admin/manual profile spoofing, and audit preview/search actions |
| Host Sharing school admin console | `HostSharingViewModel` and `SharingSettingsView` expose managed profile enrollment/revocation, masked school AI key save/delete, quota policy editing, usage dashboard rows, and recent audit events from the configured school admin services |
| Student smart-search client UI | `StudentSmartSearchViewModel` and `StudentSmartSearchView` add a classroom-client route with natural-language query entry, metadata payload preview, confirm/cancel controls, grounded answer rendering, citation rows, token/cost status, and active Host/profile enforcement |
| Classroom AI client contract | `ILibraryHostClient`, `LibraryHostHttpClient`, and `CachingLibraryHostClient` expose typed preview/search calls for `/api/v1/ai/search/preview` and `/api/v1/ai/search` without caching AI responses |
| Student AI history deletion | `IStudentPrivateRepository.DeleteAiHistoryAsync()` and `StudentSmartSearchViewModel.DeleteHistoryAsync()` clear private smart-search history for the active profile/Host while preserving other Host/profile state |
| Admin institution AI history purge | `ISchoolAiHistoryManagementService.PurgeInstitutionHistoryAsync()` and Host Sharing admin controls purge Host `AiQueryHistory` plus `AiUsageLedger` rows behind a typed confirmation while preserving immutable audit rows |
| Admin audit filtering and CSV export | `HostSharingViewModel.SchoolAuditFilterText` filters recent school audit rows by actor/action/resource/payload, and `ExportSchoolAuditCsvAsync()` exports the filtered rows as CSV |
| Admin school AI key test action | Host Sharing includes a status-only "Test key" action through `ISchoolAiKeyProvider.GetStatusAsync()`; it never returns or renders plaintext key material |
| Phase 18 safety scan report | `docs/security/phase-18-safety-scan-2026-06-02.md` records dependency, secret-pattern, admin-auth, architecture, core, and UI verification evidence plus residual release gaps |

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
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after school AI key provider: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal"` | Passed after school AI key provider: 11 SchoolAdmin scaffold/key tests covering disabled key zeroing, credential-backed save/status, provider key normalization, status-only public return, no plaintext in data-directory files, delete, invalid-provider buffer clearing, and authorization |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_SchoolAdmin" --logger "console;verbosity=minimal"` | Passed after school AI key provider: 2 SchoolAdmin bounded-context/disabled-scope architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_SchoolAiKeyProvider" --logger "console;verbosity=minimal"` | Passed after school AI key provider: 2 key-provider architecture tests covering no direct AI provider dependency and no public plaintext-key return |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after school AI key provider: 594 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after school AI key provider: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after school AI key provider |
| `git diff --check` | Passed after school AI key provider: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after school AI policy/quota service: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAiPolicyServiceTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 6 policy/quota tests covering default policy projection, entitlement updates, ledger writes, student exhaustion, class exhaustion, concurrent reservation budget ceiling, and unsupported tier elevation |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests\|FullyQualifiedName~SchoolProfileEnrollmentServiceTests" --logger "console;verbosity=minimal"` | Passed after school AI policy/quota registration: 14 SchoolAdmin scaffold/enrollment tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after school AI policy/quota service: 600 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after school AI policy/quota service: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after school AI policy/quota service |
| `git diff --check` | Passed after school AI policy/quota service: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after usage dashboard service: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolUsageDashboardServiceTests" --logger "console;verbosity=minimal"` | Passed: 4 dashboard tests covering counts, tokens, costs, quota percentage, date filtering, zero-usage enrolled profiles, and invalid range rejection |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests\|FullyQualifiedName~SchoolAiPolicyServiceTests\|FullyQualifiedName~SchoolProfileEnrollmentServiceTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after usage dashboard registration: 20 SchoolAdmin backend tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after usage dashboard service: 604 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after usage dashboard service: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after usage dashboard service |
| `git diff --check` | Passed after usage dashboard service: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after DPIA screening service: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolDpiaScreeningServiceTests" --logger "console;verbosity=minimal"` | Passed: 6 DPIA tests covering metadata-only minor approval, ContentAware minor/unknown-age blocking, no-egress tier approval, audit writing, and invalid payload-scope rejection |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminScaffoldTests" --logger "console;verbosity=minimal"` | Passed after DPIA registration: 11 SchoolAdmin scaffold/key tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~ArchTests_SchoolAdminScaffold" --logger "console;verbosity=minimal"` | Passed after DPIA registration: 1 architecture smoke test |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after DPIA screening service: 610 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after DPIA screening service: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after DPIA screening service |
| `git diff --check` | Passed after DPIA screening service: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after enrollment-token redemption: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolProfileEnrollmentServiceTests" --logger "console;verbosity=minimal"` | Passed: 5 enrollment tests covering enroll/list/revoke, token hashing, default entitlement, admin-role rejection, one-time token redemption, replay rejection, and revoked-profile rejection |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LanHostEndpointTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after enrollment-token redemption: 1 real HTTPS Host endpoint test covering managed profile session issue, token replay rejection, admin-route guard, catalogue/search/detail/profile-sync, page render, and file-stream mode |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after enrollment-token redemption: 612 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after enrollment-token redemption: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after enrollment-token redemption |
| `git diff --check` | Passed after enrollment-token redemption: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after Host AI proxy slice: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ClassroomAnswerGrounderTests\|FullyQualifiedName~AiProxyEndpointHandlerTests\|FullyQualifiedName~SchoolAiPolicyServiceTests\|FullyQualifiedName~SchoolDpiaScreeningServiceTests" --logger "console;verbosity=minimal"` | Passed: 18 focused grounding/proxy/policy/DPIA tests |
| `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AiProxyEndpointHandlerTests` | Passed: 6 Host AI proxy tests, including quota exhaustion and rate-limit rejection before provider invocation |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LanHostEndpointTests" --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed after Host AI proxy slice: 1 real HTTPS Host endpoint test covering managed profile AI preview, confirmed search, unconfirmed blocking, spoof rejection, grounding, and audit |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed after Host AI proxy slice: 35 architecture tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed after Host AI proxy slice |
| `git diff --check` | Passed after Host AI proxy slice: no whitespace errors |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after Host Sharing school admin console: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~HostSharingViewModelTests" --logger "console;verbosity=minimal"` | Passed: 13 Host Sharing ViewModel tests including school admin refresh, key save/delete, policy save, enrollment, and revocation |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminPanelRenderTests" --logger "console;verbosity=minimal"` | Passed: 1 headless render test covering the school admin console sections and enrolled profile row |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed after student smart-search client UI: 10 projects, 0 warnings, 0 errors |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LibraryHostHttpClientTests" --logger "console;verbosity=minimal"` | Passed: 13 classroom Host HTTP client tests including AI preview/search bearer auth, request body, and response mapping |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~StudentSmartSearchViewModelTests" --logger "console;verbosity=minimal"` | Passed: 3 student smart-search tests covering preview/confirm state, no-active-connection messaging, and headless view rendering |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~StudentPrivateRepositoryTests\|FullyQualifiedName~SchoolAiHistoryManagementServiceTests" --logger "console;verbosity=minimal"` | Passed: 8 private-history and school-admin purge tests covering scoped student deletion, Host query-history purge, usage-ledger purge, and audit preservation |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~HostSharingViewModelTests" --logger "console;verbosity=minimal"` | Passed: 13 Host Sharing tests including school admin refresh, key/policy/enrollment/revocation, and AI-history purge confirmation |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~StudentSmartSearchViewModelTests\|FullyQualifiedName~SchoolAdminPanelRenderTests" --logger "console;verbosity=minimal"` | Passed: 4 UI tests covering smart-search history save/delete and school-admin purge control rendering |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~HostSharingViewModelTests" --logger "console;verbosity=minimal"` | Passed: 14 Host Sharing tests including audit filtering and filtered CSV export |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter "FullyQualifiedName~SchoolAdminPanelRenderTests" --logger "console;verbosity=minimal"` | Passed: school-admin render test covering audit export control visibility |
| `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | Passed: no vulnerable packages reported across all 10 projects |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed: 35 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 628 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --logger "console;verbosity=minimal" -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1` | Passed: 125 UI tests |
| `rg -n "(sk_live\|sk_test\|pk_live\|STRIPE\|SUPABASE\|service_role\|api[_-]?key\|secret\|token\\s*=\|password\\s*=\|Authorization:\\s*Bearer\|AWS_\|BEGIN (RSA\|OPENSSH\|PRIVATE) KEY)" src tests docs` | Reviewed: hits are test fixtures, generated token variables, password buffers, credential-store abstractions, or documentation; no production hardcoded provider key found |
| `git log --all --oneline -- "*.pem" "*.p12" "*.pfx" "*.key" "*.env"` | Passed: no historical key/certificate/env-file entries returned |
| `gitleaks version`; `trufflehog --version` | Not run: neither external scanner is installed locally |

## Remaining Phase 18 Work

ADR-0013 is accepted and owner-ratified in the canonical ADR record. That
decision gate is closed; it does not constitute the remaining platform,
security-tool, accessibility, or workflow evidence.

- Host-local admin sign-in UI/session creation beyond the internal Host-issued admin
  session path; admin-route enforcement is implemented and tested.
- Student smart-search localization/accessibility polish and richer classroom AI
  history browsing beyond the delete/purge controls now implemented.
- Admin console polish: rotate/test-key actions and richer usage charts beyond
  the key, quota, dashboard, audit filter/export, and purge surface now in Host
  Sharing settings.
- Windows/macOS live credential-store verification for school AI key storage.
- External `gitleaks`/`trufflehog` secret scan on a machine with those tools
  installed; local regex secret scan and package/security review are complete.
