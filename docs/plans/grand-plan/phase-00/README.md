# Phase 00 — Decision Closure & Project Inception

One sentence: Close every open question, context gap, and governance gap before
a line of production code is written, so the build phases start from a signed,
unambiguous baseline.

---

## 1. Status & metadata

| Field | Value |
| --- | --- |
| **Status** | Not started |
| **Tier** | MVP (all decisions gate the MVP build) |
| **Estimate** | 2 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD Phase 0 (pre-build decisions) |
| **Platforms** | Decision scope: Windows 10+ (WebView2) + macOS 13+ (WKWebView); Linux = bonus, not MVP gate (CON-6 must be confirmed here) |
| **Baseline date** | 2026-05-30 |

---

## 2. Objectives

1. All 8 PRD open questions (OQ-01..OQ-08) are answered with recorded decisions,
   each owner-signed and traceable to an ADR or this phase's decision log.
2. All 8+ SRS context gaps (CON-1..CON-8+) are assigned concrete values or
   formally deferred with a documented rationale and a phase-gate target.
3. ADR-0001 through ADR-0009 are ratified (moved from Proposed to Accepted) by
   the owner; any amendments recorded.
4. Repo governance is in place: Conventional Commits policy, branch strategy,
   Change Impact Analysis (CIA) workflow, and the hybrid validation gate are
   operational.
5. Open-source readiness artifacts exist: LICENSE (chosen and applied),
   CONTRIBUTING.md, CODE_OF_CONDUCT.md, and CLA mechanism are drafted and
   owner-approved.
6. Cross-platform scope is confirmed: Windows 10+ / macOS 13+ parity is a hard
   gate for every subsequent phase; Linux MVP scope is formally decided (CON-6).
7. No phase-blocking ambiguity remains; every subsequent phase has a clear,
   traceable starting contract.

---

## 3. Scope

### In scope

- Answering OQ-01 (.NET 10 vs 8), OQ-02 (PDFium wrapper), OQ-03 (annotation
  write-back), OQ-04 (thumbnail/spine storage), OQ-05 (FTS in MVP), OQ-06 (OCR
  in MVP), OQ-07 (EPUB/CBZ), OQ-08 (cloud sync) — all per PRD §10.
- Assigning values to the 8 SRS context gaps (CON-1..CON-8+): reference
  hardware spec, install minimums, Linux MVP scope, command-palette command set,
  Work/Edition cardinality and merge/split rules, sidecar naming convention per
  class, target-user jurisdictions and Data Protection Acts, provider trust
  weights and field-match scoring for the metadata confidence model, corpus
  licensing provenance.
- Ratifying ADR-0001 through ADR-0009 with owner sign-off; recording any
  amendments required by context-gap answers.
- Drafting ADR-0010 (opt-in Library Host mode / CI-2 amendment) as a
  Proposed ADR ready for Phase 01 LAN spike evidence.
- Establishing repo governance: Conventional Commits (enforced by a commit-msg
  hook), branch strategy (`main` / `develop` / `feature/<ID>` / `release/<ver>`
  / `hotfix/<ver>`), PR template with CIA checklist, and the hybrid validation
  gate (`python -m engine validate Ogma-Library`).
- Open-source readiness: select and apply a LICENSE (MIT or Apache 2.0 — owner
  decision), draft CONTRIBUTING.md, CODE_OF_CONDUCT.md (Contributor Covenant
  2.1), and choose a CLA mechanism (CLA Assistant or DCO).
- Confirming cross-platform scope (Windows 10+ / macOS 13+) and Linux stance.
- Confirming the security constraints baseline: CTRL-OGMA-001..CTRL-OGMA-024
  scope and the CI-2 "no inbound listener" default (amended by ADR-0010).
- A short decision-closure document (`decisions.md` in this folder) capturing
  every answer with its owner sign-off date.

### Explicitly out of scope

- Writing any production C# code (that begins in Phase 02).
- Creating the Visual Studio solution or project files (Phase 02).
- Any UI design or icon procurement (Phase 03).
- Implementing the hybrid validation gate engine (it is assumed to exist;
  this phase only confirms it runs against the repo).
- Writing the full SRS, PRD, HLD, or ADRs from scratch — those reference docs
  are the signed baseline; this phase only closes gaps and ratifies decisions
  already drafted.

