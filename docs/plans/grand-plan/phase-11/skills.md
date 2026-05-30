# Phase 11 — Skills

Skills and slash commands for Semantic Search & Embeddings.

---

## Always-on

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Write cosine unit tests and determinism test before implementation | Red tests first |
| `superpowers:verification-before-completion` | After each WP; benchmarks must pass before marking done | CI + benchmarks green |
| `superpowers:requesting-code-review` + `/code-review` | After WP3 (cosine math), WP4 (ranking), WP6 (erasure + spike) | Findings resolved |
| `superpowers:systematic-debugging` | If cosine SIMD gives wrong results or ranking is non-deterministic | Root-cause note |
| `superpowers:using-git-worktrees` | Per WP | Clean branches |
| `documentation-generation:architecture-decision-records` | P11-WP6-T6 — ADR-0006 ANN amendment stub | ADR updated |
| `documentation-generation:docs-architect` | After WP6 — developer guide sections for hybrid ranking and ANN plan | Developer guide updated |

---

## WP1 — Embedding Schema & Ollama Provider

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:vector-databases` | P11-WP1-T1 — `EmbeddingVectors` BLOB storage; dimensionality; unique index strategy | Correct schema |
| `ai:ai-llm-integration` | P11-WP1-T3, T4 — Ollama HTTP embedding API; request/response format; error handling; timeout | Working `OllamaEmbeddingAdapter` |
| `ai:ai-model-gateway` | P11-WP1-T4, T5 — route through `IAiProvider`; architecture test | Gateway compliance |
| `superpowers:brainstorming` | Before P11-WP1-T1 — evaluate BLOB vs. JSON vs. SQLite extension column for vector storage; decide BLOB float[] | Design decision in migration comment |

---

## WP2 — Embedding Generation Pipeline

| Skill | Task | Artifact |
| --- | --- | --- |
| `devops-cloud:reliability-engineering` | P11-WP2-T1, T3 — `IHostedService` discipline; rate limiting; idempotency check on `ModelVersion` | Idempotent pipeline |
| `ai:ai-llm-integration` | P11-WP2-T4 — `IsAvailableAsync` graceful fallback; `OllamaUnavailable` event | Graceful degradation |
| `sdlc-meta:advanced-testing-strategy` | P11-WP2-T6 — mock Ollama HTTP for pipeline tests (no real Ollama in CI) | Test suite without real Ollama |

---

## WP3 — Cosine Similarity & Semantic Search

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:vector-databases` | P11-WP3-T1, T2 — cosine similarity algorithm; `System.Numerics.Vectors` SIMD pattern for float arrays | SIMD-vectorized cosine |
| `full-stack-orchestration:performance-engineer` | P11-WP3-T4, T5 — BenchmarkDotNet P95 measurement; SIMD optimization trigger | Benchmark passing ≤ 1,500 ms |
| `language-standards` (C# / .NET 10) | P11-WP3-T1, T2 — `Vector<float>` usage; `ReadOnlySpan<float>` for zero-alloc inner product | Efficient, safe SIMD code |

---

## WP4 — Hybrid Ranking

| Skill | Task | Artifact |
| --- | --- | --- |
| `ai:ai-output-design` | P11-WP4-T1, T2 — hybrid score design; weight default rationale; UX of ranking | Ranking formula with clear defaults |
| `superpowers:brainstorming` | Before P11-WP4-T3 — evaluate recency decay functions (linear / exponential / step); confirm exponential with 30-day half-life | Design decision in code |
| `sdlc-meta:advanced-testing-strategy` | P11-WP4-T5 — 100-query determinism test design | Determinism test |

---

## WP5 — Match-Location Explanation

| Skill | Task | Artifact |
| --- | --- | --- |
| `ai:ai-output-design` | P11-WP5-T4 — match-location badge visual design; tooltip copy; confidence label copy | Badge UI and copy |
| `ai:ux-for-ai` | P11-WP5-T4 — "why this result" explainability UX; ensure user can always understand a result | Explainability UX note |
| `frontend-ux:interaction-design-patterns` | P11-WP5-T4 — badge layout in result list; truncation when many badges | Badge layout design |
| `avalonia-desktop-development` | P11-WP5-T4 — badge `ItemsControl` with tooltip; Automation peers | Accessible badge control |

---

## WP6 — Erasure & ANN Spike Plan

| Skill | Task | Artifact |
| --- | --- | --- |
| `ai:ai-security` | P11-WP6-T1, T3 — embedding erasure audit; CTRL-OGMA-023 compliance; audit event design | Compliant erasure |
| `sdlc-meta:advanced-testing-strategy` | P11-WP6-T4 — erasure test with row-count oracle; audit event assertion | Erasure test green |
| `architecture:system-architecture-design` | P11-WP6-T5, T6 — ANN spike plan; `IVectorIndex` interface; trigger criteria | Spike document + ADR stub |

---

## WP7 — UI, Icons, i18n, Accessibility

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:ux-content-strategy` | P11-WP7-T1 — copy for Ollama-unavailable notice; erasure confirmation; match-location tooltip wording | `semanticsearch.en.resx` |
| Content translation | P11-WP7-T1 — `fr` translation | `semanticsearch.fr.resx` |
| `ai:ai-output-design` | P11-WP7-T3 — semantic mode indicator design; confidence label color mapping | Mode indicator and confidence badges |

---

## WP8 — Tests & Benchmarks

| Skill | Task | Artifact |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P11-WP8-T2 — mock-Ollama semantic search integration test | Semantic search test |
| `full-stack-orchestration:performance-engineer` | P11-WP8-T4 — semantic P95 benchmark; SIMD optimization loop | Benchmark ≤ 1,500 ms |
| `/run` + `/verify` | P11-WP8-T6 — observe NL query returning semantic results; observe Ollama-unavailable graceful fallback | Verified session log |
| `comprehensive-review:full-review` | End of phase | Final review report |
