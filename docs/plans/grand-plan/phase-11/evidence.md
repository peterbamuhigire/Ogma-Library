# Phase 11 Verification Evidence

Last updated: 2026-06-01

This file tracks implementation evidence for Phase 11 Semantic Search &
Embeddings.

## Current Position

Phase 11 WP1, WP2 backend foundations, WP3 semantic-search foundations, and
the WP4 hybrid ranking formula have started locally. The embedding schema has
been aligned with the Phase 11 model/version requirements,
`Books.EmbeddingStatus` is in the EF model, a Phase 12-compatible `IAiProvider`
stub exists in Application, the local Ollama embedding provider is registered
behind Application contracts, and the embedding generation pipeline can process
pending chunks idempotently.

The implementation deliberately keeps embeddings as a rebuildable local index:
SQLite remains the source of truth, and vectors are stored as little-endian
float BLOBs keyed back to `SearchChunks`.

## Automated Verification

| Command | Result |
| --- | --- |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase11EmbeddingSchemaTests` | Passed: 4 Phase 11 schema, repository, and Ollama adapter tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~EmbeddingGenerationServiceTests\|FullyQualifiedName~EmbeddingGenerationWorkerTests"` | Passed: 4 embedding generation service and worker tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~CosineSimilarityServiceTests` | Passed: 6 cosine arithmetic, zero-vector, SIMD-width, and deterministic top-K tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SemanticSearchServiceTests` | Passed: 2 semantic search and exact-fallback tests |
| `dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~HybridRankingServiceTests` | Passed: 7 hybrid ranking formula, fallback, tie-break, and 100-query determinism tests |
| `dotnet test tests\OgmaLibrary.Tests.Architecture\OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-restore` | Passed: 18 architecture tests |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | Passed: 0 warnings, 0 errors |

## Evidence Map

| Area | Evidence |
| --- | --- |
| AI provider gateway stub | `IAiProvider` under `src/OgmaLibrary.Application/Ai/` |
| Ollama embedding contract | `IOllamaEmbeddingProvider` and `OllamaEmbeddingResult` under `src/OgmaLibrary.Application/Search/` |
| Local-only Ollama adapter | `OllamaEmbeddingAdapter` under `src/OgmaLibrary.Infrastructure/AI/Ollama/`; tests assert loopback `/api/embeddings` use and unavailable-service degradation |
| Embedding schema | `20260601104115_Phase11EmbeddingSchema` adds `Books.EmbeddingStatus`, `EmbeddingVectors.ModelName`, `ModelVersion`, `GeneratedAtUtc`, non-null vector BLOBs, and unique `(ChunkId, ModelName, ModelVersion)` index |
| Vector repository | `IEmbeddingVectorRepository` and `EmbeddingVectorRepository` round-trip float arrays through SQLite BLOBs and support per-book reads plus full erasure |
| Embedding generation pipeline | `IEmbeddingGenerationService`, `ISemanticSearchReadModel`, `EmbeddingGenerationService`, and `EmbeddingGenerationWorker`; tests cover idempotency, unavailable Ollama degradation, failure jobs, progress events, and worker polling |
| Cosine similarity | `CosineSimilarityService` computes SIMD-backed cosine scores with deterministic top-K ordering |
| Semantic search service | `ISemanticSearchService` and `SemanticSearchService` embed the query locally, brute-force score stored vectors, join chunks back to books, deduplicate by book, and fall back to exact Phase 10 search when Ollama or embeddings are unavailable |
| Hybrid ranking formula | `IHybridRankingService` and `HybridRankingService` blend exact, semantic, recency, status, and rating signals with deterministic score/`BookId` ordering and no-embedding fallback |
| Architecture guard | Architecture tests verify Application semantic search does not depend on Infrastructure AI and the Ollama adapter remains an internal Infrastructure detail |

## Remaining Phase 11 Work

- WP2: backend generation service and worker are implemented locally; remaining work is broader batching/rate-limit tuning, model-change configuration, and UI surfacing of unavailable-Ollama events.
- WP3: cosine similarity, deterministic top-K, and semantic search service are implemented locally; P95 benchmark remains.
- WP4: hybrid ranking formula, defaults, determinism, and graceful no-embedding fallback are implemented locally; persistence-backed weight settings remain.
- WP5: match-location explanation and result enrichment.
- WP6: embedding erasure service with audit event plus ANN spike/ADR stub.
- WP7/WP8: UI, icons, i18n, accessibility, full regression, manual checks, and remote CI evidence.
