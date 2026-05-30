# Phase 23 — Skills & Slash Commands

> Phase-scoped detail. The bird's-eye map is `SKILLS-INDEX.md`.

---

## Always-on (every phase)

| Skill / command | Task | Artifact |
| --- | --- | --- |
| `superpowers:writing-plans` → `superpowers:executing-plans` | Before WP1 | Execution plan for the 3-week phase (excluding soak) |
| `superpowers:test-driven-development` | WP6, WP7 | Extension SDK tests, importer tests before implementation |
| `superpowers:verification-before-completion` | End of each WP | Checklist confirming CI green and all deliverables present |
| `superpowers:systematic-debugging` | Any soak-period bug | Root-cause analysis before hotfix |
| `superpowers:requesting-code-review` + `/code-review` | End of WP6, WP7 | Extension SDK interfaces and importers review |
| `superpowers:using-git-worktrees` | WP1–WP11 | `feature/P23-beta-ops`, `feature/P23-extension-sdk`, `feature/P23-importers` |

---

## Phase-specific skills

### WP1 — Go-live readiness

**`sdlc-meta:sdlc-user-deploy`**
- Tasks: P23-WP1-T1, P23-WP1-T2
- Produce: `docs/ops/GO-LIVE-CHECKLIST.md`
- Invocation: Use this skill to structure the deployment-readiness checklist in
  the format: precondition check → gate status → fallback plan (if gate fails
  at launch time). The checklist must be binary (pass/fail) for each item so
  the go-live sign-off session is efficient.

---

### WP2 — SLO monitoring

**`devops-cloud:reliability-engineering`**
- Tasks: P23-WP2-T1 through P23-WP2-T4
- Produce: `docs/ops/SLO-DEFINITIONS.md`, `SloAggregator`, `integrity_check`
  on startup, SLO dashboard.
- Invocation: Use this skill to design the error-budget policy with a specific
  trigger threshold (50% budget burn = feature freeze) and to calculate the
  error budget for each SLO from the measurement window and threshold. Provide
  the mathematical basis: e.g., 99.5% crash-free over 7 days = 0.5% error rate
  = max 30.2 minutes of crash-attributed session time per week.

**`devops-cloud:observability-monitoring`**
- Tasks: P23-WP2-T2
- Produce: `SloAggregator` implementation.
- Invocation: Use this skill to design the local-only telemetry aggregation:
  how to compute a 7-day rolling rate from a rotating local log without any
  server-side infrastructure; how to handle the case where opt-in telemetry is
  off (graceful degradation to zero data points, not an error).

---

### WP3 — Incident response

**`devops-cloud:reliability-engineering`** (continued)
- Tasks: P23-WP3-T1 through P23-WP3-T7
- Produce: All four runbooks, SEV-tier definitions, incident log template.
- Invocation: Use this skill to apply the detect → triage → contain →
  eradicate → recover → review incident lifecycle to each runbook scenario;
  verify that each runbook step is actionable by a single person with the
  stated tools and access.

**`ai:ai-incident-response`**
- Tasks: P23-WP3-T2, P23-WP3-T3
- Produce: The signing-key compromise and malicious-update runbooks.
- Invocation: Use this skill specifically for the AI/signing-key incident
  scenarios: the Anthropic/OpenAI-side threat (compromised API key exposed in
  the codebase) is a separate runbook concern from the Velopack signing-key
  compromise; this skill provides the template for both and the communication
  strategy (GitHub Security Advisory, user notification).

---

### WP4–WP5 — Beta promotion and soak

**`sdlc-meta:sdlc-post-deployment`**
- Tasks: P23-WP4-T1..T2, P23-WP5-T1..T3
- Produce: Beta promotion commit; GitHub Release announcement; beta soak report.
- Invocation: Use this skill to structure the post-deployment monitoring
  checklist (first-hour check, first-day check, end-of-week check) and the
  soak-exit decision framework.

**`product-business:product-led-growth`**
- Tasks: P23-WP5-T2
- Produce: Beta feedback triage template for GitHub Issues.
- Invocation: Use this skill to design the `beta-feedback` issue template:
  what information to capture (version, OS, library size, steps to reproduce,
  expected vs actual), how to tag and route issues (bug vs feature vs
  performance vs docs), and how to close-with-reference for out-of-scope
  requests using the V1/V2 roadmap.

