# Phase 11 — Semantic Search & Embeddings

Single-sentence mission: generate and store per-chunk embeddings via local
Ollama (no cloud upload), deliver natural-language semantic search with
brute-force cosine similarity (ANN deferred to a spike+ADR), explain every
match by location type, blend exact/recency/status/rating/semantic scores into
a deterministic reproducible hybrid ranking, and provide one-command embedding
erasure (FR-SEARCH-003,004,005; ADR-0006; CTRL-OGMA-023, NFR-PROD-014).

---

## 1. Title & one-line mission

**Phase 11 — Semantic Search & Embeddings**
Realize the semantic layer of the Search Index bounded context: local embedding
generation via Ollama, cosine-similarity ranking (brute-force first, ANN
deferred), match-location explanation badges, hybrid ranking with deterministic
reproducible order, and privacy-safe embedding erasure.

---

## 2. Status & metadata

| Field | Value |
| --- | --- |
| **Tier** | V1 (FR-SEARCH-003, FR-SEARCH-004, FR-SEARCH-005) |
| **Estimate** | 3 engineer-weeks |
| **Owner** | Peter Bamuhigire / Chwezi Core Systems |
| **PRD build-phase mapping** | PRD original Phase 5 (Search — semantic layer) |
| **Platforms** | Windows 10+ + macOS 12+; CI on both; Ollama runs on the same host |
| **Status** | WP1 and WP2 backend foundations plus WP3 semantic search foundations and WP4 hybrid ranking formula started locally: embedding schema upgrade, `IAiProvider` stub, Ollama embedding contract/adapter, vector repository, embedding generation service/worker, semantic read-model progress events, SIMD cosine scoring, deterministic top-K, semantic search service, hybrid ranking service, architecture guards, and focused tests implemented; remaining WP3 benchmark and WP5+ pending |
| **Depends on** | Phase 10 (Search & Indexing — `SearchChunks`, FTS5 results, `ISearchReadModel`), Phase 12 (AI Gateway — `IAiProvider` gateway used for Ollama embedding calls; FR-AI-006) |
| **Unblocks** | Phase 13 (AI advisor uses hybrid ranking); Phase 16 (LAN Host serves semantic search read-model) |

---

## 3. Objectives

1. A natural-language query ("books about colonial education reform") finds
   semantically relevant books even when none of the query words appear in
   the book text — using embeddings generated locally via Ollama without any
   cloud upload (FR-SEARCH-004, FR-AI-006).
2. Every search result displays a match-location badge explaining *where* the
   match was found: title, author, annotation note, extracted text page N,
   or semantic (FR-SEARCH-003) — making every result explainable.
3. Hybrid ranking blends exact-match score (from Phase 10 metadata + FTS5),
   recency (last-opened date), reading status, user rating, and semantic
   cosine score into a single deterministic, reproducible ranked list
   (FR-SEARCH-005); same query + same corpus always returns the same order.
4. Embedding generation runs as a background job; the user can search before
   all embeddings are complete (graceful degradation: results without semantic
   score fall back to exact + recency + rating ranking).
5. A user can erase all stored embeddings with one action; the action completes
   durably before confirming; no embedding data can be recovered after erasure
   (CTRL-OGMA-023, NFR-PROD-014).
6. The ANN upgrade path is documented in a spike plan and a stub ADR amendment;
   ANN is not implemented in this phase (ADR-0006 brute-force first).
7. The semantic search read-model (`ISemanticSearchReadModel`) is
   LAN-projection-ready for Phase 16/17 without building LAN here.

---

## 4. Scope

### In scope

- `EmbeddingVectors` table: `(Id UUID, ChunkId FK→SearchChunks, ModelName,
  ModelVersion, Vector BLOB, GeneratedAtUtc, DimensionCount)`.
- `EmbeddingGenerationService`: background worker consuming `SearchChunks` not
  yet embedded; calls `IOllamaEmbeddingProvider` (the Ollama adapter of
  `IAiProvider`); idempotent per chunk; progress observable.
- `IOllamaEmbeddingProvider`: wraps Ollama HTTP API (local, no cloud); zero
  bytes leave the device; routes through the single `IAiProvider` gateway
  (Phase 12 coordination — see Dependencies).
