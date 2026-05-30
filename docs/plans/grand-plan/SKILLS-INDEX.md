# Skills Index — which skill to use, where, and why

Ogma Library is built with the skills engine at `~/.claude/skills`. This index
maps **skills** (catalog skills under `skills/<category>/<name>/SKILL.md`) and
**slash‑command / plugin skills** (e.g. `superpowers:*`, `frontend-design`,
`security-scanning:*`, `documentation-generation:*`, `/code-review`,
`/security-review`, `/run`, `/verify`) to the 24 phases.

Each phase's `skills.md` restates the relevant rows with concrete,
task‑level invocation guidance. This index is the bird's‑eye map.

> Convention: a skill is named only where it informs a real task and should
> produce a real artifact. Listing skills decoratively is discouraged
> (`CONVENTIONS.md`).

---

## Always‑on (every phase)

| Skill / command | Use |
| --- | --- |
| `superpowers:brainstorming` | Before any creative/design work in a phase, explore intent & options. |
| `superpowers:writing-plans` → `superpowers:executing-plans` / `superpowers:subagent-driven-development` | Turn each phase's `tasks.md` into an executable plan and drive it. |
| `superpowers:test-driven-development` | Write tests before implementation for every feature/bugfix. |
| `superpowers:verification-before-completion` | No "done" claim without running the verification commands. |
| `superpowers:requesting-code-review` + `/code-review` (and `comprehensive-review:code-reviewer`) | Review at the end of each work package / before merge. |
| `superpowers:systematic-debugging` | Any bug/test failure before proposing a fix. |
| `superpowers:using-git-worktrees` | Isolate feature branches per `feature/<ID>` convention. |
| `sdlc-meta:world-class-engineering`, `sdlc-meta:git-collaboration-workflow` | Engineering standards & branch/commit/PR discipline. |
| `language-standards` + `csharp`/`dotnet` standards, `typescript-effective`/`javascript-modern` (3D bridge) | Code style for C# and the Three.js/JS bridge. |
| **`frontend-ux:avalonia-desktop-development`** (+ `_reference/AVALONIA-STANDARDS.md`) | **The** Avalonia/.NET 10 standard for every UI phase: MVVM + compiled bindings, virtualization for the 2,000‑book catalogue, control‑themes/Fluent theming, `avares://` PNG icon bundling, localization, accessibility automation peers, WebView hosting for the 3D shelf, packaging for Win/macOS, headless view‑model testing. |
| `documentation-generation:docs-architect`, `sdlc-meta:doc-architect`, `documentation-generation:changelog-automation` | Keep architecture docs, ADRs, and changelog current — **open‑source readiness**. |

## Part I — Inception & Foundation

| Phase | Primary skills |
| --- | --- |
| **00 Decision Closure** | `sdlc-meta:project-requirements`, `sdlc-meta:spec-architect`, `sdlc-meta:sdlc-planning`, `documentation-generation:architecture-decision-records` (ratify ADR‑0001…0009), `product-business:product-strategy-vision`, `product-business:premium-product-positioning`, `security:dpia-generator` + `security:uganda-dppa-compliance` (jurisdiction/minors gap), `sdlc-meta:capability-matrix`. |
| **01 Risk Spikes** | `architecture:system-architecture-design`, `sdlc-meta:advanced-testing-strategy`, `ai:ai-model-gateway` (gateway spike), `backend-databases:database-internals` + `vector-databases` (FTS5/embeddings spike), `frontend-mobile-development:react-native-architecture`? no — for the WebView/Three.js bridge use `frontend-design` + `typescript-effective`; `devops-cloud:reliability-engineering` for spike rigor; `network-security` (LAN transport spike). |
| **02 Scaffolding** | `architecture:system-architecture-design`, `sdlc-meta:sdlc-design`, `architecture:validation-contract`, `sdlc-meta:custom-sub-agents`, `comprehensive-review:architect-review`, `cicd-pipeline-design`/`cicd-pipelines`/`cicd-devsecops` (CI), `language-standards` (Directory.Build.props, analyzers, .editorconfig), `documentation-generation:docs-architect` (developer guide), `sdlc-meta:e2e-testing` harness. |
| **03 Design System & Icons & i18n** | `frontend-design:frontend-design`, `frontend-ux:premium-ui-ux-design`, `frontend-ux:practical-ui-design`, `frontend-ux:webapp-gui-design`, `frontend-ux:ux-principles-101`, `frontend-ux:interaction-design-patterns`, `frontend-ux:motion-design`, `frontend-ux:design-audit`, `frontend-ux:ux-content-strategy`, `frontend-ux:image-compression` (icon PNG pipeline), `document-skills:theme-factory`/`brand-guidelines` (tokens), `frontend-ux:tailwind-css`/`practical-ui-design` for layout system. **i18n**: `ux-content-strategy` + `content-writing`. |

