# Phase 28 — Documentation, in-app help, beta usability test and re-audit

## 1. Header

| Field | Value |
|---|---|
| Wave | G. Ship |
| Size | 7–10 engineering days, plus participant scheduling (about 2 weeks elapsed) |
| Depends on | All earlier phases; 26 (installable builds) and 27 (macOS) for the usability test and final acceptance |
| Owner decisions | **D-12** beta cohort (recommended: 20 participants across home readers, students and teachers, recruited by the owner); final release sign-off |
| Primary defects | K90 (requirements usable by users), K91 (plan and evidence sprawl), UX-006, UX-007 |
| Requirements | FR-UX-006 (offline help), FR-UX-007 (locate and resume in 60 s), the 162-ID accountability set, NFR usability and documentation |

## 2. Why this phase exists

The Sept-23 cycle began with a raw diagnostic score of **30/100** (reported `min(30, 65) = 30`)
because the product failed in the real window while its paperwork looked complete (K05, K91). This
phase closes the loop the way the Kaizen doctrine requires:

- **Users need help in the product.** There is no user guide, no admin or teacher guide and no
  offline in-app help (FR-UX-006 is missing). `docs/developer-guide/` holds only `README.md` and
  `search-index.md`.
- **Usability has never been measured with people.** FR-UX-007 ("locate and resume in 60 s") is
  unmeasured, and the 95 target requires 190 of 200 unassisted task successes from at least 20
  participants (prior-audit rubric).
- **The score must be re-measured with the same instrument.** Use the same 10-dimension index
  (Purpose 8, Visual 14, UX 14, Reading 14, Catalogue 12, Search/AI 10, Security 10, Cross-platform 6,
  Engineering 6, Documentation 6; each scored 0–4) and the same evidence classes (MEASURED, CODE,
  HEURISTIC, NOT ASSESSED) as [01-audit-report.md](../01-audit-report.md). Otherwise the result is
  not comparable.
- **Release needs one evidence bundle, not hundreds of files.** The prior cycle produced 238
  evidence files and about 24k lines of audit text against about 150 lines of fixes. The release
  record should be a single `validation-contract` bundle that a fresh reader can check.

## 3. Objectives and exit criteria

1. **User guide** (tasks first: add a library, read, annotate, search, ask the Advisor, privacy,
   backup), **installation guide** (Windows and macOS, WebView2, SmartScreen and Gatekeeper
   notes), **FAQ and troubleshooting** (including *Export diagnostics*), **release notes** for the beta,
   and an **admin and teacher guide** (Host set-up, publishing, join codes, student profiles, AI
   policy and quotas, erasure, backup).
2. **Offline in-app help (FR-UX-006):** a help panel reachable from every screen (F1 on Windows,
   ⌘? on macOS, and a Help item in navigation); context links from empty states and errors; en and
   fr; works with no network.
3. **Developer guide refresh:** architecture map, build and gates, the real-window harness, the
   corpus generators, the release pipeline, and how to run a Kaizen re-audit.
4. **Usability test:** at least 20 participants (D-12), 10 tasks each (200 task attempts), on the
   installed signed build, split across Windows and macOS. **≥ 190 unassisted successes.** SUS
   collected (target ≥ 80). FR-UX-007 measured: median locate-and-resume ≤ 60 s, and ≥ 90 % of
   participants within 60 s.
5. **Final re-audit** with the same index, instruments and journeys as the baseline. Every score
   above 65 is backed by MEASURED or rendered evidence. Hard gates: zero open P0; all 162 requirement
   IDs accepted or formally deferred; the 8 golden journeys pass on both OSs.
6. **Release evidence bundle** in the seven-category `validation-contract` format at
   `docs/releases/sept23-beta-evidence-bundle.md`, linking (not copying) the phase completion records.
7. Design QA ship gate (`design-qa-and-pre-launch-review`): **SHIP** or **SHIP-WITH-FOLLOWUPS** with
   named follow-ups.
8. **Owner sign-off** recorded, with the owner's own walkthrough verdict.
9. **Retrospective and next cycle:** standardisation records for what worked; the next Kaizen cycle
   scope, date and owner; the four historical plans archived with a pointer to this plan.

## 4. Skills to load before starting

