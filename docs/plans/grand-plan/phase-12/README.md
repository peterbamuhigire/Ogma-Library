# Phase 12 — AI Gateway & Privacy Center

Single mission: introduce the provider-neutral `IAiProvider` gateway as the **sole
off-device egress chokepoint**, the four privacy tiers, payload preview before any
cloud send, a full audit trail, and the Privacy Center surface — so Ogma Library
can be intelligent without ever being opaque.

---

## 1. Title & one-line mission

**Phase 12 — AI Gateway & Privacy Center**

Establish the single, class-level `IAiProvider` egress chokepoint, the four
privacy tiers, consent and audit infrastructure, and the Privacy Center UI so
every AI feature in the product — including school-managed AI in Phase 18 — routes
through one controllable, auditable, reversible surface.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| Tier | MVP (gateway + Tier-0/1 + Privacy Center core) · V1 (Tier-2 content-aware, cost, history erasure) |
| Estimate | 3 engineer-weeks |
| Owner | Peter Bamuhigire / Chwezi Core Systems |
| PRD build-phase mapping | Original Phase 6 (AI advisor foundation) |
| Platforms | Windows (WebView2 not needed here) + macOS; fully cross-platform .NET 10 |
| ADRs in scope | ADR-0007 (provider-neutral AI gateway + 4 tiers) |
| Security/CTRL IDs | SI-1, CTRL-OGMA-016, 017, 018, 019, 020, 022 |
| Status | WP1 contracts, WP2 persistence, WP3 gateway core, WP4 provider adapters, WP5 payload preview, WP6 Privacy Center shell, WP7 cost display, and WP8 architecture guards implemented locally: AI privacy/consent/audit/history contracts, provider-neutral DTOs, repository interfaces, EF migration, SQLite repositories, payload-preview/consent/audit gateway enforcement, OpenAI-compatible/DeepSeek-compatible, Anthropic, Ollama adapters, localized payload preview dialog shell, Privacy Center tier/history/embedding/audit operations, culture-aware cost text, AI egress boundary tests, and focused tests; WP9 pending |

---

## 3. Objectives

When this phase is done, the following are true:

1. **Single chokepoint.** Every off-device AI call in the codebase routes through
   `IAiProvider`; an architecture test asserts no other code path touches a
   provider HTTP client directly (SI-1).
2. **Four tiers enforced.** Offline (Tier-0), metadata-only cloud default (Tier-1),
   content-aware opt-in (Tier-2), and local Ollama (Tier-3) are implemented and
   tested; the default install is Tier-0 (no egress).
3. **Payload preview passes gate G6.** Before any Tier-1+ call the user sees the
   exact payload; a test asserts no call fires without the preview gate being
   cleared (NFR-PROD-011, CTRL-OGMA-017, beta gate G6).
4. **Consent and audit per call.** Every off-device call writes an immutable
   `AiAuditEvent` row; no call succeeds without prior per-tier consent recorded
   (CTRL-OGMA-018, 019); no-training opt-out is the default (CTRL-OGMA-022).
5. **AI fully disableable.** `IAiAdvisorService` can be disabled globally; the
   core catalogue, reader, and search work identically with AI off (FR-AI-001).
6. **Privacy Center surface.** A dedicated settings area shows API keys (write-only
   display), last-N calls with exact payloads, delete-history and
   delete-embeddings actions, and an exportable audit log.
7. **LAN reuse proven.** The gateway is designed so Phase 18 (School-Managed AI)
   can substitute its own `IAiProvider` implementation without altering the
   `Application` layer; the host-side composition root is the only binding point.

---

## 4. Scope

### In scope

- `IAiProvider` interface (in `Application` layer) and its implementation adapter
  classes in `Infrastructure` for OpenAI-compatible, Anthropic (with prompt
  caching, via `claude-api` skill), DeepSeek-compatible, and Ollama.
- `IAiAdvisorService` (use-case interface) and `IAiPrivacyService` (tier + consent
  management), both in `Application`.
- `AiGateway` class — the single composition point that wraps the active provider,
  enforces tier, runs payload preview, records audit, and checks consent.
- `AiPrivacyTier` enum (Offline / MetadataOnly / ContentAware / LocalOllama) and
  `AiConsentRecord` aggregate in `Domain`.
- `AiQueryHistory` and `AiAuditEvent` table additions (EF Core migration, reversible).
- Payload-preview dialog (Avalonia): shows exactly what will be sent, with a
  "Send" / "Cancel" and a "Remember for this session" option.