- Brute-force cosine similarity: `CosineSimilarityService.Score(queryVector,
  chunkVector[])`; all vectors loaded into memory for query; ANN deferred.
- `SemanticSearchService.SearchAsync(queryText, ct)`: embed query → cosine
  search → merge with Phase 10 FTS5 results → hybrid ranking.
- Hybrid ranking formula (FR-SEARCH-005):

  ```
  hybrid_score
    = w_exact  × exact_match_score   (normalized 0–1)
    + w_recency × recency_score       (exponential decay, last-opened)
    + w_status  × status_score        (reading/read/want-to-read weights)
    + w_rating  × rating_score        (user star rating / 5)
    + w_semantic × semantic_score     (cosine similarity 0–1)
  ```

  Default weights: `{0.35, 0.10, 0.10, 0.10, 0.35}`. Weights are configurable
  in Settings; deterministic at the same weight vector + corpus.

- Match-location explanation: `MatchLocation` enum
  (`Title`, `Author`, `Tag`, `Description`, `Toc`, `NotePage`, `TextPage`,
  `Semantic`); each `SearchResult` carries `MatchLocation[]` in priority order.
- `SearchResult` enrichment: Phase 10 result extended with `SemanticScore`,
  `HybridScore`, `MatchLocations[]`, `ConfidenceLabel` (High/Medium/Low).
- Embedding erasure: `EmbeddingErasureService.EraseAllAsync(ct)` deletes all
  `EmbeddingVectors` rows in a transaction; confirms before returning; CTRL-OGMA-023.
- ANN spike plan: a written spike document (`docs/spikes/ANN-SQLite-Vec-Spike.md`)
  specifying how `sqlite-vec` / Vec1 would replace brute-force; criteria for
  triggering the spike (corpus size threshold); a stub ADR-0006 amendment entry.
- LAN-projection-ready: `ISemanticSearchReadModel` interface with
  `IObservable<SemanticIndexEvent>` (embedding generated, erasure completed).
- Performance: semantic search P95 ≤ 1,500 ms on 2,000-book corpus (NFR-PROD-004;
  brute-force cosine on all chunks of 2,000 books).
- Icons for semantic search, match-location badges, and ranking UI.

### Explicitly out of scope

- ANN (sqlite-vec / Vec1) implementation — deferred to spike+ADR (see §7).
- Cloud-based embedding providers — only Ollama local path in this phase
  (cloud path is Phase 12 opt-in behind privacy-tier consent).
- OCR-derived embeddings for scanned pages (Phase 15 delivers OCR text; Phase 11
  will pick it up automatically via `SearchChunks` once Phase 15 is done).
- Embedding export or import.
- LAN serving of semantic search (Phase 16/17).

---

## 5. Requirements covered

| ID | Tier | Summary | Verified by |
| --- | --- | --- | --- |
| FR-SEARCH-003 | V1 | Match-location explanation (title/author/note/text-page/semantic) | `SearchResult_MatchLocations_Correct` integration test |
| FR-SEARCH-004 | V1 | Semantic NL search over embeddings | `SemanticSearch_FindsRelevantBook_WithoutKeywordOverlap` integration test |
| FR-SEARCH-005 | V1 | Hybrid ranking (exact, recency, status, rating, semantic); deterministic | `HybridRanking_DeterministicOrder` determinism test |
| FR-AI-006 | V1 | Local Ollama embeddings; no cloud upload | `EmbeddingProvider_UsesOllama_NoExternalCalls` unit test (mock HTTP) |
| CTRL-OGMA-023 | V1 | Embedding erasure on demand | `EmbeddingErasure_AllRowsDeleted_BeforeConfirm` test |
| NFR-PROD-014 | V1 | AI history + embedding erasure | Erasure test + audit-event check |
| NFR-PROD-004 | V1 | Semantic search ≤ 1,500 ms P95 | `PerfBenchmark_SemanticSearch_P95` benchmark |

---

## 6. Dependencies

### Depends on