- `C:\wamp64\www\srs-skills\08-end-user-documentation\01-user-manual\SKILL.md`
- `C:\wamp64\www\srs-skills\08-end-user-documentation\02-installation-guide\SKILL.md`
- `C:\wamp64\www\srs-skills\08-end-user-documentation\03-faq\SKILL.md`
- `C:\wamp64\www\srs-skills\08-end-user-documentation\04-release-notes\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\doc-architect\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\sdlc-documentation\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\05-ux-process-research-and-psychology\ux-research-and-usability-testing\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\ux-writing-and-microcopy\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\product-design-audit\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\00-cross-cutting-ops-qa-a11y\design-qa-and-pre-launch-review\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\implementation-status-auditor\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\kaizen-improvement-system\SKILL.md`
- `C:\wamp64\www\srs-skills\09-governance-compliance\31-kaizen-engine-and-product-improvement\SKILL.md`
- `C:\wamp64\www\srs-skills\05-testing-documentation\03-test-report\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\architecture\validation-contract\SKILL.md`
- Currentness gate: `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`
  — re-verify every time-sensitive claim in the user and install guides (OS versions, WebView2,
  provider model names) before publishing them.

## 5. Scope

**In:** user, install, FAQ, release-notes and admin/teacher documentation; offline help system;
developer-guide refresh; usability study; final re-audit; release evidence bundle; design QA ship
gate; owner sign-off; retrospective; archive of superseded plans.

**Out:** marketing website and store listings (excluded by release scope); video tutorials (optional
follow-up); public launch operations.

## 6. Work breakdown