---

## 4. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| OQ-01 | MVP | .NET runtime version | ADR-0001 Accepted; decision-log entry |
| OQ-02 | MVP | PDFium wrapper choice | ADR-0004 Accepted (amended after Phase 01 spike) |
| OQ-03 | MVP | Annotation write-back strategy | ADR-0008 Accepted; decision-log entry |
| OQ-04 | MVP | Thumbnail/spine storage | ADR-0005 Accepted; decision-log entry |
| OQ-05 | MVP | FTS in MVP scope | Decision log (defer to V1); ADR-0006 note |
| OQ-06 | MVP | OCR in MVP scope | Decision log (defer to V1) |
| OQ-07 | Post-V1 | EPUB/CBZ scope | Decision log (post-V1) |
| OQ-08 | Post-MVP | Cloud sync | Decision log (exclude MVP/V1; schema-ready) |
| CON-1 | MVP | Reference hardware spec | `decisions.md` §CON-1; NFR-OGMA budgets anchored |
| CON-2 | MVP | Install minimums | `decisions.md` §CON-2 |
| CON-3 | MVP | Linux MVP scope | `decisions.md` §CON-3 (bonus/not gate) |
| CON-4 | MVP | Command-palette command set | `decisions.md` §CON-4; Phase 03 command-palette backlog |
| CON-5 | MVP | Work/Edition cardinality & merge/split rules | `decisions.md` §CON-5; domain model constraint |
| CON-6 | MVP | Sidecar naming convention per class | `decisions.md` §CON-6; ADR-0005 amendment |
| CON-7 | MVP | Target jurisdictions & Data Protection Acts | `decisions.md` §CON-7; DPIA scope |
| CON-8 | MVP | Provider trust weights & field-match scoring | `decisions.md` §CON-8; confidence model spec |
| CON-9 | MVP | Corpus licensing provenance | `decisions.md` §CON-9; golden-corpus fixture manifest |
| ADR-0001 | MVP | .NET 10 LTS ratified | ADR file status = Accepted |
| ADR-0002 | MVP | Avalonia shell ratified | ADR file status = Accepted |
| ADR-0003 | MVP | WebView Three.js 3D (spike-gated) ratified | ADR file status = Accepted |
| ADR-0004 | MVP | PDFium behind adapter (2-wrapper benchmark) | ADR file status = Accepted (amended post-Phase 01) |
| ADR-0005 | MVP | SQLite catalogue + sidecar | ADR file status = Accepted |
| ADR-0006 | MVP | Hybrid search (metadata + FTS5 + embeddings) | ADR file status = Accepted |
| ADR-0007 | MVP | Provider-neutral AI gateway + 4 tiers | ADR file status = Accepted |
| ADR-0008 | MVP | DB-first annotations/metadata, PDF write-back later | ADR file status = Accepted |
| ADR-0009 | MVP | Velopack + MSIX + notarized DMG | ADR file status = Accepted |
| NFR-PROD-012 | MVP | Signed builds + reversible migrations (governance) | Governance docs exist; CI commit-msg hook passes |
| L.7 | MVP | Open-source readiness: LICENSE, CONTRIBUTING, CLA | Files exist and owner-approved |

---

## 5. Dependencies

### Depends on

- Signed baseline reference set: PRD, SRS, HLD, ADR drafts (ADR-0001..0009),
  Test Strategy, Development Standards, Deployment & Ops, DPIA, Risk Register —
  all in `docs/references/`. These are inputs, not outputs of this phase.
- Owner (Peter Bamuhigire) availability for sign-off on each open question and
  each ADR within the 2-week window.
- The hybrid validation gate engine (`python -m engine validate Ogma-Library`)
  being installed and runnable against the repo root.

### Unblocks

- Phase 01: spikes require the ADRs to be ratified so the spikes know which
  decisions they are stress-testing (ADR-0003, ADR-0004, ADR-0006) and the LAN
  transport spike needs ADR-0010 drafted.
- Phase 02: solution scaffolding requires confirmed .NET version (ADR-0001),
  project structure (HLD §F), and open-source governance files.
- Phase 03: design system requires confirmed icon system scope and confirmed
  en/fr i18n mandate (both locked here).