## Part II — Core Library

| Phase | Primary skills |
| --- | --- |
| **04 Catalogue & Data** | `backend-databases:database-design-engineering`, `database-reliability`, `database-internals`, `sdlc-meta:sdlc-design`; EF Core migrations via `language-standards`; `architecture:validation-contract`. |
| **05 Ingestion & Scanning** | `architecture:system-architecture-design` (worker pipeline), `devops-cloud:reliability-engineering` (resumable/idempotent jobs), `sdlc-meta:advanced-testing-strategy` (per‑file isolation, golden corpus), `python-data-pipelines` patterns (conceptual) for the pipeline stages. |
| **06 Catalogue Browsing** | `frontend-ux:practical-ui-design`, `premium-ui-ux-design`, `interaction-design-patterns`, `frontend-performance` (virtualized lists), `data-visualization` (counts/filters), `react-development`? (only the 3D web surface; views are Avalonia), `design-audit` (icon coherence gate). |
| **07 Metadata & Health** | `backend-databases:database-design-engineering` (provenance), `architecture:api-error-handling`/`api-pagination` (provider clients), `frontend-ux:data-visualization` (health dashboard), `sdlc-meta:advanced-testing-strategy` (write‑back fault injection), `product-business` (quality scoring UX). |
| **08 Reader Core** | `frontend-ux:frontend-performance` (page cache/100 ms budget), `interaction-design-patterns`, `motion-design` (page turns), `practical-ui-design`; native interop rigor via `language-standards`. |
| **09 Annotations & Memory** | `backend-databases:database-reliability` (durable writes), `frontend-ux:interaction-design-patterns`, `practical-ui-design`, `sdlc-meta:advanced-testing-strategy` (annotation durability fault injection). |

## Part III — Intelligence

| Phase | Primary skills |
| --- | --- |
| **10 Search & Indexing** | `backend-databases:database-internals` (FTS5 external‑content), `database-performance`/`postgresql-performance` patterns (conceptual), `architecture:system-architecture-design`, `frontend-performance` (search‑as‑you‑type budget). |
| **11 Semantic & Embeddings** | `backend-databases:vector-databases`, `ai:ai-rag-patterns`/`rag-implementation`, `ai:ai-llm-integration` (embeddings), `python-ml-predictive` (ranking concepts), `sdlc-meta:advanced-testing-strategy` (ranking determinism). |
| **12 AI Gateway & Privacy** | `ai:ai-model-gateway`, `ai:ai-llm-integration`, `ai:ai-cost-and-metering`, `ai:ai-security`, `ai:ai-observability-and-debugging`, `ai:ux-for-ai`/`ai-agent-ux`, `ai:ai-output-design`, `security:dpia-generator`, `claude-api`/`document-skills:claude-api` (Anthropic‑compatible provider + prompt caching), `ai:ai-prompt-engineering`. |
| **13 Advisor & Plans** | `ai:ai-rag-patterns`, `ai:ai-output-design`, `ai:ai-evaluation`/`ai-agent-observability-evaluation` (recommendation eval), `ai:ux-for-ai`, `ai:ai-feature-spec`, `ai:ai-economic-value-engine` (value framing). |

## Part IV — Signature & Power

| Phase | Primary skills |
| --- | --- |
| **14 3D Bookshelf** | `frontend-design:frontend-design`, `typescript-effective`/`typescript-mastery` (Three.js bridge), `frontend-ux:motion-design`, `frontend-performance` (60 FPS), `frontend-ux:design-audit`, `architecture:validation-contract` (typed bridge messages), `frontend-ux:image-compression` (spine textures). |
| **15 OCR, Advanced Reader, Power** | `sdlc-meta:advanced-testing-strategy` (OCR golden corpus), `devops-cloud:reliability-engineering` (batch jobs), `frontend-ux:interaction-design-patterns` (split view), `security:code-safety-scanner` (password handling). |