- **Phase 10** — `SearchChunks` (chunks to embed), `ISearchReadModel`, FTS5
  results (exact scores fed into hybrid ranking), extraction pipeline (chunks
  must exist before embedding).
- **Phase 12** — `IAiProvider` gateway is the single egress chokepoint; the
  Ollama embedding provider must register through this interface so the
  architecture test (`Architecture_NoDirectProviderCalls`) remains green.
  **Note:** Phase 12 and Phase 11 have a coordination dependency. If Phase 12
  is not yet available, Phase 11 may stub `IAiProvider` and wire the real
  implementation when Phase 12 delivers. The stub must satisfy the same
  interface contract.
- **Phase 03** — Design system, icon system, i18n scaffold.
- **ADR-0006** — Hybrid search architecture; brute-force cosine first confirmed.

### Unblocks

- **Phase 13** — AI advisor reads hybrid-ranked results as first-party evidence
  for recommendations.
- **Phase 16** — `ISemanticSearchReadModel` wired to LAN Host projection;
  semantic search served over LAN.

---

## 7. Architecture & approach

### Bounded context: Search Index — semantic extension (HLD §4.6, §4.7)

Phase 11 extends the Search Index bounded context. All embedding generation
and semantic search services are co-located in `OgmaLibrary.Application.Search`.
The Ollama adapter is in `OgmaLibrary.Infrastructure.AI` (co-located with other
provider adapters, behind `IAiProvider`).

```
SemanticSearchService
  ├─ EmbeddingGenerationService    — background worker; IOllamaEmbeddingProvider
  ├─ CosineSimilarityService       — brute-force cosine over EmbeddingVectors
  ├─ HybridRankingService          — blends exact + recency + status + rating + semantic
  ├─ MatchLocationService          — determines MatchLocation[] per result
  ├─ EmbeddingErasureService       — erases all EmbeddingVectors rows
  └─ ISemanticSearchReadModel      — IObservable<SemanticIndexEvent> (LAN-ready)

IOllamaEmbeddingProvider : IAiProvider   (Infrastructure.AI)
  └─ OllamaHttpClient              — HTTP POST to localhost:11434/api/embeddings
```

#### Embedding generation (FR-AI-006)

`EmbeddingGenerationService` as `IHostedService`:

1. Poll `SearchChunks LEFT JOIN EmbeddingVectors ON Id` where no embedding
   exists or model has changed.
2. For each chunk: POST `{"model": "<configured-model>", "prompt": chunk.Text}`
   to Ollama local endpoint (`http://localhost:11434`). No external host.
3. Store `EmbeddingVectors` row: `Vector` = `BLOB` of `float[]` (4 bytes per
   dimension × N dimensions, typically 1536 or 4096 depending on model).
4. Idempotent: skip if `EmbeddingVectors` row exists and `ModelVersion` matches.
5. Progress: emit `EmbeddingGenerated(chunkId)` on `ISemanticSearchReadModel`.
6. Architecture test: `OllamaHttpClient` must not be called outside
   `OgmaLibrary.Infrastructure.AI`; all calls route through `IAiProvider`.

#### Brute-force cosine similarity

`CosineSimilarityService.Score(float[] queryVec, float[] chunkVec)`:

```csharp
float dotProduct = queryVec.Zip(chunkVec, (a, b) => a * b).Sum();
float normQ = MathF.Sqrt(queryVec.Sum(x => x * x));
float normC = MathF.Sqrt(chunkVec.Sum(x => x * x));
return dotProduct / (normQ * normC);
```

For a corpus of 2,000 books × ~200 chunks/book × ~1,536 floats/chunk = ~600 M
float operations per query. This is the upper bound for brute-force; the
P95 ≤ 1,500 ms gate will determine if vectorized SIMD (`System.Numerics.Vector`)
optimization is needed.

**ANN spike plan** (documented in `docs/spikes/ANN-SQLite-Vec-Spike.md`):
- Trigger: brute-force P95 exceeds 1,000 ms on ≥ 5,000-book corpus in a
  Phase 20 benchmark run.
