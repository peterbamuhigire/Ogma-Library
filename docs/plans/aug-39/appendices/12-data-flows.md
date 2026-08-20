# Data Flows

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

## PDF Ingestion

```mermaid
flowchart TD
 Root[Approved library root] --> Scan[Root-scoped scan session]
 Scan --> Observe[Immutable file observation]
 Observe --> Reconcile[File/content identity + reconciliation]
 Reconcile --> Validate[Brokered PDF validation]
 Validate --> Extract[Versioned metadata/text/TOC/render]
 Extract --> Proposals[Metadata proposals]
 Proposals --> Review{Confidence/user review}
 Review --> Canonical[Canonical edition/work metadata]
 Extract --> Assets[Cover/spine variants]
 Canonical --> DB[(SQLite system of record)]
 Assets --> Derived[(Versioned sidecars/cache)]
 Validate -. failure .-> State[Typed stage failure/retry]
 Extract -. failure .-> State
```

No unsuccessful or incomplete root scan can infer deletion. No proposal modifies an original PDF. Optional writeback is a distinct confirmed flow:

```mermaid
flowchart LR
 Edit[Accepted catalogue edit] --> Preview[Exact PDF diff]
 Preview --> Confirm{Explicit confirmation}
 Confirm -->|no| Stop[Catalogue only]
 Confirm -->|yes| Check[Root/hash/permission check]
 Check --> Backup[Verified local backup]
 Backup --> Write[Atomic write/replace]
 Write --> Verify[Rehash + validate]
 Verify --> Invalidate[Invalidate derived artifacts]
 Verify -. failure .-> Restore[Restore original]
```

## Content Intelligence

```mermaid
flowchart LR
 PDF[Validated content asset] --> Extract[Page-aware text/TOC]
 Extract --> Quality{Page quality}
 Quality -->|good| Select[Selected text source]
 Quality -->|image/low| OCR[Selective local OCR]
 OCR --> Select
 Select --> FTS[FTS5 projection]
 Select --> Chunk[Heading/page-aware versioned chunks]
 Chunk --> Embed[Policy-governed embeddings]
 Embed --> Vector[Versioned vector index]
 FTS --> Evidence[Page evidence]
 Vector --> Evidence
```

Every derived node carries source hash and producer/config version. Changes create a new compatible projection; old data is removed only after verification.

## AI Advisor

```mermaid
flowchart TD
 Request[User reading request] --> Intent[Intent/constraints]
 Intent --> Filter[Availability + structured filters]
 Intent --> Retrieve[Structured + FTS + semantic candidates]
 Filter --> Retrieve
 Retrieve --> Rerank[Constraint/diversity reranking]
 Rerank --> Evidence[Source-labeled metadata/pages]
 Evidence --> Local[Deterministic match/trade-off fallback]
 Evidence --> Gateway{AI enabled + consent?}
 Gateway -->|no| Result[Grounded recommendations]
 Gateway -->|yes| Preview[Exact payload preview/tier]
 Preview --> Provider[Local/cloud provider]
 Provider --> Validate[ID/citation/claim validation]
 Validate --> Result
 Provider -. unavailable/invalid .-> Local
```

The provider never chooses from the entire unbounded library. Candidate IDs are already catalogue-valid; explanations cannot add books. Core catalogue and retrieval continue without the completion model.

## 3D Shelf

```mermaid
flowchart LR
 API[C# paged catalogue projection] --> Layout[Scene section/layout DTO]
 Assets[C# cover/spine asset resolver] --> Layout
 Layout --> Host[WebView2/WKWebView secure host]
 Host --> Models[Instanced/virtualised book models]
 Models --> Render[Three.js shelves/textures/render]
 Render --> Interact[Hover/focus/select/camera]
 Interact --> Message[Validated semantic action]
 Message --> Shell[Avalonia detail/reader/search]
 Shell --> API
 API --> TwoD[Grid/list accessible equivalent]
```

The WebView cannot read arbitrary paths or navigate externally. It receives opaque asset IDs and sanitized display text.

## Privacy and provider egress

```mermaid
flowchart TB
 Local[(Local PDFs/catalogue/notes/history)] --> MetaQ[Minimized ISBN/title/author query]
 MetaQ --> MetaProviders[Google Books/Open Library]
 Local --> AIGateway[Enforced AI gateway]
 AIGateway --> Tier1[Request only]
 AIGateway --> Tier2[Request + selected metadata]
 AIGateway --> Tier3[Request + consented passages]
 Tier1 --> LocalModel[Local Ollama]
 Tier2 --> Cloud[Approved cloud provider]
 Tier3 --> Cloud
 AIGateway --> Audit[(Local redacted audit/cost)]
```

Personal notes are excluded from external payloads by default. The privacy centre shows exact provider/tier/payload, retention evidence and delete controls.

## Classroom host/client

```mermaid
flowchart LR
 Private[(Private local catalogue)] --> Publish[Explicit published projection]
 Publish --> Host[TLS/RBAC/rate-limited LAN host]
 Host --> Pair[TOFU-paired C# desktop client]
 Pair --> Cache[(Host/user-scoped offline cache)]
 Pair --> Reader[Shared desktop reader UI]
 Reader --> PrivateState[(Private user reading/annotation state)]
 PrivateState --> Sync[Idempotent authorized sync]
 Sync --> Host
 Private -. never directly exposed .-> Host
```

Standalone mode does not start `Host`. School-managed AI remains host-side and is restricted to published evidence and policy/quotas.