- All subsequent phases: require a clean repo governance baseline (Conventional
  Commits, branch strategy, CI) so PRs can be merged correctly.

---

## 6. Architecture & approach

### Components and bounded contexts touched

Phase 00 touches **no bounded context in code**; it is a governance and decision
phase. The artifacts it produces are:

- `docs/plans/grand-plan/phase-00/decisions.md` — the decision log.
- `docs/adrs/ADR-0001.md` through `docs/adrs/ADR-0009.md` — status changed from
  Proposed to Accepted (with any amendments). `docs/adrs/ADR-0010.md` drafted as
  Proposed.
- `LICENSE` (repo root) — chosen license applied.
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CLA.md` or `.github/CONTRIBUTORS`
  mechanism (repo root or `.github/`).
- `.github/commit-msg` hook or Husky equivalent — Conventional Commits
  enforcement.
- `.github/PULL_REQUEST_TEMPLATE.md` — with Change Impact Analysis checklist.
- `docs/governance/BRANCH-STRATEGY.md` — branch naming and merge rules.
- `docs/governance/CIA-WORKFLOW.md` — Change Impact Analysis process.
- `docs/governance/REFERENCE-HARDWARE.md` — CON-1 answer: the spec for the two
  reference machines (Windows, macOS) used to anchor all NFR-OGMA budgets.

### Cross-platform approach (Windows + macOS)

No code runs in Phase 00. The cross-platform decisions made here are:

- Confirm Windows 10 1903+ (WebView2 available) and macOS 13 Ventura+
  (WKWebView with WebGL2, assessed in Phase 01 spike ADR-0003).
- Confirm Linux is a "bonus" only: not a release gate for MVP, V1, or V2;
  any Linux CI work is best-effort.
- Record the macOS signing and notarization requirements (Apple Developer
  Program membership, entitlements for disk access, notarization via
  `xcrun notarytool`) so Phase 22 is not surprised. Reference ADR-0009.

### Hybrid validation gate

The gate `python -m engine validate Ogma-Library` is confirmed operational.
This phase verifies it runs (exit 0) on the repo root. The gate is a waterfall
phase-class gating mechanism; it blocks Phase-07-class outputs if it fails.
Exact gate behavior is documented in `docs/governance/HYBRID-GATE.md`.

### Governance model

- **Conventional Commits** (`feat`, `fix`, `chore`, `docs`, `test`, `refactor`,
  `perf`, `ci`, `build`; `!` breaking; footer `Closes #NNN`). Enforced by
  a commit-msg hook in `.git/hooks/commit-msg` (checked into `.github/hooks/`
  and installed by a setup script so all contributors get it).
- **Branch strategy:** `main` (signed releases only) → `develop` (integration)
  → `feature/<phase-ID>-<slug>` → `release/<semver>` → `hotfix/<semver>`.
  Fast-forward merges are forbidden on `main`; squash or merge commits on
  `develop` after PR review.
- **CIA checklist:** every PR must answer: (a) which bounded contexts are
  affected, (b) which FR/NFR/ADR IDs are touched, (c) does the change alter a
  baselined requirement (→ requires owner sign-off and ADR amendment), (d) are
  all new strings externalized in en + fr, (e) are new controls iconified +
  accessible.

### Licensing decision (Owner ask #1)

The owner must choose between **MIT** and **Apache 2.0**. Both permit app-store
distribution and commercial use. Apache 2.0 adds a patent grant (recommended
for a product that may incorporate novel AI/search algorithms). This phase
records the choice; the LICENSE file is committed in the same PR.

### Jurisdiction and DPIA scope (CON-7)

The target-user jurisdictions determine which Data Protection Acts apply.
Uganda's Data Protection and Privacy Act 2019 (DPPA) is a candidate (Chwezi
Core Systems is Uganda-based). GDPR applies if any EU data subjects use the
product. This phase uses the `security:dpia-generator` and
`security:uganda-dppa-compliance` skills to document the legal basis per
off-device feature and the rights of data subjects (including minors for the
classroom track, Phases 16-18). The jurisdiction answer is recorded in
`decisions.md` §CON-7 and shapes the DPIA work in Phase 19.

---

## 7. Work breakdown (summary)

