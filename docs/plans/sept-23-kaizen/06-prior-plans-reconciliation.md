# Reconciliation with prior audits and plans

Purpose: build on earlier work instead of repeating it, and state what this plan replaces.

Sources:

- `docs/audit/` (30 August)
- `docs/plans/aug-39/`
- `docs/kaizen/2026-09-10-product-kaizen-no-visual/`
- `docs/plans/astra-audit-sept/` (14 September)
- `docs/implementation/execution/` (status, dashboard, remaining gates, the 2026-09-14 execution
  and acceptance evidence)
- `docs/plans/grand-plan/ICON-SYSTEM.md`
- `git log --since=2026-09-10`
- spot checks of current source

## 1. Timeline

| Date | Artefact | Headline |
|---|---|---|
| 2026-08-30 | `docs/audit/` | "Substantial engineering prototype", **40–48 % complete** (planning point 44 %). Beta NO-GO. Produced the Aug-39 roadmap. |
| 2026-09-06 | Aug-39 ledger normalised | Phases 1–9 and 12 COMPLETE; the rest IN PROGRESS. **120 of 165** criteria closed, 45 open. CI: 1,153 tests per platform green. |
| 2026-09-10 | Kaizen "no-visual" | Visual checks skipped **by request**. Raw 82.2, published 65 (cap). Release FAIL (4 blockers). |
| 2026-09-14 | Astra audit (`a93900a`) | First native-window audit. Raw 41, published **40** (owner-requested cap), target 95. Findings F01–F30. 21-phase plan estimated at 118–201 person-days. |
| 2026-09-14, 11:11–12:10 | "First tranche", 21 commits `57fd674`..`0ad3c0c` | One small change mapped to each astra phase in about 42 minutes, totalling about 150 lines of source. Phase 21 handed off as "conditional / not release-approved". No re-score. |
| 2026-09-14 → 09-25 | — | **No commits.** The working tree shows about 210 uncommitted deletions of both test projects (K01). |
| **2026-09-25** | **This audit** | First real-window run with a real library. The catalogue is invisible (K10, a regression since 4 September), the reader crashes (K30), and the Advisor is dead (K50). Raw **30**, published **30**. |

The Sept-25 raw score (30) is lower than astra's (41) because this audit exercised a populated
library and sustained reading. Astra audited with an empty or fixture library and did not detect
the pager overlay or the reader crash. The product did not get worse after 14 September; the
evidence got deeper.

## 2. Astra findings F01–F30: disposition on 25 September

Disposition key:

- **FIXED**: addressed; only residual acceptance remains.
- **PARTIAL**: some remediation; the core problem remains.
- **TOKEN**: a commit was mapped to the finding but did not address it.
- **OPEN**: no substantive change.

The Sept-23 column gives the defect ID and phase that now own the finding.

