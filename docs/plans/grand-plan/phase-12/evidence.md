# Phase 12 Evidence

Date started: 2026-06-01

## Current Status

WP1 domain and Application contracts have started locally. This slice keeps the
existing Phase 11 embedding provider compatible while expanding `IAiProvider`
toward the Phase 12 gateway contract.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AiContractTests` | Passed: 5 WP1 contract tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 18 architecture tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |

## Implemented Locally

| Area | Evidence |
| --- | --- |
| Privacy tier | `AiPrivacyTier` defaults to `Offline=0` |
| Consent record | `AiConsentRecord` tracks tier/provider/scope/grant/revoke state and exposes `IsActive` |
| Audit event | `AiAuditEvent` captures provider/model/tier/token/cost/hash metadata |
| Query history | `AiQueryHistoryEntry` provides erasable query-history shape separate from immutable audit |
| Gateway DTOs | `AiRequest`, `AiCompletion`, `AiPayloadPreview`, and `AiContentChunk` define provider-neutral data flow |
| Application interfaces | `IAiProvider`, `IAiAdvisorService`, and `IAiPrivacyService` are in `Application/Ai` |

## Remaining Phase 12 Work

- WP2: persistence entities/configurations/repositories and migration.
- WP3: gateway core, payload builder, consent/preview/audit enforcement.
- WP4: provider adapters and recorded HTTP fixture tests.
- WP5/WP6: payload preview and Privacy Center UI.
- WP7: cost calculator/formatter.
- WP8/WP9: chokepoint architecture tests, full integration, security review, and remote CI evidence.