- Technology: `sqlite-vec` extension (Vector similarity ANN in SQLite).
- ADR amendment: ADR-0006 §ANN to be added after spike validates the wrapper.
- Implementation: a `IVectorIndex` interface allowing swap of brute-force for
  ANN without touching callers.

#### Hybrid ranking formula (FR-SEARCH-005)

All component scores are normalized to `[0, 1]` before blending:

| Component | Normalization | Default weight |
| --- | --- | --- |
| `exact_match_score` | Phase 10 relevance score / max possible score | 0.35 |
| `recency_score` | `exp(-λ × days_since_last_opened)`; λ = ln(2)/30 (half-life 30 days) | 0.10 |
| `status_score` | `Reading=1.0, WantToRead=0.7, Read=0.5, NotStarted=0.0` | 0.10 |
| `rating_score` | `user_rating / 5.0` (0 if unrated) | 0.10 |
| `semantic_score` | Cosine similarity clipped to `[0, 1]` | 0.35 |

Tie-breaking: `ORDER BY hybrid_score DESC, BookId ASC` (deterministic on equal
score). Same query + same corpus + same weights → same result list.
Weights are stored in `Settings` and used consistently; changing weights
invalidates cached ranking (none cached).

#### Match-location explanation (FR-SEARCH-003)

`MatchLocationService.GetLocations(searchResult, ftsResult, semanticResult)`:
returns `MatchLocation[]` in descending confidence order. A result can have
multiple locations (e.g., both title match and semantic match).
The UI displays a badge per location: "In title", "In author", "In notes",
"On page N", "Semantic match".

#### Embedding erasure (CTRL-OGMA-023, NFR-PROD-014)

`EmbeddingErasureService.EraseAllAsync(ct)`:
1. Begin transaction.
2. `DELETE FROM EmbeddingVectors`.
3. `UPDATE Books SET EmbeddingStatus = NotEmbedded`.
4. Commit.
5. Emit audit event `EmbeddingVectorsErased(count, erasedAtUtc)` to local
   audit trail (CTRL-OGMA-018).
6. Return only after step 5.

The Privacy Center (Phase 12) surfaces this action. In Phase 11 it is exposed
as a Settings panel item.

#### LAN-projection-ready

`ISemanticSearchReadModel` emits `SemanticIndexEvent`:
`EmbeddingGenerated(chunkId, bookId)`, `EmbeddingErasureCompleted(count)`,
`EmbeddingModelChanged(model)`. Phase 16 wires it to the Host.

---

## 8. Work breakdown (summary)

| Work package | Key tasks | Detail |
| --- | --- | --- |
| WP1 — Embedding schema & provider | `EmbeddingVectors` migration; `IOllamaEmbeddingProvider`; gateway wiring | `tasks.md` WP1 |
| WP2 — Embedding generation pipeline | Background worker; idempotent; progress observable | `tasks.md` WP2 |
| WP3 — Cosine similarity & brute-force search | `CosineSimilarityService`; SIMD optimization if needed; P95 benchmark | `tasks.md` WP3 |
| WP4 — Hybrid ranking | Formula implementation; determinism test; weight configurability | `tasks.md` WP4 |
| WP5 — Match-location explanation | `MatchLocationService`; badge UI | `tasks.md` WP5 |
| WP6 — Erasure & ANN spike plan | `EmbeddingErasureService`; audit event; spike document; ADR stub | `tasks.md` WP6 |
| WP7 — UI, icons, i18n, a11y | Semantic search UI; match-location badges; en/fr strings | `tasks.md` WP7 |
| WP8 — Tests & benchmarks | All layers; determinism tests; P95 benchmark; erasure test | `tasks.md` WP8 |

---

## 9. Cross-cutting checklist

- [ ] **Colorful icons + manifest**: `icons.md` complete; semantic-search,
      match-location badges, and ranking icons listed; owner procurement request
      issued.
- [ ] **i18n (en/fr)**: all semantic search UI strings, match-location badge
      labels, and erasure confirmation copy externalized in en + fr.
- [ ] **Accessibility**: match-location badges have aria-labels; semantic search
      result list navigable by keyboard; confidence labels have accessible text.
