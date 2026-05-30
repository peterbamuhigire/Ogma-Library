# Phase 10 — Skills

Skills and slash commands for Search & Indexing.

---

## Always-on

| Skill / command | When | Artifact |
| --- | --- | --- |
| `superpowers:test-driven-development` | Write FTS5 and extraction tests before implementing | Red tests first |
| `superpowers:verification-before-completion` | After each WP; after benchmarks | CI + benchmarks green |
| `superpowers:requesting-code-review` + `/code-review` | After WP1 (schema), WP3 (pipeline), WP5 (rebuild) | Findings resolved |
| `superpowers:using-git-worktrees` | Per WP | Clean branches |
| `documentation-generation:docs-architect` | After WP1 (FTS5 schema note in developer guide) | Developer guide updated |

---

## WP1 — Schema & Migrations

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-internals` | P10-WP1-T4, T5 — FTS5 external-content table design; trigger DDL; `tokenize` parameter choice | Correct FTS5 migration |
| `backend-databases:database-design-engineering` | P10-WP1-T2, T3 — `ExtractedPages` and `SearchChunks` schema; index design; cardinality | Well-formed migration |
| `documentation-generation:architecture-decision-records` | After WP1 — confirm ADR-0006 implementation notes; add chunking-parameter rationale | ADR-0006 amendment |

---

## WP2 — Metadata Search

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-performance` (conceptual) | P10-WP2-T1, T2 — covering index; query plan via `EXPLAIN QUERY PLAN`; measure on 2,000-book corpus | Index + benchmark passing |
| `frontend-ux:frontend-performance` | P10-WP2-T3 — debounce pattern; `CancellationToken` on every keystroke; no UI stall | Non-blocking search |
| `superpowers:brainstorming` | Before P10-WP2-T1 — decide between LIKE vs. FTS5 prefix for metadata (FTS5 prefix chosen if token cardinality is low) | Design decision in code |

---

## WP3 — Extraction Pipeline

| Skill | Task | Artifact |
| --- | --- | --- |
| `devops-cloud:reliability-engineering` | P10-WP3-T1, T8 — `IHostedService` discipline; idempotency check on `ContentHash`; resume logic | Idempotent pipeline |
| `sdlc-meta:advanced-testing-strategy` | P10-WP3-T8 — resumability test design; deterministic kill-and-restart pattern in tests | Resume test green |
| `language-standards` (C# / .NET 10) | P10-WP3-T2, T3 — tokenization; span-based chunking; `ReadOnlySpan<char>` for zero-alloc text slicing | Efficient chunking |

---

## WP4 — FTS5 Full-Text Search

| Skill | Task | Artifact |
| --- | --- | --- |
| `backend-databases:database-internals` | P10-WP4-T1, T5 — FTS5 `bm25()` ranking; `snippet()`; `integrity_check`; query plan optimization | Correct FTS5 queries |
| `full-stack-orchestration:performance-engineer` | P10-WP4-T4 — BenchmarkDotNet FTS5 warm benchmark; P95 gate | Benchmark passing |

---

## WP5 — Index Manager

| Skill | Task | Artifact |
| --- | --- | --- |
| `devops-cloud:reliability-engineering` | P10-WP5-T4, T5 — G7 reliability test design; interrupted-rebuild recovery | G7 test green |
| `frontend-ux:data-visualization` | P10-WP5-T3 — Index Manager dashboard layout; progress bars; per-book status list | Index Manager UI |
| `frontend-ux:interaction-design-patterns` | P10-WP5-T3 — rebuild confirmation dialog; cancel button; progress feedback | Rebuild UX |
| `avalonia-desktop-development` | P10-WP5-T3 — real-time `IObservable` binding to progress; virtualized per-book list | Real-time dashboard |

---

## WP6 — UI, Icons, i18n, Accessibility

| Skill | Task | Artifact |
| --- | --- | --- |
| `frontend-ux:ux-content-strategy` | P10-WP6-T1 — source `en` copy for search empty states, Index Manager messages | `search.en.resx` |
| Content translation | P10-WP6-T1 — `fr` translation | `search.fr.resx` |
| `avalonia-desktop-development` | P10-WP6-T4 — Automation peers; `aria-label` on progress bars | Accessibility peers |

---

## WP7 — Tests & Benchmarks

| Skill | Task | Artifact |
| --- | --- | --- |
| `sdlc-meta:advanced-testing-strategy` | P10-WP7-T3 — golden-corpus extraction test design | Test suite green |
| `/run` + `/verify` | P10-WP7-T5 — drive app; observe search flow; observe rebuild | Verified session log |
| `comprehensive-review:full-review` | End of phase | Final review report |
