# Phase

Phase 1 — Evidence Baseline and Scope Freeze

# Status

COMPLETE — 2026-08-20

# Requirements Implemented

- Ratified exactly 101 functional requirements, 29 non-functional requirements
  and 32 controls from the canonical v2.1 SRS.
- Made all 162 unique IDs accountable through the approved requirement-phase
  matrix and a CI-enforced SRS-to-matrix comparison.
- Froze one Windows/macOS C# desktop application across standalone, classroom
  Host and classroom Client modes; excluded mobile, PWA and public-site delivery.
- Retained FR-EXT-002 and FR-EXT-003 in the assigned roadmap phases.

# Major Code Changes

- Added [`scripts/Test-RequirementAccountability.ps1`](../../../scripts/Test-RequirementAccountability.ps1)
  and wired it into the Windows/macOS CI matrix.
- Corrected [`CLAUDE.md`](../../../CLAUDE.md) so contributors use the 39-phase
  roadmap, current evidence rules and locked verification commands.
- Added actionable response diagnostics to the concurrent LAN catalogue load test.
- Upgraded the direct esbuild development dependency from 0.28.0 to 0.28.2 to
  resolve GHSA-g7r4-m6w7-qqqr, then made npm audit, 3D typecheck, deterministic
  bundling and the performance budget part of cross-platform CI.
- Created the master execution ledger and Phase 1 evidence pack.

# Database Changes

None. The current EF migration directory was recorded as Git tree
`9f8a12e0e4dba07ca41e33ec5e90c3f4214a9c57`.

# Pipeline Changes

CI now fails if SRS counts drift, an SRS requirement is unassigned or the roadmap
matrix contains an unknown requirement ID.

# Search Changes

No runtime change. Current 512-token/64-token-overlap assumptions and the search
source tree were recorded; the search contract remains unfrozen until Phase 26.

# AI/RAG Changes

No runtime change. Existing advisor/answer-mode functionality is explicitly
non-release until Phases 27–30. Personal notes are excluded from external AI
payloads by default under the freeze decision.

# UI Changes

None. Existing UI tests establish a baseline, not Phase 18/19 completion.

# 3D Changes

None. Existing 3D assets/tests are classified as scaffolding until Phases 31–33.

# Security/Privacy Changes

- Defined public, operational, confidential and private-library evidence classes.
- Prohibited credentials, private paths, PDF passages, notes and full prompts in
  execution evidence.
- Recorded unavailable platform/provider/signing gates as `NOT ASSESSED`.

# Tests Added

- Cross-platform requirement-accountability gate for all 162 SRS IDs.
- Enhanced LAN load-test failure diagnostics without weakening its HTTP 200,
  content or latency assertions.
- Added CI gates for 3D dependency audit, TypeScript compilation, deterministic
  bundle generation and performance budgets.

# Evaluations Performed

- Read the full 39-phase roadmap and all appendices before implementation.
- Reconciled the roadmap against the v2.1 SRS and current audit.
- Classified competing phase counts, internal document labels, reader mapping,
  notes egress, public-site scope, writeback, advisor and 3D claims.
- Verified the requirement gate locally.

# Performance Results

- Initial Release build: 43.81 seconds, 0 warnings, 0 errors.
- Complete regression: 800/800 tests passed on the current Windows host.
- LAN-host cohort: 59/59 passed.
- Concurrent catalogue load: 12 consecutive focused passes at 20 clients; the
  test's p95 budget remains under 2,000 ms.
- 3D synthetic budget on this host: shelf p95 0.143 ms; grid3d p95 0.103 ms.
- NuGet and npm audits: zero known vulnerabilities after the esbuild upgrade.
- Final post-change Release build: 17.44 seconds, 0 warnings, 0 errors; format,
  analyzer and high-confidence secret scans passed.
- Physical `W-REF-01`, `M-REF-01`, macOS, GPU and signing results remain
  `NOT ASSESSED` and are assigned to later phases.

# Deviations From Plan

- The supplied execution prompt says “36-phase” in its title while the exact
  plan directory and owner correction define 39 phases. The repository plan is
  followed exactly: 39 phases.
- The audit anchors source findings to `5514276`; Phase 1 execution begins from
  `de8a424`, preserving both provenance points.
- Digital research was not invoked because Phase 1 makes no volatile external
  provider or standards claim. Live provider/terms evidence is assigned to the
  phases that exercise it.

# Deferred Findings

- One initial full-suite run returned HTTP 500 in the 20-client LAN catalogue
  smoke test. It did not recur in 12 focused runs, the 59-test LAN cohort or the
  next complete run. It remains a Phase 17 reliability watch item; diagnostics
  now capture the response body if it recurs.
- macOS CI/physical testing, provider contracts, signing, assistive technology,
  AI relevance and physical performance are `NOT ASSESSED`, with later owners.
- Historical source comments that use old phase numbering will be removed when
  their subsystem is changed, avoiding a risky repository-wide cosmetic rewrite.

# Kaizen Cleanup

- Replaced competing contributor status guidance with one authority chain.
- Avoided a second manually maintained 162-row registry by making the canonical
  SRS-to-roadmap relationship executable.
- Centralized phase truth in `00-execution-status.md`.
- Converted a low-information LAN assertion into actionable recurrence evidence.
- Preserved reference documents and audit history rather than rewriting sources.

# Definition of Done Verification

- [x] Owner approved the scope/conflict log through the 2026-08-20 execution directive.
- [x] All 101 FRs, 29 NFRs and 32 controls have accountable IDs and phases.
- [x] Evidence manifest identifies commit, environment, hashes and exclusions.
- [x] Stale contributor guidance is corrected and historical claims are subordinated.
- [x] Freeze decision `OGMA-FREEZE-2026-08-20-01` is recorded.
- [x] Automated baseline is green on the current Windows host.
- [x] Unavailable evidence is labeled `NOT ASSESSED` rather than passed.
