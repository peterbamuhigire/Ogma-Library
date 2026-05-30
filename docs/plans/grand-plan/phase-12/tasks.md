# Phase 12 — Tasks

Work packages and granular tasks. IDs are stable; requirement IDs trace to
`SOURCE-SUMMARY.md` and `README.md §5`.

---

## WP1 — Domain & Interfaces

**Goal:** Define the domain types and application-layer contracts that the rest
of the phase implements against. Tests are written first (TDD).

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP1-T1 | Define `AiPrivacyTier` enum (`Offline=0`, `MetadataOnly=1`, `ContentAware=2`, `LocalOllama=3`) with XML doc comments in `Domain/Ai/` | Phase 02 scaffold | 0.5 h | FR-AI-002, ADR-0007 |
| P12-WP1-T2 | Define `AiConsentRecord` aggregate: Id, Tier, Provider, Scope (`"library:<id>"` / `"session"` / `"query"`), GrantedAt, RevokedAt (nullable) | P12-WP1-T1 | 1 h | CTRL-OGMA-019 |
| P12-WP1-T3 | Define `AiAuditEvent` value object: immutable fields (OccurredAt, Tier, Provider, Model, PromptTokens, CompletionTokens, PromptCacheTokens, EstimatedCostUsd, PayloadHash, ResponseHash, QueryHistoryEntryId) | P12-WP1-T1 | 1 h | CTRL-OGMA-018 |
| P12-WP1-T4 | Define `AiQueryHistoryEntry` aggregate: Id, OccurredAt, QueryType, QueryText, ResponseSummary, Deleted (soft-delete flag) | P12-WP1-T1 | 0.5 h | FR-AI-009 |
| P12-WP1-T5 | Define `IAiProvider` interface in `Application/Ai/`: `Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken ct)` | P12-WP1-T1 | 1 h | FR-AI-002, SI-1 |
| P12-WP1-T6 | Define `IAiAdvisorService` interface: `GetRecommendationsAsync`, `GetReadingPlanAsync`, `GetAnswerAsync` (stubs — implementations in Phase 13); `IsEnabled` property | P12-WP1-T5 | 1 h | FR-AI-001, FR-AI-003 stub |
| P12-WP1-T7 | Define `IAiPrivacyService` interface: `GetActiveTier()`, `SetTier(AiPrivacyTier)`, `RecordConsent(AiConsentRecord)`, `HasConsent(tier, provider, scope)`, `BuildPayloadPreview(AiRequest) -> AiPayloadPreview` | P12-WP1-T1 | 1 h | FR-AI-004, FR-AI-005, CTRL-OGMA-017 |
| P12-WP1-T8 | Define `AiRequest`, `AiCompletion`, `AiPayloadPreview` DTOs with validation rules (non-null required fields; content-chunks only present for Tier-2) | P12-WP1-T5 | 1 h | FR-AI-004, FR-AI-005 |
| P12-WP1-T9 | Write unit tests: `AiPrivacyTier_DefaultIsOffline`, `AiConsentRecord_RevokeAt_MakesConsent_Invalid`, `AiRequest_ContentChunks_ForbiddenFor_Tier1` | P12-WP1-T1..T8 | 1.5 h | FR-AI-004, CTRL-OGMA-019 |

---

## WP2 — Data Layer

**Goal:** Persist consent records, immutable audit events, and erasable query
history via EF Core migration M012.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP2-T1 | Add EF Core entity configs for `AiConsentRecord`, `AiAuditEvent`, `AiQueryHistoryEntry` in `Infrastructure/Persistence/` | P12-WP1-T2..T4 | 1.5 h | CTRL-OGMA-018, FR-AI-009 |
| P12-WP2-T2 | Author migration M012 (`AddAiGatewayTables`); implement both `Up()` and `Down()` (reversible); test `Down()` restores prior schema | P12-WP2-T1 | 1 h | NFR-PROD-012, reversibility |
| P12-WP2-T3 | Implement `ConsentRepository` (EF Core): `UpsertAsync`, `GetActiveConsent(tier, provider, scope)`, `RevokeAll(tier)` | P12-WP2-T1 | 1.5 h | CTRL-OGMA-019 |
| P12-WP2-T4 | Implement `AuditRepository` (EF Core): `AppendAsync(AiAuditEvent)` (append-only; no update/delete); `GetRecentAsync(count)`, `ExportToJsonAsync(stream)` | P12-WP2-T1 | 2 h | CTRL-OGMA-018, CTRL-OGMA-020 |
| P12-WP2-T5 | Implement `QueryHistoryRepository`: `AddAsync`, `ListAsync(page, size)`, `SoftDeleteAsync(id)`, `HardDeleteAllAsync()` (for "delete history" action); no `AiAuditEvent` rows are deleted | P12-WP2-T1 | 1.5 h | FR-AI-009, NFR-PROD-014 |
| P12-WP2-T6 | Integration tests: `ConsentRepository_UpsertAndRevoke`, `AuditRepository_AppendIsImmutable` (asserts no EF update/delete tracked), `QueryHistoryRepository_HardDelete_LeavesAuditIntact` | P12-WP2-T3..T5 | 2 h | CTRL-OGMA-018, FR-AI-009 |

