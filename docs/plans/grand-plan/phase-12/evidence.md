# Phase 12 Evidence

Date started: 2026-06-01

## Current Status

WP1 domain/Application contracts, WP2 persistence, and the WP3 gateway core are implemented locally.
This slice keeps the existing Phase 11 embedding provider compatible while
expanding `IAiProvider` toward the Phase 12 gateway contract, and adds durable
consent, immutable audit, erasable AI query-history persistence, payload-preview
gating, consent enforcement, provider dispatch, and cost-attributed audit writes.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AiContractTests` | Passed: 5 WP1 contract tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 9 AI contract/persistence tests, including Phase 12 migration backfill from Phase 11 schema |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests\|FullyQualifiedName~AiGatewayTests"` | Passed: 16 AI contract/persistence/gateway tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 18 architecture tests |
| `dotnet test OgmaLibrary.sln --configuration Release --no-restore` | Architecture passed 18; core passed 309; UI had one timeout in `SearchIndexPanels_Pseudolocale_RenderWithoutBlankFrame` during full parallel solution run |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~SearchViewModelTests.SearchIndexPanels_Pseudolocale_RenderWithoutBlankFrame` | Passed: 1 targeted UI retry |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore` | Passed: 104 UI tests |
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
| Persistence interfaces | `IAiConsentRepository`, `IAiAuditRepository`, and `IAiQueryHistoryRepository` are in `Application/Ai` |
| EF migration | `20260601122941_Phase12AiGatewayTables` adds `AiConsentRecords`, `AiAuditEvents`, and stable/query-type fields on `AiQueryHistory` |
| Upgrade safety | Migration backfills existing `AiQueryHistory` rows to `legacy-{QueryId}` before adding the unique `HistoryId` index |
| Consent repository | Upsert, active-consent lookup, and per-tier revoke are implemented over SQLite |
| Audit repository | Append-only audit write, recent-read, and JSON export are implemented; audit survives query-history deletion |
| Query history repository | Add, page, soft-delete, and hard-delete are implemented over the existing `AiQueryHistory` table |
| Gateway core | `AiGateway` enforces active tier, payload preview, consent, provider dispatch, query-history write, immutable audit, and provider-failure audit |
| Disabled provider | `AiDisabledProvider` fails closed when AI is disabled |
| Payload builder | `AiPayloadBuilder` builds exact payload previews and stable SHA-256 hashes including query text, metadata, and content chunks |
| Cost attribution | `AiCostCalculator` estimates per-call USD cost from provider/model token pricing |

## Remaining Phase 12 Work

- WP4: provider adapters and recorded HTTP fixture tests.
- WP5/WP6: payload preview and Privacy Center UI.
- WP7: cost calculator/formatter.
- WP8/WP9: chokepoint architecture tests, full integration, security review, and remote CI evidence.
