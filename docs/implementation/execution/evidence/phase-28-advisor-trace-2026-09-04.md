# Phase 28 Evidence — Advisor Execution Trace

Date: 2026-09-04
Scope: durable advisor request, intent, candidate, result and outcome trace

Recommendation runs now append an immutable `AdvisorExecutionTrace` audit
event. The trace is versioned (`advisor-trace-v1`), stores a SHA-256 query
hash rather than the raw request, records the deterministic interpreted intent,
caps candidate IDs at 50, records result IDs, and identifies provider/model and
outcome. This makes candidate-stage behavior inspectable without copying the
user’s request into the audit payload.

`Phase28AdvisorTraceTests.RecommendationPipeline_PersistsVersionedTraceWithoutRawQuery`
verifies the provider-success path and its privacy boundary.

Remaining Phase 28–30 gates include editable intent UI, candidate diagnostics,
reference resolution, source-labeled provider evidence, benchmark evaluation,
and final accessibility/performance proof.