## Part V — Networked / Classroom

| Phase | Primary skills |
| --- | --- |
| **16 LAN Host** | `architecture:system-architecture-design`, `architecture:realtime-systems`, `architecture:microservices-communication`/`distributed-systems-patterns` (host↔client contracts), `security:network-security`, `devops-cloud:reliability-engineering`, `saas:saas-control-plane-engineering` (concepts), `documentation-generation:architecture-decision-records` (ADR‑0010). |
| **17 Client / Classroom** | `mobile-cross:pwa-offline-first` (offline cache/sync), `security:dual-auth-rbac`/`mobile-rbac` (roles), `architecture:event-driven-architecture` (sync), `frontend-ux:enterprise-ux-process` (multi‑user flows), `saas:saas-tenant-onboarding-automation` (enrollment concepts). |
| **18 School Admin & Managed AI** | `saas:saas-admin-backoffice-tooling`, `saas:saas-entitlements-and-plan-gating`, `saas:saas-rate-limiting-and-quotas`, `ai:ai-entitlements-and-feature-gating`, `ai:ai-cost-and-metering`, `ai:ai-agent-governance-and-limits`, `ai:ai-agent-safety-and-red-team`, `security:uganda-dppa-compliance` + `security:dpia-generator` (minors' data), `frontend-ux:data-visualization` (usage dashboards). |

## Part VI — Hardening & Quality

| Phase | Primary skills |
| --- | --- |
| **19 Security & Compliance** | `security-scanning:security-hardening`, `security-scanning:security-sast`, `security-scanning:sast-configuration`, `security-scanning:stride-analysis-patterns`, `security-scanning:attack-tree-construction`, `security-scanning:threat-mitigation-mapping`, `security-scanning:security-requirement-extraction`, `security:web-app-security-audit`, `security:linux-security-hardening`, `security:dpia-generator`, `/security-review`, `comprehensive-review:security-auditor`. |
| **20 Performance & Reliability** | `full-stack-orchestration:performance-engineer`, `frontend-ux:frontend-performance`, `devops-cloud:reliability-engineering`, `devops-cloud:observability-monitoring`/`observability-platform`, `sdlc-meta:advanced-testing-strategy` (fault injection), `backend-databases:database-reliability`. |
| **21 A11y, Full i18n, QA** | `frontend-ux:design-audit`, `frontend-ux:ux-principles-101` (a11y), `full-stack-orchestration:test-automator`, `sdlc-meta:e2e-testing`, `sdlc-meta:sdlc-testing`, `comprehensive-review:full-review`, `document-skills:webapp-testing`, `ux-content-strategy`/`content-writing` (es/it/de copy), `sdlc-meta:markdown-lint-cleanup`. |

## Part VII — Distribution & Launch

| Phase | Primary skills |
| --- | --- |
| **22 Packaging & Stores** | `devops-cloud:deployment-release-engineering`, `cicd-pipeline-design`/`cicd-pipelines`/`cicd-devsecops`, `mobile-cross:app-store-review` (Mac App Store), `mobile-cross:google-play-store-review`? (n/a) — for Windows Store use store‑submission checklist within `deployment-release-engineering`; `mobile-cross:mobile-custom-icons` (store icon assets), `documentation-generation:changelog-automation`. |
| **23 Beta, Launch, Ops, SDK** | `sdlc-meta:sdlc-user-deploy`, `sdlc-meta:sdlc-post-deployment`, `sdlc-meta:sdlc-maintenance`, `devops-cloud:observability-monitoring`, `devops-cloud:reliability-engineering` (SLOs/runbooks), `ai:ai-incident-response`, `product-business:product-led-growth`/`growth-telemetry-pipeline`, `documentation-generation:api-documenter`/`reference-builder` + `documentation-generation:tutorial-engineer` (**Extension SDK docs**, open‑source), `sdlc-meta:skill-writing`/`mcp-builder` (plugin & MCP extension surface). |

## Slash commands used throughout

`/code-review` (quality + correctness, escalate to `ultra` for cloud review on
big merges) · `/security-review` (security/privacy phases) · `/run` and
`/verify` (drive and confirm the app builds and behaves on Win + macOS) ·
`/simplify` (cleanup) · `/init` (keep `CLAUDE.md` current) · `/loop` &
`/schedule` (recurring checks: nightly benchmarks, beta soak monitoring).