- Privacy Center screen: API key entry (masked, OS credential store), last-N call
  table with payload drill-down, delete-history button, delete-embeddings button,
  export-audit button.
- Per-call cost display row (model name + token count + estimated USD/EUR,
  FR-AI-010); currency formatting via `CultureInfo` (en/fr).
- Architecture test: `AiGateway_IsTheOnly_EgressPoint` asserts no type outside
  `Infrastructure.Ai` calls an HTTP client to a provider URL.
- Anthropic provider implementation uses prompt caching (cache-control headers per
  the Anthropic API); see `claude-api` skill for implementation guidance.
- All new strings externalized in `en` + `fr`.
- Icon manifest for this phase (see `icons.md`).

### Explicitly out of scope

- Recommendation logic and reading plans (Phase 13).
- Local embeddings pipeline (Phase 11 owns that; Phase 12 consumes it via
  `IAiProvider` Tier-3 path).
- School-managed AI entitlements and quotas (Phase 18, reuses this gateway).
- DPIA per off-device feature (full DPIA in Phase 19; Phase 12 records the
  DPIA-relevant data model so Phase 19 can complete it).
- The 3D shelf (Phase 14) and OCR (Phase 15).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-AI-001 | MVP | AI fully disableable; never blocks core | `AiDisabled_CorePathsWork` integration test: scan, browse, read, search all pass with AI disabled |
| FR-AI-002 | MVP | Provider choice: OpenAI-compat / Anthropic / DeepSeek / Ollama | `AiProviderFactory_CanInstantiate_All_Providers` unit test |
| FR-AI-004 | MVP | Default metadata-only; cloud sends title/author/tags/categories/descriptions/notes only | `Tier1_Payload_ContainsOnly_MetadataFields` payload-inspection test |
| FR-AI-005 | V1 | Content-aware only on explicit per-library/per-query opt-in; privacy label before any chunk | `Tier2_RequiresExplicit_Consent_Before_ContentSend` integration test |
| FR-AI-009 | V1 | Local query history; delete + disable retention | `QueryHistory_Delete_RemovesAll_AuditRows` and `RetentionDisabled_NoHistoryWritten` tests |
| FR-AI-010 | V1 | Per-cloud-call model usage + estimated cost | `CostEstimate_FormattedPer_Locale` parametric test (en, fr) |
| NFR-PROD-011 | MVP | Privacy-tier + payload preview | `PayloadPreview_Shown_Before_Every_EgressCall` integration test |
| NFR-PROD-013 | MVP | Local audit trail | `AuditEvent_WrittenFor_Every_ProviderCall` integration test |
| NFR-PROD-014 | V1 | AI history + embedding erasure | `EraseHistory_DeletesQueryHistory_And_EmbeddingVectors` integration test |
| CTRL-OGMA-016 | MVP | Single off-device egress chokepoint | Architecture test `AiGateway_IsTheOnly_EgressPoint` |
| CTRL-OGMA-017 | MVP | Payload preview before any send | `PayloadPreview_Shown_Before_Every_EgressCall` |
| CTRL-OGMA-018 | MVP | Audit per call | `AuditEvent_WrittenFor_Every_ProviderCall` |
| CTRL-OGMA-019 | MVP | Per-tier/provider consent | `EgressCall_Requires_ConsentRecord_For_Tier` |
| CTRL-OGMA-020 | MVP | User can see and export call history/payloads | `PrivacyCenter_ExportAudit_ProducesValid_Json` |
| CTRL-OGMA-022 | MVP | No-training opt-out is default | `Anthropic_Provider_Sends_NoTraining_Header_ByDefault` |
| SI-1 | MVP | Single egress point | Architecture test (see CTRL-OGMA-016 row) |

---

## 6. Dependencies

### Depends on

