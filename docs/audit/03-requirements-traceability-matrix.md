# Requirements Traceability Matrix

## Method

Status is based on the strict end-to-end definition in the audit brief. “Code evidence” means executable implementation, not merely a type or route. “Test evidence” is scoped: a mock/headless test does not prove live providers, physical macOS, WebView/GPU, security containment or packaging. The normative source is `Ogma-Library_SRS_v2.1_2026-08-13.docx`; PRD, HLD, DPIA and User Guide provide supporting intent.

## Functional requirements

| ID | Requirement | Source | Domain | Code Evidence | DB Evidence | UI Evidence | Test Evidence | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FR-LIB-001 | Configure library roots and recursively discover PDFs | SRS §Library | Ingestion | `PdfDiscoveryService`, composition settings | File/root intent incomplete | Folder picker | Discovery tests | PARTIALLY IMPLEMENTED | Effectively one root; no root health model |
| FR-LIB-002 | Apply exclusions and safe path rules | SRS §Library | Integrity | discovery filters | None dedicated | Limited | Unit tests | IMPLEMENTED BUT DEFECTIVE | Prefix check, symlink and platform-case rules unsafe |
| FR-LIB-003 | Detect content, rename and move by identity | SRS §Library | Identity | `BookIdentityService` | Hash on book, not file | Status only | Identity tests | REQUIRES REDESIGN | Placeholder hashes and false-match tiers |
| FR-LIB-004 | Mark unavailable without deleting curated records | SRS §Library | Integrity | `UnavailableFileFlagService` | file status | Badge/state | Tests | IMPLEMENTED BUT DEFECTIVE | Disconnected root is treated as missing files |
| FR-LIB-005 | Background non-blocking ingest | SRS §Library | Jobs | orchestrator/worker | generic Jobs | scan progress | Worker tests | PARTIALLY IMPLEMENTED | discovery/registration not fully durable; startup blocks |
| FR-LIB-006 | Incremental/resumable scanning | SRS §Library | Ingestion | mtime/size checks | scan fields partial | rescan | Tests partial | PARTIALLY IMPLEMENTED | no scan session/checkpoint/watch lifecycle |
| FR-LIB-007 | Library health, retry and recovery | SRS §Library | Resilience | health services partial | error strings | health view | Headless tests | PARTIALLY IMPLEMENTED | root/provider/stage diagnostics fragmented |
| FR-CAT-001 | Grid, list, directory and 3D parity | SRS §Catalogue | UX | catalogue + shelf scaffolds | catalogue | grid/list; placeholders | UI tests | IMPLEMENTED BUT NON-COMPLIANT | directory/3D unavailable; covers disconnected |
| FR-CAT-002 | Filter and sort catalogue | SRS §Catalogue | UX | read model/query DTOs | indexes partial | controls | Tests partial | PARTIALLY IMPLEMENTED | visible sort control is not fully wired |
| FR-CAT-003 | Shelves/collections/tags/favourites | SRS §Catalogue | Organisation | services exist | join tables | partial UI | Unit/UI tests | PARTIALLY IMPLEMENTED | end-to-end curation incomplete |
| FR-CAT-004 | Complete book detail | SRS §Catalogue | UX | detail read model | fields exist | detail view | UI tests | PARTIALLY IMPLEMENTED | cover, provenance/editor/reanalysis gaps |
| FR-CAT-005 | Bulk edit with preview and undo | SRS §Catalogue | Metadata | no complete flow | audit/proposal pieces | absent | none | NOT STARTED | Safe batch curation absent |
| FR-CAT-006 | Smart shelves | SRS §Catalogue | Organisation | no complete evaluator | schema partial | absent | none | SCAFFOLDED | No saved query lifecycle |
| FR-CAT-007 | Work/edition merge and split | SRS §Catalogue | Identity | entity types only | Work/Edition tables | absent | schema tests | REQUIRES REDESIGN | No population/reconciliation workflow |
| FR-META-001 | Detect ISBN from filename and PDF pages | SRS §Metadata | Metadata | ISBN detectors | metadata fields | indirect | Unit tests | IMPLEMENTED WITHOUT TESTS | Core has tests; real corpus coverage absent |
| FR-META-002 | Query approved bibliographic providers | SRS §Metadata | Integration | Google/Open Library adapters | raw response/proposals | Enrich trigger | Mock tests | PARTIALLY IMPLEMENTED | Live/quota/cache evidence absent |
| FR-META-003 | Confidence-based merge and review | SRS §Metadata | Metadata | confidence service | proposals/confidence | review incomplete | Unit tests | IMPLEMENTED BUT DEFECTIVE | threshold can auto-apply weak matches |
| FR-META-004 | Field-level provenance and protected overrides | SRS §Metadata | Metadata | provenance service | provenance rows | not exposed fully | Unit tests | PARTIALLY IMPLEMENTED | user editor/override lifecycle missing |
| FR-META-005 | Confirmed, reversible PDF writeback | SRS §Metadata | Integrity | enrichment writeback | audit partial | no confirmation | tests do not prove consent | IMPLEMENTED BUT NON-COMPLIANT | Automatically writes to original PDFs: P0 |
| FR-META-006 | Provider resilience, timeout, retry and cache | SRS §Metadata | Resilience | HTTP rate handler | no durable cache policy | degraded state weak | Mock tests | PARTIALLY IMPLEMENTED | Missing persistent cache/backoff/quota contract |
| FR-META-007 | Metadata quality and review queue | SRS §Metadata | Metadata | quality fields partial | proposals | incomplete | partial | PARTIALLY IMPLEMENTED | No coherent possible-match queue |
| FR-META-008 | Metadata health and reprocess controls | SRS §Metadata | Operations | re-enrich pieces | job/error data | partial | partial | PARTIALLY IMPLEMENTED | Stage/version visibility incomplete |
| FR-READ-001 | Open local PDF and resume position | SRS §Reader | Reader | reader services | reading state | reader view | tests | IMPLEMENTED WITHOUT TESTS | Physical Windows/macOS file-access acceptance absent |
| FR-READ-002 | Page navigation and zoom | SRS §Reader | Reader | implemented | state | controls | UI/unit tests | IMPLEMENTED |
| FR-READ-003 | Layout/fullscreen controls | SRS §Reader | Reader | partial | settings | controls | headless tests | PARTIALLY IMPLEMENTED | Physical-platform UX not proven |
| FR-READ-004 | In-document search | SRS §Reader | Search | page text search | pages/FTS | reader search | tests | PARTIALLY IMPLEMENTED | extraction quality and match navigation incomplete |
| FR-READ-005 | Bookmarks | SRS §Reader | Reader | services | bookmarks | pane | tests | IMPLEMENTED WITHOUT TESTS | No physical-platform persistence evidence |
| FR-READ-006 | Highlights, notes and annotations | SRS §Reader | Reader | annotation services | annotation rows | pane/tools | tests | PARTIALLY IMPLEMENTED | coordinate fidelity/export/manual UX not accepted |
| FR-READ-007 | Password-protected PDF handling | SRS §Reader | PDF | password provider | no password persistence intended | prompt path | unit tests | PARTIALLY IMPLEMENTED | password passed through process environment; platform validation absent |
| FR-READ-008 | OCR for image-only PDFs | SRS §Reader | OCR | Tesseract adapter | OCR fields/jobs | limited | fixtures partial | PARTIALLY IMPLEMENTED | quality/language/resource policy incomplete |
| FR-READ-009 | Citation capture | SRS §Reader | Reader | citation service partial | annotations | partial | tests partial | PARTIALLY IMPLEMENTED | Evidence/export UX incomplete |
| FR-READ-010 | Split/parallel reading | SRS §Reader | UX | placeholder | none specific | placeholder | placeholder test | SCAFFOLDED | Test codifies non-feature |
| FR-READ-011 | Export annotations/citations | SRS §Reader | Portability | export pieces | source data | incomplete | unit tests partial | PARTIALLY IMPLEMENTED | Round-trip and format acceptance absent |
| FR-READ-012 | Reading memory/context | SRS §Reader | Reader | service/view | memory rows | pane | tests | PARTIALLY IMPLEMENTED | Advisor linkage and privacy controls incomplete |
| FR-READ-013 | Annotation layers | SRS §Reader | Reader | services | layer rows | pane | tests | PARTIALLY IMPLEMENTED | Collaboration/visibility acceptance incomplete |
| FR-READ-014 | Durable recovery after restart | SRS §Reader | Reliability | persistence exists | SQLite | resume | tests partial | IMPLEMENTED WITHOUT TESTS | Crash/kill durability not tested |
| FR-READ-015 | Reader accessibility and keyboard operation | SRS §Reader | Accessibility | commands/labels partial | N/A | controls | headless tests | PARTIALLY IMPLEMENTED | Narrator/VoiceOver/WCAG evidence absent |
| FR-SEARCH-001 | Metadata typeahead within 150 ms | SRS §Search | Search | SQL contains query | indexes | search panel | small perf tests | PARTIALLY IMPLEMENTED | No reference 50k workload |
| FR-SEARCH-002 | Page-aware full-text search | SRS §Search | Search | FTS5 services | virtual table/pages | results | tests | PARTIALLY IMPLEMENTED | extraction/index recovery gaps |
| FR-SEARCH-003 | Show match location and navigate | SRS §Search | UX | result DTOs | page refs | result UI | tests partial | PARTIALLY IMPLEMENTED | End-to-end reader jump incomplete |
| FR-SEARCH-004 | Semantic search | SRS §Search | AI/Search | Ollama vector service | vectors | search mode | mock tests | IMPLEMENTED BUT DEFECTIVE | Brute-force all vectors; lifecycle incomplete |
| FR-SEARCH-005 | Hybrid search and ranking | SRS §Search | Search | combined service | FTS/vectors | results | synthetic tests | IMPLEMENTED BUT DEFECTIVE | Incompatible score scales combined naively |
| FR-SEARCH-006 | Index manager/status/rebuild | SRS §Search | Operations | `IndexManagerService` | status/jobs | panel | tests | PARTIALLY IMPLEMENTED | Version cascade and stale cleanup incomplete |
| FR-AI-001 | AI disabled by default and optional | SRS §AI | Privacy | tier/settings concepts | settings/audit | privacy views unlinked | unit tests | PARTIALLY IMPLEMENTED | Runtime settings path incomplete |
| FR-AI-002 | Provider-neutral completion and embedding gateway | SRS §AI | AI | adapters/extensions | audit | no settings journey | tests | IMPLEMENTED BUT DEFECTIVE | `IAiGateway`, provider and preview gate not composed |
| FR-AI-003 | Recommend only available catalogue books | SRS §AI | Advisor | ID validation | books/history | advisor view unlinked | mock tests | PARTIALLY IMPLEMENTED | unavailable/semantic retrieval flow weak |
| FR-AI-004 | Metadata-only recommendation tier | SRS §AI | Advisor | candidate payload builder | history/audit | view unlinked | tests | PARTIALLY IMPLEMENTED | literal metadata search can return zero candidates |
| FR-AI-005 | Explicit opt-in content-aware tier | SRS §AI | Privacy/RAG | tier model | audit | preview dialog unlinked | tests partial | SCAFFOLDED | Advisor does not assemble passage evidence |
| FR-AI-006 | Local model option | SRS §AI | AI | Ollama adapters | config partial | no complete setup | mock tests | PARTIALLY IMPLEMENTED | Live lifecycle/health not accepted |
| FR-AI-007 | Reading plans | SRS §AI | Advisor | parser/view model | history | view unlinked | tests | SCAFFOLDED | Not reachable as a complete use case |
| FR-AI-008 | Answer mode with citations | SRS §AI | RAG | throws `NotImplementedException` | schema pieces | view absent | expected-failure tests | NOT STARTED | Explicitly unimplemented |
| FR-AI-009 | Advisor history and deletion | SRS §AI | Privacy | history service | history rows | privacy view unlinked | tests | PARTIALLY IMPLEMENTED | Stores full queries and summaries; UX incomplete |
| FR-AI-010 | Cost/token visibility and limits | SRS §AI | Cost | accounting fields | audit rows | partial | tests | PARTIALLY IMPLEMENTED | Provider usage/runtime UI incomplete |
| FR-AI-011 | Privacy centre and payload preview | SRS §AI | Privacy | services/views | consent/audit | views not in shell | headless tests | SCAFFOLDED | Controls exist but users cannot reliably reach them |
| FR-LAN-001 | Opt-in host; no listener by default | SRS §LAN | Classroom | host service/settings | config | sharing settings | tests | PARTIALLY IMPLEMENTED | Physical socket/default verification absent |
| FR-LAN-002 | TLS identity, discovery and TOFU | SRS §LAN | Security | certificate/mDNS services | trust records | setup partial | unit/integration tests | PARTIALLY IMPLEMENTED | Multi-machine Windows/macOS evidence absent |
| FR-LAN-003 | Publish selected roots/books only | SRS §LAN | Security | published read model | publication rows | admin UI partial | tests | PARTIALLY IMPLEMENTED | Hostile path tests incomplete |
| FR-LAN-004 | Authenticated role-based access | SRS §LAN | Security | auth/authorization services | users/roles | partial | tests | PARTIALLY IMPLEMENTED | Live attack/expiry coverage absent |
| FR-LAN-005 | Range-safe PDF streaming | SRS §LAN | File access | endpoints/range handling | publication link | client reader | tests | PARTIALLY IMPLEMENTED | Hostile and large-file tests absent |
| FR-LAN-006 | Classroom catalogue/search endpoints | SRS §LAN | Classroom | host API | read model | client UI | tests | PARTIALLY IMPLEMENTED | Physical LAN latency/load absent |
| FR-LAN-007 | Per-user private state isolation | SRS §LAN | Privacy | services | user-scoped rows | client UI | tests | PARTIALLY IMPLEMENTED | Adversarial tenant isolation not proven |
| FR-LAN-008 | Audit, quota and session controls | SRS §LAN | Security | services partial | audit/quota | admin views | tests partial | PARTIALLY IMPLEMENTED | Operational review absent |
| FR-LAN-009 | Service health and graceful shutdown | SRS §LAN | Operations | health services | status | settings | tests partial | PARTIALLY IMPLEMENTED | Real interruption/restart evidence absent |
| FR-LAN-010 | Standalone isolation from classroom mode | SRS §LAN | Architecture | opt-in composition | settings | mode UI | architecture tests | IMPLEMENTED WITHOUT TESTS | Network capture/physical verification absent |
| FR-CLIENT-001 | Pair desktop client with classroom host | SRS §Client | Classroom | pairing services | trust/session | pairing view | tests | PARTIALLY IMPLEMENTED | No physical two-machine acceptance |
| FR-CLIENT-002 | Secure credential storage | SRS §Client | Security | DPAPI/Keychain adapters | token refs | settings | unit tests | PARTIALLY IMPLEMENTED | Keychain physical validation absent |
| FR-CLIENT-003 | Browse/search published catalogue | SRS §Client | Classroom | client services | cache | views | tests | PARTIALLY IMPLEMENTED | Live host/client acceptance absent |
| FR-CLIENT-004 | Stream/open published PDFs | SRS §Client | Classroom | streaming client | cache | reader integration | tests partial | PARTIALLY IMPLEMENTED | Real range/reconnect/platform proof absent |
| FR-CLIENT-005 | Offline cache with policy | SRS §Client | Offline | cache services | cache records | state UI partial | tests | PARTIALLY IMPLEMENTED | Quota/eviction/recovery not accepted at scale |
| FR-CLIENT-006 | Sync private reading state | SRS §Client | Sync | sync services | state rows | views | tests | PARTIALLY IMPLEMENTED | Conflict/replay physical tests absent |
| FR-CLIENT-007 | Private annotations | SRS §Client | Privacy | scoped service | scoped annotations | reader | tests | PARTIALLY IMPLEMENTED | Hostile cross-user tests insufficient |
| FR-CLIENT-008 | Student search/advisor | SRS §Client | AI | smart-search view/services | history | view | mock tests | SCAFFOLDED | Depends on broken advisor/runtime |
| FR-CLIENT-009 | Reconnect and resume | SRS §Client | Reliability | retry components | state | status | tests partial | PARTIALLY IMPLEMENTED | Network interruption acceptance absent |
| FR-CLIENT-010 | Clear local classroom data | SRS §Client | Privacy | deletion services partial | cache/token tables | settings partial | tests partial | PARTIALLY IMPLEMENTED | End-to-end erasure proof absent |
| FR-CLIENT-011 | Host unavailable/degraded states | SRS §Client | UX | state models | cache | states partial | tests | PARTIALLY IMPLEMENTED | Full state matrix absent |
| FR-CLIENT-012 | Client accessibility/localisation | SRS §Client | Accessibility | localisation partial | N/A | views | headless tests | PARTIALLY IMPLEMENTED | Language and physical AT gaps |
| FR-CLIENT-013 | Client diagnostics without private leakage | SRS §Client | Operations | diagnostics partial | logs sparse | partial | tests sparse | SCAFFOLDED | No structured redacted logging |
| FR-ADMIN-001 | Admin setup and role management | SRS §Admin | Classroom | services | admin rows | views partial | tests | PARTIALLY IMPLEMENTED | Complete journey not accepted |
| FR-ADMIN-002 | Publish/unpublish catalogue scope | SRS §Admin | Classroom | services | publication rows | views partial | tests | PARTIALLY IMPLEMENTED | Live revocation behavior absent |
| FR-ADMIN-003 | Manage users/classes | SRS §Admin | Classroom | services | tables | views partial | tests | PARTIALLY IMPLEMENTED | UX and migration completeness unclear |
| FR-ADMIN-004 | Configure permissions/policies | SRS §Admin | Security | policy services | policy rows | partial | tests | PARTIALLY IMPLEMENTED | Enforcement matrix incomplete |
| FR-ADMIN-005 | Manage quotas | SRS §Admin | Cost | quota service | quota rows | partial | tests | PARTIALLY IMPLEMENTED | Live concurrency/provider limits unproven |
| FR-ADMIN-006 | Configure managed AI | SRS §Admin | AI | adapters/settings | config/audit | partial | mock tests | SCAFFOLDED | Core AI composition broken |
| FR-ADMIN-007 | Protect school AI keys | SRS §Admin | Security | secret stores | secret references | partial | unit tests | PARTIALLY IMPLEMENTED | Operational key custody absent |
| FR-ADMIN-008 | Audit classroom activity safely | SRS §Admin | Privacy | audit services | audit rows | partial | tests | PARTIALLY IMPLEMENTED | Retention/redaction review absent |
| FR-ADMIN-009 | Revoke sessions/devices | SRS §Admin | Security | session services | session rows | partial | tests | PARTIALLY IMPLEMENTED | Live revocation timing absent |
| FR-ADMIN-010 | Export/backup classroom configuration | SRS §Admin | Recovery | partial services | data exists | absent/incomplete | sparse | SCAFFOLDED | No verified restore path |
| FR-ADMIN-011 | Health/diagnostics dashboard | SRS §Admin | Operations | health pieces | status | partial | tests | SCAFFOLDED | No structured observability pipeline |
| FR-ADMIN-012 | Minors/privacy policy controls | SRS §Admin | Privacy | policy models partial | policy rows | partial | tests sparse | SCAFFOLDED | DPIA controls not operationally proven |
| FR-ADMIN-013 | Admin accessibility/localisation | SRS §Admin | Accessibility | partial | N/A | views | headless tests | PARTIALLY IMPLEMENTED | Physical AT/language coverage absent |
| FR-EXT-001 | Provider/plugin extension points | SRS §Extensions | Extensibility | attributes/interfaces | N/A | no manager | architecture tests | SCAFFOLDED | Contract exists; safe discovery/lifecycle absent |
| FR-EXT-002 | Read-only local integration API | SRS §Extensions | API | no complete endpoint | N/A | settings absent | none | NOT STARTED | Must remain local/desktop scoped |
| FR-EXT-003 | Import from Zotero/Calibre/Goodreads and themes | SRS §Extensions | Import | no complete adapters | no import staging | absent | none | NOT STARTED | Separate import formats need validation/review |
| FR-UX-001 | First run and browse while scan continues | SRS §UX | UX | async scan partial | jobs | empty/scan views | headless tests | PARTIALLY IMPLEMENTED | startup blocking and non-durable discovery undermine promise |
| FR-UX-002 | Complete loading/empty/error/degraded states | SRS §UX | UX | state models partial | error data | some states | UI tests | PARTIALLY IMPLEMENTED | No product-wide state matrix |
| FR-UX-003 | Command palette/keyboard productivity | SRS §UX | UX | commands partial | N/A | incomplete | sparse | SCAFFOLDED | No complete discoverable palette |
| FR-UX-004 | Localisation: en/fr then es/it/de | SRS §UX | i18n | localisation service | N/A | mixed resource/hard-coded text | tests partial | PARTIALLY IMPLEMENTED | en/fr incomplete; final languages absent |
| FR-UX-005 | Accessible 2D alternative and keyboard flow | SRS §UX | Accessibility | grid/list | N/A | available but incomplete | headless tests | PARTIALLY IMPLEMENTED | 3D is unavailable; AT acceptance absent |
| FR-UX-006 | Offline/degraded help and recovery guidance | SRS §UX | UX | partial | status | partial | sparse | SCAFFOLDED | Provider/root-specific guidance incomplete |
| FR-UX-007 | Resume interrupted work within 60 seconds | SRS §UX | Reliability | state persistence partial | SQLite/jobs | status partial | no crash acceptance | PARTIALLY IMPLEMENTED | Generic jobs and swallowed save failure weaken recovery |
| FR-UX-008 | Coherent themes and visual design system | SRS §UX | Design | tokens/theme | N/A | Fluent/Inter, hard-coded values | screenshots/headless | IMPLEMENTED BUT NON-COMPLIANT | Conflicts with v2.1 Spectral/Public Sans direction |