---

## WP3 — AiGateway Core

**Goal:** Implement the central `AiGateway` class that is the single off-device
egress chokepoint and enforces all tier/consent/preview/audit rules.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP3-T1 | Implement `PayloadBuilder`: constructs the outbound payload from `AiRequest`; enforces field whitelist per tier (Tier-1: title/author/tags/categories/descriptions/notes only; Tier-2 adds chunks); throws `TierViolationException` on any attempt to include chunks at Tier-1 | P12-WP1-T8 | 2 h | FR-AI-004, FR-AI-005 |
| P12-WP3-T2 | Implement `CostCalculator`: given provider model and token counts, returns `EstimatedCostUsd`; pluggable price table (JSON config per provider, updatable without recompile) | P12-WP1-T3 | 1.5 h | FR-AI-010 |
| P12-WP3-T3 | Implement `AiGateway.SendAsync` as described in README §7.3; inject `IAiPrivacyService`, `ConsentRepository`, `AuditRepository`, `QueryHistoryRepository`, `CostCalculator`, `IPreviewGate` (dialog abstraction) | P12-WP2, P12-WP3-T1, P12-WP3-T2 | 3 h | SI-1, CTRL-OGMA-016..020, CTRL-OGMA-022, FR-AI-001 |
| P12-WP3-T4 | Implement `AiDisabledGateway` — a null-object `IAiProvider` that throws `AiDisabledException` for any call; bound when `AiPrivacyTier == Offline` | P12-WP3-T3 | 0.5 h | FR-AI-001 |
| P12-WP3-T5 | Integration test `PayloadBuilder_Tier1_StripsForbiddenFields`: sends a request with content chunks; asserts `TierViolationException` thrown before any network call | P12-WP3-T1 | 1 h | FR-AI-004 |
| P12-WP3-T6 | Integration test `AiGateway_Tier0_ThrowsWithoutNetwork`: disabled tier produces `AiDisabledException`; mock network client asserts zero calls | P12-WP3-T4 | 1 h | FR-AI-001 |
| P12-WP3-T7 | Integration test `AiGateway_AlwaysWritesAuditEvent`: inject failing provider; assert audit row still written (even on provider error) | P12-WP3-T3 | 1 h | CTRL-OGMA-018 |
| P12-WP3-T8 | Integration test `AiGateway_RequiresConsentBefore_EgressCall`: no consent record → `ConsentRequiredException`; no HTTP call issued | P12-WP3-T3 | 1 h | CTRL-OGMA-019 |

---

## WP4 — Provider Adapters

**Goal:** Implement the four `IAiProvider` adapters; wire DI factory; verify each
against a recorded-response fixture.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP4-T1 | Implement `OpenAiCompatProvider`: POST `/v1/chat/completions`; maps `AiRequest` → OpenAI chat payload; maps response → `AiCompletion`; reads base URL from settings (supports OpenAI + DeepSeek-compatible endpoints) | P12-WP1-T5 | 2 h | FR-AI-002 |
| P12-WP4-T2 | Implement `AnthropicProvider` using `claude-api` skill guidance: POST to Anthropic Messages API; adds `cache_control: ephemeral` on system block and large metadata context blocks; sends `X-Anthropic-No-Training: 1` header by default; maps `PromptCacheInputTokens` to `AiCompletion.PromptCacheTokens` | P12-WP1-T5 | 3 h | FR-AI-002, CTRL-OGMA-022 |
| P12-WP4-T3 | Implement `OllamaProvider`: POST `http://localhost:{port}/api/chat`; port configurable; marks completion with `IsLocal=true` so `AiGateway` skips preview and audit for Tier-3 Ollama calls | P12-WP1-T5 | 1.5 h | FR-AI-002, FR-AI-006 |
| P12-WP4-T4 | Implement `AiProviderFactory`: reads active provider setting; constructs the correct implementation; validates that an API key exists before constructing cloud providers | P12-WP4-T1..T3 | 1 h | FR-AI-002 |
| P12-WP4-T5 | Record HTTP fixtures for each provider (WireMock.Net or similar); unit test each adapter against fixture: correct headers, payload shape, response mapping | P12-WP4-T1..T3 | 2.5 h | FR-AI-002 |
| P12-WP4-T6 | Test `AnthropicProvider_Sends_NoTraining_Header`: asserts `X-Anthropic-No-Training: 1` present on every request regardless of settings | P12-WP4-T2 | 0.5 h | CTRL-OGMA-022 |
| P12-WP4-T7 | Test `AnthropicProvider_Sets_CacheControl_OnSystem_And_LargeContext_Blocks`: asserts cache-control annotations present on expected message parts | P12-WP4-T2 | 0.5 h | prompt caching |

