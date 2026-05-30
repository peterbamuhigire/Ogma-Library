# Phase 00 — Tasks

> Work packages and tasks for Decision Closure & Project Inception.
> ID format: `P00-WP<n>-T<m>`. Each task lists: description, dependencies,
> rough estimate (days), and the requirement / ADR / context-gap IDs it
> satisfies.

---

## WP1 — Answer all 8 PRD open questions

**Goal:** produce a signed decision for every OQ in PRD §10 so no phase-blocking
ambiguity remains.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP1-T1 | Draft the OQ-01 decision brief: .NET 10 LTS vs .NET 8. Confirm .NET 10 LTS (supported to 2028-11-14, ADR-0001). Record the .NET 8 bridge policy (documented temporary bridge only). | Signed PRD | 0.25 d | OQ-01, ADR-0001 |
| P00-WP1-T2 | Draft the OQ-02 decision brief: PDFium wrapper choice. Confirm the 2-wrapper benchmark requirement (ADR-0004) for Phase 01. Note that final selection is spike-gated; record the two candidate wrappers (PdfiumViewer.WPF port and PDFiumSharp or PdfPig render path) as the Phase 01 spike input. | Signed SRS, HLD | 0.25 d | OQ-02, ADR-0004 |
| P00-WP1-T3 | Draft the OQ-03 decision brief: annotation write-back strategy. Confirm DB-first, PDF write-back later (ADR-0008); record the backup→diff→verify→restore protocol that governs any future PDF mutation. | HLD §F | 0.25 d | OQ-03, ADR-0008, NFR-PROD-010 |
| P00-WP1-T4 | Draft the OQ-04 decision brief: thumbnail/spine storage. Confirm sidecar asset folder (ADR-0005); record the CON-6 sidecar naming convention as a dependency (see WP2-T6). | HLD §F | 0.25 d | OQ-04, ADR-0005 |
| P00-WP1-T5 | Draft the OQ-05 decision brief: FTS in MVP. Confirm defer to V1 (FR-SEARCH-002 is V1); record that metadata search (FR-SEARCH-001, NFR-OGMA-003) is MVP. | PRD §9, SRS | 0.25 d | OQ-05, ADR-0006, FR-SEARCH-001/002 |
| P00-WP1-T6 | Draft the OQ-06 decision brief: OCR in MVP. Confirm defer to V1 (FR-READ-010 is V1). | PRD §9 | 0.1 d | OQ-06, FR-READ-010 |
| P00-WP1-T7 | Draft the OQ-07 decision brief: EPUB/CBZ. Confirm post-V1 scope; note schema-readiness requirement (no blocking schema additions needed at MVP). | PRD §10 | 0.1 d | OQ-07 |
| P00-WP1-T8 | Draft the OQ-08 decision brief: cloud sync. Confirm excluded from MVP/V1; confirm schema-ready design is required (no DPIA executed until cloud sync feature is scoped; DPIA prereq recorded). | PRD §10, SRS CI-1 | 0.1 d | OQ-08, CTRL-OGMA (privacy), FR-AI-008 note |
| P00-WP1-T9 | Compile WP1 decisions into `decisions.md` §OQ section; prepare one-page decision brief for owner sign-off session (WP6). | P00-WP1-T1..T8 | 0.5 d | All OQ entries |

---

## WP2 — Assign values to all SRS context gaps

