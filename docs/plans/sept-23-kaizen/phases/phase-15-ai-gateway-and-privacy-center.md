# Phase 15: AI gateway, providers and Privacy Center

## 1. Header

| Field | Value |
|---|---|
| Wave | D, Search and AI |
| Size | 6–9 engineering days |
| Depends on | Phase 08 (Settings exists; the Privacy section is the mount point) |
| Owner decisions | **D-06** AI providers (recommended: one cloud provider plus local-only mode first, with exact model IDs confirmed by the currentness review in this phase) |
| Primary defects | K50 (gateway and privacy part), K17 (AI entries), K81 (raw errors) |
| Requirements | AI-001 (W), AI-002 (B), AI-004 (B), AI-005 (B), AI-009 (B), AI-010 (B), AI-011 (B); CTRL AI-privacy controls; NFR privacy |

## 2. Why this phase exists

The AI Reading Advisor is advertised in the navigation but can never work (K50, measured:
*Recommend* returns "AI advisor unavailable: AI features are disabled"; *Ask locally* stays
disabled). The chain is broken at every link:

1. **The privacy tier lives only in memory** and defaults to `Offline`
   (`Infrastructure/AI/AiPrivacyService.cs:11-30`; `Domain/Ai/AiPrivacyTier.cs`:
   `Offline = 0`, `MetadataOnly = 1`, `ContentAware = 2`, `LocalOllama = 3`). Nothing persists a
   user's choice.
2. **The Privacy Center is never shown.** `PrivacyCenterViewModel` and `Views/Ai/PrivacyCenterView.axaml`
   exist but are not constructed or routed (AI-011 B). History deletion, retention, cost and usage
   display live only there (AI-009/010 B).
3. **Only a disabled provider is registered.** `AddFailClosedAiRuntime` binds `IAiProvider` to
   `AiDisabledProvider` and `IAiPreviewGate` to `FailClosedPreviewGate`
   (`Infrastructure/AI/AiServiceExtensions.cs:60-69`), even though `AnthropicProvider`,
   `OpenAiCompatProvider`, `OllamaChatProvider` and `AiProviderFactory` exist in
   `Infrastructure/AI/Providers/`. `IAiProviderFactory` and `IAiProviderProfileService` are
   registered but unused (AI-002 B).
4. **View models hard-code a fictitious model:** `"openai", "gpt-test"` in
   `RecommendationPanelViewModel.cs:376` and `ReadingPlanViewModel.cs:155`; the Host view model
   defaults to `"openai"` (`HostSharingViewModel.cs:97,423`).
5. **Consent and payload preview are unreachable:** `AvaloniaPreviewGate` exists
   (`App/Ai/AvaloniaPreviewGate.cs`) but the fail-closed gate is bound; `RecordConsentAsync` has no
   callers (AI-004/005 B).
6. **Cost accounting has no price table.** `AiUsageBudgetService` accumulates `UsedCostUsd` from a
   nullable cost (`AiUsageBudgetService.cs:144-188`). Sept-14 commit `a5e56c7` stopped treating an
   unknown price as free, but there is still no model price source (astra F17/F30).

This phase makes the AI runtime **configurable, consented, persisted and honest**. Phase 16 then
makes the Advisor useful on top of it.

## 3. Objectives and exit criteria

1. **Persisted privacy tier** per user profile (SQLite settings table, audited change events).
   The default stays `Offline` (AI-001 fail-closed is preserved).
2. **Privacy Center mounted** at Settings → Privacy and AI, with sections: current tier (plain-language
   explanation of what leaves the device at each tier), providers, consent history, AI history
   (view, export, delete: AI-009), retention period, and usage and cost today and this month (AI-010).
3. **Provider profiles (AI-002):** add, edit or remove profiles through `IAiProviderProfileService`;
   provider type (Anthropic / OpenAI-compatible / local Ollama chat); model ID chosen from a
   **curated, dated model list** (not free text by default; an "advanced: custom model ID" option
   is allowed); API key stored only in the OS credential store (Windows Credential Manager, macOS
   Keychain), never in the database, logs or diagnostics; *Test connection* makes a minimal
   metadata-only call and reports latency and model.
4. **Runtime binding:** `IAiProvider` resolves through `AiProviderFactory` from the active profile at
   call time; `AiDisabledProvider` is used only when there is no profile or the tier is Offline.
   The hard-coded `"openai","gpt-test"` pairs are removed; options come from the active profile.
5. **Consent and preview (AI-004/005):** first use of each tier above Offline shows a consent dialog
   recording `AiConsentRecord`; every content-aware call shows the payload preview through
   `AvaloniaPreviewGate`, with "Always allow for this session" as an explicit, revocable choice
   (astra F18 finding: preview remembered once per session without disclosure is not acceptable).
