# Skills-engine routing

The skill engines are git repositories of `SKILL.md` files under `C:\wamp64\www\`. They are **not**
registered with the native Skill tool, so executors must read them directly: each engine's
`CLAUDE.md` first, then `AGENTS.md` or `README.md`, then the skill. "Codex-only" sections are skipped.

On 2026-09-25 every path below was checked for existence (75 of 75 present).

## 1. Engines consulted

| Engine | Router read | Skills found | Role in this plan |
|---|---|---|---|
| `chwezi-dev-engine` | `CLAUDE.md` → `AGENTS.md` | 168 | Audit method, Kaizen, testing, .NET/Avalonia, architecture, security, AI, reliability, release |
| `design-system-skills` | `CLAUDE.md` → `doctrine/design-doctrine.md` | 102 | Cross-cutting: UI audit, tokens, typography, states, accessibility, ship gate |
| `srs-skills` | `CLAUDE.md` | 160 | Traceability, acceptance criteria, Definition of Done, test documentation, user docs, AI SLOs |
| `windows-admin-engine-skills` | `CLAUDE.md` → `AGENTS.md` | 21 | Windows desktop E2E through UI Automation, security posture, fleet rollout, dev workstation |
| `digital-research-engine` | `CLAUDE.md` | 61 | Currentness gate, source evaluation and verification |
| `chwezi-engine-agents` | `CLAUDE.md` | 3 | Engine routing and validation |
| `chwezi-accounting-doctrine` | — | — | Not engaged (no finance work) |

## 2. Doctrine that binds every phase

1. **65-cap baseline, 95 target.** The first audit publishes `min(raw, 65)`. 95 is a target, not
   a claim. Nothing is scored 70 or above without executable or rendered proof.
2. **NOT ASSESSED is never a pass.** Every unavailable check has an owner and a consequence.
   Safety, privacy, security and release blockers stay blockers whatever the score.
3. **Evidence classes stay separate.** HEURISTIC, MEASURED and NOT_ASSESSED are kept apart. Only
   MEASURED may say "passes". A green unit test is not product proof.
4. **Currentness gate.** At the start of each cycle and each consuming phase, time-sensitive claims
   go through the Digital Research gate ([00-currentness-register.md](00-currentness-register.md)).
5. **Action row fields.** Every action row has: gap, root cause, change, hypothesis, owner,
   measure, evidence, risk, rollback, acceptance and re-audit date.
6. **PDCA in small reversible slices.** Standardise only after acceptance, and re-measure with the
   same evidence class.
7. **Never close on prose.** An item is not "implemented" because of a commit, not "verified"
   because of an unrun test, and not "released" because of a merge. This follows the verified
   triage state machine: reported → reproduced → scoped → accepted → planned → implemented →
   verified → released → learned.
8. **Design hard rules.** State the typeface and palette intent before producing UI. No banned
   fonts (Inter, Geist, Roboto, Open Sans, Lato, Arial, system stacks, Space Grotesk, Instrument
   Serif, Poppins, Montserrat, Nunito). Pair display with body. Minimum text 12 px; scale ratio
   ≥ 1.25. Ogma's Spectral, Public Sans and JetBrains Mono comply.
9. **Book-content ban.** Plans and evidence may name books but must never store their content.

## 3. Phase → skills

All paths are relative to `C:\wamp64\www\`. The abbreviations below are used in the table.

| Prefix | Expands to |
|---|---|
| `dev/` | `chwezi-dev-engine/skills/` |
| `des/` | `design-system-skills/skills/` |
| `srs/` | `srs-skills/` |
| `win/` | `windows-admin-engine-skills/skills/` |
| `dr/` | `digital-research-engine/skills/` |

These skills apply to **every** phase and are not repeated per row:

- `dev/sdlc-meta/kaizen-improvement-system`
- `dev/execution-plan-scripts` (prompt anatomy)
- `dev/sdlc-meta/world-class-engineering` (gates and the Definition-of-Done pack)
- `srs/07-agile-artifacts/02-definition-of-done`
- the currentness gate
- for UI phases, `design-system-skills/governance/design-quality-gate.md`

| Phase | Skills (read `SKILL.md` in each) |
|---|---|
| 00 Ground truth and repo recovery | `dev/sdlc-meta/implementation-status-auditor`; `dev/languages/csharp-dotnet-development`; `srs/09-governance-compliance/01-traceability-matrix`; `win/virtualization-containers-and-development/windows-development-workstation`; `dev/devops-cloud/cicd-pipelines` |
| 01 Real-window harness | `win/virtualization-containers-and-development/windows-desktop-e2e-testing`; `dev/sdlc-meta/advanced-testing-strategy`; `dev/frontend-ux/avalonia-desktop-development`; `srs/05-testing-documentation/01-test-strategy`; `srs/05-testing-documentation/02-test-plan`; `srs/02-requirements-engineering/agile/02-acceptance-criteria` |
| 02 Crash safety, logging, threading | `dev/languages/csharp-dotnet-development`; `dev/devops-cloud/observability-monitoring`; `dev/devops-cloud/reliability-engineering`; `dev/sdlc-meta/systematic-bug-diagnosis`; `dev/frontend-ux/avalonia-desktop-development` |
| 03 Shell emergency fixes | `des/00-cross-cutting-ops-qa-a11y/click-path-audit`; `des/14-conversion-and-web-page-patterns/empty-error-and-loading-states`; `des/14-conversion-and-web-page-patterns/onboarding-and-first-run-design`; `dev/frontend-ux/avalonia-desktop-development` |
| 04 Reader engine stability | `dev/sdlc-meta/systematic-bug-diagnosis`; `dev/devops-cloud/reliability-engineering`; `dev/languages/csharp-dotnet-development`; `dev/security/vibe-security-skill` (isolation must not regress) |
| 05 Roots, scanning, file validity | `dev/backend-databases/database-design-engineering`; `dev/backend-databases/database-reliability`; `dev/devops-cloud/reliability-engineering`; `dev/architecture/system-architecture-design` (ADR for D-03/D-04); `des/14-conversion-and-web-page-patterns/empty-error-and-loading-states` |
| 06 Pipeline, jobs, identity | `dev/devops-cloud/reliability-engineering`; `dev/backend-databases/database-design-engineering`; `des/12-data-viz-and-dashboards/dashboard-and-data-product-design`; `dev/sdlc-meta/systematic-bug-diagnosis` |
| 07 Navigation and IA | `des/04-web-and-ui-design/webapp-gui-design`; `des/05-ux-process-research-and-psychology/heuristic-evaluation-and-design-critique`; `des/00-cross-cutting-ops-qa-a11y/ux-remediation-and-redesign`; `des/00-cross-cutting-ops-qa-a11y/click-path-audit`; `dev/frontend-ux/avalonia-desktop-development` |
| 08 Settings and capability centre | `des/04-web-and-ui-design/webapp-gui-design`; `des/10-content-design-and-ux-writing/error-empty-and-system-messaging`; `dev/languages/csharp-dotnet-development` (validated options); `dev/security/vibe-security-skill` (secrets plan) |
| 09 Design system and identity | `des/09-design-systems-tokens-and-theming/design-tokens-and-naming`; `des/04-web-and-ui-design/component-states-and-interaction-fidelity`; `des/11-imagery-illustration-and-art-direction/iconography-system-design`; `des/01-typography-and-fonts/ai-slop-typography-audit`; `des/00-cross-cutting-ops-qa-a11y/visual-product-slop-audit`; `des/00-cross-cutting-ops-qa-a11y/design-engine-and-product-improvement` |
| 10 Catalogue experience | `des/04-web-and-ui-design/webapp-gui-design`; `des/12-data-viz-and-dashboards/dashboard-and-data-product-design` (health dashboard); `dev/frontend-ux/avalonia-desktop-development` (virtualisation); `des/14-conversion-and-web-page-patterns/empty-error-and-loading-states` |
| 11 Detail inspector | `des/04-web-and-ui-design/webapp-gui-design`; `des/10-content-design-and-ux-writing/error-empty-and-system-messaging`; `des/04-web-and-ui-design/component-states-and-interaction-fidelity` |
| 12 Reader experience | `des/04-web-and-ui-design/webapp-gui-design`; `dev/frontend-ux/avalonia-desktop-development`; `des/00-cross-cutting-ops-qa-a11y/accessibility-wcag-2-2-compliance` (keyboard, full screen) |
| 13 Unified search | `dev/ai/ai-rag-patterns` (evaluation oracle, hybrid ranking); `dev/backend-databases/database-design-engineering`; `des/14-conversion-and-web-page-patterns/empty-error-and-loading-states`; `srs/02-requirements-engineering/agile/02-acceptance-criteria` |
| 14 Local semantic capability | `dev/ai/ai-rag-patterns`; `dev/ai/ai-evaluation`; `dev/languages/csharp-dotnet-development` (embeddings in .NET); `dr/source-evaluation` (model licence) |
| 15 AI gateway and Privacy Center | `dev/ai/ai-model-gateway`; `dev/ai/ai-llm-integration`; `dev/ai/ai-security`; `dev/security/vibe-security-skill`; `srs/09-governance-compliance/16-ai-data-flow-and-dpia`; `des/04-web-and-ui-design/ai-output-design`; `dr/source-verification` (model IDs) |
| 16 Advisor and reading plans | `dev/ai/ai-rag-patterns`; `dev/ai/ai-evaluation`; `dev/ai/ai-observability-and-debugging`; `dev/ai/ai-security` (injection from PDF text); `srs/05-testing-documentation/04-ai-eval-harness-spec`; `srs/06-deployment-operations/10-ai-hallucination-slo-doc`; `des/04-web-and-ui-design/ai-output-design` |
| 17 OCR and extraction | `dev/sdlc-meta/advanced-testing-strategy` (flake policy); `dev/devops-cloud/reliability-engineering`; `dev/sdlc-meta/systematic-bug-diagnosis` |
| 18 3D bookshelf | `dev/frontend-ux/avalonia-desktop-development` (WebView fallback); `dev/frontend-ux/frontend-performance`; `des/00-cross-cutting-ops-qa-a11y/accessibility-wcag-2-2-compliance` (drag alternative, reduced motion) |
| 19 Classroom Host and admin | `dev/architecture/distributed-systems-patterns`; `dev/security/vibe-security-skill` (authorisation matrix); `dev/security/dpia-generator`; `win/networking-and-remote-management/windows-network-admin`; `win/policy-security-and-compliance/windows-security-analysis` |
| 20 Classroom client and sync | `dev/architecture/distributed-systems-patterns`; `dev/mobile-cross/pwa-offline-first` (transferable offline rules); `srs/02-requirements-engineering/hybrid/hybrid-synchronization`; `dev/security/dpia-generator` |
| 21 Accessibility | `des/00-cross-cutting-ops-qa-a11y/accessibility-wcag-2-2-compliance`; `dev/sdlc-meta/advanced-testing-strategy` (manual assistive-technology passes); `win/virtualization-containers-and-development/windows-desktop-e2e-testing` |
| 22 Localisation and copy | `des/00-cross-cutting-ops-qa-a11y/internationalization-and-rtl-design`; `des/10-content-design-and-ux-writing/error-empty-and-system-messaging` |
| 23 Security and privacy | `dev/security/vibe-security-skill`; `dev/security/code-safety-scanner`; `dev/security/dpia-generator`; `dev/ai/ai-security`; `dev/backend-databases/database-reliability`; `win/policy-security-and-compliance/windows-security-analysis` |
| 24 Performance and reliability | `dev/devops-cloud/reliability-engineering`; `dev/devops-cloud/observability-monitoring`; `dev/frontend-ux/frontend-performance`; `dev/backend-databases/database-reliability` |
| 25 Extensibility and imports | `dev/architecture/system-architecture-design` (ADR and ports); `dev/security/vibe-security-skill` (plugin trust boundary) |
| 26 Packaging, installers, signing | `dev/frontend-ux/avalonia-desktop-development` (packaging); `dev/languages/csharp-dotnet-development`; `dev/devops-cloud/deployment-release-engineering`; `dev/devops-cloud/cicd-pipelines`; `dev/architecture/validation-contract`; `win/fleet-hybrid-and-management-planes/windows-fleet-management`; `srs/06-deployment-operations/05-go-live-readiness` |
| 27 macOS parity | `dev/frontend-ux/avalonia-desktop-development`; `dev/architecture/validation-contract`; `des/00-cross-cutting-ops-qa-a11y/accessibility-wcag-2-2-compliance` (VoiceOver) |
| 28 Docs, usability, re-audit | `dev/sdlc-meta/doc-architect`; `dev/sdlc-meta/sdlc-documentation`; `srs/08-end-user-documentation/01-user-manual`; `srs/08-end-user-documentation/02-installation-guide`; `srs/08-end-user-documentation/04-release-notes`; `des/05-ux-process-research-and-psychology/ux-research-and-usability-testing`; `des/00-cross-cutting-ops-qa-a11y/product-design-audit`; `des/00-cross-cutting-ops-qa-a11y/design-qa-and-pre-launch-review`; `srs/09-governance-compliance/31-kaizen-engine-and-product-improvement`; `dev/sdlc-meta/implementation-status-auditor` |

Supporting skills used by several phases:

- `srs/09-governance-compliance/05-architecture-decision-records` (for D-03, D-04, D-05, D-09, D-10)
- `win/meta/kaizen-engine-and-product-improvement` (R0–R5 risk classes for installer and fleet work)
- `chwezi-engine-agents/adapters/claude-code/skills/skills-engine-agents` (engine validation)

## 4. Gaps where no engine skill exists

| Gap | Consequence | How the plan compensates |
|---|---|---|
| **Avalonia real-window test automation.** Avalonia is absent from the framework table in `windows-desktop-e2e-testing`, and Avalonia's UIA provider quality is unverified. | Locator reliability is uncertain. This audit saw the folder picker hide its commit button from UIA, and ListItems expose record dumps. | Phase 01 verifies the provider with Accessibility Insights and sets AutomationIds on every control. It keeps a headless `Avalonia.Headless` layer for logic and uses a UIA layer (the prototype `Invoke-OgmaUia.ps1` promoted to a maintained harness) plus pixel visibility checks for real-window acceptance. Native dialogs are driven through a test-only path-injection seam instead of the OS picker. |
| **Code signing, MSIX, Windows installer and Apple notarisation.** Only the Avalonia and C# skills touch packaging. | Rules are time-sensitive and unverified (CUR-19, CUR-20). | Phase 26 adds T1 currentness records before implementing, uses `deployment-release-engineering` and the `validation-contract` release bundle, and keeps signing NOT ASSESSED until certificates exist (D-08). |
| **Local-first desktop, LAN Host/Client sync.** | No canonical doctrine for mDNS, TOFU or offline cache on a school LAN. | Phases 19 and 20 borrow from `distributed-systems-patterns` (idempotency, partition, reconciliation) and `pwa-offline-first` (outbox, real reachability, restart-reconnect-dedupe tests), and require a two-machine measured journey. |
| **SQLite FTS5 and search relevance.** | No relevance oracle guidance specific to FTS. | Phase 13 applies the `ai-rag-patterns` evaluation method to ordinary search: a fixed query set with expected results over the synthetic corpus, measuring precision@k and recall and treating them as regression gates. |
| **PDF engine and worker-process resource policy.** | Nothing specific. | Phase 04 applies `reliability-engineering` (timeouts, classification, degrade) and `systematic-bug-diagnosis` to K30, with a 1,000-page-turn soak as the oracle. |
| **Desktop observability** (no server telemetry) | Engine skills are server-oriented. | Phase 02 translates `observability-monitoring` into a local structured log, crash record and support bundle with redaction. |
