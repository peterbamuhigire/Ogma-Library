# Phase 25 — Versioned Embeddings and Vector Lifecycle

> [Roadmap index](./README.md) · [Previous](./phase-24-selective-ocr-and-extraction-quality.md) · [Next](./phase-26-semantic-and-hybrid-retrieval.md)

## Objective
Make embeddings reproducible, stale-detectable, scalable and safely rebuildable.

## Business/Product Rationale
Invisible stale vectors undermine semantic trust and waste AI cost.

## SDLC Requirements
FR-SEARCH-004/006, vector lifecycle, AI cost/version requirements.

## Current Repository State
`src/OgmaLibrary.Infrastructure/Search/EmbeddingGenerationService.cs` and vector entities store vectors/model dimensions; extractor/chunker/version cascades remain incomplete.

## Gap Analysis
No full source→extractor→chunker→embedding version key, robust cleanup or scalable retrieval decision.

## Architectural Impact
Define embedding provider abstraction separately from completion and a vector-index contract.

## Database Work
Chunk/source hash, extractor/chunker/model/provider/dimension/index versions, generated date, status and tombstones.

## Backend Work
Heading/page-aware chunking, batching, idempotency, cost/cache keys, dimension validation, side-by-side rebuild and deletion cleanup.

## Frontend Work
Index model/status, stale count, estimated cost/time, rebuild/cancel.

## PDF Processing Impact
Consumes selected versioned text/TOC.

## Metadata Impact
Separate metadata-document embeddings from content chunks.

## Search Impact
Vector query contract and candidate IDs.

## AI/RAG Impact
Primary deliverable.

## 3D Bookshelf Impact
None.

## External Integrations
Local Ollama plus provider adapter only through privacy gateway policy.

## Privacy Requirements
Remote embedding payload preview/consent and provider evidence; local option preserved.

## Security Requirements
Dimension/response validation, timeouts and no arbitrary endpoints without explicit configuration.

## Performance Requirements
Incremental batches, bounded memory, no all-vector load for target scale.

## Error & Recovery Behaviour
Failed new index leaves prior compatible index available; resume by chunk key.

## Logging/Observability
Chunks embedded/cache hit/failure, model/version/dimension, latency/tokens/cost.

## Testing
Unit chunk/version keys; DB constraints/tombstones; provider-recording pipeline; privacy/API validation; UI rebuild E2E; change/delete/model/dimension lifecycle; scale/memory/cost performance.

## Skills Engines Applied
`skills-web-dev` RAG/vector lifecycle; `srs-skills` traceability; digital research evidence for provider claims.

## Dependencies
Phases 17 and 23–24.

## Parallelisation
Chunker and vector backend evaluation may proceed; selection requires benchmark evidence.

## Migration Considerations
Legacy vectors remain isolated and are deleted only after new index verification.

## Definition of Done
- [x] Every vector is attributable to complete source/version tuple.
- [x] Changes/deletes/model shifts deterministically invalidate.
- [x] Rebuild is resumable and side-by-side.
- [x] Target scale avoids all-vector memory load.
- [x] Privacy/cost controls pass.

## Kaizen Review
1. Complexity: version tuple/index swap. 2. Share artifact manifests. 3. Simplify embedding callers. 4. Remove unversioned vectors/brute load. 5. Document rebuild math. 6. Pattern: compatibility-keyed projection. 7. Debt decreases.
