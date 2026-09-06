# Phase 30 Progress - Advisor UX and Quality Evaluation

Date: 2026-09-04

## Delivered in this increment

- Integrated reachable Advisor and Reading Plan routes into the main desktop
  shell, with navigation owned by `MainShellViewModel` and provider logic kept
  behind application contracts.
- Added an interpreted-intent panel to the recommendation surface so users can
  see the topics, exclusions, difficulty, length, mood, comparison and broad
  discovery signals before acting on results.
- Added source/uncertainty presentation to recommendation cards while retaining
  qualitative confidence bands and avoiding false-precision AI confidence UI.
- Added a deterministic offline evaluation harness reporting Precision@K,
  Recall@K, MRR, nDCG, grounding, constraint satisfaction and diversity for
  versioned labeled cases.
- Added UI and offline-metric regression coverage, including accessible labels,
  degraded local behavior and route rendering.
- Added caller-owned JSON export for non-deleted advisor history, preserving
  the existing hard-delete path and keeping immutable audit export separate.
- Added a Privacy Center save-file flow for advisor-history export with a JSON
  filename/type contract; cancellation and unavailable platform storage leave
  the user in place without creating a partial file.
- Added explicit `AdvisorEvaluationThresholds` and a fail-closed evaluation
  gate. Empty evaluation sets fail, every reported metric is checked against an
  approved lower bound, and invalid thresholds are rejected.
- Added a consent-gated, privacy-minimized advisor feedback store. It accepts
  only a request hash, rating, and bounded reason code, applies a 90-day local
  retention window and entry bound, and persists atomically; focused tests
  cover consent denial, bounds, reload, and raw-content exclusion.
- Added an explicit consent and one-to-five rating control to the desktop
  advisor answer surface. The UI submits only a SHA-256 request hash and the
  bounded rating through the existing feedback service.
- Added an explicit, unchecked-by-default consent control for local page and
  note evidence; its request boundary is covered by default-deny and rendered
  headless UI tests.
- Closed the retrieval-contract dependency against Phase 26's executable v1
  freeze (`semantic-search-v1`, `rrf-v1`, `hybrid-v1`, and versioned offline
  evaluation). Evidence:
  `evidence/phase-30-retrieval-contract-dependency-2026-09-06.md`.

## Design decisions

- The interpreted-intent panel is a compact, reversible review surface rather
  than a second prompt editor; the user can revise the original request and see
  the interpretation update.
- Evidence limitations are shown as text and source labels, not color alone or
  an unsupported confidence percentage.
- Advisor output remains useful offline, but evaluation metrics do not imply a
  release pass without a human-labeled benchmark set and approved thresholds.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed with
  0 warnings and 0 errors through the final test build.
- Offline quality and interpreted-intent slice: 5 passed.
- Advisor VM slice: 3 passed.
- Advisor/plan rendered UI slice: 1 passed.
- AI persistence/privacy slice: 11 passed, including visible-history-only
  export and Privacy Center export command coverage.
- Evaluation-set/run persistence slice: 5 passed, covering versioned runs,
  ranked results, relevance judgments, reports, replacement, load, and delete.
- Advisor view-model slice: 51 passed; headless recommendation/answer/feedback
  render: 1 passed; isolated Release build: 0 warnings and 0 errors.
- Current-head local gate reconciliation is recorded in
  `evidence/phase-30-local-gate-reconciliation-2026-09-04.md`.

## Remaining phase gate

The offline threshold and feedback-consent/UI gates are closed by focused
tests; the evaluator is ready to consume a real human-labeled set without
treating missing evidence as approval. Quarantined live-provider evaluation,
and full-shell accessibility/keyboard evidence remain before Phase 30 closure.
The final AI retrieval-contract dependency is closed by the Phase 26 v1 shape
and version guard. Physical file-picker walkthrough evidence is still a
platform/release gate.