| ID | Sev | Summary | Disposition | Commit | Sept-23 owner |
|---|---|---|---|---|---|
| F01 | P0 | Command palette unbidden and undismissable | FIXED (acceptance partial; hard-coded close strings) | `157e49f` | 07 (regression test), 22 (strings) |
| F02 | P0 | Empty right inspector always visible | PARTIAL (binding fixed; not natively re-verified) | `157e49f` | 11 |
| F03 | P0 | Toolbar overlaps | PARTIAL (horizontal scroll stopgap) | `036c95a` | K11 → 03, 07 |
| F04 | P1 | Collection CRUD crowds the sidebar | OPEN | — | 07, 10 |
| F05 | P1 | Thin, faint text; display-size tabs | OPEN | — | K15 → 09 |
| F06 | P1 | AI Smart Search shown in standalone; Sharing shown without a view model | OPEN (`MainShellViewModel.cs:275`) | — | K17 → 07 |
| F07 | P1 | No settings, help or privacy route; Privacy Center unmounted | OPEN | — | K16 → 08, 15 |
| F08 | P1 | Advisor looks ready although AI is disabled | TOKEN | `3d515c9` | K50 → 15, 16 |
| F09 | P0 | Reader lacks layout modes, full screen and in-document find | OPEN | — | K34 → 12 |
| F10 | P1 | Reader chrome crowds the page | OPEN | — | K34 → 12 |
| F11 | P1 | Render exceptions left a stale page | FIXED (no test) | `5a13d0e` | 04 (add test) |
| F12 | P1 | Annotation and citation behaviour unproved on real geometry | TOKEN | `bd1db17` | 12 |
| F13 | P1 | Fixed cards; badges compete with titles | TOKEN | `61fd522` | 10 |
| F14 | P1 | Inspector: 7 wrapping tabs, blank cover, File tab first | PARTIAL (minor) | `bbb1ae0` | 11 |
| F15 | P1 | Overlapping search entry points | OPEN | — | 07, 13 |
| F16 | P0 before AI claims | No labelled holdout for relevance | PARTIAL (wording) | `d402afc` | 13, 16 |
| F17 | P0 before paid AI | Unknown price counted as zero; no reservation | PARTIAL | `a5e56c7` | 15 |
| F18 | P1 | Preview remembered once per session | OPEN (`AiGateway.cs:22`) | — | 15 |
| F19 | P0 classroom | Clients store full PDFs | OPEN (`ClassroomBookFileMaterializer.cs:69`) | — | K62 → 20 (D-09) |
| F20 | P0 institutional | Materialised files not profile-keyed | TOKEN | `6ab3ebc`, `fa9bb69` | 20, 23 |
| F21 | P0 release | Isolation, hostile PDFs, signing and install unverified | OPEN / NOT ASSESSED | — | 23, 26 |
| F22 | P0 release | Keyboard, Cmd parity and AT unproved | PARTIAL | `e7d83ab` | 21, 27 |
| F23 | P1 | Scan status counts misleading | PARTIAL (relabel) | `6bb2fad` | K13 → 03, 06 |
| F24 | P1 | README obsolete | OPEN | — | 28 |
| F25 | P0 governance | Requirement matrix mis-maps READ-007..015 and NFR-PROD-005/006 | OPEN | `57fd674` (path only) | 00 |
| F26 | P1 | UI tests assert booleans, not visibility | OPEN | — | K05 → 01 |
| F27 | P1 | 3D gated by env; physical WebView open | TOKEN | `521f46f` | K60 → 18 |
| F28 | P1 | OCR proof synthetic only | OPEN | — | 17 |
| F29 | P1 | Mixed emoji, glyph and SVG controls; colour-only selection | PARTIAL (minor) | `287aee9` | 09 (D-07) |
| F30 | P1 | AI budget store loses state on corruption | OPEN | — | 15 |
| gate | — | `dotnet format --verify-no-changes` failing | OPEN since 09-06 | — | K03 → 00 |

**Tally:** FIXED 2, PARTIAL 8, TOKEN 5, OPEN 15. Every P0 except F01 remains open or partial.

## 3. Astra phase status

All 21 phase briefs still say "Status: planned".

| Astra phase | Evidence | Status |
|---|---|---|
| 01 Scope and traceability | SRS path only | Partial (minimal) |
| 02 Dismissal and visibility | Bindings plus close button | Mostly done |
| 03 Shell navigation | Horizontal scroll | Partial (stopgap) |
| 04 Visual system | Text labels | Token |
| 05 First use and processing | "files" relabel | Token |
| 06 Catalogue | Automation name | Token |
| 07 Inspector overhaul | Width, title, cover | Partial (minor) |
| 08 Reader core | Render-failure state | Partial |
| 09–12 | Live regions and labels | Token |
| 13 AI settings and cost | Unknown price is not free | Partial |
| 14–18 | Live regions | Token |
| 19 Accessibility and parity | Cmd modifier | Partial (minimal) |
| 20 Hardening and packaging | — | Not started |
| 21 Acceptance | Conditional handoff | Handoff only |

## 4. Open release gates (carried into this plan)

| Type | Gates | Sept-23 phase |
|---|---|---|
| Physical and platform | Any macOS run; packaged WebView2/WKWebView and WebGL2; two-machine classroom (firewall, mDNS, TOFU, drop and reconnect); full-app kill and restart; writeback interruption; ACL and disconnected volumes; macOS Tesseract | 18, 19, 20, 24, 27 |
| Signing and release | Signed Windows installer; notarised macOS; clean install, upgrade and rollback; migration drills; artefact promotion; owner acceptance | 26, 28 |
| Legal and privacy | Provider terms approval; DPIA covering minors; school-controller approval; classroom storage ADR (F19) | 08, 15, 20, 23 |
| Accessibility | Narrator and VoiceOver on six surfaces; keyboard-only journeys; usability with 20 or more participants (190 of 200 tasks unassisted) | 21, 28 |
| Performance | Reference machines at 2k and 50k books; extraction, GPU, 3D, LAN and AI budgets | 24 |
| Security | PDF containment escape probes with a named reviewer (P0 since 30 August); third-party hostile corpus; penetration test; secret store; backup and restore; AI spend controls; cross-profile leakage | 15, 23 |
| AI quality | Human-labelled held-out corpus for relevance, citation support and abstention | 16 |

## 5. Owner feedback on record

