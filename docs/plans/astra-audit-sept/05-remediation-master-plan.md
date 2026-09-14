# Comprehensive 21-phase remediation programme

Planning date: 2026-09-14. Owner: Peter Bamuhigire, Lead Consultant.
Status: proposed implementation programme, not executed. Baseline: [40/100 capped readiness](01-product-audit.md). Target: **95/100 with all blocking gates closed**.

## Programme intent

Deliver a dependable Windows/macOS PDF e-library for legally acquired personal and school collections. Reading, retrieval, organization and preservation are the primary outcomes. Optional AI must improve those outcomes while making its limits, data use and cost understandable. Classroom capabilities must preserve student privacy and support ordinary school operators.

Retain the existing .NET/Avalonia modular architecture, canonical catalogue and isolated processing. Do not rewrite the app wholesale, introduce another frontend framework, create competing book identity, or use a visual redesign to bypass source/consent controls. Use current canonical skill engines listed in [the skills register](06-skills-sources-and-kaizen.md), rereading the matching phase's implementation skills before work.

## Sequence and deliverables

Effort is an initial estimate in combined engineering/design/QA person-days, not elapsed time, price or a commitment. Ranges assume reuse of current services; native containment, classroom stream redesign, accessibility and packaging may exceed them. Phase01 refines estimates after baseline and staffing. Due dates are relative: phase closure is due at its scheduled end; calendar dates require an actual start and team capacity.

| Phase | Detailed brief | Depends on | Estimated person-days | Accountable role |
|---|---|---|---:|---|
| 01 | [Scope, evidence and traceability](phases/phase-01.md) | None | 3-5 | Product/requirements lead |
| 02 | [Dismissal, visibility and command palette](phases/phase-02.md) | 01 | 2-4 | Desktop engineer |
| 03 | [Shell and task navigation](phases/phase-03.md) | 02 | 5-8 | UX lead + desktop engineer |
| 04 | [Visual system, type and icons](phases/phase-04.md) | 03 concept | 4-7 | Design lead |
| 05 | [First use, import and processing](phases/phase-05.md) | 03,04 | 5-8 | Ingestion + desktop engineer |
| 06 | [Catalogue and collections](phases/phase-06.md) | 04,05 | 5-8 | Catalogue engineer |
| 07 | [Right book inspector overhaul](phases/phase-07.md) | 02,04,06 contracts | 5-8 | UX lead + desktop engineer |
| 08 | [Reader core and reading workspace](phases/phase-08.md) | 03,04,07 | 8-13 | Reader engineer |
| 09 | [Annotations, citations and reading memory](phases/phase-09.md) | 08 | 6-10 | Reader/data engineer |
| 10 | [Metadata, identity and safe writeback](phases/phase-10.md) | 05-07 | 6-10 | Metadata engineer |
| 11 | [Full-text, OCR and activity operations](phases/phase-11.md) | 05,08,10 | 7-12 | Search/worker engineer |
| 12 | [Semantic and hybrid retrieval](phases/phase-12.md) | 10,11 | 6-10 | Retrieval engineer |
| 13 | [AI settings, privacy and cost controls](phases/phase-13.md) | 03,04,01 privacy contract | 7-12 | AI/security engineer |
| 14 | [Advisor, local answers and reading plans](phases/phase-14.md) | 08,12,13 | 6-10 | AI/product lead |
| 15 | [Optional 3D library](phases/phase-15.md) | 06,08,12 | 6-10 | 3D/native engineer |
| 16 | [Classroom Host and publication](phases/phase-16.md) | 05,10,13 | 7-12 | Networking/security engineer |
| 17 | [Classroom Client, reading and offline privacy](phases/phase-17.md) | 08,09,16 | 8-14 | Client/data engineer |
| 18 | [School administration and managed AI](phases/phase-18.md) | 13,14,16,17 | 6-10 | School product/security lead |
| 19 | [Accessibility, localization and OS parity](phases/phase-19.md) | All relevant surfaces01-18 | 8-13 | Accessibility QA lead |
| 20 | [Security, recovery, performance and packaging](phases/phase-20.md) | 01-19 feature complete | 10-18 | Security/release lead |
| 21 | [Independent acceptance and Kaizen handover](phases/phase-21.md) | All previous gates | 5-8 + pilot window | Peter + independent QA |

These are work packages, not a rigid single-pass waterfall. Discovery and native-platform preparation begin in phase01; accessibility and security apply to every phase. Phases19/20 collect system-wide proof rather than postpone quality work until the end. After common contracts stabilize, metadata, AI governance and classroom infrastructure can proceed in independent branches with disjoint ownership. Do not parallelize competing edits to MainShellViewModel, CatalogueShellView or ReaderView.

