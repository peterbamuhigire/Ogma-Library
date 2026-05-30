# Phase 12 — Skills & Slash Commands

This file maps each skill to the specific work package(s) it informs and the
artifact it should produce. See `SKILLS-INDEX.md` for the project-wide map.

---

## Primary skills

### `claude-api` (document-skills:claude-api)

- **Task:** P12-WP4-T2 — `AnthropicProvider` implementation.
- **Why:** The Anthropic adapter must use prompt caching correctly (ephemeral
  cache-control on the system block and large metadata context blocks), send the
  no-training header by default, and map the `prompt_cache_input_tokens` field
  in the response to `AiCompletion.PromptCacheTokens`.
- **Artifact:** `src/OgmaLibrary.Infrastructure/Ai/Providers/AnthropicProvider.cs`
  with XML doc comments and accompanying unit tests against a WireMock fixture.
- **Invocation:** Invoke `claude-api` before writing `AnthropicProvider`; use its
  guidance on the Messages API, caching headers, and model-ID conventions.

### `ai:ai-model-gateway`

- **Task:** P12-WP3-T3 — `AiGateway` central class design.
- **Why:** The gateway aggregates tier enforcement, payload building, preview,
  consent, audit, and cost in a single composition seam. The skill provides
  patterns for provider-neutral abstraction, error handling, and retry policy.
- **Artifact:** `AiGateway.cs` with inline comments tracing each step to the CTRL
  IDs it satisfies.

### `ai:ai-security`

- **Tasks:** P12-WP3-T3, P12-WP8-T1 — consent model, no-training default,
  architecture test.
- **Why:** Ensures that the consent model, payload hash, and architecture test
  design meet the CTRL-OGMA-016..022 controls and that the R2 risk tier is
  handled rigorously.
- **Artifact:** Review checklist applied to `AiGateway`, `ConsentRepository`, and
  `AuditRepository`; findings resolved before phase DoD.

### `ai:ai-cost-and-metering`

- **Tasks:** P12-WP7-T1, P12-WP7-T2 — price table, `CostCalculator`, locale
  formatting.
- **Why:** Token-to-cost mapping and display, including cache hit savings, must be
  correct and locale-aware.
- **Artifact:** `CostCalculator.cs`, `CostFormatter.cs`, price-table JSON config,
  and parametric locale tests.

### `ai:ux-for-ai` + `ai:ai-output-design`

- **Tasks:** P12-WP5-T2..T3, P12-WP6-T2 — payload preview dialog, Privacy Center.
- **Why:** The payload-preview UX must be honest and comprehensible (not
  intimidating); the Privacy Center must feel like *calm control*, not a
  compliance screen. These skills provide AI-UX design patterns.
- **Artifact:** Annotated UX rationale comment block in `PayloadPreviewViewModel`
  and `PrivacyCenterViewModel`; no "dark pattern" patterns (no pre-checked
  consent, no obscured opt-out).

### `ai:ai-observability-and-debugging`

- **Tasks:** P12-WP2-T4, P12-WP9-T5..T6 — audit event schema, benchmark.
- **Why:** The `AiAuditEvent` schema must capture enough to debug provider issues,
  identify cost anomalies, and satisfy CTRL-OGMA-020 (user can inspect payloads).
- **Artifact:** Schema fields reviewed against observability needs; benchmark
  infrastructure for gateway overhead.

### `security:dpia-generator`

- **Tasks:** P12-WP3-T3, P12-WP2 — DPIA-readiness of data model.
- **Why:** The `AiConsentRecord` and `AiAuditEvent` schema must capture the
  data-flows and lawful bases that Phase 19 will need for the full DPIA.
- **Artifact:** A comment block in the migration M012 file noting which fields are
  DPIA-relevant and what processing purpose each serves.

### `frontend-design:frontend-design`

- **Tasks:** P12-WP5-T3, P12-WP6-T2 — Avalonia UI for payload preview and
  Privacy Center.
- **Why:** These screens must meet the "premium means calm control" aesthetic and
  the colorful-icon standard from `ICON-SYSTEM.md`.
- **Artifact:** `PayloadPreviewDialog.axaml`, `PrivacyCenterView.axaml` with
  design tokens, colorful icons wired from `IconCatalog`, and RTL-safe layout.
- **Note:** Also reference `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md`
  for Avalonia-specific binding, styling, and DataTemplate conventions.

### `security-scanning:security-hardening`

- **Task:** P12-WP8-T1..T3 — architecture egress test.
- **Why:** The architecture test is the mechanical enforcement of the
  single-chokepoint principle; the skill provides patterns for writing dependency
  rule tests (NetArchTest) that fail fast on violations.
- **Artifact:** `AiGatewayChokepoint.cs` architecture test class in
  `tests/OgmaLibrary.ArchitectureTests/`.

---

## Always-on skills (applied every work package)

| Skill | How applied |
| --- | --- |
| `superpowers:test-driven-development` | Write failing test for each WP task before the implementation |
| `superpowers:verification-before-completion` | Run `dotnet test`, architecture tests, and benchmark before claiming a WP done |
| `superpowers:requesting-code-review` + `/code-review` | End of each WP merge |
| `/security-review` | P12-WP9-T7 — mandatory for this privacy/AI phase |
| `superpowers:systematic-debugging` | Any test failure before proposing a fix |
| `documentation-generation:docs-architect` | Update HLD §7 and ADR-0007 after WP3 |

---

## Slash commands

| Command | When |
| --- | --- |
| `/code-review` | After each WP; escalate to `--effort high` for WP3 and WP8 |
| `/security-review` | P12-WP9-T7, mandatory; all R2 findings resolved before DoD |
| `/verify` | After WP5 and WP6 UI work: drive the app to confirm payload-preview dialog and Privacy Center render correctly on Windows and macOS |
| `/simplify` | After WP3 and WP4: clean up `AiGateway` and provider adapters |