| # | Task | Targets | Acceptance check |
|---|---|---|---|
| 28.1 | Documentation architecture: audiences, doc set, locations (`docs/user-guide/`, `docs/admin-guide/`, `docs/developer-guide/`), style and screenshot policy (synthetic library only, no private books). | `doc-architect` output | Owner approves the doc map |
| 28.2 | User guide and FAQ, written from the golden journeys, with screenshots from the Phase 01 harness on the final build. | `docs/user-guide/` | Every golden journey documented; screenshots current |
| 28.3 | Installation guide (Windows Velopack and MSIX, macOS DMG), including WebView2, SmartScreen and Gatekeeper behaviour, upgrade and uninstall, and data locations. | `docs/user-guide/install.md` | A tester installs using only the guide |
| 28.4 | Admin and teacher guide for classroom mode, AI policy, quotas, erasure and backup. Cross-reference the DPIA (Phase 23). | `docs/admin-guide/` | A teacher-role participant completes Host set-up from the guide |
| 28.5 | Offline in-app help: help content as localised resources, a help panel with search, F1 and ⌘? bindings, context links from empty and error states. | App views, localisation resources | Real-window journey: open help from three contexts offline; en and fr |
| 28.6 | Developer guide refresh, including the harness, corpora, gates, release and the re-audit procedure. | `docs/developer-guide/README.md` | A new contributor runs all gates from the guide |
| 28.7 | Beta release notes and known issues. | `docs/releases/` | Release notes match the evidence bundle |
| 28.8 | Usability study design: protocol, consent form (adults; guardian consent for minors, if any, per the DPIA), 10 tasks (including locate-and-resume for FR-UX-007), success criteria, SUS, observer script, data handling (no recordings leave the owner's control). | `docs/qa/usability-sept23/protocol.md` | Owner approves the protocol |
| 28.9 | Run sessions with 20+ participants across both OSs; log each task (success, assisted, fail, time, errors); collect SUS. | Session logs (anonymised) | 200 task attempts recorded |
| 28.10 | Analyse: success rate, time on task, error themes, SUS; triage findings with `ux-remediation-and-redesign` rules (a gate-failing finding is Must); fix Critical/Serious findings and re-test those tasks. | `docs/qa/usability-sept23/report.md` | ≥ 190/200 unassisted after fixes; FR-UX-007 met |
| 28.11 | Final re-audit: re-run the 8 golden journeys on both OSs; score the 10-dimension index with the same evidence classes; run `implementation-status-auditor` for FR status; confirm zero open P0. | `docs/plans/sept-23-kaizen/09-final-reaudit.md` | Scored with evidence per dimension |
| 28.12 | Design QA ship gate on the final build. | Design QA record | SHIP or SHIP-WITH-FOLLOWUPS |
| 28.13 | Release evidence bundle in the `validation-contract` seven-category format. | `docs/releases/sept23-beta-evidence-bundle.md` | A fresh reader can verify each claim from its link |
| 28.14 | Owner walkthrough and sign-off. | Sign-off record | Owner signature and date |
| 28.15 | Retrospective, standardisation records, the next Kaizen cycle (scope, date, owner); archive the astra 21-phase plan, the Sept-10 Kaizen and the 24-phase grand plan with a pointer here; update `CLAUDE.md` and `00-execution-status.md`. | `docs/plans/`, `CLAUDE.md` | One authoritative plan pointer remains |

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| No offline help (UX-006) | Never scoped into a build phase | In-app help panel | Users self-serve common tasks | 0 topics → help for every journey | Real-window journey | Help drifts from the UI | Help screenshots regenerated by the harness each release |
| Usability unmeasured | No participants in earlier cycles | 20-person study | Real users reveal what tests cannot | 0 → ≥ 190/200 unassisted; SUS ≥ 80 | Study report | Recruitment delay | Run in two waves of 10 |
| Scores not comparable across audits | Different rubrics per cycle | Same index and classes | Progress is real, not relabelled | 30 baseline → measured final | Re-audit | Pressure to inflate | Every > 65 dimension needs linked MEASURED evidence |
| Evidence sprawl (K91) | Evidence produced per micro-change | One release bundle | Reviewers can verify quickly | 238 files → 1 bundle with links | Bundle | Missing links | Bundle checklist in `validation-contract` |

## 8. Test plan

- **Documentation tests:** an independent tester installs and completes the journeys using only the
  guides; broken-link check across `docs/`.
- **Help system:** real-window journeys opening help offline in en and fr; F1 and ⌘? bindings;
  screen-reader reading of help content (Phase 21 rules).
- **Usability:** the study itself, with pre-registered success criteria.
- **Re-audit:** the 8 golden journeys on R-Win-Low, R-Win-Std and R-Mac with the installed builds.

## 9. Acceptance commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet test OgmaLibrary.sln --configuration Release --no-build -m:1 --filter "Category!=Benchmark&Category!=Soak"
./scripts/Test-ReleaseAcceptance.ps1
# Real-window golden journeys on the installed build, both OSs (Phase 01 harness)
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence while open |
|---|---|---|
| 20+ participants (D-12) | Owner recruits | Usability gate open; score capped below 95 |
| Guardian consent process for minors | Owner and school partner | Minors excluded from the study |
| Owner sign-off | Owner | No release |
| Any NOT ASSESSED gate inherited from phases 23–27 | Named owners in those phases | Listed in the bundle; blocks release if it is a blocker class |

## 11. Risks and mitigations

- **The study finds major problems late.** Run a 5-participant pilot at the end of wave C, then the
  full study here.
- **Score inflation under release pressure.** The re-audit uses the baseline instrument; any
  dimension above 65 without linked MEASURED evidence is capped.
- **Documentation goes stale quickly.** Generate screenshots from the harness; add a release checklist
  item to regenerate them.

## 12. Execution prompt

```
## Prompt 28 - Documentation, usability test and re-audit
You are closing the Sept-23 Kaizen cycle for C:\wamp64\www\Ogma-Library. Read first, in order:
C:\wamp64\www\Ogma-Library\CLAUDE.md; docs/plans/sept-23-kaizen/README.md; docs/plans/sept-23-kaizen/AGENT_BRIEF.md;
docs/plans/sept-23-kaizen/01-audit-report.md (baseline instrument); docs/plans/sept-23-kaizen/08-master-plan.md (D-12, score trajectory);
docs/plans/sept-23-kaizen/phases/phase-28-docs-usability-and-reaudit.md.
Read the SKILL.md files in section 4 directly.
A. Documentation: 28.1 -> 28.2..28.7 (parallel), screenshots from the harness on the final build only.
B. Usability: 28.8 protocol (owner approval) -> 28.9 sessions -> 28.10 analysis and fixes.
C. Close: 28.11 re-audit with the SAME index and evidence classes -> 28.12 design QA -> 28.13 bundle -> 28.14 owner
sign-off -> 28.15 retrospective and archive.
Never claim a score above 65 for a dimension without linked MEASURED evidence. Never publish participant
personal data or private book content. Record completion at docs/implementation/execution/phase-sept23-28-completion.md.
```