---

## WP5 — Payload Preview UI

**Goal:** Avalonia dialog that shows the exact payload before any egress call;
wired into `AiGateway` via `IPreviewGate` abstraction.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP5-T1 | Define `IPreviewGate` interface in `Application/Ai/`: `Task<PreviewDecision> ShowAsync(AiPayloadPreview)` returning `Send`, `Cancel`, or `RememberSession` | P12-WP1-T7 | 0.5 h | NFR-PROD-011, CTRL-OGMA-017 |
| P12-WP5-T2 | Implement `PayloadPreviewViewModel`: binds `AiPayloadPreview`; exposes formatted field list, byte count, tier label, provider name; commands: `SendCommand`, `CancelCommand`, `RememberSessionCommand` | P12-WP5-T1 | 2 h | CTRL-OGMA-017 |
| P12-WP5-T3 | Implement `PayloadPreviewDialog` Avalonia view: scrollable field list; byte-count badge; tier color badge; "What is this?" expandable explanation copy (i18n key `ai.preview.explanation`); Send/Cancel buttons with icons | P12-WP5-T2 | 2.5 h | CTRL-OGMA-017, NFR-PROD-011 |
| P12-WP5-T4 | Implement `AvaloniaPreviewGate` (binds `IPreviewGate` to `PayloadPreviewDialog`); register in DI | P12-WP5-T2..T3 | 1 h | CTRL-OGMA-017 |
| P12-WP5-T5 | Externalize all strings in `en` + `fr`; add icons from `icons.md`; keyboard: Tab through fields, Enter = Send, Escape = Cancel; screen-reader: dialog announces payload size and tier | P12-WP5-T3 | 1.5 h | i18n, a11y |
| P12-WP5-T6 | Integration test `PayloadPreview_Shown_Before_Every_EgressCall`: intercept `IPreviewGate`; assert it is called exactly once per `AiGateway.SendAsync` at Tier-1 and Tier-2; assert it is NOT called at Tier-0 or Tier-3 | P12-WP5-T4 | 1 h | NFR-PROD-011, beta gate G6 |

---

## WP6 — Privacy Center UI

**Goal:** A Privacy Center settings screen covering API keys, call history,
payload drill-down, delete actions, and audit export.

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP6-T1 | `PrivacyCenterViewModel`: sections — ActiveTier (combo), Providers (per-provider: key entry masked, test-connection button), RecentCalls (paginated list of `AiAuditEvent` rows), Actions (delete history, delete embeddings, export audit) | P12-WP2, P12-WP3-T3 | 3 h | CTRL-OGMA-020, FR-AI-009, FR-AI-010 |
| P12-WP6-T2 | `PrivacyCenterView` Avalonia layout: tabbed or sectioned; RecentCalls table (timestamp, provider, model, tier badge, token count, estimated cost, "View payload" button); cost totals footer | P12-WP6-T1 | 3 h | CTRL-OGMA-020, FR-AI-010 |
| P12-WP6-T3 | Payload drill-down panel: clicking "View payload" opens `PayloadDetailPanel` showing the exact SHA-256-verified payload field list and the response summary (if history retained) | P12-WP6-T1 | 1.5 h | CTRL-OGMA-020 |
| P12-WP6-T4 | Delete-history action: confirmation dialog → `QueryHistoryRepository.HardDeleteAllAsync()`; toast confirmation; does NOT delete `AiAuditEvent` rows | P12-WP6-T1 | 1 h | FR-AI-009, NFR-PROD-014 |
| P12-WP6-T5 | Delete-embeddings action: confirmation dialog → calls Phase 11 `IEmbeddingService.DeleteAllEmbeddingsAsync()`; toast confirmation | P12-WP6-T1 | 1 h | NFR-PROD-014 |
| P12-WP6-T6 | Export-audit action: saves `AuditRepository.ExportToJsonAsync()` to user-chosen file via OS save dialog; opens file manager to the saved location | P12-WP6-T1 | 1 h | CTRL-OGMA-020 |
| P12-WP6-T7 | Disable-AI toggle: sets `AiPrivacyTier.Offline`; visual indication that all AI features are paused; confirm that navigation to AI features shows a "AI is disabled" empty state | P12-WP6-T1 | 1 h | FR-AI-001 |
| P12-WP6-T8 | Externalize all strings `en` + `fr`; apply icons from `icons.md`; keyboard navigation; screen-reader for tier badges and cost totals | P12-WP6-T2..T7 | 1.5 h | i18n, a11y |
| P12-WP6-T9 | Integration test `PrivacyCenter_DeleteHistory_LeavesAuditIntact` and `PrivacyCenter_ExportAudit_ProducesValid_Json` | P12-WP6-T4..T6 | 1.5 h | CTRL-OGMA-020, NFR-PROD-014 |

