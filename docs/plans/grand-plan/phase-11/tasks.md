# Phase 11 — Tasks

Work packages and tasks for Semantic Search & Embeddings.

---

## WP1 — Embedding Schema & Ollama Provider

**Goal:** `EmbeddingVectors` table; `IOllamaEmbeddingProvider` wired through
`IAiProvider` gateway; architecture test green.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP1-T1 | `EmbeddingVectors` table: `(Id UUID, ChunkId FK→SearchChunks, ModelName VARCHAR, ModelVersion VARCHAR, Vector BLOB, DimensionCount INT, GeneratedAtUtc)`; unique index `(ChunkId, ModelName, ModelVersion)`; migration | 2 h | Phase 10, Phase 04 | FR-SEARCH-004, ADR-0006 |
| P11-WP1-T2 | `Books.EmbeddingStatus` column (`NotEmbedded`, `Embedding`, `Embedded`); migration | 1 h | P11-WP1-T1 | FR-SEARCH-004 |
| P11-WP1-T3 | `IOllamaEmbeddingProvider` interface (extends / implements `IAiProvider` embedding contract): `EmbedAsync(text, model, ct) → float[]`; `IsAvailableAsync(ct) → bool` | 2 h | Phase 12 `IAiProvider` (or stub) | FR-AI-006 |
| P11-WP1-T4 | `OllamaEmbeddingAdapter` implementation: `HttpClient` POST to `http://localhost:11434/api/embeddings`; deserialize float array; no external hosts | 3 h | P11-WP1-T3 | FR-AI-006, CTRL-OGMA-023 |
| P11-WP1-T5 | Architecture test: `Architecture_NoDirectOllamaCallOutsideInfrastructure` — no type outside `OgmaLibrary.Infrastructure.AI` calls `OllamaEmbeddingAdapter` directly | 1 h | P11-WP1-T4 | FR-AI-006 |
| P11-WP1-T6 | `IEmbeddingVectorRepository`: `CreateAsync`, `GetForChunkAsync`, `GetAllForBookAsync`, `DeleteAllAsync`; EF Core implementation | 2 h | P11-WP1-T1 | CTRL-OGMA-023 |

**WP1 exit:** migration passes; architecture test green; `IsAvailable` returns false gracefully when Ollama absent.

---

## WP2 — Embedding Generation Pipeline

**Goal:** background worker embeds all `SearchChunks` not yet embedded;
idempotent; graceful when Ollama is unavailable; progress observable.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP2-T1 | `EmbeddingGenerationService` as `IHostedService`: poll `SearchChunks LEFT JOIN EmbeddingVectors` for unembedded chunks; rate-limited (configurable requests/s to avoid CPU spike) | 2 h | WP1 | FR-SEARCH-004 |
| P11-WP2-T2 | Per-chunk embedding: call `IOllamaEmbeddingProvider.EmbedAsync`; on success write `EmbeddingVectors` row; on failure log to `Jobs` table; continue | 2 h | P11-WP2-T1 | FR-SEARCH-004 |
| P11-WP2-T3 | Idempotency: before embedding, check `GetForChunkAsync` — skip if `ModelVersion` matches configured model; re-embed if model changed | 1 h | P11-WP2-T2 | NFR-OGMA-009 |
| P11-WP2-T4 | Graceful degradation: if `IOllamaEmbeddingProvider.IsAvailableAsync()` returns false, pause pipeline; emit `OllamaUnavailable` event; UI shows notice; do not block search | 2 h | P11-WP2-T1 | FR-AI-006 |
| P11-WP2-T5 | Progress observable: `ISemanticSearchReadModel` emits `EmbeddingGenerated(chunkId, bookId, totalEmbedded, totalChunks)` | 1 h | P11-WP2-T2 | FR-SEARCH-006 (extension) |
| P11-WP2-T6 | Tests: idempotency (second run produces same row count); failure handling (one chunk fails → others proceed); Ollama-unavailable degradation | 3 h | P11-WP2-T4 | FR-AI-006, NFR-OGMA-009 |

**WP2 exit:** pipeline runs idempotently; unavailable Ollama degrades gracefully; progress emits.

---

## WP3 — Cosine Similarity & Semantic Search