## How to execute a phase

Each linked brief states outcome, findings/requirements, source files, work slices, state/failure cases, acceptance evidence, owner, estimate, dependencies and rollback. At implementation kickoff, bind it to a commit and assigned people. Use the following common closure record:

```text
Phase / task / owner / reviewer / start / finish
Finding IDs and unchanged canonical requirement IDs
Baseline evidence and product hypothesis
Changed files and dependency contracts
Normal, failure, interrupted and recovery tests
Windows/macOS and assistive-technology result, or NOT ASSESSED
Artifact digest, screenshots, test logs and source versions
Measure before -> after, residual risk, rollback rehearsal
Reviewer decision and next remeasurement date
```

Tasks are complete when the acceptance evidence exists, not when code compiles. A phase may contain completed implementation with open platform acceptance; state both. No production flag, legal waiver, reviewer signature or customer pilot is inferred from Peter's authorization to conduct this audit.

## Critical journey acceptance

The following proposed acceptance targets supplement the canonical SRS. They are design/test decisions to baseline in phase01, not measurements achieved today. Never silently weaken a stricter existing NFR.

| Journey | Proposed pass oracle |
|---|---|
| Start and dismiss | Fresh start has no palette/inspector; all opened transient surfaces dismiss through visible action and Escape; focus returns; no data changes from dismissal |
| Import and understand progress | Chosen folder imports legally controlled fixture set; discovered/imported/skipped/failed counts reconcile to unique files; cancellation and restart do not duplicate books or lose state |
| Find and inspect | Named book found by title/author/content; selected identity stays consistent; close inspector restores viewport, sort/filter/query and focus |
| Read and resume | Known 3-page and representative PDFs render expected content; navigation and page label agree; modes, zoom and position survive reopening; offline core reading works |
| Study and export | Highlight/note/bookmark selection survives close/reopen, rotation/zoom and app restart; supported export parses and matches source; originals unchanged |
| AI and privacy | Offline/default core works with zero provider calls; preview/tier/consent exactly match captured transport; missing price never reads as free; erasure removes scoped derived records |
| School use | Publish one permitted subset; enroll/revoke two profiles; no cross-profile notes/cache leakage; loss/reconnect preserves permitted state; no unapproved full-file retention |
| Recovery and release | Signed release candidate installs, upgrades and recovers on both target OSes; backup/restore preserves identity/notes; malicious/corrupt input fails within limits |

## The 95% target contract

The 40% publication cap applies to this baseline audit only. Future acceptance uses the uncapped index so improvement can be demonstrated. Keep the same category weights from the audit and allow evidence-backed fractional ratings during independent final scoring, with an explicit checklist denominator per category established in phase01.

**To report 95/100:**

1. Independent weighted score >=95, with every category >=90% of its own acceptance checklist and no denominator removed to inflate results.
2. Zero open P0, security/data-loss/privacy/critical accessibility blockers; no unassessed mandatory Windows/macOS release gate.
3. All 162 canonical obligations accounted for: accepted evidence or an explicitly approved scope change/deferral. A deferred optional capability must not be advertised or count as accepted implementation. No silent retirement of classroom, 3D or reader requirements.
4. Every critical journey passes on Windows and macOS in the release artifact. Usability acceptance: at least 20 representative participants across reader, librarian, teacher/admin and accessibility needs, 10 defined tasks each, >=190/200 unassisted successes, with no individual critical task below90% and no severe accessibility failure. This is an observed sample target, not population-wide95% proof; report confidence limits and cohort composition.
5. AI holdout quality meets the agreed canonical/proposed contract: report relevance, citation support, abstention, latency, cost and safety separately. As an initial proposed floor, >=95% of displayed factual claims supported by cited local evidence on the held-out set, 100% valid local book/page references, zero cross-profile disclosure; independently adjudicate failures. Reference existing frozen search-contract thresholds before choosing retrieval metrics.
6. NFR performance is measured on W-REF-01/M-REF-01 or owner-approved equivalents with documented differences: retain the SRS's separate2k and50k scales and LAN budgets. Do not combine fast small-fixture tests with target-scale claims.
7. A time-boxed school/personal beta observation window completes without unresolved release blockers; signed owner, school-controller where applicable and independent technical acceptance are linked.

The user-success sample, claim-support target and phase estimates above are proposed planning thresholds. They are not facts about existing users, performance or market demand. If phase01 chooses a different valid measure, record the reason before implementation and retain a comparison to this baseline.

## Crosswalk to the existing 39-phase roadmap

This crosswalk preserves every old phase's accountability. It groups remediation by user outcome; it does not repeat the known erroneous detailed mappings in the old matrix. Phase01 must reconcile those against the exact SRS text.