- [ ] **Privacy/egress**: `IOllamaEmbeddingProvider` calls only `localhost`; an
      architecture test asserts no external HTTP call exits the device;
      CTRL-OGMA-023 erasure tested.
- [ ] **Reversibility**: embedding erasure is irreversible by design (CTRL-OGMA-023);
      a confirmation dialog with explicit warning is shown; the erasure is
      transactional (all deleted or none).
- [ ] **Performance**: NFR-PROD-004 (semantic ≤ 1,500 ms P95) CI-gated on
      2,000-book corpus; brute-force SIMD optimization applied if needed.
- [ ] **Bounded-context tests**: Semantic search does not depend on Reader or AI
      advisor; Ollama provider is in Infrastructure; `IAiProvider` is the only
      allowed cross-boundary interface.
- [ ] **Documentation**: hybrid ranking formula, weight defaults, ANN spike
      criteria, embedding erasure audit trail all documented with XML doc
      comments and developer guide entries.

---

## 10. Definition of Done

**Global DoD (README §6) fully applied, plus:**

- [ ] FR-SEARCH-004: `SemanticSearch_FindsRelevantBook_WithoutKeywordOverlap`
      — query "governance education Africa" finds a book whose text contains
      relevant content but no query keywords in title/author.
- [ ] FR-SEARCH-003: `SearchResult_MatchLocations_Correct` — result for a book
      matched by semantic similarity carries `MatchLocation.Semantic`; result
      matched by title carries `MatchLocation.Title`.
- [ ] FR-SEARCH-005: `HybridRanking_DeterministicOrder` — run same query twice
      with same corpus and weights; result lists are identical.
- [ ] FR-AI-006: `EmbeddingProvider_UsesOllama_NoExternalCalls` — mock HTTP
      intercept shows all embedding calls target `localhost:11434`; zero calls
      to external hosts.
- [ ] CTRL-OGMA-023 / NFR-PROD-014: `EmbeddingErasure_AllRowsDeleted_BeforeConfirm`
      — `EmbeddingVectors` count = 0 after erasure; audit event present.
- [ ] NFR-PROD-004: `PerfBenchmark_SemanticSearch_P95 ≤ 1500 ms` on 2,000-book
      corpus on both CI runners.
- [ ] ANN spike plan document present at `docs/spikes/ANN-SQLite-Vec-Spike.md`;
      ADR-0006 amendment stub committed.
- [ ] Graceful degradation: search returns Phase 10 (exact) results when no
      embeddings exist; no crash or empty results.
- [ ] Architecture test: no direct Ollama HTTP call outside Infrastructure.
- [ ] `/code-review` completed; findings resolved.

---

## 11. Skills to use

See `skills.md` for full guidance. Summary:

- `backend-databases:vector-databases` — embedding storage (BLOB); cosine
  similarity patterns; ANN spike planning.
- `ai:ai-rag-patterns` / `ai:ai-llm-integration` — Ollama embedding API;
  chunking→embedding pipeline; graceful degradation.
- `ai:ai-output-design` — match-location explanation badges; confidence labels;
  hybrid ranking result display.
- `ai:ux-for-ai` — surfacing semantic search to users; "why this result"
  explainability UX.
- `sdlc-meta:advanced-testing-strategy` — determinism test; erasure test;
  no-external-calls mock.
- `full-stack-orchestration:performance-engineer` — cosine SIMD optimization;
  P95 benchmark.
- `documentation-generation:architecture-decision-records` — ADR-0006 ANN
  amendment stub.

---

## 12. Deliverables

