# Phase 00 — Skills & Slash Commands

> Phase-scoped invocation guide. For the bird's-eye map see
> `docs/plans/grand-plan/SKILLS-INDEX.md`. For every skill named here, use
> it for the specific task listed — not decoratively.

---

## Always-on (apply throughout this phase)

| Skill / command | When to invoke | Expected artifact |
| --- | --- | --- |
| `superpowers:writing-plans` | Before starting WP1: turn tasks.md into a sequenced execution plan | An ordered plan with time blocks for the 2-week window |
| `superpowers:executing-plans` | After the plan is written, to drive WP1–WP6 | Stepwise execution of each task with checkpoints |
| `superpowers:verification-before-completion` | At the end of each WP and before Phase 00 close | Checklist run confirming every DoD item is green before marking done |
| `superpowers:requesting-code-review` + `/code-review` | At WP5 completion (governance scripts and templates) | Code review of commit-msg hook, PR template, and CIA workflow docs |
| `documentation-generation:changelog-automation` | When committing the ADR ratifications and governance files | A `CHANGELOG.md` entry for the Phase 00 batch |

---

## WP1 — PRD open questions

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:project-requirements` | P00-WP1-T1..T8 | Structure each OQ answer using the skill's decision brief template: context → options → decision → rationale → consequences → sign-off block |
| `sdlc-meta:spec-architect` | P00-WP1-T9 (compile decisions.md) | The decisions.md OQ section following the spec-architect's structured document format; traceable to PRD §10 IDs |
| `product-business:product-strategy-vision` | P00-WP1-T1/T5/T6/T7/T8 | Validate that each OQ answer is consistent with the four product promise nouns (private, durable, beautiful, command) and the 7 PRD principles |

---

## WP2 — SRS context gaps

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:spec-architect` | P00-WP2-T1..T9 | Structured CON-N entries in decisions.md; each entry has: gap ID, question, assigned value, owner, date, and any downstream impact |
| `security:dpia-generator` | P00-WP2-T7 (CON-7 jurisdictions) | A preliminary DPIA scope document identifying the off-device features, the applicable laws (Uganda DPPA 2019, GDPR), and the legal basis per feature class. This is Phase 19's DPIA starting point. |
| `security:uganda-dppa-compliance` | P00-WP2-T7 | A checklist of Uganda DPPA 2019 requirements applicable to Ogma Library's data model (user identity, AI queries, annotations, reading progress). Flag the classroom/minors track for elevated scrutiny in Phase 19. |
| `sdlc-meta:capability-matrix` | P00-WP2-T4 (command-palette set) | A capability matrix mapping each command-palette command to the FR/NFR it implements and the bounded context it invokes; used as Phase 03 scope boundary |
| `product-business:premium-product-positioning` | P00-WP2-T1 (reference hardware) | Validate that the reference hardware spec represents the target buyer (not just the developer's machine); position the product for mid-range consumer hardware, not developer workstations |

---

## WP3 — ADR ratification

| Skill | Task | What to produce |
| --- | --- | --- |
| `documentation-generation:architecture-decision-records` | P00-WP3-T1..T10 | Updated ADR files (ADR-0001..ADR-0009 status = Accepted, ADR-0010 status = Proposed) in `docs/adrs/` following the ADR template: Title / Status / Context / Decision / Consequences. Each file must include the ratification date and owner name. |

### Concrete invocation for ADR-0010 (LAN Host mode)

Invoke `documentation-generation:architecture-decision-records` with these
inputs for ADR-0010:

- **Title:** Opt-in Library Host mode (CI-2 amendment)
- **Context:** SRS CI-2 states "the application opens no inbound network
  listener." The classroom product vision requires an opt-in server surface on
  the host computer. See `LAN-CLASSROOM-ARCHITECTURE.md §1`.
- **Decision:** Library Host mode is a deliberate, opt-in, LAN-bounded exception
  to CI-2. The default single-user install is unaffected. Host mode requires
  explicit admin activation, runs its own threat model, and is gated on the
  Phase 01 LAN transport spike.
- **Consequences:** New ADRs 0010-0012 govern the classroom track; Phase 01 LAN
  spike validates the transport choice; DPIA updated in Phase 19 for the
  classroom inbound surface.

---

## WP4 — Open-source readiness

| Skill | Task | What to produce |
| --- | --- | --- |
| `documentation-generation:docs-architect` | P00-WP4-T2 (CONTRIBUTING.md) | A CONTRIBUTING.md that covers: repo structure, Conventional Commits format, branch naming, PR process, test-running instructions, CIA checklist, and the open-source readiness note from SOURCE-SUMMARY §L.7 |
| `sdlc-meta:git-collaboration-workflow` | P00-WP4-T2 / P00-WP5-T1..T4 | The branch strategy and commit-message hook consistent with the world-class engineering standard; verify the hook script handles edge cases (merge commits, rebase, amend) |

---

## WP5 — Governance setup

| Skill | Task | What to produce |
| --- | --- | --- |
| `sdlc-meta:git-collaboration-workflow` | P00-WP5-T1 (commit-msg hook) | A `.github/hooks/commit-msg` shell script and a `scripts/Install-Hooks.ps1` PowerShell script. The shell script uses a regex matching the Conventional Commits 1.0 spec: `^(feat|fix|chore|docs|test|refactor|perf|ci|build)(\(.+\))?(!)?: .{1,100}`. Test with at least two positive and two negative examples committed in the Phase 00 branch. |
| `sdlc-meta:sdlc-planning` | P00-WP5-T2/T3 (branch strategy + CIA workflow) | Branch-strategy and CIA-workflow docs that a new contributor can follow without asking for help; includes worked examples for a typical feature PR and a requirement-change PR |

---

## Slash commands in this phase

| Command | When | Purpose |
| --- | --- | --- |
| `/code-review --effort medium` | After WP5 is complete (governance scripts) | Review the commit-msg hook script and PR template for correctness and edge cases |
| `/verify` | After P00-WP5-T5 (hybrid gate) | Confirm `python -m engine validate Ogma-Library` exits 0 in the actual repo environment |
| `/init` | At phase close | Update or create `CLAUDE.md` at the repo root with the Phase 00 governance decisions so future Claude sessions inherit the context |

---

## Notes on skills NOT used in Phase 00

- `avalonia-desktop-development` / `docs/plans/grand-plan/_reference/AVALONIA-STANDARDS.md` —
  no UI work in this phase; first used in Phase 03.
- `frontend-design:frontend-design` and `frontend-ux:*` — no design work;
  first used in Phase 03.
- `security-scanning:*` — no code to scan; security work starts in Phase 02
  (architecture tests) and Phase 19 (full hardening).
- `superpowers:test-driven-development` — no production code in this phase;
  the commit-msg hook test in P00-WP5-T1 is a manual test, not a TDD cycle.