## Non-functional requirement summary

| IDs | Requirement family | Evidence | Status | Key gap |
| --- | --- | --- | --- | --- |
| NFR-OGMA-001..009 | Local-first behavior, portability, data integrity, platform compatibility, privacy and accessibility | Architecture/tests/SQLite | PARTIALLY IMPLEMENTED | Physical macOS, backup/reimport, accessibility and offline failure evidence absent |
| NFR-PROD-001..004 | Startup/catalogue/search/reader performance | Small synthetic tests | PARTIALLY IMPLEMENTED | No 2k/5k/50k reference data or physical hardware proof |
| NFR-PROD-005..009 | 3D frame rate, AI latency, durability, recoverable jobs, non-blocking UI | Scaffold/perf arithmetic/tests | IMPLEMENTED BUT DEFECTIVE | No real WebView/GPU, AI path, crash/lease testing |
| NFR-PROD-010..014 | Crash-free, accessibility, portability, safe destructive operations, signed distribution | Plans and partial tests | IMPLEMENTED BUT NON-COMPLIANT | Automatic PDF writeback; no signing/notarization/update/rollback |
| NFR-LAN-001..003 | Secure/local LAN performance and isolation | Code/mock tests | PARTIALLY IMPLEMENTED | No physical hostile/multi-machine/load evidence |
| NFR-CLIENT-001..003 | Client performance, secure credential handling and offline recovery | Code/mock tests | PARTIALLY IMPLEMENTED | No physical platform/network acceptance |