**Goal:** embed query; brute-force cosine over all stored vectors; return
ranked `SemanticSearchResult[]`; P95 ≤ 1,500 ms.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP3-T1 | `CosineSimilarityService.Score(float[] a, float[] b) → float`: dot product / (|a| × |b|); SIMD-vectorized via `System.Numerics.Vectors` | 2 h | P11-WP1-T1 | FR-SEARCH-004, NFR-PROD-004 |
| P11-WP3-T2 | `CosineSimilarityService.TopK(float[] query, IEnumerable<(Guid chunkId, float[])> corpus, int k) → (chunkId, score)[]`: parallel SIMD over corpus; returns top K by score | 3 h | P11-WP3-T1 | FR-SEARCH-004, NFR-PROD-004 |
| P11-WP3-T3 | `SemanticSearchService.SearchAsync(queryText, ct)`: (1) embed query via `IOllamaEmbeddingProvider`; (2) load `EmbeddingVectors` for all books; (3) `TopK(query, corpus, 50)`; (4) join to `SearchChunks` and `Books` for context | 3 h | P11-WP3-T2, WP2 | FR-SEARCH-004 |
| P11-WP3-T4 | Performance benchmark: `PerfBenchmark_SemanticSearch_P95` — 2,000-book corpus (pre-embedded from fixture); 20 queries; assert P95 ≤ 1,500 ms on both CI runners | 2 h | P11-WP3-T3 | NFR-PROD-004 |
| P11-WP3-T5 | SIMD optimization: if P95 exceeds 1,000 ms in benchmark, apply `Vector<float>` inner product; re-run benchmark | 2 h | P11-WP3-T4 | NFR-PROD-004 |
| P11-WP3-T6 | Unit tests: cosine(a, a) = 1.0; cosine(a, -a) = -1.0; cosine(orthogonal) = 0.0; TopK returns correct ordering | 2 h | P11-WP3-T2 | FR-SEARCH-004 |

**WP3 exit:** P95 ≤ 1,500 ms benchmark passes on both CI runners; cosine unit tests pass.

---

## WP4 — Hybrid Ranking

**Goal:** blend exact + recency + status + rating + semantic into deterministic
ranked list; weights configurable; same query = same order.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP4-T1 | `HybridRankingWeights` settings model: `{ExactWeight, RecencyWeight, StatusWeight, RatingWeight, SemanticWeight}`; defaults `{0.35, 0.10, 0.10, 0.10, 0.35}`; stored in `Settings` | 1 h | Phase 03/04 settings | FR-SEARCH-005 |
| P11-WP4-T2 | `HybridRankingService.Rank(exact[], semantic[], weights, books[]) → RankedResult[]`: normalize each component; apply weights; sum; `ORDER BY hybridScore DESC, BookId ASC` | 3 h | P11-WP4-T1, Phase 10 score | FR-SEARCH-005 |
| P11-WP4-T3 | `RecencyScore(DateTimeOffset? lastOpened, DateTimeOffset now)`: exponential decay `exp(-λ × days)`; λ such that half-life = 30 days; 0 if never opened | 1 h | P11-WP4-T2 | FR-SEARCH-005 |
| P11-WP4-T4 | `StatusScore(ReadingStatus)`: `Reading=1.0, WantToRead=0.7, Read=0.5, NotStarted=0.0` | 1 h | P11-WP4-T2 | FR-SEARCH-005 |
| P11-WP4-T5 | Determinism test: `HybridRanking_DeterministicOrder` — run same query + corpus twice; assert `result[i].BookId` identical across runs; run 100 queries | 2 h | P11-WP4-T2 | FR-SEARCH-005 |
| P11-WP4-T6 | Graceful degradation: if `SemanticScore` is absent (no embeddings), set `semanticWeight = 0`; redistribute to exact weight; determinism test covers this case | 1 h | P11-WP4-T2 | FR-SEARCH-005 |

**WP4 exit:** determinism test passes over 100 queries; graceful-degradation case covered.

---

## WP5 — Match-Location Explanation

**Goal:** every search result carries `MatchLocation[]`; UI shows explanation
badges.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP5-T1 | `MatchLocation` enum: `Title, Author, Tag, Description, Toc, NotePage, TextPage, Semantic` | 1 h | Phase 10 `SearchChunk.Source` | FR-SEARCH-003 |
| P11-WP5-T2 | `MatchLocationService.GetLocations(metadataResult, ftsResult, semanticResult)`: Title/Author from metadata match fields; TextPage/NotePage/Tag/Description/Toc from `SearchChunk.Source`; Semantic if semanticScore ≥ threshold | 2 h | P11-WP5-T1 | FR-SEARCH-003 |
| P11-WP5-T3 | `SearchResult` enrichment: add `MatchLocations[]`, `SemanticScore`, `HybridScore`, `ConfidenceLabel` (High ≥ 0.8, Medium ≥ 0.5, Low < 0.5) | 1 h | P11-WP5-T2, WP4 | FR-SEARCH-003 |
| P11-WP5-T4 | Match-location badge control: small colored chip per `MatchLocation`; tooltip explains badge; keyboard-accessible | 2 h | P11-WP5-T3 | FR-SEARCH-003 |
| P11-WP5-T5 | Tests: a semantic-only match has `[Semantic]`; a title match has `[Title]`; a note match has `[NotePage]`; multi-location result has all correct locations | 2 h | P11-WP5-T2 | FR-SEARCH-003 |

**WP5 exit:** match-location badges visible on all result types; tooltip explains each.

---

## WP6 — Erasure & ANN Spike Plan

