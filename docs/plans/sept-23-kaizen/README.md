# Ogma Library: Sept-23 Kaizen audit and 29-phase remediation plan

Requested by Peter Bamuhigire ("study this project, build and run the app and test every corner,
it's not working as I wish"). Audit executed 25 September 2026 against commit `0ad3c0c`.

**Verdict:** the product does not currently work for a normal user. The library is hidden
behind a mis-nested panel. Reading crashes the app. Primary actions are off-screen or disabled.
Covers, file validity, duplicates, search and the AI Advisor are broken. The engineering
foundations are strong, so this is an integration and verification failure, not a rewrite case.
**Score: 30/100** (raw 30, capped reporting `min(raw, 65)`); target 95, conditional on evidence.

## Start here

| # | Document | What it gives you |
|---|---|---|
| 1 | [01-audit-report.md](01-audit-report.md) | Verdict, score, method, root-cause analysis of the drift |
| 2 | [03-defect-register.md](03-defect-register.md) | Every finding (K01–K92) with severity, evidence class, location and phase |
| 3 | [08-master-plan.md](08-master-plan.md) | 29 phases in 7 waves, dependency graph, owner decisions D-01..D-12, score trajectory, Definition of Done |
| 4 | [phases/](phases/) | One detailed, executable document per phase |
| 5 | [AGENT_BRIEF.md](AGENT_BRIEF.md) | Invariants every implementing agent must follow |
| 6 | [02-test-report.md](02-test-report.md) | Commands, results, timings, real-window journey results |
| 7 | [04-inventory-and-requirements.md](04-inventory-and-requirements.md) | Projects, runtime bindings, surfaces, 101-FR coverage table |
| 8 | [05-ui-ux-audit.md](05-ui-ux-audit.md) | Screen-by-screen UX and visual audit with screenshots |
| 9 | [06-prior-plans-reconciliation.md](06-prior-plans-reconciliation.md) | What earlier plans found and fixed, and what went wrong |
| 10 | [07-skills-engine-routing.md](07-skills-engine-routing.md) | Engine doctrine applied and the skills each phase must load |
| 11 | [00-currentness-register.md](00-currentness-register.md) | Time-sensitive claims and their verification status |
| — | [evidence/](evidence/) | Screenshots, logs, the UI Automation driver, the corpus generator, the root-cause patch |

## The five things to know

1. **Your working tree does not build.** 210 test files are deleted but uncommitted, and the
   solution still references them. Decide D-01 first (restore recommended).
2. **One XAML nesting error hides the whole library** (K10, since `822e760` on 4 September). It
   was verified by patching a scratch copy; the fix is Phase 03.
3. **Reading crashes the app** after about 20 page turns (K30). The stack trace was captured;
   the fix is Phase 04, on top of the Phase 02 safety net.
4. **Many finished backend features have no door.** Settings, AI provider setup, the Privacy
   Center, classroom Host, bulk edit, smart shelves and more are unreachable (K16, K50, K90).
   Phases 07, 08, 10 and 15 open them.
5. **The tests were green while the product was broken** (K05). Phase 01 makes the real window
   the acceptance oracle for every later phase.

## Relationship to earlier plans

- The Aug-39 roadmap stays the **requirement accountability** authority (its FR/NFR/CTRL-to-phase matrix).
- This plan becomes the **execution sequence**, subject to owner decision D-02.
- The astra 21-phase plan (14 September) and the Sept-10 Kaizen are superseded as execution plans
  and kept as history.

See [06-prior-plans-reconciliation.md](06-prior-plans-reconciliation.md).

## Status register

Update this table as each phase moves. A phase is **COMPLETE** only under the Definition of Done in
[08-master-plan.md §5](08-master-plan.md#5-definition-of-done-every-phase).

| Phase | Title | Wave | Status | Completion record |
|---:|---|:---:|---|---|
| 00 | Ground truth, repository and toolchain recovery | A | **COMPLETE** 2026-09-25 | [record](../../implementation/execution/phase-sept23-00-completion.md) |
| 01 | Real-window acceptance harness and golden journeys | A | **COMPLETE** 2026-09-25 (hosted-runner CI run NOT ASSESSED) | [record](../../implementation/execution/phase-sept23-01-completion.md) |
| 02 | Crash safety, logging and threading foundation | A | **IMPLEMENTED** 2026-09-25 (G3/G8 journey proof pending Phase 01) | [record](../../implementation/execution/phase-sept23-02-completion.md) |
| 03 | Shell emergency fixes: visible catalogue and truthful status | A | **COMPLETE** 2026-09-25 | [record](../../implementation/execution/phase-sept23-03-completion.md) |
| 04 | Reader engine stability and page rendering | A | **COMPLETE** 2026-09-25 | [record](../../implementation/execution/phase-sept23-04-completion.md) |
| 05 | Library roots, scanning and file validity | B | **IMPLEMENTED** 2026-09-25 (E2E journeys G2/G6 pending Phase 01) | [record](../../implementation/execution/phase-sept23-05-completion.md) |
| 06 | Processing pipeline, jobs and identity promotion | B | **IMPLEMENTED** 2026-09-25 (E2E G2 processing journeys pending re-run) | [record](../../implementation/execution/phase-sept23-06-completion.md) |
| 07 | Navigation and information architecture | B | IN PROGRESS (worktree lane) | — |
| 08 | Settings and capability centre | B | NOT STARTED | — |
| 09 | Design system and visual identity | C | NOT STARTED (needs D-07) | — |
| 10 | Catalogue experience | C | NOT STARTED | — |
| 11 | Book detail inspector overhaul | C | NOT STARTED | — |
| 12 | Reader experience | C | NOT STARTED | — |
| 13 | Unified, trustworthy search | D | NOT STARTED | — |
| 14 | Local semantic capability and embeddings | D | NOT STARTED (needs D-05) | — |
| 15 | AI gateway, providers and Privacy Center | D | NOT STARTED (needs D-06) | — |
| 16 | Reading Advisor, grounded answers and reading plans | D | NOT STARTED | — |
| 17 | OCR and extraction quality | D | NOT STARTED | — |
| 18 | 3D bookshelf | E | NOT STARTED | — |
| 19 | Classroom Host and school administration | E | NOT STARTED | — |
| 20 | Classroom client, offline cache and sync | E | NOT STARTED (needs D-09, D-11) | — |
| 21 | Accessibility | F | NOT STARTED | — |
| 22 | Localisation and product copy | F | NOT STARTED | — |
| 23 | Security and privacy hardening | F | NOT STARTED | — |
| 24 | Performance, reliability and scale | F | NOT STARTED | — |
| 25 | Extensibility and imports | G | NOT STARTED (needs D-10) | — |
| 26 | Packaging, installers and signing | G | NOT STARTED (needs D-08) | — |
| 27 | macOS parity and cross-platform acceptance | G | NOT STARTED | — |
| 28 | Documentation, in-app help, beta usability test and re-audit | G | NOT STARTED (needs D-12) | — |

## Reproducing the audit

```powershell
# 1. Build a clean HEAD copy (never the dirty tree) and generate the synthetic library
git worktree add --detach ..\ogma-audit HEAD
cd ..\ogma-audit; dotnet restore OgmaLibrary.sln --locked-mode; dotnet build OgmaLibrary.sln -c Release --no-restore
python docs\plans\sept-23-kaizen\evidence\tools\New-SyntheticCorpus.py "$env:TEMP\ogma-corpus"
Copy-Item tests\golden-corpus\ocr-pipeline\scanned-image-only.pdf "$env:TEMP\ogma-corpus\Edge Cases\"
# 2. Drive the real window with an isolated data directory
$env:OGMA_UIA_OUT = "$env:TEMP\ogma-uia"
.\docs\plans\sept-23-kaizen\evidence\tools\Invoke-OgmaUia.ps1 -Action launch -DataDir "$env:TEMP\ogma-data"
.\docs\plans\sept-23-kaizen\evidence\tools\Invoke-OgmaUia.ps1 -Action shot -Out first-run
.\docs\plans\sept-23-kaizen\evidence\tools\Invoke-OgmaUia.ps1 -Action dump -Out first-run
```