| Existing Aug-39 phase(s) | Main new phase(s) | Preserved obligation |
|---|---|---|
| 01 evidence/scope | 01,21 | Baseline, all162 IDs, acceptance integrity |
| 02 composition/startup | 02,03,05,20 | Recoverable startup and coherent runtime configuration |
| 03-04 identity/schema | 05,06,10,20 | Canonical identity, migration and durability |
| 05 roots/path security | 05,10,20 | Roots, relinking and safe path boundaries |
| 06-08 state/discovery/reconciliation | 05,11,20 | Durable processing, counts, cancellation/recovery |
| 09 duplicates/bibliography | 06,10 | Identity-aware grouping and curation |
| 10 PDF validation/containment | 08,20 | Untrusted input and resource isolation |
| 11 extraction/ISBN | 10,11,20 | Native extraction, provenance and real-corpus proof |
| 12-15 metadata/provider/review/writeback | 07,10,13,20 | Curation, privacy, protected overrides, backup/undo |
| 16 assets | 06,07,15,20 | Covers, thumbnails/spines, budgets and provenance |
| 17 worker reliability | 05,11,20 | Activity operations, restart/soak and diagnostics |
| 18 design/shell | 02-04,07,19 | Dismissal, navigation, visual system and accessibility |
| 19-20 catalogue/detail/reading state | 06,07,09 | Book selection, organization, state and file availability |
| 21 reader | 08,09,17,19 | Modes/find/fullscreen, study tools, portability and remote reading |
| 22-24 search/FTS/OCR | 08,11,19,20 | Exact/fuzzy/full-text, page targets, native OCR |
| 25-26 embeddings/retrieval | 12,14,20 | Versioned index lifecycle, quality and scale |
| 27 AI gateway | 13,18,20 | Provider configuration, privacy, spend and retention |
| 28-30 advisor/answers/quality | 12-14,19 | Intent, explanations, local evidence and measured usefulness |
| 31-33 native3D/visuals/performance | 15,19,20 | Accessible optional spatial view with native fallback |
| 34 Host | 16,20 | Publication, TLS/trust, authentication, load and audit |
| 35 Client | 17,19,20 | Profiles, storage/stream contract, offline and sync |
| 36 School administration | 18,19,20 | Managed AI, quotas, school workflows and controller evidence |
| 37 security/privacy | 13,16-20 | Whole-system privacy, hostile input, secrets and recovery |
| 38 release/beta | 20,21 | Supported toolchain, signed packaging, beta and rollback |
| 39 acceptance/handover | 21 | Evidence-bound cross-platform release decision |

Requirements by family: LIB ->05/20; CAT ->06/07/10/15; META ->07/10/11; READ ->08/09/11/17/19; SEARCH ->08/11/12; AI ->12-14; LAN ->16/20; CLIENT ->17/19; ADMIN ->18; UX ->02-09/19; EXT ->01/09/21 scope disposition. All NFR and CTRL families retain owners across19-21 plus their domain phases. EXT plugin/local API/importer scope must be explicitly delivered or formally deferred; it is not implicitly implemented by having interfaces.

## Risk and dependency management

| Risk | Early action / accountable owner | Stop or rollback trigger |
|---|---|---|
| UI redesign becomes another cosmetic pass | UX lead tests complete tasks and minimum-window behavior each phase | A critical journey still blocked or new control overlap |
| Scope expands beyond PDF library | Peter reviews phase01 capability inventory | New format/cloud/LMS/payment scope without a decision |
| Service exists but route does not | Desktop engineer maintains executable route inventory | Advertised capability cannot be reached from a real menu/control |
| Native PDF/3D/OCR differs by OS | Release lead reserves Mac and reference hardware in phase01 | Unsupported native assets, crash or unverified containment |
| Streaming correction changes privacy promise | Client/security lead resolves ADR before phase17 implementation | Full files retained contrary to approved contract or cross-profile access |
| AI spending/quality appears better than measured | AI lead maintains price/source/evaluation versions | Unknown cost, unbounded paid request, unsupported answer or privacy breach |
| Long programme accumulates conflicting plans | Requirements lead maps every accepted change to canonical IDs | Duplicate completion authority or unexplained status drift |

## Handoff policy

Do not close these phases by editing completion prose alone. After each bounded change, inspect the actual diff, execute relevant normal/failure checks, review rendered/native behavior, retain the evidence and record a reviewer decision. Regressions reopen the phase. Maintain rollback for data/schema changes independently from binary rollback.

The first implementation milestone is a clean, dismissible, readable shell with an unobstructed synthetic PDF. The final milestone is a safe, accepted Windows/macOS library that readers and school operators can use without understanding its internal processing architecture.
