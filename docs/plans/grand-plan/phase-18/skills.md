# Phase 18 — Skills & Slash Commands

Phase-scoped guidance. Every entry states which task it informs and what
artifact it must produce.

---

## Always-on

| Skill / command | Used in | Produces |
| --- | --- | --- |
| `superpowers:brainstorming` | Before WP1 (ADR-0013), WP5 (AI proxy pipeline), WP7 (dashboard design) | Structured options for DPIA model, pipeline sequencing, dashboard layout |
| `superpowers:test-driven-development` | Every WP — especially WP5 (AI proxy) and WP6 (quota) | Tests written before implementation; mock `IAiProvider` wired in all AI proxy tests |
| `superpowers:verification-before-completion` | Phase DoD | Secret-scan, DPIA coverage, quota-race verification on Win + macOS |
| `superpowers:requesting-code-review` + `/code-review` | P18-WP12-T7 | Resolved findings |
| `superpowers:systematic-debugging` | Any failing test | Diagnosis before fix |
| `superpowers:using-git-worktrees` | Phase 18 branch | `feature/P18-school-admin` |
| `documentation-generation:docs-architect` | P18-WP1-T6 | Updated `SOURCE-SUMMARY.md` |

---

## Phase-18-specific skills

### `saas:saas-admin-backoffice-tooling`

- **When:** WP2 (library publishing UI), WP3 (enrollment management), WP7
  (usage dashboard), WP8 (audit log viewer).
- **Produces:** Admin console views in `src/OgmaLibrary.App/Views/Admin/`;
  `IUsageDashboardService`; `AdminAuditLogView.axaml`.
- **Guidance:** Admin console is Host-local (no remote access). Use a sidebar
  navigation pattern with sections: Library, Profiles, AI Policy, Usage,
  Audit. Apply the calm-control design language at high information density
  (many table rows, many settings). Every destructive action (revoke profile,
  purge history) requires a confirmation dialog with the consequence stated
  explicitly.

### `saas:saas-entitlements-and-plan-gating` + `ai:ai-entitlements-and-feature-gating`

- **When:** WP6 (entitlement model, quota enforcement), WP5-T4 (quota check in
  pipeline), WP6-T5 (quota concurrency test).
- **Produces:** `ISchoolAiPolicyService` with atomic quota decrement;
  `SchoolAiEntitlements` DB model; `QuotaExhausted` response type.
- **Guidance:** The quota check must be atomic with the `AiUsageLedger` write
  (SQLite `BEGIN IMMEDIATE` transaction). Do not read quota then write in two
  steps — that is a race. Test with 20 concurrent requests against a quota of 15
  (P18-WP6-T5).

### `saas:saas-rate-limiting-and-quotas`

- **When:** WP6-T2 (sliding-window rate limiter per student).
- **Produces:** In-memory `TokenBucket` rate limiter; `429 TooManyRequests`
  response with `Retry-After`.
- **Guidance:** The rate limiter is per-session (in-memory, resets on Host
  restart). This is sufficient for Phase 18; a persistent rate limiter (survives
  restart) is a Phase 20 reliability improvement if needed. Apply the token-bucket
  algorithm (refill rate = `rateLimit / 60` tokens/second; burst = `rateLimit`).

### `ai:ai-cost-and-metering`

- **When:** WP5-T8 (`AiUsageLedger` write), WP6 (quota decrement), WP7
  (dashboard cost aggregation).
- **Produces:** `AiUsageLedger` schema; `IUsageDashboardService.GetSummaryAsync()`;
  `estimatedCostUsd` computation (tokens × per-token price from provider metadata).
- **Guidance:** The cost estimate is advisory, not billing. Use the provider's
  reported `usage.promptTokens` + `usage.completionTokens` and the published
  per-token price for the active model (configurable in `ISchoolAiPolicyService`).
  Display as "~$X.XX" in the dashboard. Do not store raw cost as a billing
  commitment.

### `ai:ai-agent-governance-and-limits`

- **When:** WP5 (entire AI proxy pipeline design), WP5-T7
  (`ClassroomAnswerGrounder`).
- **Produces:** `IAiProxyEndpointHandler` pipeline; `ClassroomAnswerGrounder`;
  the grounding verification test.
- **Guidance:** Apply output moderation at two levels: (1) citation grounding —
  every citation verified against Host catalogue (P18-WP5-T7); (2) payload
  scope enforcement — metadata-only tier physically restricts the payload
  built before the provider call, not as a post-hoc filter. The grounding step
  is the last step before the response is returned to the student.

### `ai:ai-agent-safety-and-red-team`

- **When:** P18-WP12-T1 (red-team the AI proxy endpoint).
- **Produces:** Red-team report; `ClassroomAnswerGrounder` hardening; DPIA
  bypass attempt results.
- **Guidance:** Attempt: (1) prompt injection via student query to exfiltrate
  API key; (2) fabricated citation injection in AI response to get a non-existent
  book citation through the grounder; (3) DPIA bypass by forging admin headers.
  Document each attempt and the control that blocks it.

### `security:dpia-generator` + `security:uganda-dppa-compliance`

- **When:** WP5-T3 (`IDpiaScreeningService`), WP3-T4 (birth-year / minor
  determination), P18-WP12-T2 (DPIA unit tests).
- **Produces:** `IDpiaScreeningService` implementation; DPIA configuration UI
  in admin console (jurisdiction selector, legal basis); DPIA unit tests.
- **Guidance:** The DPIA check must be synchronous in the pipeline (no async
  DPIA that could be skipped on timeout). Supported jurisdictions in V2:
  Uganda DPPA, EU GDPR — add more in Phase 19 based on owner's Phase 00
  jurisdiction decision. If jurisdiction is not configured, the check must
  return `Disqualified` (fail-safe, not fail-open).

### `frontend-ux:data-visualization`

- **When:** WP7 (usage dashboard charts).
- **Produces:** Bar chart (queries by student) and line chart (daily spend)
  using Avalonia LiveCharts2 or SkiaSharp-rendered charts; screen-reader
  fallback data table (P18-WP7-T3).
- **Guidance:** The chart library must render on both Windows and macOS without
  a WebView dependency (admin console is native Avalonia, not WebView-hosted).
  Confirm LiveCharts2 or SkiaSharp chart approach is consistent with Phase 03
  design tokens before implementing.

### `/security-review`

- **When:** P18-WP12-T6, after WP4, WP5, WP6 complete.
- **Focus areas:** AI key storage and memory zeroing (WP4); prompt injection
  resistance in AI proxy (WP5); DPIA fail-safe behavior (WP5-T3); quota race
  condition (WP6-T1); admin role enforcement on admin routes (WP1-T5).
- **Produces:** Security review findings; resolved issues before merge.

### `claude-api` (from `document-skills:claude-api`)

- **When:** WP4-T2 (`ISchoolAiKeyProvider` Anthropic-compatible provider
  implementation), WP5-T6 (provider call with prompt caching).
- **Produces:** Anthropic-compatible provider adapter with prompt caching enabled
  for classroom query workloads (reduces cost for repeated similar payloads).
- **Guidance:** Use the `anthropic` SDK pattern from the `claude-api` skill;
  ensure the school key is passed via the SDK's key injection point, not
  hard-coded or environment-variable-based. Prompt caching is especially
  valuable for metadata-only payloads that share a common system prompt.
