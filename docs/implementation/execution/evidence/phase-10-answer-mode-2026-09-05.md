# AI Answer-Mode Remediation Evidence

Date: 2026-09-05

This evidence belongs to the analysis-report remediation plan
`docs/plans/analysis-report-2026-07-07/phase-10-semantic-search-embeddings/`.
It must not be confused with grand-plan Phase 10, which remains in progress
under the canonical execution ledger because its PDF sandbox and physical
security gates are still open.

## Change

`AdvisorService` no longer has a compatibility constructor that silently
injects an unavailable non-V2 answer scaffold. `IAnswerPipeline` is now
mandatory, and the production composition supplies
`LocalEvidenceAnswerPipeline`. That pipeline searches local evidence, applies
the content-aware privacy boundary, cites displayed excerpts, handles the
no-evidence case safely, and records an optional privacy-safe trace.

## Verification

| Check | Result |
| --- | --- |
| `dotnet restore OgmaLibrary.sln` | Passed; all projects up to date |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AdvisorServiceTests\|FullyQualifiedName~LocalEvidenceAnswerPipelineTests\|FullyQualifiedName~AdvisorViewModelTests" --logger "console;verbosity=minimal"` | Passed; 13 passed, 0 failed, 0 skipped |
| `dotnet test OgmaLibrary.sln --configuration Release --no-build --logger "console;verbosity=minimal"` | Passed; 898 core, 41 architecture, and 155 UI tests; 1,094 total, 0 failed, 0 skipped |

## Finding disposition

- F-ARCH-002: resolved for the local answer workflow. The service cannot
  silently fall back to the unavailable scaffold.
- F-FUNC-003: resolved for the local answer workflow. Answer requests reach a
  configured cited pipeline and safe no-evidence behavior is tested.
- F-SEC-005: already resolved by Phase 04 controls; this remediation retained
  the gateway/secret boundary and introduced no provider-secret handling.

External-provider terms/conformance, OS-level sandbox/escape evidence,
independent security approval, physical accessibility, and release evidence
remain separate open gates. No such gate is claimed here.