| WP | Work package | Estimate |
| --- | --- | --- |
| WP1 | Answer all 8 PRD open questions; record in decisions.md | 2 d |
| WP2 | Assign values to all 8+ SRS context gaps; record in decisions.md | 2 d |
| WP3 | Ratify ADR-0001..ADR-0009; draft ADR-0010 | 1 d |
| WP4 | Apply LICENSE, draft CONTRIBUTING.md, CODE_OF_CONDUCT.md, CLA | 1 d |
| WP5 | Establish repo governance (Conventional Commits hook, branch strategy, CIA, hybrid gate verification) | 1.5 d |
| WP6 | Owner sign-off review session; record sign-offs in decisions.md | 1.5 d |

Detail in `tasks.md`.

---

## 8. Cross-cutting checklist

- [x] **Colorful icons + manifest:** Phase 00 has no UI surface. `icons.md`
  contains a one-line stub. No icon procurement needed.
- [x] **i18n (en/fr strings externalized):** No user-facing strings are produced
  in this phase. The i18n mandate (en/fr MVP) is formally confirmed in
  decisions.md §CON-i18n and becomes a binding gate from Phase 03 onward.
- [x] **Accessibility (keyboard + SR):** No UI produced. The WCAG 2.2 AA
  accessibility mandate is confirmed as a gate in decisions.md.
- [x] **Privacy/egress:** No network calls produced. The single egress
  chokepoint (HLD §F, CTRL-OGMA architecture) is ratified via ADR-0007.
  CON-7 (jurisdictions) and the DPIA scope are answered here.
- [x] **Reversibility:** No destructive data operations in this phase.
  The reversibility principle (principle 4) is confirmed binding on all phases.
- [x] **Performance budgets:** NFR-OGMA-001..009 budgets are anchored to the
  reference hardware values assigned in CON-1. Trend baseline: Phase 02.
- [x] **Bounded-context tests:** Not applicable (no code). Architecture test
  skeletons are specified in Phase 02 based on the 9-context map confirmed here.
- [x] **Documentation:** All decisions documented in decisions.md; ADR files
  updated; CONTRIBUTING.md and developer-facing governance docs drafted.

---

## 9. Definition of Done

### Global DoD (Phase 00 slice)

- [ ] Every OQ (OQ-01..OQ-08) has a recorded answer with an owner sign-off date
  in `decisions.md`.
- [ ] Every SRS context gap (CON-1..CON-9+) has an assigned value or a formal
  deferral recorded in `decisions.md`.
- [ ] ADR-0001..ADR-0009 are in `Accepted` status; ADR-0010 is in `Proposed`
  status; all ADR files are committed to `docs/adrs/`.