**Goal:** every CON-N gap has a concrete value or a formal deferral with a
phase-gate target so downstream phases can proceed unambiguously.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP2-T1 | **CON-1 — Reference hardware.** Specify both reference machines: (a) Windows: e.g. Core i5 (2020+), 8 GB RAM, SATA SSD, 1080p display, Windows 10 21H2; (b) macOS: e.g. M1 MacBook Air 8 GB, macOS 13.0, 2560x1600 Retina. Anchor NFR-OGMA-001..009 budgets to these specs. Record in `docs/governance/REFERENCE-HARDWARE.md`. Owner must confirm or adjust the spec. | Owner availability | 0.5 d | CON-1, NFR-OGMA-001..009, NFR-PROD-002/003 |
| P00-WP2-T2 | **CON-2 — Install minimums.** Define minimum supported OS versions: Windows 10 version 1903 (build 18362, WebView2 availability); macOS 13.0 Ventura (WKWebView WebGL2 maturity, ADR-0003). Define minimum RAM (8 GB) and disk space (500 MB for app + sidecar, up to 10 GB for large libraries). Record in `decisions.md`. | P00-WP2-T1 | 0.25 d | CON-2, ADR-0002, ADR-0003 |
| P00-WP2-T3 | **CON-3 — Linux MVP scope.** Formally decide: Linux = "community bonus, not an MVP gate, not a V1 release target." Record the basis (Avalonia does support Linux; the blocker is WebView2 dependency for 3D shelf, ADR-0003). CI may include a best-effort Linux build job from Phase 02 onward. Record in `decisions.md`. | ADR-0003 Proposed | 0.25 d | CON-3, ADR-0003 |
| P00-WP2-T4 | **CON-4 — Command-palette command set.** Produce a first-pass enumeration of ~30 MVP command-palette entries covering: library management (scan, rescan, open folder, preferences), navigation (go to book, go to shelf, reader navigation), search (quick search, FTS when available), AI (ask advisor, disable AI, privacy settings), and application commands (keyboard shortcuts help, about, check for updates). This list is the Phase 03 command-palette backlog input. Owner must approve. | Owner availability, PRD §6 | 0.5 d | CON-4, FR-CAT-001, Phase 03 scope |
| P00-WP2-T5 | **CON-5 — Work/Edition cardinality & merge/split rules.** Define: a "Work" is an abstract bibliographic entity; an "Edition" is a specific manifestation; a "BookFile" is a physical PDF. One Work may have N Editions; one Edition may have N BookFiles (e.g. multiple scans). Merge rule: user explicitly merges two Work records; system never auto-merges without confirmation. Split rule: user explicitly splits an Edition from a Work; sidecar assets follow. Record in `decisions.md`; this is an input to Phase 04 domain model. | HLD §F, Owner | 0.5 d | CON-5, Phase 04 domain model |
| P00-WP2-T6 | **CON-6 — Sidecar naming convention per class.** Define the sidecar directory structure: `<library-root>/.ogma/books/<book-id>/covers/`, `.../thumbnails/`, `.../spines/`, `.../ocr/`, `.../extracted-text/`, `.../embeddings/`, `.../backups/`. The `<book-id>` is the stable identity hash. Record in `decisions.md` and as an amendment note to ADR-0005. | ADR-0005 Proposed | 0.25 d | CON-6, ADR-0005, FR-LIB-003/005 |
| P00-WP2-T7 | **CON-7 — Target jurisdictions & Data Protection Acts.** Using `security:dpia-generator` and `security:uganda-dppa-compliance`: identify applicable laws (Uganda DPPA 2019 as primary for Chwezi Core Systems; GDPR as secondary for EU data subjects; applicable US state laws if distributed on US stores). Record the legal basis for each off-device feature class. Note that classroom mode (Phases 16-18) processes minors' data — elevated compliance bar flagged as a tracked item for Phase 19 DPIA. Record in `decisions.md`. | Owner availability | 0.5 d | CON-7, CTRL-OGMA-024, CTRL-OGMA-001, Phase 19 DPIA |
| P00-WP2-T8 | **CON-8 — Provider trust weights & field-match scoring.** Define the initial confidence model: (a) field-level trust hierarchy for metadata merge (ISBN-validated > Google Books > Open Library > filename heuristic > PDF DocInfo > user-entered); (b) numerical weight ranges per source (e.g. Google Books ISBN match = 0.95, filename heuristic = 0.30); (c) merge policy (highest-confidence field wins; ties prompt user; user-override always wins at 1.0). Record in `decisions.md` §CON-8; this is a Phase 07 implementation input. | HLD §F, PRD §7 | 0.5 d | CON-8, FR-META-001..003, FR-META-007 |
| P00-WP2-T9 | **CON-9 — Corpus licensing provenance.** Produce a manifest of the planned golden-corpus PDF files. For each file, record: title, source, license (public domain / CC0 / CC-BY / owner-supplied synthetic). Confirm every file is cleared for inclusion in the test suite (including any CI/CD artifacts that may be published). Replace any uncleared file with a synthetic equivalent before Phase 02. Record in `docs/testing/GOLDEN-CORPUS-MANIFEST.md`. | Owner availability | 0.5 d | CON-9, SOURCE-SUMMARY §J golden corpus |
| P00-WP2-T10 | Compile WP2 results into `decisions.md` §CON section; include action items for owner approval in WP6. | P00-WP2-T1..T9 | 0.5 d | All CON entries |

