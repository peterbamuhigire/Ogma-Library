# Phase 28 Progress - Advisor Intent, Candidates and Reranking

Date: 2026-08-30

## Delivered in this increment

- Added deterministic `advisor-intent-v1` extraction for topic, mood, difficulty,
  length, comparison, combined, negative and broad-discovery requests.
- Made structured intent part of every `RecommendationQuery` and included the
  interpreted constraints in the bounded metadata prompt payload.
- Changed advisor retrieval from literal-only gating to a union of metadata and
  semantic search IDs, with authoritative local detail/status revalidation.
- Added page-count extraction and known-length filtering while retaining books
  whose length metadata is unknown.
- Added deterministic candidate scoring with negative exclusions, difficulty and
  mood signals, stable ordering, and broad-discovery author diversity.
- Added grounded `local-advisor-v1` cards when the AI provider is disabled or
  violates the active provider boundary. Explicit preview cancellation and
  missing cloud consent remain visible policy decisions.
- Updated the stale pre-V2 advisor scaffold test to assert its current honest
  unavailable response.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed with
  0 warnings and 0 errors.
- Phase 28 intent/retrieval/ranking/fallback slice: 13 passed.
- Adjacent advisor pipeline, composition and service slice: 23 passed.

## Remaining phase gate

Durable versioned advisor request/intent/candidate traces, editable interpreted
intent UI, candidate-stage diagnostics, reference-book resolution beyond the
deterministic comparison hint, source-labeled evidence assembly, benchmark
Recall@K/nDCG evaluation, and final advisor UI/performance gates remain for
phases 29-30.
