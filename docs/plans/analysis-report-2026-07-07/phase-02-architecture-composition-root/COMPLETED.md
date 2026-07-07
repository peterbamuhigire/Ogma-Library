# Phase 02 Completion Record

Date: 2026-07-07

## Summary

Phase 02 closed the runtime/package and architecture-decision findings by adding
accepted ADR-0014 and ADR-0015 to the live ADR catalogue, aligning EF Core and
explicit Microsoft.Extensions package references to 10.0.9 on net10.0,
committing NuGet lock files, and changing CI restore to locked mode.

Composition-root architecture tests now assert that platform branches and
runtime activation points stay at the App boundary and that the ratified package
policy remains executable. During full-suite verification, the large-library
batch enrichment path was optimized to prefetch idempotency keys in chunks
instead of querying once per book, and the core xUnit assembly now uses the same
non-parallel collection policy already used by UI tests so load/integration
tests do not race unrelated fixtures.

## Acceptance Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| AC02-1: Every finding assigned to the phase has a concrete code, test, release, or documentation change. | Pass | F-BLD-002: `docs/adrs/0014-ef-core-10-on-net10-runtime.md` plus EF Core 10.0.9 package updates. F-BLD-003: committed `packages.lock.json` files and `.github/workflows/ci.yml` locked restore. F-DOC-001: `docs/adrs/0014-ef-core-10-on-net10-runtime.md` and `docs/adrs/0015-documentation-baseline-v2.md` accepted. F-ARCH-001: ADR-0015 baseline plus `Architecture_CompositionRoot_CentralizesRuntimeBranches`. |
| AC02-2: Existing behavior is preserved unless the phase explicitly requires change. | Pass | `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` passed: 37 architecture, 628 core, 126 UI tests. |
| AC02-3: Targeted tests for `docs/adrs; docs/references; src/OgmaLibrary.App/CompositionRoot.cs; architecture tests` pass. | Pass | `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~Architecture_CompositionRoot_CentralizesRuntimeBranches\|FullyQualifiedName~Architecture_RatifiedRuntimePackagePolicy_IsExecutable" --logger "console;verbosity=normal"` passed 2 tests. |
| AC02-4: Full repository verification passes. | Pass | Restore, locked restore, Release build, vulnerable package scan, targeted tests, and full solution tests all passed. |
| AC02-5: Documentation and traceability are updated. | Pass | Updated ADR index, README runtime/package references, changelog, findings register, master plan, and this completion record. |
| AC02-6: No safety gate is weakened. | Pass | `TreatWarningsAsErrors` remains enabled; NuGet audit remains enabled; CI restore is stricter with `--locked-mode`; no assertions were loosened or skipped. |

## Verification

| Command | Result |
| --- | --- |
| `dotnet restore OgmaLibrary.sln --use-lock-file --force-evaluate` | Pass. Generated lock files for all solution projects. |
| `dotnet restore OgmaLibrary.sln` | Pass. Canonical restore completed with lock files present. |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Pass. Build succeeded with 0 warnings and 0 errors. |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter "FullyQualifiedName~Architecture_CompositionRoot_CentralizesRuntimeBranches\|FullyQualifiedName~Architecture_RatifiedRuntimePackagePolicy_IsExecutable" --logger "console;verbosity=normal"` | Pass. 2 targeted architecture tests passed. |
| `dotnet restore OgmaLibrary.sln --locked-mode` | Pass. Locked restore completed for all solution projects. |
| `dotnet list OgmaLibrary.sln package --vulnerable --include-transitive` | Pass. No vulnerable packages reported for any solution project. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MetadataSearchServiceTests.PerfBenchmark_MetadataSearch_P95_LessThan150ms\|FullyQualifiedName~HealthDashboardTests.BatchEnrichment_2000Books_CompletesWithRetry" --logger "console;verbosity=normal"` | Pass. 2 focused core performance tests passed after the batch enrichment optimization. |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~LanHostLoadSmokeTests.CatalogueEndpoint_HandlesTwentyConcurrentAuthenticatedClients --logger "console;verbosity=normal"` | Pass. LAN load smoke test passed. |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-build --filter FullyQualifiedName~MainShell_BackgroundCompletion_RefreshesCatalogueAndLoadedDetail --logger "console;verbosity=normal"` | Pass. UI refresh regression test passed. |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` | Pass. 37 architecture tests, 628 core tests, and 126 UI tests passed. |

## Deviations

- Verification-driven extra touch: `src/OgmaLibrary.Infrastructure/Metadata/BatchEnrichmentOrchestrator.cs` was optimized after the canonical full-suite gate repeatedly failed the existing 2,000-book batch test under solution-level load. This did not change acceptance thresholds or skip tests; it removed an O(n) database-query pattern.
- Verification-driven extra touch: `tests/OgmaLibrary.Tests/TestAssemblyInfo.cs` disables xUnit collection parallelization for the core test assembly, matching the existing UI test assembly policy. Internal product load tests still exercise concurrent clients; this prevents unrelated integration fixtures from racing each other in the canonical solution run.

## Findings Resolved

| Finding | Resolution |
| --- | --- |
| F-BLD-002 | ADR-0014 accepted and EF Core/Microsoft.Extensions package references aligned to 10.0.9 on net10.0. |
| F-BLD-003 | NuGet lock files committed; CI restore now runs `dotnet restore OgmaLibrary.sln --locked-mode`; locked restore and vulnerable-package scan passed. |
| F-DOC-001 | ADR-0014 and ADR-0015 are present in `docs/adrs` and accepted. |
| F-ARCH-001 | ADR-0015 makes the v2.0 remediation baseline binding and architecture tests enforce composition-root/runtime boundaries; remaining beta controls remain tracked by security and release findings. |

## Projected Score

Per `00-master-plan.md`, Phase 02 moves the projected audit score from 60.0%
to **61.5%**.