Direct feedback is sparse; most of it was paraphrased by auditors.

1. Palette: *"Peter reports that the palette is distracting, has no apparent purpose and is
   impossible to close."* (astra `02-ui-ux-audit.md:46`)
2. Right panel: *"Peter explicitly requested this overhaul."* (astra `phases/phase-07.md:5`)
3. Visual direction: *"I want a beautiful app. Buttons and menus must have colorful icons. Design it
   with that in mind and always ask for them — I will buy premium PNG icons."*
   (`docs/plans/grand-plan/ICON-SYSTEM.md:3`). **Not delivered.**
4. Standing decisions:
   - English and French for the MVP.
   - Strictest-common-denominator privacy (Uganda DPPA, GDPR, COPPA/FERPA).
   - Distribution via GitHub, the Mac App Store and the Microsoft Store.
   - Classroom LAN after the MVP.
5. The 25 September request: *"its not working as i wish … test everything and make a plan to fully
   remediate the app and make it work."*

Nothing records the owner's reaction to the 14 September tranche.

## 6. Recurring failure patterns and countermeasures

| # | Pattern | How this plan counters it |
|---|---|---|
| 1 | Headless or view-model evidence closed gates that failed in the real window (palette closed by test on 09-05, found undismissable on 09-14; catalogue invisible since 09-04 with 163 green UI tests) | Phase 01 real-window harness with visibility, occlusion and reachability assertions. The window is the oracle in every Definition of Done. |
| 2 | Skipping visuals inflated scores (82.2 → 41 in four days) | Every score in this plan uses rendered evidence, and the 65-cap applies to the first audit. |
| 3 | Services built but not reachable (Privacy Center, settings, reader modes, providers) | "Reachable from a visible control in a golden journey" is an acceptance condition. The requirement table in `04-inventory-and-requirements.md` counts backend-only as not done. |
| 4 | Paperwork outweighed product change (238 evidence files versus about 150 lines of fixes) | One completion record per phase. Evidence proportionate to change. |
| 5 | Plan-mapping theatre (one token commit per phase) | "Done" is a user-observable outcome with before and after screenshots and owner walkthroughs per wave. Mapping a commit to a phase closes nothing. |
| 6 | Plan proliferation and conflicting authorities; F25 still wrong | D-02: Sept-23 is the execution sequence and Aug-39 is the requirement matrix. Phase 00 fixes F25 and records the authority in `CLAUDE.md`. |
| 7 | External gates blocked with no acquisition plan | Each NOT ASSESSED gate has an owner and an acquisition task (Mac hardware, certificates, reviewer, participants) in Phases 23 and 26–28, started early. |
| 8 | Quick fixes broke project rules (hard-coded strings and colours, no tests) | The Definition of Done requires localised strings, tokens, and a failing-then-passing test. |
| 9 | Stopgaps instead of redesign on the owner's two complaints | Phase 07 (navigation and IA) and Phase 11 (inspector) are full redesigns. Stopgaps must be labelled and must have a follow-up phase. |
| 10 | Chronic gate failure normalised (format since 09-06) | Phase 00 closes the format gate. CI must be green before any later phase starts. |
| 11 | Repo hazard: deleted test projects | Phase 00, decision D-01. |

## 7. What to preserve

All audits agree on these strengths. Preserve them:

- the inward-dependency modular architecture and its architecture tests;
- the canonical identity, occurrence and asset model;
- durable leased jobs;
- the isolated renderer, after its resource policy is fixed;
- reader persistence, annotations and citations;
- the fail-closed `AiGateway` tier, consent and payload boundaries;
- honest labelling of local-evidence answers;
- the font roles (Public Sans, Spectral, JetBrains Mono);
- explicit NOT ASSESSED discipline.

## 8. Authority decision (D-02)

**Recommended, pending owner confirmation:**

- **Execution sequence:** this Sept-23 Kaizen plan (29 phases, `08-master-plan.md`).
- **Requirement accountability:** the Aug-39 matrix (`docs/plans/aug-39/appendices/01-requirement-phase-matrix.md`),
  corrected for F25 in Phase 00, with a Sept-23 phase column added.
- **Superseded as execution plans (kept as history):** the astra 21-phase plan, the Sept-10
  Kaizen backlog and the grand plan.
- **Status reporting:** `docs/implementation/execution/00-execution-status.md` gains a Sept-23
  section. Aug-39 phase statuses stay frozen, and Aug-39 gates close only through Sept-23 phase
  completion records that cite them.

Phase 00 records this in `CLAUDE.md` and `docs/plans/aug-39/README.md` once the owner confirms.
