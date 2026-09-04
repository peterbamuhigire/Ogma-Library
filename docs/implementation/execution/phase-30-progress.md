# Phase 30 Progress - Advisor UX and Quality Evaluation

Date: 2026-08-30

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

## Remaining phase gate

Feedback consent, evaluation-set/run persistence, human-labeled offline
thresholds, quarantined live-provider evaluation, full-shell
accessibility/keyboard evidence, and final AI retrieval freeze remain before
Phase 30 closure. Physical file-picker walkthrough evidence is still a
platform/release gate.
