# Technical Debt Register

| ID | Problem | Severity | Area | Location | Consequence | Remediation | Phase |
| --- | --- | --- | --- | --- | --- | --- | ---: |
| TD-001 | File hash/fingerprint stored on book rather than each file asset | BLOCKER | database/identity | `BookRow`, `BookFileRow` | Cannot model multiple files or changes safely | Replace with root/file/asset/edition/work model | 3–4 |
| TD-002 | Domain mapping fabricates a 64-character “hash” from path | CRITICAL | architecture | `BookRepository.GeneratePlaceholderHash` | False identity and misleading invariants | Remove; make unknown identity explicit | 3–4 |
| TD-003 | Identity service rehashes and uses unsafe size+mtime/fingerprint tiers | CRITICAL | ingestion | `BookIdentityService` | False merge, wasted I/O, duplicate corruption | Single versioned fingerprint pipeline and reviewable matches | 7–9 |
| TD-004 | ISBN/DOI identity tier is documented but returns new book | HIGH | metadata | `BookIdentityService` | Duplicate editions proliferate | Implement edition matching after canonical model | 9, 12 |
| TD-005 | Missing-file sweep ignores root availability and scan success | BLOCKER | library integrity | `UnavailableFileFlagService` | External-drive disconnect corrupts availability state | Root-scoped scan sessions and absence confirmation | 5, 8 |
| TD-006 | Path containment uses string prefix and ignores platform/symlink semantics | CRITICAL | security | `PdfDiscoveryService` and validators | Traversal/root confusion | Canonical platform adapter with boundary-aware comparisons | 5 |
| TD-007 | Only one effective library root | HIGH | ingestion | composition/settings | Violates multi-root and portability requirements | First-class roots with stable IDs/bookmarks/health | 5 |
| TD-008 | Discovery and registration are not fully durable jobs | HIGH | jobs | ingestion orchestrator | Crash loses progress or causes duplicate work | Scan sessions, staged jobs, idempotency keys | 6–8 |
| TD-009 | Generic polling jobs lack leases/atomic claims/backoff | CRITICAL | infrastructure | workers/Jobs table | Duplicate processing and unreliable recovery | Lease-based state machine and retry schedule | 6, 17 |
| TD-010 | Terminal job save failure is swallowed | CRITICAL | reliability | `BookIngestionWorker` | Silent stuck/inconsistent jobs | Fail-safe persistence and poison queue | 17 |
| TD-011 | Automatic enrichment writes to original PDFs without confirmation | BLOCKER | metadata/integrity | `BookMetadataEnrichmentService` | Modifies user files and invalidates identity silently | Disable immediately; preview/confirm/backup/rehash workflow | 14–15 |
| TD-012 | Weak provider match can auto-apply at 0.70 | CRITICAL | metadata | confidence/enrichment services | Catalogue corruption | Calibrated rules and mandatory review for ambiguity | 12–14 |
| TD-013 | Provider results lack durable cache/quota/fallback policy | HIGH | metadata | provider adapters | Rate-limit failures and repeated leakage/cost | Provider gateway with TTL, negative cache and provenance | 13 |
| TD-014 | No complete manual metadata/provenance editor | HIGH | frontend | book detail/Avalonia views | Users cannot safely curate | Review/editor with override locks and undo | 14 |
| TD-015 | Covers are single-size first-page JPEG and read model returns null | HIGH | covers/frontend | `ThumbnailService`, `CatalogueReadModel` | Signature visual product shows placeholders | Asset resolver, variants, manifests, UI contract | 16 |
| TD-016 | Spine job supported but never enqueued | MEDIUM | covers/3D | registration/worker | 3D assets never produced | Unified asset pipeline | 16 |
| TD-017 | Search has no fuzzy matching | HIGH | search | search services | Common misspellings fail | Add normalized/fuzzy catalogue index | 22 |
| TD-018 | Hybrid search combines incompatible scores naively | CRITICAL | search | combined search service | Unstable relevance | Calibrated fusion and benchmark | 26 |
| TD-019 | Semantic search loads all vectors and brute-force scores them | HIGH | AI/RAG | embedding/search service | Does not scale to required catalogue sizes | Versioned ANN/vector strategy or bounded two-stage retrieval | 25–26 |
| TD-020 | Chunking is whitespace/page based without structural version | HIGH | RAG | extraction/chunking | Poor evidence and stale vectors | Heading/page-aware chunker with explicit versions | 23–25 |
| TD-021 | Advisor retrieves by literal metadata before semantic ranking | BLOCKER | AI/RAG | candidate reader/advisor | Signature feature fails conceptual queries | Rewrite intent→retrieve→rerank→evidence pipeline | 28 |
| TD-022 | AI core services are not fully registered in runtime composition | CRITICAL | architecture/AI | `AiServiceExtensions`, `CompositionRoot` | Advisor cannot resolve reliably | Typed provider/gateway composition and health checks | 27 |
| TD-023 | Advisor/privacy/plan views are not reachable in shell | HIGH | frontend/AI | App views/navigation | Scaffolding mistaken for product | Integrate only after safe runtime/retrieval | 27–30 |
| TD-024 | Answer mode throws `NotImplementedException` | HIGH | AI/RAG | advisor service | Documented requirement absent | Build cited answer mode after retrieval freeze | 29–30 |
| TD-025 | Explanations have no source-labeled evidence | CRITICAL | AI quality | advisor DTO/prompt/UI | Hallucinated claims cannot be distinguished | Evidence DTO, citation validation and abstention | 29 |
| TD-026 | 3D native WebView adapters and bootstrap are not implemented | BLOCKER | 3D | Bookshelf3D bridge/App view | Signature experience cannot start | Implement Windows WebView2/macOS WKWebView adapters | 31 |
| TD-027 | 3D renderer shows brown boxes and ignores cover/spine URIs | HIGH | 3D/frontend | `src/shelf3d` | Visual gimmick, not a bookshelf | Atlas/texture/spine renderer and shelf geometry | 32 |
| TD-028 | 3D performance test measures layout math, not frame rate | HIGH | performance/testing | shelf perf script | False performance confidence | In-WebView GPU telemetry on reference hardware | 33 |
| TD-029 | PDF “sandbox” is only a child process with environment flags | BLOCKER | security | PDF worker/`WindowsChildProcessLimit` | Malicious PDF retains user-level access | Real OS containment, brokered I/O and resource limits | 10, 37 |
| TD-030 | Password passes through process environment | HIGH | security | PDF worker invocation | Inspectable secret exposure | One-shot IPC/secure handle and zeroisation | 10 |
| TD-031 | No central structured, privacy-classified logging | HIGH | observability | cross-cutting | Failures cannot be diagnosed safely | Event IDs, redaction, metrics, bounded retention | 17, 38 |
| TD-032 | App startup synchronously blocks on migration/init | HIGH | performance | `App.axaml.cs` | Frozen/slow launch | Async startup coordinator and degraded shell | 2, 38 |
| TD-033 | UI uses Inter/Fluent and hard-coded colors/strings contrary to v2.1 | HIGH | design system | AXAML/resources | Incoherent and non-compliant brand | Tokenised Ogma theme and typography | 18 |
| TD-034 | Mojibake/emoji/glyph pseudo-icons and stale phase labels | MEDIUM | frontend | catalogue/reader AXAML | Unprofessional, inaccessible UI | Licensed SVG/icon resources and content audit | 18 |
| TD-035 | Localisation is partial and mixed with hard-coded strings | HIGH | frontend | AXAML/localization service | Cannot meet language/accessibility requirements | Resource extraction and pseudo-localisation gate | 18, 39 |
| TD-036 | Work/edition tables are schema-only | CRITICAL | database/domain | EF model/migrations | Cannot distinguish editions/works | Populate via controlled identity workflows | 3–4, 9 |
| TD-037 | Composition root is oversized and manually inconsistent | HIGH | maintainability | `CompositionRoot.cs` | Missing bindings and difficult testing | Module registrars/options validation | 2 |
| TD-038 | Release packaging/signing/update trust chain absent | BLOCKER | deployment/security | CI/repository | Cannot ship trusted application | Signed Windows/macOS pipeline and rollback proof | 38–39 |
| TD-039 | CI hybrid gate is informational placeholder | MEDIUM | governance | `.github/workflows/ci.yml` | Required evidence can be skipped | Vendor/pin executable gate or remove claim | 1, 39 |
| TD-040 | Historical docs/ADRs/CLAUDE claims are duplicated or stale | MEDIUM | documentation | `docs/`, `CLAUDE.md` | Agents and reviewers follow false state | Archive/index and regenerate current controls | 1, 39 |

