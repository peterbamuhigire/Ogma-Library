# Implementation Completeness

## Scoring method

Percentages are evidence-weighted ranges collapsed to a planning point, not line-count arithmetic. A capability receives credit for executable domain behavior, but loses credit when its trigger, persistence, failure recovery, UI or tests are disconnected. The strict status definition makes these values lower than the historical v2.1 traceability claim.

| Capability | Expected Scope | Actual Scope | Status | Completion % | Confidence |
| --- | --- | --- | --- | ---: | --- |
| Architecture/Foundation | Modular local-first desktop composition | Good project boundaries; oversized/incomplete composition | PARTIALLY IMPLEMENTED | 60 | High |
| Library Scanning | Multi-root, incremental, resumable, safe reconciliation | Recursive single-root manual scan | IMPLEMENTED BUT DEFECTIVE | 42 | High |
| File Identity | File/asset/edition/work distinction and moves | Book-level hash, placeholder file hash, unsafe tiers | REQUIRES REDESIGN | 22 | High |
| PDF Processing | Isolated validation, extraction, password/OCR recovery | Subprocess pipeline with incomplete containment/versioning | PARTIALLY IMPLEMENTED | 52 | High |
| Metadata Extraction | Embedded, filename, pages, ISBN, TOC | Useful primitives; sparse canonicalization | PARTIALLY IMPLEMENTED | 50 | High |
| Metadata Matching | Confidence, alternatives, review | Scoring/proposals but unsafe auto-apply | IMPLEMENTED BUT DEFECTIVE | 32 | High |
| External Enrichment | Resilient cached provider federation | Two adapters, weak durable cache/quota/fallback | PARTIALLY IMPLEMENTED | 43 | High |
| Cover Management | Source resolution, variants, custom covers, invalidation | One first-page JPEG; read model returns null | IMPLEMENTED BUT DEFECTIVE | 18 | High |
| Catalogue | Reliable 2D browsing and detail | Grid/list/detail partial; directory placeholder | PARTIALLY IMPLEMENTED | 52 | High |
| Collections/Organisation | Shelves, tags, favourites, status and notes | Schema/services and partial UI | PARTIALLY IMPLEMENTED | 45 | Medium |
| Structured Search | Fielded filters, sort, fast typeahead | SQL contains and partial filters | PARTIALLY IMPLEMENTED | 50 | High |
| Fuzzy Search | Typo and spelling tolerance | Not found | NOT STARTED | 0 | High |
| Full-Text Search | Page-aware FTS with lifecycle | FTS5/pages/chunks, incomplete recovery | PARTIALLY IMPLEMENTED | 52 | High |
| Semantic Search | Versioned vector retrieval at scale | Brute-force local cosine and incomplete lifecycle | IMPLEMENTED BUT DEFECTIVE | 30 | High |
| AI Reading Advisor | Intent, retrieval, rerank, grounding, explanation | Keyword-gated mockable pipeline, uncomposed runtime | REQUIRES REDESIGN | 18 | High |
| RAG/Embeddings | Extraction, chunks, versions, vectors, invalidation | Core pieces; naive chunking and stale-risk | PARTIALLY IMPLEMENTED | 32 | High |
| Book Details | Rich record, editor, provenance, actions | Read-only detail plus partial actions | PARTIALLY IMPLEMENTED | 48 | High |
| 2D Library UX | Premium accessible shell and state matrix | Functional skeleton with hard-coded styling/strings | IMPLEMENTED BUT NON-COMPLIANT | 40 | High |
| Reader | Responsive reading, annotations and recovery | Significant working surface; split/export gaps | PARTIALLY IMPLEMENTED | 58 | Medium-High |
| 3D Bookshelf | Hosted, textured, interactive, scalable, accessible | Brown-box renderer and disconnected bridge | SCAFFOLDED | 10 | High |
| Settings | Roots, providers, privacy, storage, themes, diagnostics | Fragmented sharing/settings surfaces | SCAFFOLDED | 20 | High |
| Classroom Modes | Secure host/client/admin in same app | Significant services and tests; no live acceptance | PARTIALLY IMPLEMENTED | 38 | Medium |
| Privacy | Clear local/external controls and erasure | Good concepts, incomplete reachability/governance | PARTIALLY IMPLEMENTED | 38 | High |
| Security | Path, PDF, LAN, secrets, update controls | Partial controls; sandbox/writeback/release failures | IMPLEMENTED BUT NON-COMPLIANT | 32 | High |
| Background Processing | Durable stages, leases, retry and cancellation | Generic polling jobs and partial statuses | IMPLEMENTED BUT DEFECTIVE | 35 | High |
| Failure Recovery | Root/provider/parser/AI failure isolation | Some retries/states; major ambiguity and swallowed save | PARTIALLY IMPLEMENTED | 30 | High |
| Performance | Reference budgets at target sizes | Mostly small/synthetic tests | SCAFFOLDED | 18 | High |
| Testing | Layered automated + platform/quality/security evidence | Strong 800-test automated suite; decisive gaps | PARTIALLY IMPLEMENTED | 60 | High |
| Deployment | Signed installers, updates, rollback and operations | CI build/test only | NOT STARTED | 5 | High |
| Documentation | Controlled, reconciled, current operational docs | Rich v2.1 pack with metadata/conflict/staleness issues | PARTIALLY IMPLEMENTED | 72 | High |

## Roll-up

| Dimension | Estimated completion | Confidence |
| --- | ---: | --- |
| Backend | 47% | High |
| Frontend | 39% | High |
| Database | 52% | High |
| PDF pipeline | 50% | High |
| Metadata pipeline | 37% | High |
| Search | 36% | High |
| AI/RAG | 22% | High |
| 3D experience | 10% | High |
| Security | 32% | High |
| Testing | 60% | High |
| Overall functional completion | **40–48%; planning point 44%** | Medium-High |

The range reflects uncertainty in physical Windows/macOS, live provider, classroom and performance behavior. It does not mean 44% of source lines remain. It means roughly 44% of the promised, release-evidenced product is currently complete.

## Keep / improve / refactor / rewrite / remove

| Classification | Existing work |
| --- | --- |
| KEEP | Project dependency direction and architecture tests; locked restore; EF migration discipline; FTS5 basis; field provenance concept; reader cache/annotation foundations; provider interfaces; AI tier/audit concepts; 800-test harness |
| IMPROVE | PDF extraction adapters; Google/Open Library integrations; reader; catalogue queries; LAN service boundaries; localisation resources; CI build/test matrix |
| REFACTOR | `CompositionRoot.cs`; direct EF/context patterns; generic job/status strings; logging; navigation/state handling; generated-asset manifests |
| REWRITE | File identity/reconciliation; metadata auto-apply/writeback; advisor candidate retrieval and evidence generation; native 3D hosting/render contract; PDF security boundary |
| REMOVE | Placeholder-hash generation; directory/split-view/3D “implemented” claims; obsolete phase comments; stale `CLAUDE.md` claims; dead/duplicate ADR and plan assertions after archival; emoji/mojibake pseudo-icons |

