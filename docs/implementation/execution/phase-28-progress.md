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
- Added a durable, privacy-preserving `advisor-trace-v1` audit event for
  recommendation runs, retaining query hash, interpreted intent, bounded
  candidate IDs, result IDs, provider/model and outcome without raw query text.
- Extended the trace with bounded candidate-stage counts for catalogue,
  payload, provider, provenance validation, hybrid ranking, and final results,
  making empty or reduced recommendation runs diagnosable without retaining
  raw prompts or provider content.
- Updated the stale pre-V2 advisor scaffold test to assert its current honest
  unavailable response.
- Made the interpreted-intent editor explicitly two-way bound and verified that
  changing the query recomputes the displayed intent before recommendations are
  requested.
- Added bounded local comparison-reference resolution and deterministic
  author/category/tag overlap signals; the resolved reference is excluded from
  recommendations and unavailable references fail closed to the existing
  candidate path.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore` passed with
  0 warnings and 0 errors.
- Phase 28 intent/retrieval/ranking/fallback slice: 13 passed.
- Adjacent advisor pipeline, composition and service slice: 23 passed.
- Advisor stage-diagnostics trace slice: 1 passed.
- Editable interpreted-intent slice: 1 passed, with the rendered advisor route
  already covered by the adjacent UI test.
- Local reference-resolution/reranking slice: 14 passed; latest full isolated
  solution suite: 883 core + 41 architecture + 142 UI = 1,066 passed, 0 failed,
  0 skipped. See `evidence/phase-28-reference-resolution-2026-09-04.md`.

## Remaining phase gate

Human-labelled benchmark Recall@K/nDCG evaluation, reference-machine
confirmation, and final advisor UI/performance gates remain for phases 29-30.
Local reference resolution, editable intent, candidate-stage diagnostics, and
source-labelled evidence assembly are closed by the versioned evidence records
above and in Phase 29.
