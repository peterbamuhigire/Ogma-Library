# Phase 13 Code Review Evidence

Date: 2026-06-01
Scope: Phase 13 AI Reading Advisor and Plans, including domain records, gateway
pipelines, hybrid ranking merge, reading-plan parser, advisor service, Avalonia
surfaces, evaluation harness, extension markers, and WP11 structural tests.

## Review Checks

| Area | Result |
| --- | --- |
| Local-only provenance | Passed. Recommendation and reading-plan parsers validate returned book ids against local candidates. Hallucinated recommendation provenance is stripped or replaced with deterministic local fallback. |
| Privacy tier behavior | Passed. `AdvisorService` fails closed when the active tier is Offline, and UI view models surface a contained error state. |
| Provider boundary | Passed. Application advisor code depends on `IAiGateway` and pipeline contracts, not provider adapters. Architecture tests cover this. |
| Extension API exposure | Passed. `IRecommendationSource` and `IAiCatalogueReader` are internal and marked `[ExtensionPoint]`; architecture tests prevent accidental public exposure before Phase 23. |
| Structured output handling | Passed. Recommendation and reading-plan output is parsed from strict JSON and structurally validated before display. Reading plans retry once on invalid provider output. |
| UI accessibility basics | Passed. Advisor cards and plan steps have accessible names; query/goal fields and action buttons now expose automation names; render tests cover both surfaces. |
| Evaluation reproducibility | Passed. WP9 uses deterministic mock fixtures and commits the benchmark result. |

## Findings

No blocking defects found in the Phase 13 closeout review.

## Residual Notes

- Recommendation and reading-plan view models still use the Phase 13 placeholder
  provider/model binding (`openai` / `gpt-test`). This is acceptable for the
  current internal surface because provider selection is owned by the broader AI
  settings flow, but it should be replaced with a settings-backed binding before
  end-user release.
- Answer mode intentionally remains a V2 scaffold and throws the documented
  `NotImplementedException`.
