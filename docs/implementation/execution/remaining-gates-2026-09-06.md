# Phases 7-39 remaining-gate register

Date: 2026-09-06

Authority: the Definition of Done in each approved Aug-39 phase plan. The phase
plans and their progress records remain authoritative when this summary and a
phase-specific record differ.

## Reconciliation result

The 33 plans from Phase 7 through Phase 39 contain 165 Definition-of-Done
criteria. Repository inspection and executable evidence have closed 120. The
remaining 45 criteria are intentionally open; none is silently represented as
passing.

Twelve phases have all five repository-verifiable criteria checked: 7, 8, 9,
12, 15, 20, 22, 23, 25, 26, 28, and 29. A checked phase-plan criterion means
that criterion has evidence; it does not supersede a progress record that keeps
the overall phase in progress because physical, platform, legal, or release
acceptance remains outstanding.

## Open criteria and required closing evidence

| Phase | Open | Boundary that prevents honest closure | Required evidence |
| ---: | ---: | --- | --- |
| 10 | 2 | Cross-platform PDF containment and independent security acceptance | Denied network/filesystem/child-process escape probes on supported Windows and macOS builds; named security reviewer decision |
| 11 | 1 | Representative extraction resource budget | Approved real target-scale PDF corpus, machines, thresholds, and repeatable results |
| 13 | 1 | Privacy statement combines local implementation with legal/owner facts | Legal/owner approval, archived provider terms, live redirect/attribution observation |
| 14 | 1 | Physical accessibility journey | Keyboard and screen-reader execution on installed Windows and macOS builds |
| 16 | 1 | Reference-hardware asset budget | Large-library run with real GPU/UI rendering and accepted disk/memory thresholds |
| 17 | 1 | Whole-application crash recovery | Kill/restart rehearsal against the packaged application and durable queue on reference machines |
| 18 | 2 | Concurrently edited literal-bearing surfaces; physical accessibility | Reconcile remaining direct font/color/string literals after owner edits settle; Windows keyboard/Narrator and macOS VoiceOver evidence |
| 19 | 1 | Physical catalogue accessibility | End-to-end keyboard and screen-reader flow on installed builds |
| 21 | 4 | Reader-wide FR trace, round-trip/crash, accessibility, and platform budgets | Requirement-linked installed viewer journeys on both OSes, annotation/export recovery, assistive technology, and approved resource results |
| 24 | 2 | Representative OCR acceptance and unsupported macOS native package | Approved mixed real-PDF corpus accuracy/resource results; supported packaged Tesseract runtime on both platforms |
| 27 | 1 | Combined OS-secret, cost, and deletion journey | Physical credential-store lifecycle, enforced budget, deletion, and provider conformance evidence |
| 30 | 2 | Live-provider evaluation and physical accessible rendering | Approved offline/live corpus thresholds plus keyboard/screen-reader visual journey |
| 31 | 2 | Native embedded browser execution | Packaged WebView2/WKWebView/WebGL2 render and crash/reload/fallback drills on physical Windows and macOS hosts |
| 32 | 1 | Visual acceptance | Accepted screenshots and interactions from both packaged platform builds |
| 33 | 2 | Real renderer performance | GPU/WebView telemetry and a 500-book frame-budget run on approved reference hardware |
| 34 | 2 | Real LAN topology and combined load/privacy acceptance | Two-machine Windows/macOS firewall, discovery, TOFU, hostile-client, load, and privacy matrix |
| 35 | 3 | Physical credentials, network transitions, accessibility, and load | Pairing in OS credential stores; browse/stream/offline/reconnect on both OSes; assistive-technology and load results |
| 36 | 2 | Combined managed-AI lifecycle and formal governance approval | Physical quota/audit/retention/erasure journey; approved DPIA/minors treatment and backup/restore evidence |
| 37 | 5 | Final security/privacy assurance aggregates independent and physical proof | CTRL evidence/risk decisions, hostile boundary testing, both-OS secret/backup/export/erasure lifecycle, accurate approved data flow/DPIA, and P0/P1 disposition |
| 38 | 4 | Release engineering requires signed artifacts and reference environments | Accepted SLO run, signed Windows and notarized macOS clean installs, upgrade/rollback/migration drills, schema/evidence-pack owner approval |
| 39 | 5 | Final release acceptance and handover | Complete requirement disposition, final quality-gate approval, installed-build journeys, P0/P1 and residual-risk acceptance, artifact promotion and operational handover |

## Closing policy

- `NOT ASSESSED` remains the result when the required machine, signed artifact,
  corpus, live provider, legal decision, or accountable approver is absent.
- Hosted CI is cross-platform source/build/test evidence, not evidence of a
  human operating a signed installed application on reference hardware.
- Synthetic fixtures are retained as regression protection but do not replace
  an explicitly required representative or hostile third-party corpus.
- A combined criterion closes only when every clause is evidenced.
- Owner authorization to perform work is not substituted for a recorded owner
  acceptance decision where the plan explicitly requires approval.

## Next executable sequence

1. Produce signed immutable Windows and macOS release candidates from one
   evidence-bound commit.
2. Run the Phase 10, 14, 17-19, 21, 24, and 27 physical platform matrices.
3. Run reference corpus/hardware budgets for Phases 11, 16, 24, 30, and 33.
4. Run the two-machine classroom and managed-AI journeys for Phases 34-36.
5. Complete independent security, DPIA/legal, release-schema, residual-risk,
   and final handover approvals for Phases 13 and 37-39.

This sequence requires external machines, credentials, signing/notarization
material, approved corpora, and accountable reviewers. It must not be simulated
from repository state.