---

## WP3 — Ratify ADRs and draft ADR-0010

**Goal:** every ADR (0001..0009) is in `Accepted` status with a recorded
ratification date; ADR-0010 is drafted as `Proposed` ready for the LAN spike.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP3-T1 | Review ADR-0001 (.NET 10 LTS) for completeness; add ratification section with date and owner name; change status to Accepted. Note .NET 8 bridge policy (from WP1-T1). | P00-WP1-T1 | 0.1 d | ADR-0001, OQ-01 |
| P00-WP3-T2 | Review ADR-0002 (Avalonia shell) for completeness; add macOS WKWebView note (ADR-0003 dependency); change status to Accepted. | Signed HLD | 0.1 d | ADR-0002, CON-2 |
| P00-WP3-T3 | Review ADR-0003 (WebView Three.js 3D, spike-gated) for completeness; add the Phase 01 spike acceptance criterion (WebGL2 on macOS 13, 60 FPS, ADR-0003 gate); change status to Accepted (with spike condition noted). | P00-WP2-T2/T3 | 0.1 d | ADR-0003, OQ-02 note, FR-CAT-001 |
| P00-WP3-T4 | Review ADR-0004 (PDFium behind adapter, 2-wrapper benchmark); add Phase 01 spike parameters (which two wrappers, which metrics — throughput, memory, license); change status to Accepted (amended by Phase 01 spike result). | P00-WP1-T2 | 0.1 d | ADR-0004, OQ-02 |
| P00-WP3-T5 | Review ADR-0005 (SQLite catalogue + sidecar); add CON-6 sidecar naming convention; change status to Accepted. | P00-WP2-T6 | 0.1 d | ADR-0005, OQ-04, CON-6 |
| P00-WP3-T6 | Review ADR-0006 (hybrid search); add OQ-05 FTS-deferred-to-V1 note; change status to Accepted. | P00-WP1-T5 | 0.1 d | ADR-0006, OQ-05, FR-SEARCH-001/002 |
| P00-WP3-T7 | Review ADR-0007 (provider-neutral AI gateway + 4 tiers); verify alignment with CON-7 jurisdiction and CTRL-OGMA egress controls; change status to Accepted. | P00-WP2-T7 | 0.1 d | ADR-0007, FR-AI-001/002/004, CTRL-OGMA |
| P00-WP3-T8 | Review ADR-0008 (DB-first annotations, PDF write-back later); add OQ-03 confirmation and CON-5 Work/Edition note; change status to Accepted. | P00-WP1-T3, P00-WP2-T5 | 0.1 d | ADR-0008, OQ-03, FR-READ-007/008 |
| P00-WP3-T9 | Review ADR-0009 (Velopack + MSIX + notarized DMG); add macOS notarization entitlement notes (disk access, WebView entitlement); add Windows Store MSIX packaging note; change status to Accepted. | Deployment & Ops doc | 0.1 d | ADR-0009, NFR-PROD-012, SOURCE-SUMMARY §K |
| P00-WP3-T10 | Draft ADR-0010 (opt-in Library Host mode; CI-2 amendment). Content: context (CI-2 "no inbound listener"), decision (Library Host mode is opt-in, LAN-bounded, explicitly started by admin, with its own threat model), consequences (new security ADRs in Phase 16-18, LAN transport spike in Phase 01). Status: Proposed. | LAN-CLASSROOM-ARCHITECTURE.md, P00-WP2-T7 | 0.25 d | ADR-0010, LAN-CLASSROOM §1, Phase 01 LAN spike |

---

## WP4 — Open-source readiness artifacts