| Dependency | Why |
| --- | --- |
| Phase 02 — Solution scaffolding | 9-project solution, DI composition root, architecture test harness |
| Phase 03 — Design system & i18n | Avalonia theming, icon system, `ILocalizationService`, string resource pipeline |
| Phase 04 — Catalogue & data layer | EF Core + SQLite in place; `AiQueryHistory` and `AiAuditEvent` table migrations extend this |
| Phase 05 — Ingestion pipeline | `BookMetadata` domain types consumed by payload builder |
| Phase 11 — Semantic search & embeddings | Embedding vectors exist in DB; Phase 12 provides the "delete embeddings" action in Privacy Center (reads Phase 11's `EmbeddingVectors` table) |
| ADR-0007 ratified | Provider-neutral gateway decision confirmed |

### Unblocks

| Unblocked | How |
| --- | --- |
| Phase 13 — AI Reading Advisor | Consumes `IAiAdvisorService` + `IAiProvider` |
| Phase 18 — School-Managed AI | Substitutes a school-keyed `IAiProvider` into the same `AiGateway`; the gateway is the reuse surface |
| Phase 19 — Security Hardening | DPIA uses the `AiAuditEvent` schema and consent model Phase 12 establishes |

---

## 7. Architecture & approach

### 7.1 Bounded contexts touched

- **AI Advisor** (primary — new context introduced in this phase).
- **Settings & Security** (Privacy Center screen; OS credential store for API keys).
- **Catalogue** (read-only: payload builder reads `BookMetadata` fields).

### 7.2 Component map

```
Application/
  IAiProvider          — interface for a single off-device call (prompt → completion)
  IAiAdvisorService    — use-case interface: recommendations, reading plans, answers
  IAiPrivacyService    — tier query/set, consent record, payload preview request

Domain/
  AiPrivacyTier        — enum: Offline | MetadataOnly | ContentAware | LocalOllama
  AiConsentRecord      — when, who, tier, scope (library-level vs query-level)
  AiAuditEvent         — immutable: timestamp, tier, provider, model, token counts,
                         estimated cost, payload hash, response hash
  AiQueryHistoryEntry  — user query + structured response summary (erasable)

Infrastructure/
  AiGateway            — composes: active provider, tier enforcer, payload builder,
                         preview gate, consent check, audit writer, cost calculator
  Providers/
    OpenAiCompatProvider   — OpenAI + DeepSeek endpoint
    AnthropicProvider      — Anthropic API; uses prompt caching (cache-control:
                             ephemeral on system block; cache-control: ephemeral on
                             large context blocks per claude-api skill guidance)
    OllamaProvider         — local; no egress; bypasses preview/audit requirements
  PayloadBuilder       — constructs the off-device payload from tier rules
  ConsentRepository    — reads/writes AiConsentRecords (EF Core)
  AuditRepository      — append-only writes to AiAuditEvent; export to JSON

OgmaLibrary.App/
  ViewModels/
    PrivacyCenterViewModel  — binds Privacy Center screen
    PayloadPreviewViewModel — binds payload-preview dialog
  Views/
    PrivacyCenterView       — settings area
    PayloadPreviewDialog    — shows payload before send
```

### 7.3 AiGateway call sequence

```
Caller -> AiGateway.SendAsync(request)
  1. Check tier: Tier-0? → throw AiDisabledException (no network I/O)
  2. Check Tier-3? → route to OllamaProvider (no preview, no audit)
  3. Build payload from PayloadBuilder (respects tier rules)
  4. Open PayloadPreviewDialog if not "remember for session" → user confirms
  5. Check ConsentRecord for this tier+provider; if absent → prompt consent screen
  6. Dispatch to active IProvider.CompleteAsync(payload)
  7. Write AiAuditEvent (immutable) + AiQueryHistoryEntry (if retention enabled)
  8. Return structured response to caller
```

### 7.4 Privacy tiers in detail

| Tier | Enum value | What leaves the device | Default |
| --- | --- | --- | --- |
| Offline | 0 | Nothing | Yes (install default) |
| MetadataOnly | 1 | title, author, tags, categories, descriptions, notes | Cloud default once a key is set |
| ContentAware | 2 | Tier-1 fields + text chunks from selected pages/books | Opt-in per library + per query |
| LocalOllama | 3 | Nothing (local Ollama, no egress) | User choice |

FR-AI-004 mandates Tier-1 as the cloud default; Tier-2 requires a separate,
explicit opt-in dialog (FR-AI-005, CTRL-OGMA-019).

### 7.5 Anthropic provider & prompt caching

The `AnthropicProvider` follows the `claude-api` skill's guidance:

- System block marked `cache_control: {"type": "ephemeral"}` on calls with a
  stable library-context prefix (title+author list for recommendations).
- Large context injections (metadata-only payload) also marked ephemeral.
- Cache hit rate tracked in `AiAuditEvent.PromptCacheTokens` for cost visibility.
- No-training: `X-Anthropic-No-Training: 1` header sent on every request
  (CTRL-OGMA-022 default).

### 7.6 LAN classroom reuse design

`AiGateway` is bound in the DI composition root (`App` project). Phase 18
(School-Managed AI) will:

1. Inject a school-keyed `AnthropicProvider` (or other) for the Host composition.
2. The `AiGateway` class, `IAiPrivacyService`, payload preview, and audit trail
   are **reused without modification** — only the provider binding changes.
3. The Host becomes the chokepoint for the whole classroom; student clients call
   the Host's `IAiAdvisorService` proxy over the LAN, which internally calls the
   same `AiGateway`.

This design satisfies LAN-CLASSROOM-ARCHITECTURE.md §5: "all AI traffic routes
through the Host's `IAiProvider` gateway under the four privacy tiers."

### 7.7 Data schema additions (EF Core migration M012)

```sql
-- Reversible migration; down() drops the tables
CREATE TABLE AiConsentRecords (
  Id TEXT PRIMARY KEY,
  Tier INTEGER NOT NULL,
  Provider TEXT NOT NULL,
  Scope TEXT NOT NULL,      -- 'library:<id>' or 'session' or 'query'
  GrantedAt TEXT NOT NULL,
  RevokedAt TEXT
);

CREATE TABLE AiAuditEvents (
  Id TEXT PRIMARY KEY,
  OccurredAt TEXT NOT NULL,
  Tier INTEGER NOT NULL,
  Provider TEXT NOT NULL,
  Model TEXT NOT NULL,
  PromptTokens INTEGER,
  CompletionTokens INTEGER,
  PromptCacheTokens INTEGER,
  EstimatedCostUsd REAL,
  PayloadHash TEXT NOT NULL,  -- SHA-256 of the exact payload sent
  ResponseHash TEXT NOT NULL,
  QueryHistoryEntryId TEXT    -- FK to AiQueryHistoryEntries (nullable)
);

ALTER TABLE AiQueryHistory ADD COLUMN HistoryId TEXT NOT NULL DEFAULT '';
ALTER TABLE AiQueryHistory ADD COLUMN QueryType TEXT NOT NULL DEFAULT '';
UPDATE AiQueryHistory SET HistoryId = 'legacy-' || QueryId WHERE HistoryId = '';
UPDATE AiQueryHistory SET QueryType = 'legacy' WHERE QueryType = '';
CREATE UNIQUE INDEX UX_AiQueryHistory_HistoryId ON AiQueryHistory (HistoryId);
```

All migration scripts are reversible (down() method present and tested).

### 7.8 Cross-platform notes

- API key storage: `Infrastructure.Security.OsCredentialStore` uses DPAPI on
  Windows and Keychain Services on macOS (introduced Phase 02 stub, implemented
  Phase 19 fully — Phase 12 uses the stub interface with a fallback to encrypted
  `settings.db` field, gated behind a TODO tracked in the phase change log).
- HTTP client: `System.Net.Http.HttpClient` with `SocketsHttpHandler`; fully
  cross-platform; no P/Invoke.
- Payload-preview dialog: Avalonia `Window`; no platform-specific rendering needed.

---

## 8. Work breakdown (summary)

Full task detail in `tasks.md`.

| WP | Work Package | Key tasks |
| --- | --- | --- |
| WP1 | Domain & interfaces | `AiPrivacyTier`, `AiConsentRecord`, `AiAuditEvent`, `IAiProvider`, `IAiAdvisorService`, `IAiPrivacyService` |
| WP2 | Data layer | EF Core migration M012; repositories for consent + audit + history |
| WP3 | AiGateway core | `AiGateway` class with tier enforcement, payload builder, preview gate, consent check, audit writer |
| WP4 | Provider adapters | `OpenAiCompatProvider`, `AnthropicProvider` (with prompt caching), `OllamaProvider`; DI factory |
| WP5 | Payload preview UI | `PayloadPreviewDialog` + `PayloadPreviewViewModel`; en/fr strings; icon manifest |
| WP6 | Privacy Center UI | `PrivacyCenterView` + `PrivacyCenterViewModel`; key entry, call table, delete actions, export |
| WP7 | Cost display | `CostCalculator`; per-call USD/EUR display; locale formatting |
| WP8 | Architecture test | `AiGateway_IsTheOnly_EgressPoint`; architecture test project (Phase 02 harness) |
| WP9 | Integration & golden-corpus | Full integration test suite; all FR/CTRL IDs covered |

---

## 9. Cross-cutting checklist

- [x] **Colorful icons + manifest** — `icons.md` lists all new icons; owner
      procurement request appended; placeholders wired during build.
- [x] **i18n (en/fr)** — All Privacy Center labels, payload-preview copy, consent
      prompts, cost display, and error messages are resource-keyed; `fr` present
      in same PR; pseudolocale check passes.
- [x] **Accessibility (keyboard + SR)** — Privacy Center and payload-preview dialog
      are keyboard-navigable; all icons paired with accessible labels from
      `IconCatalog`; screen-reader walkthrough covers the consent and delete flows.
- [x] **Privacy/egress** — `AiGateway_IsTheOnly_EgressPoint` architecture test;
      payload-preview integration test; R2 (privacy-breach) defects are
      unwaivable release blockers.
- [x] **Reversibility** — `AiQueryHistory` delete is soft-then-hard (reversible
      window); `AiAuditEvent` is immutable (not deletable, exportable); EF Core
      migration M012 has a down() method.
- [x] **Performance budgets** — NFR-OGMA-007: AI metadata-only call gateway
      overhead (excluding provider latency) ≤ 50 ms measured by benchmark.
- [x] **Bounded-context tests** — `AiContext_HasNo_DirectDependency_On_Reader` and
      similar; `AiGateway_IsTheOnly_EgressPoint` architecture test.
- [x] **Documentation** — XML doc comments on all public interfaces; ADR-0007
      updated with implementation notes; `docs-architect` run to update HLD §7.

---

## 10. Definition of Done

Global DoD (grand-plan README §6) plus:

- [ ] Every FR/NFR/CTRL ID in section 5 has a passing deterministic test or tagged gap.
- [ ] Golden-corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format --verify-no-changes`, `dotnet build` (warnings = errors),
      `dotnet test`, and architecture tests pass on both Windows and macOS CI runners.
- [ ] New user strings externalized and present in `en` + `fr`; pseudolocale CI check passes.
- [ ] Every new control has a colorful icon and accessible label; keyboard + screen-reader
      walkthrough passes; `icons.md` complete.
- [ ] ADR-0007 implementation notes recorded; HLD §7 updated; hybrid validation gate passes.
- [ ] `AiGateway_IsTheOnly_EgressPoint` architecture test passes (SI-1, CTRL-OGMA-016).
- [ ] `PayloadPreview_Shown_Before_Every_EgressCall` integration test passes (beta gate G6,
      NFR-PROD-011, CTRL-OGMA-017).
- [ ] `AuditEvent_WrittenFor_Every_ProviderCall` and `EgressCall_Requires_ConsentRecord_For_Tier`
      pass (CTRL-OGMA-018, 019).
- [ ] `AnthropicProvider` sends `X-Anthropic-No-Training: 1` by default; test asserts header
      present (CTRL-OGMA-022).
- [ ] Privacy Center delete-history and delete-embeddings actions tested end-to-end
      (NFR-PROD-014, CTRL-OGMA-020).
- [ ] EF Core migration M012 down() tested (reversibility).
- [ ] Cost display formats correctly in `en` and `fr` locales (FR-AI-010).
- [ ] `/code-review` and `/security-review` completed; all findings resolved.
- [ ] LAN reuse design documented: comment in `AiGateway` explaining Phase 18 injection point.

---

## 11. Skills to use

Full detail in `skills.md`.

| Skill | Task |
| --- | --- |
| `claude-api` | WP4: Anthropic provider adapter with prompt caching |
| `ai:ai-model-gateway` | WP3: AiGateway architecture |
| `ai:ai-security` + `security:dpia-generator` | WP3/WP8: consent model, no-training default, CTRL IDs |
| `ai:ai-cost-and-metering` | WP7: cost calculator and display |
| `ai:ux-for-ai` + `ai:ai-output-design` | WP5/WP6: payload-preview and Privacy Center UX |
| `ai:ai-observability-and-debugging` | WP2/WP9: audit event schema and query |
| `frontend-design:frontend-design` | WP5/WP6: Privacy Center and payload-preview UI |
| `superpowers:test-driven-development` | All WPs |
| `security-scanning:security-hardening` | WP3/WP8: architecture test; egress chokepoint |
| `/security-review` | Phase gate |

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `IAiProvider`, `IAiAdvisorService`, `IAiPrivacyService` | `src/OgmaLibrary.Application/Ai/` |
| `AiPrivacyTier`, `AiConsentRecord`, `AiAuditEvent`, `AiQueryHistoryEntry` | `src/OgmaLibrary.Domain/Ai/` |
| `AiGateway`, provider adapters, `PayloadBuilder`, repositories | `src/OgmaLibrary.Infrastructure/Ai/` |
| `PayloadPreviewDialog`, `PrivacyCenterView` + ViewModels | `src/OgmaLibrary.App/Views/Settings/` |
| EF Core migration M012 | `src/OgmaLibrary.Infrastructure/Persistence/Migrations/` |
| Architecture test `AiGatewayChokepoint` | `tests/OgmaLibrary.ArchitectureTests/` |
| Integration tests (AI gateway suite) | `tests/OgmaLibrary.Tests.Integration/Ai/` |
| `icons.md` (icon manifest, Phase 12) | `docs/plans/grand-plan/phase-12/icons.md` |
| ADR-0007 implementation notes | `docs/adr/0007-ai-gateway.md` (amended) |
| HLD §7 update | `docs/references/HLD.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Provider API breaking change invalidates adapter | R5 | Abstract behind `IAiProvider`; adapter has its own integration tests against a recorded-response fixture; version pinned |
| Payload-preview dialog bypassed by a future caller | R2 | Architecture test + integration test on every AI-using path; any new caller must pass the same gateway |
| Anthropic prompt-caching cache miss rate higher than expected, raising cost | R5 | Cache hit rate tracked in `AiAuditEvent`; cost dashboard in Privacy Center makes it visible; caching is an optimization, not a correctness dependency |
| OS credential store stub in Phase 12 defers full DPAPI/Keychain to Phase 19 | R2 | Keys stored in encrypted `settings.db` field as fallback; flagged as beta-gate blocker pending Phase 19 completion |
| Consent fatigue: too many preview dialogs degrade UX | R5 | "Remember for this session" option; per-library consent level; UX tested with `ux-for-ai` skill review |

---

## 14. Owner asks

1. **Icon procurement.** Procure the premium PNG icon set listed in `icons.md`
   (privacy-tier icons, provider logos, key, audit, cost, disable-AI icons) in
   the agreed style/sizes from `ICON-SYSTEM.md`. Placeholders are in use during
   build; premium PNGs are a release blocker.

2. **Provider preference order.** Confirm the default provider shown in the
   Privacy Center when a user first enables cloud AI: should it be
   OpenAI-compatible, Anthropic, or no default (force an explicit choice)?
   Needed before WP6 UI copy is final.

3. **No-training opt-out wording.** Confirm the exact French wording for the
   no-training consent toggle (CTRL-OGMA-022) so it is legally accurate in the
   francophone market. (English draft: "Do not use my data to train AI models —
   this is the default."; French needs native review.)

4. **Cost currency.** FR-AI-010 shows estimated cost. Confirm whether to show
   USD only, or USD + EUR (formatted per locale). Needed before WP7 finalization.

---

## 15. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Chwezi Core Systems | v1.0 baseline authored |
| 2026-06-01 | Codex | WP1 contracts started: AI privacy tier, consent/audit/history records, gateway request/completion/preview DTOs, advisor/privacy interfaces, and focused contract tests |
| 2026-06-01 | Codex | WP2 persistence implemented: consent/audit/history repository contracts, SQLite repositories, Phase 12 EF migration with legacy history backfill, and persistence tests |
| 2026-06-01 | Codex | WP3 gateway core implemented: payload preview gate contract, gateway enforcement, disabled provider, payload hashing, cost attribution, and focused gateway tests |
| 2026-06-01 | Codex | WP4 provider adapters implemented: OpenAI-compatible/DeepSeek-compatible chat, Anthropic Messages with no-training/cache-control headers, local Ollama chat, provider factory, and stubbed HTTP contract tests |
| 2026-06-01 | Codex | WP5 payload preview implemented: localized view model, Avalonia dialog shell, preview gate bridge, Send/Cancel/Remember decisions, and focused payload-preview tests |
| 2026-06-01 | Codex | WP6 Privacy Center shell implemented: concrete privacy service, tier controls, recent audit list, delete-history action, embedding erasure action, audit export, en/fr labels, and focused tests |
| 2026-06-01 | Codex | WP7 cost display implemented: culture-aware USD formatter, Privacy Center formatted cost rows, and en/fr decimal-format tests |
| 2026-06-01 | Codex | WP8 architecture guards implemented: AI provider HTTP client ownership test and AI/Reader bounded-context dependency test |
