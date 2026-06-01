# Phase 12 Evidence

Date started: 2026-06-01

## Current Status

WP1-WP9 are implemented and verified locally.
This slice keeps the existing Phase 11 embedding provider compatible while
expanding `IAiProvider` toward the Phase 12 gateway contract, and adds durable
consent, immutable audit, erasable AI query-history persistence, payload-preview
gating, consent enforcement, provider dispatch, cost-attributed audit writes,
OpenAI-compatible/DeepSeek-compatible, Anthropic, local Ollama chat adapters,
localized payload-preview dialog shell, and Privacy Center operations for tier,
history deletion, embedding erasure, audit export, localized per-call cost display,
architecture guards for the AI egress boundary, and SQLite-backed gateway integration.

## Verified Locally

| Gate | Evidence |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AiContractTests` | Passed: 5 WP1 contract tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 9 AI contract/persistence tests, including Phase 12 migration backfill from Phase 11 schema |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests\|FullyQualifiedName~AiGatewayTests"` | Passed: 16 AI contract/persistence/gateway tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiProviderAdapterTests\|FullyQualifiedName~AiGatewayTests\|FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 21 AI provider/gateway/contract/persistence tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PayloadPreviewViewModelTests\|FullyQualifiedName~AiProviderAdapterTests\|FullyQualifiedName~AiGatewayTests\|FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 24 AI payload/provider/gateway/contract/persistence tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PrivacyCenterViewModelTests\|FullyQualifiedName~PayloadPreviewViewModelTests\|FullyQualifiedName~AiProviderAdapterTests\|FullyQualifiedName~AiGatewayTests\|FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 27 AI privacy-center/payload/provider/gateway/contract/persistence tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PrivacyCenterViewModelTests\|FullyQualifiedName~PayloadPreviewViewModelTests\|FullyQualifiedName~AiProviderAdapterTests\|FullyQualifiedName~AiGatewayTests\|FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 29 AI tests after adding cost formatter coverage |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiGatewayIntegrationTests\|FullyQualifiedName~PrivacyCenterViewModelTests\|FullyQualifiedName~PayloadPreviewViewModelTests\|FullyQualifiedName~AiProviderAdapterTests\|FullyQualifiedName~AiGatewayTests\|FullyQualifiedName~AiContractTests\|FullyQualifiedName~AiPersistenceTests"` | Passed: 30 AI integration/privacy/payload/provider/gateway/contract/persistence tests |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | Passed |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore` | Passed: 330 core tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 20 architecture tests |
| `dotnet test tests\OgmaLibrary.Tests.Ui\OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore` | Passed: 104 UI tests |
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
| Provider adapters | `OpenAiCompatProvider`, `AnthropicProvider`, and `OllamaChatProvider` translate normalized requests and map token usage |
| Provider factory | `AiProviderFactory` creates OpenAI-compatible, DeepSeek-compatible, Anthropic, Ollama, and disabled providers from settings bindings |
| Payload preview UI | `PayloadPreviewViewModel`, `PayloadPreviewDialog`, and `AvaloniaPreviewGate` show exact payload fields with Send, Cancel, and Remember-for-session decisions |
| Payload preview i18n | English and French payload-preview labels are added to `InMemoryLocalizationService` |
| Privacy service | `AiPrivacyService` defaults to Offline, stores active tier, checks consent, records consent, and delegates payload preview building |
| Privacy Center | `PrivacyCenterViewModel` and `PrivacyCenterView` expose active tier, recent audit calls, delete history, erase embeddings, and audit export |
| Cost display | `AiCostFormatter` formats USD estimates with culture-specific number formatting and Privacy Center rows expose `CostText` |
| Architecture guards | `Architecture_AiProviderHttpClients_StayInAdapterNamespaces` and `Architecture_AiContext_DoesNotDependOnReader` enforce SI-1 and bounded-context discipline |
| Integration | `AiGatewayIntegrationTests` runs a full consent -> preview -> provider -> audit/history persistence -> history erasure path over SQLite |

## Remaining Phase 12 Work

- Remote CI evidence: pending authenticated GitHub Actions access.
- Security/code review: local implementation has architecture/privacy tests; formal external review still pending before beta signoff.
- Manual accessibility walkthrough: payload preview and Privacy Center controls still need screen-reader/manual keyboard evidence before public beta.