**Goal:** the repo has a clean, owner-approved open-source governance foundation
before production code is written.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP4-T1 | Apply the chosen LICENSE file to the repo root. Use SPDX identifier in the file header. Confirm the license is compatible with App Store distribution (MSIX + Mac App Store). | Owner license decision (Owner ask #1) | 0.25 d | L.7 (SOURCE-SUMMARY §L.7), NFR-PROD-012 |
| P00-WP4-T2 | Draft `CONTRIBUTING.md`: how to fork, branch, commit (Conventional Commits), open a PR, run the test suite, and the CIA checklist. Reference `docs/governance/BRANCH-STRATEGY.md`. | P00-WP5-T1 | 0.25 d | L.7, open-source readiness |
| P00-WP4-T3 | Draft `CODE_OF_CONDUCT.md` using Contributor Covenant 2.1 template; fill in contact email for enforcement. | Owner | 0.1 d | L.7, open-source readiness |
| P00-WP4-T4 | Set up CLA mechanism: either CLA Assistant GitHub App (automated, recommended for future external contributors) or DCO (add `Signed-off-by` to commit message requirements). Record the choice in `decisions.md`. | Owner decision (Owner ask #7) | 0.25 d | L.7, CONTRIBUTING.md |

---

## WP5 — Repo governance setup

**Goal:** the repo has enforced Conventional Commits, a documented branch
strategy, a CIA workflow, and a verified hybrid validation gate.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP5-T1 | Write `.github/hooks/commit-msg` script (or Husky configuration) that validates Conventional Commits format. Write a `scripts/install-hooks.sh` (and PowerShell equivalent `scripts/Install-Hooks.ps1`) that contributors run once. Test: commit a non-conforming message → hook exits 1; commit a conforming message → exits 0. | None | 0.25 d | NFR-PROD-012, Conventional Commits governance |
| P00-WP5-T2 | Write `docs/governance/BRANCH-STRATEGY.md`: define `main`, `develop`, `feature/<ID>-<slug>`, `release/<semver>`, `hotfix/<semver>` branches; state merge rules (no FF on main; squash/merge on develop); state protection rules (main: require PR + CI green + owner review; develop: require PR + CI green). | None | 0.25 d | Governance, CIA workflow |
| P00-WP5-T3 | Write `docs/governance/CIA-WORKFLOW.md`: the Change Impact Analysis process. For every PR, the author must answer: (a) bounded contexts affected, (b) FR/NFR/ADR IDs touched, (c) does this change a baselined requirement? (owner sign-off + ADR amendment required if yes), (d) new strings externalized en+fr?, (e) new controls iconified + accessible? | P00-WP5-T2 | 0.25 d | Governance, global DoD §6/8 |
| P00-WP5-T4 | Write `.github/PULL_REQUEST_TEMPLATE.md` embedding the CIA checklist as a GitHub PR description template with checkboxes. | P00-WP5-T3 | 0.1 d | Governance |
| P00-WP5-T5 | Document and verify the hybrid validation gate: write `docs/governance/HYBRID-GATE.md` explaining `python -m engine validate Ogma-Library`; run it against the current repo state; confirm exit 0. Record the result (with timestamp) in `decisions.md`. | Hybrid gate engine installed | 0.25 d | SOURCE-SUMMARY §A hybrid gate, global DoD §6 |

---

## WP6 — Owner sign-off session

**Goal:** Peter Bamuhigire formally signs off on all open questions, context gap
values, ADR ratifications, and governance artifacts produced in WP1-WP5.

| ID | Task | Depends on | Est. | Satisfies |
| --- | --- | --- | --- | --- |
| P00-WP6-T1 | Prepare the sign-off brief: a single document summarizing each OQ decision, each CON-N value, the ADR status list, the governance setup, and the open-source artifacts. Maximum 2 pages of decisions; attach supporting detail as appendices. | P00-WP1..WP5 complete | 0.25 d | All OQ/CON/ADR entries |
| P00-WP6-T2 | Conduct the sign-off session with Peter (can be async via a PR review or sync meeting). Record: full name, date, and the specific items approved. Any items deferred get a new deadline. | P00-WP6-T1, Owner availability | 0.25 d | Owner sign-off requirement (README §7) |
| P00-WP6-T3 | Update `decisions.md` with the sign-off record (name, date, any conditions). Update ADR files with the ratification date. Merge the phase-00 feature branch. | P00-WP6-T2 | 0.25 d | decisions.md, ADR files |
| P00-WP6-T4 | Run the global DoD checklist for Phase 00 (all items in README §9). Record pass/fail for each item. File any open items as tracked GitHub issues before declaring Phase 00 closed. | P00-WP6-T3 | 0.25 d | Global DoD |