---

### WP6 — Extension SDK

**`architecture:system-architecture-design`**
- Tasks: P23-WP6-T1 through P23-WP6-T6
- Produce: `OgmaLibrary.Extensions.Sdk` project; all interfaces; `ExtensionLoader`;
  architecture tests.
- Invocation: Use this skill to design the `AssemblyLoadContext` isolation
  model: how to prevent extension assemblies from loading conflicting versions
  of shared dependencies (type-forwarding vs per-context loading); how to
  design the extension DI container boundary so the host's internal services
  are not visible to extensions.

**`sdlc-meta:mcp-builder`**
- Tasks: P23-WP8-T1, P23-WP8-T2
- Produce: `IMcpExtension` interface; `McpListenerScaffold`.
- Invocation: Use this skill to ensure the MCP protocol binding is correct
  (MCP tool definition format, call/result schema), that the loopback listener
  does not conflict with other local services (configurable port, default
  not well-known), and that the extension surface is consistent with the
  MCP specification as of .NET 10 / 2026.

---

### WP7 — Importers

**`sdlc-meta:advanced-testing-strategy`**
- Tasks: P23-WP7-T5
- Produce: `OgmaLibrary.Tests.Importers` with golden-corpus import fixtures
  and edge-case tests.
- Invocation: Use this skill to define the test oracle for each importer:
  what makes a `BookImportRecord` "correct" (all fields present, no data
  truncated, author names in the right order, ISBN validated), and what
  constitutes a handled edge case vs an unhandled failure.

---

### WP9 — Developer docs

**`documentation-generation:api-documenter`** + **`documentation-generation:reference-builder`**
- Tasks: P23-WP9-T1, P23-WP9-T2
- Produce: DocFX configuration; API reference HTML; GitHub Pages workflow.
- Invocation: Use `api-documenter` to configure DocFX for the Extension SDK
  project (XML doc → HTML → GitHub Pages); use `reference-builder` to generate
  the first-pass API reference markdown that the team reviews for accuracy and
  completeness before publishing.

**`documentation-generation:tutorial-engineer`**
- Tasks: P23-WP9-T2 through P23-WP9-T5
- Produce: Getting-started tutorial; importer documentation; MCP extension guide.
- Invocation: Use this skill to structure each tutorial in the "problem →
  concept → step-by-step code → verify → next steps" format; ensure the
  getting-started tutorial has a 30-minute completion time target (estimated
  by line-count and code complexity); use the importer data-mapping tables as
  the canonical reference for the Zotero/Calibre/Goodreads guides.

---

### WP10 — Open-source release readiness

**`documentation-generation:docs-architect`**
- Tasks: P23-WP10-T3 (`/init` for CLAUDE.md)
- Produce: Updated `CLAUDE.md`.
- Invocation: Run `/init` to scan the current project state and generate an
  up-to-date `CLAUDE.md`; review the output against the actual project structure;
  commit any changes.

---

### WP11 — V1/V2 roadmap

**`product-business:product-strategy-vision`**
- Tasks: P23-WP11-T1
- Produce: `docs/roadmap/V1-V2-ROADMAP.md`
- Invocation: Use this skill to frame the V1/V2 roadmap in terms of user-facing
  value ("V1 brings the researcher persona the full semantic + hybrid search
  they need"), not just feature IDs; this is also a community-facing document,
  so the framing should be accessible to a potential open-source contributor
  who is deciding whether to invest in the project.

---

## Slash commands

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review` (high effort) | End of WP6 | Extension SDK interface review — this is a public API commitment |
| `/code-review` (medium effort) | End of WP7, WP9 | Importers and developer docs review |
| `/security-review` | WP3, WP6-T5, WP8-T2 | Runbook review; architecture tests for extension isolation; MCP listener threat model |
| `/verify` | WP4-T1, WP6-T5, WP7-T5 | Confirm beta feed is live; architecture tests pass; importer tests pass |
| `/run` | WP7-T6, WP8-T3 | Launch app; test importer UI flow; confirm MCP listener toggle in Settings |
| `/init` | WP10-T3 | Update CLAUDE.md |
| `/loop 1d` | WP5 (beta soak) | Daily SLO check loop during the 14-day soak period |
| `superpowers:finishing-a-development-branch` | Phase gate | Decide merge/PR strategy; prepare the stable release branch |