**Goal:** one-action embedding erasure; audit event; ANN spike document.

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP6-T1 | `EmbeddingErasureService.EraseAllAsync(ct)`: transaction DELETE `EmbeddingVectors`; UPDATE `Books.EmbeddingStatus = NotEmbedded`; emit audit event; commit; return | 2 h | P11-WP1-T6 | CTRL-OGMA-023, NFR-PROD-014 |
| P11-WP6-T2 | Erasure Settings panel: "Erase all embeddings" action; two-step confirmation with 3-s countdown on confirm button; progress indicator | 2 h | P11-WP6-T1 | CTRL-OGMA-023 |
| P11-WP6-T3 | Audit event: `EmbeddingVectorsErased(count: N, erasedAtUtc)` written to `AuditEvents` table before erasure method returns | 1 h | P11-WP6-T1 | CTRL-OGMA-023, CTRL-OGMA-018 |
| P11-WP6-T4 | Erasure test: `EmbeddingErasure_AllRowsDeleted_BeforeConfirm` — insert 100 vector rows; call `EraseAllAsync`; assert `EmbeddingVectors.Count = 0`; assert audit event present | 2 h | P11-WP6-T1 | CTRL-OGMA-023 |
| P11-WP6-T5 | ANN spike plan document: write `docs/spikes/ANN-SQLite-Vec-Spike.md` covering sqlite-vec/Vec1 API, integration approach, trigger criterion (P95 > 1,000 ms at ≥ 5,000 books), `IVectorIndex` interface stub | 3 h | Phase 20 benchmark context | ADR-0006 |
| P11-WP6-T6 | ADR-0006 amendment: add §ANN stub noting "brute-force until corpus > 5,000 books triggers the sqlite-vec spike" | 1 h | P11-WP6-T5 | ADR-0006 |

**WP6 exit:** erasure test green; audit event present; spike document and ADR stub committed.

---

## WP7 — UI, Icons, i18n, Accessibility

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP7-T1 | `semanticsearch.en.resx` and `semanticsearch.fr.resx`; externalize: semantic search bar placeholder, match-location badge labels, confidence labels, Ollama-unavailable notice, erasure confirmation copy | 3 h | Phase 03 i18n scaffold | I18N-STRATEGY.md |
| P11-WP7-T2 | Wire premium icons (or placeholders) for semantic search, match-location badges, confidence indicators, erasure button | 2 h | icons.md, Phase 03 | ICON-SYSTEM.md |
| P11-WP7-T3 | Semantic search indicator: visual cue on the search bar indicating semantic mode is active (embeddings available) or degraded (Ollama unavailable) | 1 h | WP2 | FR-SEARCH-004 |
| P11-WP7-T4 | Match-location badge keyboard + SR: each badge has `aria-label` explaining location; badge list ordered by relevance; Tab navigable | 2 h | WP5 | NFR-PROD-008 |
| P11-WP7-T5 | Pseudolocale render: match-location badges, confidence labels, Ollama notice — no truncation | 1 h | P11-WP7-T1 | I18N-STRATEGY.md |

**WP7 exit:** pseudolocale clean; SR announces match locations; icons registered.

---

## WP8 — Tests & Benchmarks

| Task ID | Description | Est. | Deps | Satisfies |
| --- | --- | --- | --- | --- |
| P11-WP8-T1 | Architecture tests: `Architecture_SemanticSearch_DoesNotDependOnReader`, `Architecture_SemanticSearch_DoesNotDependOnAIAdvisor`, `Architecture_NoDirectOllamaCallOutsideInfrastructure` | 2 h | all WPs | Bounded-context discipline, FR-AI-006 |
| P11-WP8-T2 | `SemanticSearch_FindsRelevantBook_WithoutKeywordOverlap`: insert book with known content; embed content (mock Ollama); construct semantically-close query vector; assert book in top 5 | 3 h | WP3 | FR-SEARCH-004 |
| P11-WP8-T3 | `HybridRanking_DeterministicOrder` (100-query run) | 2 h | WP4 | FR-SEARCH-005 |
| P11-WP8-T4 | Performance benchmark: `PerfBenchmark_SemanticSearch_P95` on 2,000-book pre-embedded corpus; P95 ≤ 1,500 ms | 2 h | WP3 | NFR-PROD-004 |
| P11-WP8-T5 | Erasure test + audit event (P11-WP6-T4) confirmed in CI | 1 h | WP6 | CTRL-OGMA-023 |
| P11-WP8-T6 | End-to-end smoke: type NL query; semantic results appear with match-location badges; change Ollama config to unavailable; confirm graceful fallback to exact search | 2 h | all WPs | FR-SEARCH-004, FR-SEARCH-003 |
| P11-WP8-T7 | CI matrix (Windows + macOS): `dotnet format`, `dotnet build`, `dotnet test` all pass | 1 h | all WPs | Global DoD §3 |
