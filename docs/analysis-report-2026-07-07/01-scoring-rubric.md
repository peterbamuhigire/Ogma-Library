# Scoring Rubric and Audit Checklist

## Method

The audit instrument was built from every extracted document in `docs/references`, plus the local skill-engine guidance listed below. The score is weighted because a desktop product that cannot restore, ship, or meet safety gates is not rescued by isolated implementation depth.

## Skill Guidance Consulted

| Skill / engine | File read | Applied audit use |
| --- | --- | --- |
| SRS engine router | `C:/wamp64/www/srs-skills/CLAUDE.md` | Requirements traceability and anti-slop planning discipline |
| SRS feature decomposition | `C:/wamp64/www/srs-skills/03-planning/feature-decomposition/SKILL.md` | Phase decomposition and dependency ordering |
| SRS technical specification | `C:/wamp64/www/srs-skills/02-requirements/technical-specification/SKILL.md` | Architecture and module-contract checks |
| SRS test plan | `C:/wamp64/www/srs-skills/04-testing/test-plan/SKILL.md` | Verification gates and acceptance criteria |
| SRS anti-ai-slop | `C:/wamp64/www/srs-skills/06-quality/anti-ai-slop/SKILL.md` | Specificity, evidence, and measurable claims |
| SRS audit report | `C:/wamp64/www/srs-skills/07-analysis/audit-report/SKILL.md` | Findings format and severity discipline |
| Evidence pack builder | `C:/wamp64/www/srs-skills/07-analysis/evidence-pack-builder/SKILL.md` | Evidence collection and reproducibility |
| Traceability matrix | `C:/wamp64/www/srs-skills/07-analysis/traceability-matrix/SKILL.md` | Finding-to-phase traceability |
| implementation-status-auditor | `C:/Users/Peter/.agents/skills/implementation-status-auditor/SKILL.md` | Distinguishing implemented, partial, and blocked states |
| advanced-testing-strategy | `C:/Users/Peter/.agents/skills/advanced-testing-strategy/SKILL.md` | Test pyramid and regression gates |
| doc-architect | `C:/Users/Peter/.agents/skills/doc-architect/SKILL.md` | Documentation structure and completeness |
| design-audit | `C:/Users/Peter/.agents/skills/design-audit/SKILL.md` | UI/UX scoring and visual-quality defects |
| practical-ui-design | `C:/Users/Peter/.agents/skills/practical-ui-design/SKILL.md` | Pragmatic application UI checks |
| premium-ui-ux-design | `C:/Users/Peter/.agents/skills/premium-ui-ux-design/SKILL.md` | Premium polish, typography, and release-gate quality |
| system-architecture-design | `C:/Users/Peter/.agents/skills/system-architecture-design/SKILL.md` | Layering, modularity, and operational architecture |
| database-design-engineering | `C:/Users/Peter/.agents/skills/database-design-engineering/SKILL.md` | EF/SQLite schema, migration, integrity checks |
| web-app-security-audit | `C:/Users/Peter/.agents/skills/web-app-security-audit/SKILL.md` | LAN host, local web surface, and API attack surface review |
| code-safety-scanner | `C:/Users/Peter/.agents/skills/code-safety-scanner/SKILL.md` | Dependency, secret, and unsafe-code triage |
| deployment-release-engineering | `C:/Users/Peter/.agents/skills/deployment-release-engineering/SKILL.md` | Packaging, signing, rollout, rollback, and release evidence |

## Checkable Audit Rules

### Architecture and Naming

- Modules must preserve Domain/Application/Infrastructure/App/Workers separation.
- HTTP, filesystem, OS credential, PDF engine, and provider dependencies must live behind adapters.
- ADRs that govern runtime, data, AI, WebView, and release choices must be ratified before release.
- V2 scaffolds must not be mistaken for complete user workflows.
- Public surfaces must trace to SRS/PRD requirements and phase evidence.

### Security and Privacy

- NuGet advisories are release blockers when warnings-as-errors is enabled.
- Do not suppress or weaken `TreatWarningsAsErrors` to pass builds.
- LAN host mode must have explicit threat-model controls, token lifecycle, TLS/update trust, rate limiting, and audit evidence.
- Untrusted PDFs must execute in an isolated worker boundary with filesystem/network/process restrictions.
- AI provider calls require consent, payload preview, no-training headers where available, audit logging, and erasure paths.
- Minor/student data requires DPIA jurisdiction decisions and role-scoped enforcement before pilot.

### Data Layer

- SQLite migrations must be idempotent, backed up before apply, and tested for restore-on-failure.
- Schema repair code must be documented and bounded, not used as a substitute for migration correctness.
- Tokens and credentials must be hashed/encrypted at rest; comments and schema names must not imply weaker behavior.
- FTS/semantic index tables must be integrity-checked and rebuildable.

### UI, Typography, and Accessibility

- UI must be first-screen working software, not a landing or placeholder shell.
- Operational desktop workflows must be dense, quiet, scannable, and task-focused.
- Icons must be real or explicitly blocked from release when placeholder assets remain.
- Controls must use familiar symbols/icons where appropriate, with accessible names.
- Text must not overlap; font sizes must be stable, not viewport-scaled.
- WCAG 2.2 AA requires keyboard order, focus visibility, screen reader evidence, contrast, and localization checks.

### Testing and Verification

- Canonical restore/build/test must be green without disabling NuGet audit.
- Phase and release gates must include full-suite regression, architecture tests, UI render tests, security tests, and platform evidence.
- Diagnostic test runs with disabled audit are not release evidence.
- Performance gates must be measured on reference hardware and recorded.

### Documentation and Release

- `docs/references` is authoritative and must not disagree with repo state.
- Deployment docs must have matching executable pipeline artifacts.
- Completion reports must include evidence, not aspirations.
- Release requires packaging, signing/notarization, update trust-chain tests, rollback drill, SLOs, and operational runbooks.

## Severity Rules

- Critical: blocks restore/build/test/release, creates data-loss or privacy-breach risk, or exposes unsafe LAN/AI/PDF behavior.
- High: blocks public-beta readiness, major workflow completion, accessibility compliance, or platform support.
- Medium: reduces maintainability, evidence quality, reliability, or user confidence but has a bounded workaround.
- Low: polish, documentation drift, or non-blocking cleanup.