6. **Cost table:** a versioned `ai-model-prices.json` (provider, model ID, input and output price
   per million tokens, currency, source URL, verified date) shipped with the app and refreshable;
   unknown price → "cost unknown" in the UI and the budget treats it conservatively (reserves
   before the call using a worst-case estimate).
7. **Currentness gate passed:** every provider name, model ID, price and endpoint used in the
   curated list is recorded in `00-currentness-register.md` with source and verification date,
   using the digital research engine. If Anthropic is selected, model IDs are taken from the
   `claude-api` skill reference at implementation time (at the time of this plan the family
   includes Opus 5.5, Sonnet 5 and Haiku 4.5; verify the exact IDs before shipping).
8. **Architecture:** HTTP stays in Infrastructure; Domain and Application stay free of HTTP;
   architecture tests updated deliberately where they asserted "AI disabled".
9. Real-window journey: configure a provider (a local mock server in the harness), accept
   consent, see the preview, get a response, see the usage counter change, delete history.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-model-gateway\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-security\SKILL.md` (prompt injection from PDF text, key custody)
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-cost-and-metering\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-observability-and-debugging\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-llm-integration\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\security\dpia-generator\SKILL.md` (school and minors context)
- `C:\wamp64\www\digital-research-engine\skills\source-evaluation\SKILL.md`, `...\source-verification\SKILL.md`; and the currentness gate procedure in `C:\wamp64\www\digital-research-engine\CLAUDE.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\ai-output-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\ai-agent-ux\SKILL.md` (the live target of the inactive `chwezi-dev-engine` alias `ux-for-ai`)
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\design-ethics-and-anti-dark-patterns\SKILL.md` (consent without dark patterns)
- The `claude-api` skill (Claude Code built-in) if Anthropic is selected in D-06.

## 5. Scope

**In scope:** privacy tier persistence; Privacy Center mount and completion; provider profiles UI;
OS credential storage for AI keys (reuse the patterns in
`Infrastructure/ClassroomClient/ClassroomCredentialStores.cs`: Windows Credential Manager, macOS
Keychain, Linux Secret Service); runtime provider resolution; consent and preview; the price table
and budget reservation; the currentness register entries; removal of hard-coded model IDs.

**Out of scope:** Advisor ranking, answers and reading plans (Phase 16); the school proxy and student
AI (Phase 19, which reuses this runtime); embeddings (Phase 14).

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 15.1 | Currentness review for D-06: providers, model IDs, prices, endpoints, terms (data retention, training use). Present options to the owner. | `docs/plans/sept-23-kaizen/00-currentness-register.md` | Each claim VERIFIED with source and date; owner decision recorded. |
| 15.2 | Persist the tier: a settings table plus migration; `AiPrivacyService` reads and writes through a repository; change events audited (minimised). | `Infrastructure/AI/AiPrivacyService.cs:11-30`, migrations | Restart keeps the tier; the audit row exists without payload content. |
| 15.3 | AI key custody: `IAiCredentialStore` in Application; OS-backed implementations in Infrastructure (share code with the classroom secret stores); keys never serialised. | New contract; `ClassroomCredentialStores.cs` patterns | Unit: keys absent from DB, logs and diagnostics export (grep test on the export). |
| 15.4 | Runtime provider resolution: replace the static `AiDisabledProvider` binding with a resolver using `IAiProviderProfileService` + `IAiProviderFactory`; `AvaloniaPreviewGate` bound in the desktop composition root. | `AiServiceExtensions.cs:60-69`, `AiProviderFactory.cs:44-97`, App composition | Composition tests updated; the disabled provider is used only when no profile or Offline. |
| 15.5 | Remove hard-coded model IDs; `RecommendationGenerationOptions` come from the active profile. | `RecommendationPanelViewModel.cs:376`, `ReadingPlanViewModel.cs:155`, `HostSharingViewModel.cs:97,423` | Grep test: no literal model IDs in `src/OgmaLibrary.App`. |
| 15.6 | Mount the Privacy Center in Settings; complete the sections (tier, providers, consent history, AI history view/export/delete, retention, usage/cost). | `ViewModels/Ai/PrivacyCenterViewModel.cs`, `Views/Ai/PrivacyCenterView.axaml` (58 lines today) | Real-window screenshots of every section. |
| 15.7 | Consent dialog and payload preview with explicit session scope; the record stored via `IAiConsentRepository`. | `App/Ai/AvaloniaPreviewGate.cs`, `Views/Ai/PayloadPreviewDialog.axaml`, `AiConsentRepository.cs` | Headless: no provider call happens without a consent record; revoke works. |
| 15.8 | Price table and reservation: `ai-model-prices.json` (versioned, sourced), estimate before call, reserve in `AiUsageBudgetService`, settle after; unknown price shown as unknown. | `Infrastructure/AI/AiUsageBudgetService.cs`, new pricing service | Unit: reservation prevents exceeding the daily limit under concurrency. |
| 15.9 | Prompt-injection guard: PDF text is always wrapped as untrusted data in the payload builder, with instruction isolation; add a red-team fixture PDF with injected instructions. | `IAiPayloadBuilder` implementation | The red-team fixture does not change the system behaviour (asserted on a mock provider transcript). |
| 15.10 | A mock AI server for tests and the harness (loopback only, deterministic responses). | `tests/` support | Used by the real-window journey J-AI-1. |
| 15.11 | Localise every AI string; plain-language tier descriptions reviewed against the DPIA draft. | Localization | Key parity test. |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| AI can never be enabled (K50) | Fail-closed runtime never replaced by a configurable one | Profile-based provider resolution | Users can turn AI on deliberately | AI FRs usable: 2/11 → 9/11 after Phases 15–16 | Inventory re-run plus J-AI-1 | Accidental egress | Default Offline; consent; preview |
| Tier forgotten on restart | In-memory field | Persisted, audited tier | Predictable privacy | Tier survives restart: no → yes | Harness restart test | Migration issue | Down-migration |
| Privacy Center unreachable | Not routed | Settings mount | Transparency and control | AI-009/010/011 B → W | Screenshots | Crowded settings | Sub-page |
| Fictitious model IDs | Placeholder never replaced | Profile-driven options, currentness-verified list | Calls actually succeed | Literal model IDs in App: 4 → 0 | Grep test | Model deprecation | Register review date plus custom-ID escape hatch |
| Unknown cost | No price source | Versioned price table plus reservation | Budget enforcement is real | Calls with unknown cost counted as $0: possible → never | Unit tests | Stale prices | Source and date shown; refresh |

## 8. Test plan

- **Unit:** tier persistence; resolver matrix (no profile, Offline, each provider type); credential store fakes; price reservation concurrency; payload builder untrusted-data wrapping.
- **Integration:** the mock provider end to end through `AiGateway` with consent and preview; the diagnostics export has no secrets.
- **Headless UI:** Privacy Center sections; consent dialog; payload preview; history delete.
- **Real-window:** J-AI-1 configure provider → consent → preview → response → usage updates → delete history; J-AI-2 revoke consent → calls blocked with a clear message.
- **Negative:** an invalid key (error mapped to a plain message, no raw `ex.Message`); a network offline; a provider 429 and 5xx (retry, circuit breaker already built); a budget exhausted.
- **Security:** a grep-based test that no API key appears in logs, DB or exports; the red-team injection fixture.

## 9. Acceptance commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test OgmaLibrary.sln --configuration Release --no-build --filter "Category!=Performance" -m:1
dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj -c Release --no-build
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Tag Ai -UseMockAiServer
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| D-06 provider choice, account and billing | Owner | Live-provider journeys NOT ASSESSED; the mock server covers behaviour |
| Provider terms and DPIA for school use | Owner / Phase 23 | ContentAware tier stays disabled for school profiles until approved |
| Live-provider conformance (real network) | Owner-supplied key | Run once and record; never in CI |
| macOS Keychain storage for AI keys | Phase 27 | Windows evidence only |

## 11. Risks and mitigations

- **Privacy regression.** Mitigation: default Offline; consent per tier; preview for content-aware
  calls; architecture tests keep HTTP in Infrastructure; an egress allowlist per provider (already built).
- **Model and price churn.** Mitigation: the currentness register has review dates; the curated
  list is data, not code.
- **Tests asserting AI is disabled** (two composition and two architecture tests). Mitigation:
  change them to assert *fail-closed by default*, not *never enabled*.

## 12. Execution prompt

```text
You are implementing Phase 15 (AI gateway, providers and Privacy Center) of the Sept-23 Kaizen
plan in C:\wamp64\www\Ogma-Library. Read first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md and AGENT_BRIEF.md
3. docs/plans/sept-23-kaizen/phases/phase-15-ai-gateway-and-privacy-center.md
4. docs/plans/sept-23-kaizen/03-defect-register.md row K50 and 08-master-plan.md D-06
Load the skills in section 4. Run the digital-research currentness gate first (15.1) and record every
provider, model ID and price claim in 00-currentness-register.md; if Anthropic is chosen, load the
claude-api skill for exact model IDs. Do not hard-code model IDs in view models. Never store API
keys outside the OS credential store. Preserve fail-closed defaults (Offline tier). Slices: A) 15.1–15.3;
B) 15.4–15.5; C) 15.6–15.7; D) 15.8–15.9; E) 15.10–15.11. After each slice run the section 9
commands and capture screenshots under docs/implementation/execution/evidence/sept-23-kaizen/phase-15/.
Record results and NOT ASSESSED items in docs/implementation/execution/phase-sept23-15-completion.md.
```