- [ ] `LICENSE` is applied at the repo root; file is valid and owner-approved.
- [ ] `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, and the CLA mechanism are
  committed and owner-approved.
- [ ] Conventional Commits hook is in `.github/hooks/commit-msg` and enforced;
  at least one test commit validates the hook rejects a non-conforming message.
- [ ] Branch strategy and CIA workflow documents are committed to
  `docs/governance/`.
- [ ] `python -m engine validate Ogma-Library` exits 0 on the repo root.
- [ ] No open R1 or R2 defect (not applicable for a documentation phase; any
  data-loss or privacy-breach risks identified in decisions.md are flagged with
  a tracking item).
- [ ] Cross-platform scope is confirmed in writing: Windows 10 1903+ and
  macOS 13 Ventura+ are the MVP platforms; Linux is a documented bonus.
- [ ] Owner sign-off on the complete decisions.md is recorded (name + date).

### Phase-specific exit criteria

- Every subsequent phase's README "Dependencies" section can point to a specific
  entry in `decisions.md` or a specific ADR for every assumption it makes.
- The hybrid validation gate operational check is green and committed to CI.
- The open-source readiness artifacts pass a brief legal and style review
  (recorded in WP4 completion note).

---

## 10. Skills to use

See `skills.md` for full invocation guidance. Summary:

- `documentation-generation:architecture-decision-records` — draft and ratify
  ADR-0001..ADR-0010.
- `sdlc-meta:project-requirements` + `sdlc-meta:spec-architect` — structure
  the context-gap answers and decision log.
- `sdlc-meta:sdlc-planning` — confirm the phase sequencing and dependency graph.
- `security:dpia-generator` + `security:uganda-dppa-compliance` — CON-7
  jurisdiction and DPIA scope.
- `product-business:product-strategy-vision` — validate open-question answers
  against the product promise.
- `superpowers:verification-before-completion` — confirm every DoD item before
  closing the phase.

---

## 11. Deliverables

| Artifact | Location |
| --- | --- |
| `decisions.md` | `docs/plans/grand-plan/phase-00/decisions.md` |
| `ADR-0001.md` .. `ADR-0009.md` (Accepted) | `docs/adrs/` |
| `ADR-0010.md` (Proposed) | `docs/adrs/` |
| `LICENSE` | repo root |
| `CONTRIBUTING.md` | repo root |
| `CODE_OF_CONDUCT.md` | repo root |
| CLA mechanism (file or `.github/CONTRIBUTORS`) | repo root or `.github/` |
| `.github/hooks/commit-msg` | `.github/hooks/` |
| `.github/PULL_REQUEST_TEMPLATE.md` | `.github/` |
| `docs/governance/BRANCH-STRATEGY.md` | `docs/governance/` |
| `docs/governance/CIA-WORKFLOW.md` | `docs/governance/` |
| `docs/governance/HYBRID-GATE.md` | `docs/governance/` |
| `docs/governance/REFERENCE-HARDWARE.md` | `docs/governance/` |
| Phase 00 testing evidence | `docs/plans/grand-plan/phase-00/testing.md` |

---

## 12. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Owner unavailable for sign-offs within 2-week window | R5 | Schedule a single concentrated sign-off session at the end of WP2; prepare a decision brief (one page per OQ/CON) so review time is minimal. |
| ADR amendments required after Phase 01 spikes conflict with decisions made here | R5 | ADR-0003 and ADR-0004 are explicitly spike-gated; this phase accepts "Proposed / pending spike" as a valid state for those two ADRs; amendments in Phase 01 are expected and budgeted. |
| License choice delays (legal review) | R5 | Apache 2.0 is recommended; owner can accept the recommendation without separate legal counsel for an open-source project, reducing delay. |
| CON-7 jurisdiction work (DPIA, minors) is under-specified until Phase 16-18 | R4 | Phase 00 records what is known today (Uganda DPPA + GDPR as defaults); DPIA per feature is deferred to Phase 19 with a tracked item. The decision here is just the jurisdiction list, not the full DPIA. |
| Reference hardware (CON-1) not available to the team | R5 | Specify the reference machines by market model (e.g. "mid-2022 MacBook Air M1, 8 GB RAM; mid-range Windows laptop, Core i5 2020, 8 GB RAM, SATA SSD") even before physical access; Phase 20 benchmarks require physical access. |

---

## 13. Owner asks

1. **License selection:** Choose **MIT** or **Apache 2.0** for the open-source
   license. Recommendation: Apache 2.0 (patent grant). Deadline: before WP4
   completes (Day 5).
2. **Reference hardware (CON-1):** Confirm the two reference machine
   specifications (Windows + macOS models) that anchor NFR-OGMA-001..009.
   Specify or approve the suggested mid-range specs in `decisions.md`.
3. **Command-palette command set (CON-4):** Provide or approve a first-pass
   list of the ~30 command-palette commands to be available at MVP. Phase 03
   will implement them; Phase 00 needs the list so Phase 03 scope is bounded.
4. **Work/Edition cardinality (CON-5):** Confirm whether "a Work" can have
   multiple Editions (files) in the data model, and the merge/split rules.
   This is a domain model constraint that impacts Phase 04.
5. **Corpus licensing (CON-9):** Confirm which PDF files in the golden-corpus
   test harness are cleared for redistribution in the test suite (public domain,
   CC, or owner-supplied synthetic). Any file without a clear license must be
   replaced before Phase 02.
6. **Sign-off on all OQ answers and ADR ratifications** in the single sign-off
   session (WP6). Record name + date in `decisions.md`.
7. **CLA mechanism:** Choose between CLA Assistant (GitHub App, automated) and
   Developer Certificate of Origin (DCO, simpler). Recommendation: DCO for
   simplicity at this stage.

---

## 14. Change log

| Date | Author | Change |
| --- | --- | --- |
| 2026-05-30 | Grand Plan authoring | v1.0 baseline created |