| Artifact | Location |
| --- | --- |
| `EmbeddingGenerationService`, `CosineSimilarityService`, `SemanticSearchService`, `HybridRankingService`, `MatchLocationService`, `EmbeddingErasureService` | `src/OgmaLibrary.Application/Search/` |
| `IOllamaEmbeddingProvider` + `OllamaEmbeddingAdapter` | `src/OgmaLibrary.Infrastructure/AI/Ollama/` |
| `ISemanticSearchReadModel` interface | `src/OgmaLibrary.Application/Search/` |
| `EmbeddingVectors` migration | `src/OgmaLibrary.Infrastructure/Migrations/` |
| Semantic search UI (`SemanticSearchView`, match-location badge controls) | `src/OgmaLibrary.App/Views/Search/` |
| Semantic search en/fr resource files | `src/OgmaLibrary.App/Assets/Strings/semanticsearch.en.resx`, `.fr.resx` |
| ANN spike plan | `docs/spikes/ANN-SQLite-Vec-Spike.md` |
| ADR-0006 ANN amendment stub | `docs/architecture/adr/ADR-0006-hybrid-search.md` (§ANN section) |
| Performance benchmark | `src/OgmaLibrary.Benchmarks/SemanticSearchBenchmarks.cs` |
| Erasure test | `tests/OgmaLibrary.Tests/Privacy/EmbeddingErasureTests.cs` |
| `icons.md` | `docs/plans/grand-plan/phase-11/icons.md` |

---

## 13. Risks

| Risk | Tier | Mitigation |
| --- | --- | --- |
| Brute-force cosine P95 exceeds 1,500 ms on 2,000-book corpus | R3 | SIMD optimization via `System.Numerics.Vectors`; if still slow, limit to top-N chunks by FTS5 score before cosine; ANN spike triggered early |
| Ollama not installed on CI runner / user machine | R4 | `IOllamaEmbeddingProvider.IsAvailable()` returns false gracefully; search falls back to exact; UI shows "Semantic search requires Ollama — install guide" notice |
| Embedding model version change invalidates existing vectors | R5 | `EmbeddingVectors.ModelVersion` stored; stale vectors detected and re-embedded automatically |
| Erasure confirmation UX is unclear; user erases accidentally | R5 | Two-step confirmation: "Erase all embeddings? This cannot be undone." with a 3-second countdown on the confirm button |
| Hybrid ranking non-deterministic due to floating-point tie-break | R5 | `ORDER BY hybrid_score DESC, BookId ASC` ensures determinism; determinism test covers 100 queries |
| Phase 12 (`IAiProvider`) not yet available when Phase 11 is built | R4 | Stub `IAiProvider` interface in Phase 10/11 scope; wire real implementation when Phase 12 delivers; architecture test passes with stub |

---

## 14. Owner asks

1. **Premium icon procurement (Semantic Search set):** Please review `icons.md`
   and purchase the named premium icons for semantic search, match-location
   badges, and ranking UI.

2. **Ollama embedding model selection:** The default Ollama model for embedding
   generation needs to be confirmed (proposed: `nomic-embed-text` at 768
   dimensions — small, fast, local-first). Please confirm or specify the
   preferred model before WP1 implementation.

3. **Hybrid ranking default weights:** The proposed defaults
   `{exact: 0.35, recency: 0.10, status: 0.10, rating: 0.10, semantic: 0.35}`
   are balanced. Please confirm or adjust. These are exposed as user-configurable
   settings so the user can tune them.

4. **ANN trigger threshold:** The ANN spike is triggered when brute-force
   P95 exceeds 1,000 ms at ≥ 5,000 books. Please confirm this threshold or
   specify a different corpus size that would trigger the ANN upgrade.

---

## 15. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-05-30 | Initial v1.0 baseline authored | Grand-plan agent |
| 2026-06-01 | WP1 started: Phase 11 embedding schema upgrade, Application AI provider stub, local-only Ollama embedding adapter, vector repository, focused schema/provider tests, and architecture guards added | Codex |
| 2026-06-01 | WP2 backend started: embedding generation service, idempotent pending-chunk selection, unavailable-Ollama degradation, failure-job recording, semantic progress events, worker polling, and focused tests added | Codex |
| 2026-06-01 | WP3 first slice started: SIMD-backed cosine similarity service, deterministic top-K vector ranking, and focused unit tests added | Codex |
| 2026-06-01 | WP3 semantic service started: query embedding, brute-force vector scoring, chunk/book projection, book-level deduplication, exact-search fallback, and focused semantic tests added | Codex |
| 2026-06-01 | WP4 hybrid ranking started: configurable default weights, exact/semantic/recency/status/rating normalization, no-embedding exact fallback, deterministic `BookId` tie-break, and 100-query determinism tests added | Codex |