## Security and privacy control summary

| IDs | Control family | Evidence | Status | Key gap |
| --- | --- | --- | --- | --- |
| CTRL-001..004 | OS secret stores, redaction and deletion | DPAPI/Keychain/adapters | PARTIALLY IMPLEMENTED | Full user lifecycle and physical Keychain proof absent |
| CTRL-005..008 | Untrusted PDF isolation and resource limits | Child process/Windows Job Object | IMPLEMENTED BUT NON-COMPLIANT | Environment flags are not a network/filesystem sandbox; no macOS containment |
| CTRL-009..011 | Canonical roots, symlink consent, writeback boundary/backup/audit | scattered validators/writeback | IMPLEMENTED BUT DEFECTIVE | Root prefix and automatic writeback violate controls |
| CTRL-012..013 | Signed artifacts and independently verified updates | ADR/plan only | NOT STARTED | No signing, notarization, feed or rollback implementation |
| CTRL-014..015 | At-rest protection and backup | partial settings | SCAFFOLDED | No operational encryption/restore proof |
| CTRL-016..023 | AI gateway, consent, minimisation, region/retention/no-training, erasure/DPIA | gateway concepts/audit tables | PARTIALLY IMPLEMENTED | Runtime gateway not composed; views unreachable; provider evidence absent |
| CTRL-024..032 | LAN TLS/TOFU/published roots/sessions/isolation/school keys/quotas/minors | substantial host/client code | PARTIALLY IMPLEMENTED | Physical adversarial and minors-governance acceptance absent |

## Conservative conclusion

The repository contains far more than a prototype, but the earlier “87 implemented” claim is not supportable under end-to-end acceptance. Only a narrow reader/navigation subset merits `IMPLEMENTED`; most capabilities are partial, scaffolded, defective, or untested on their actual platform boundary. The most urgent redesigns are file identity, root reconciliation, safe metadata writeback, advisor retrieval, real 3D hosting, and release trust.
