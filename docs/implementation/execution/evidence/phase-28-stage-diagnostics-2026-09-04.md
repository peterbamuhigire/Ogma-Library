# Phase 28 advisor stage diagnostics evidence

Date: 2026-09-04

The versioned advisor trace now records bounded counts for catalogue candidates,
payload candidates, provider cards, provenance-validated cards, hybrid-ranked
candidates, and final cards. It continues to retain only a query hash and
bounded local IDs; raw query text and provider response content are not stored.
Fallback paths record the stages they reached and their final result count.

Verification: `Phase28AdvisorTraceTests` passed 1/1 and asserted the provider,
validated, and final stage counts in a persisted `advisor-trace-v1` event.

Remaining Phase 28/29/30 gates include editable intent UI, reference-book
resolution, source-labeled evidence assembly, human-labeled quality thresholds,
and physical accessibility/performance evidence.