---

## WP7 — Cost Display & Locale Formatting

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP7-T1 | `CostCalculator` price table JSON config (provider → model → USD per 1M tokens input/output); load at startup; fallback to zero if model unknown | P12-WP3-T2 | 1 h | FR-AI-010 |
| P12-WP7-T2 | `CostFormatter`: formats `EstimatedCostUsd` as USD or EUR per active `CultureInfo`; uses `CultureInfo.NumberFormat`; parametric test over `en-US`, `en-GB`, `fr-FR` | P12-WP7-T1 | 1 h | FR-AI-010, i18n |
| P12-WP7-T3 | Wire `CostFormatter` into `PrivacyCenterViewModel` per-call row and session total | P12-WP7-T2, P12-WP6-T1 | 0.5 h | FR-AI-010 |

---

## WP8 — Architecture Test

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP8-T1 | `AiGateway_IsTheOnly_EgressPoint` architecture test (NetArchTest or equivalent): assert no type in any project except `Infrastructure.Ai` has a dependency on `HttpClient` or an `IAiProvider` implementation class | P12-WP3-T3 | 2 h | SI-1, CTRL-OGMA-016 |
| P12-WP8-T2 | `AiContext_HasNo_DirectDependency_On_Reader`: assert `AI` bounded context does not reference `Reader` domain types directly | P12-WP8-T1 | 0.5 h | bounded-context discipline |
| P12-WP8-T3 | Add `AiGatewayChokepoint` architecture test class to CI test run; confirm it fails when a deliberate violation is introduced (red-green test) | P12-WP8-T1 | 0.5 h | SI-1 |

---

## WP9 — Integration & Golden-Corpus

| ID | Task | Dependencies | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P12-WP9-T1 | End-to-end `AiDisabled_CorePathsWork` integration test: disable AI (Tier-0); run scan, browse, search flows; assert no AI exceptions propagate | All WPs | 2 h | FR-AI-001 |
| P12-WP9-T2 | `Tier1_Payload_ContainsOnly_MetadataFields` with `simple-text` golden-corpus fixture: send a recommendation request; intercept payload; assert no PDF text content present | P12-WP3, P12-WP4 | 1.5 h | FR-AI-004 |
| P12-WP9-T3 | `Tier2_RequiresExplicit_Consent_Before_ContentSend`: attempt Tier-2 call without consent; assert `ConsentRequiredException` and zero HTTP calls | P12-WP3, P12-WP5 | 1 h | FR-AI-005, CTRL-OGMA-019 |
| P12-WP9-T4 | `QueryHistory_Delete_RemovesAll_AuditRows` false-positive guard: verify AuditEvent table row count unchanged after QueryHistory hard-delete | P12-WP2, P12-WP6-T4 | 1 h | FR-AI-009 |
| P12-WP9-T5 | `RetentionDisabled_NoHistoryWritten`: set history retention off; make AI call; assert `AiQueryHistoryEntry` count = 0 while `AiAuditEvent` count = 1 | P12-WP3 | 1 h | FR-AI-009 |
| P12-WP9-T6 | Benchmark `AiGateway_Overhead_Under_50ms`: measure gateway processing (consent check + payload build + preview gate mock + audit write) with a mocked provider returning immediately; assert P95 < 50 ms | P12-WP3 | 1.5 h | NFR-OGMA-007 |
| P12-WP9-T7 | Run `/security-review` and `/code-review`; record findings; resolve all R2-tier items before phase DoD | All WPs | — | Global DoD |
