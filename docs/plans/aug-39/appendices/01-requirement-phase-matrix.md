# Requirement-to-Phase Matrix

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

The source is `Ogma-Library_SRS_v2.1_2026-08-13.docx` unless otherwise stated. Grouped rows preserve every normative ID while keeping the matrix operable. Acceptance evidence means executable evidence produced in the named phase, not the existence of a class/table/view.

| Requirement | Source | Phase | Domain | Acceptance Evidence |
| --- | --- | ---: | --- | --- |
| FR-LIB-001, FR-LIB-002 | SRS Library | 5, 7 | Roots/discovery | Physical Windows/macOS multi-root, exclusions, symlink/path conformance and 50k scan |
| FR-LIB-003 | SRS Library | 3–4, 8–9 | Identity | File/asset/edition/work invariants; rename/move/replace/duplicate suite |
| FR-LIB-004 | SRS Library | 5, 8 | Integrity | External-drive/permission/root-failure scenarios preserve catalogue data |
| FR-LIB-005 | SRS Library | 6–7, 17 | Jobs | Durable background scan; UI stays usable; crash/restart proof |
| FR-LIB-006 | SRS Library | 6–8 | Incremental scan | Checkpoint/idempotency/change-detection suite |
| FR-LIB-007 | SRS Library | 6, 17–18 | Health | Activity centre, typed failures, retry/cancel/repair E2E |
| FR-CAT-001 | SRS Catalogue | 19, 31–33 | Browse/3D | Grid/list/directory/3D parity and accessible fallback |
| FR-CAT-002 | SRS Catalogue | 19 | Catalogue | Filter/sort correctness and 50k performance |
| FR-CAT-003 | SRS Catalogue | 20 | Organisation | Collections/tags/favourites/status end-to-end |
| FR-CAT-004 | SRS Catalogue | 14, 20 | Book detail | Composite detail/editor/open/reprocess/missing-file acceptance |
| FR-CAT-005 | SRS Catalogue | 14 | Bulk curation | Preview, atomic/partial results and undo tests |
| FR-CAT-006 | SRS Catalogue | 20 | Smart shelves | Saved deterministic query/edit/rebuild tests |
| FR-CAT-007 | SRS Catalogue | 3–4, 9 | Work/edition | Reversible merge/split and duplicate-class evidence |
| FR-META-001 | SRS Metadata | 11–12 | Extraction | ISBN/embedded/filename/page corpus with evidence/provenance |
| FR-META-002 | SRS Metadata | 13 | Providers | Recorded/live-quarantine Google/Open Library contract tests |
| FR-META-003 | SRS Metadata | 12–14 | Matching | Calibrated score, ambiguity and mandatory review suite |
| FR-META-004 | SRS Metadata | 12, 14 | Provenance | Field source/confidence/lock/override visible and protected |
| FR-META-005 | SRS Metadata | 15 | Writeback | No automatic writes; confirm/backup/restore/rehash tests |
| FR-META-006 | SRS Metadata | 13, 17 | Resilience | Cache/TTL/quota/timeout/backoff/outage tests |
| FR-META-007 | SRS Metadata | 12–14 | Quality | Possible-match queue, quality states and review E2E |
| FR-META-008 | SRS Metadata | 14, 17 | Health | Version/status/reprocess/retry evidence |
| FR-READ-001, FR-READ-002, FR-READ-003 | SRS Reader | 21 | Core reader | Physical both-OS open/resume/navigation/zoom/layout/fullscreen |
| FR-READ-004 | SRS Reader | 21, 23 | Reader search | Query→snippet→page jump E2E |
| FR-READ-005, FR-READ-006 | SRS Reader | 21 | Bookmarks/annotations | Crash durability, coordinate and UI acceptance |
| FR-READ-007 | SRS Reader | 10, 21 | Password PDFs | Secure IPC and physical password-flow tests |
| FR-READ-008 | SRS Reader | 10, 24 | OCR | Selective image/mixed PDF accuracy/resource suite |
| FR-READ-009 | SRS Reader | 21 | Citations | Capture/source/export/round-trip evidence |
| FR-READ-010 | SRS Reader | 21 | Split view | Functional two-document/session E2E; placeholder removed |
| FR-READ-011 | SRS Reader | 21 | Export | Versioned export/reimport round-trip |
| FR-READ-012, FR-READ-013 | SRS Reader | 20–21 | Memory/layers | Local/private persistence and layer behavior tests |
| FR-READ-014 | SRS Reader | 17, 21 | Durability | Kill/restart/resume fault-injection suite |
| FR-READ-015 | SRS Reader | 18, 21 | Accessibility | Keyboard, Narrator and VoiceOver physical acceptance |
| FR-SEARCH-001 | SRS Search | 22 | Structured/fuzzy | “tolkein” and 50k p95 ≤150 ms gate |
| FR-SEARCH-002, FR-SEARCH-003 | SRS Search | 23 | Full text | Page-aware FTS/snippet/reader navigation and ≤500 ms gate |
| FR-SEARCH-004 | SRS Search | 25–26 | Semantic | Versioned vector lifecycle, relevance and scale suite |
| FR-SEARCH-005 | SRS Search | 26 | Hybrid | Calibrated fusion, nDCG/MRR and degraded fallback |
| FR-SEARCH-006 | SRS Search | 23, 25 | Index manager | Status/rebuild/cancel/crash/stale-cleanup evidence |
| FR-AI-001, FR-AI-002 | SRS AI | 27 | Gateway | Disabled default; provider-neutral runtime; no bypass architecture test |
| FR-AI-003, FR-AI-004 | SRS AI | 28–30 | Recommendations | Catalogue-only retrieval, availability, ranking and eight-prompt benchmark |
| FR-AI-005 | SRS AI | 27, 29 | Content tier | Explicit passage consent/preview and cited output |
| FR-AI-006 | SRS AI | 25, 27 | Local models | Ollama setup/health/failure and local-only flow |
| FR-AI-007 | SRS AI | 28, 30 | Reading plans | Validated grounded plan and reachable UI |
| FR-AI-008 | SRS AI | 29–30 | Answer mode | Cited answer, injection/factuality/abstention evaluation |
| FR-AI-009 | SRS AI | 27, 30 | History | Retention/export/delete and privacy E2E |
| FR-AI-010 | SRS AI | 27, 30 | Cost | Token/cost/budget accounting and limit enforcement |
| FR-AI-011 | SRS AI | 27, 30 | Privacy centre | Reachable exact payload/provider/tier controls |
| FR-LAN-001, FR-LAN-002 | SRS LAN | 34 | Host/TLS | No default listener; TLS/mDNS/manual/TOFU physical suite |
| FR-LAN-003, FR-LAN-004 | SRS LAN | 34, 37 | Publication/RBAC | Hostile published-scope and authorization tests |
| FR-LAN-005, FR-LAN-006 | SRS LAN | 34 | Streaming/search | Range/search/load and path-security suite |
| FR-LAN-007, FR-LAN-008 | SRS LAN | 34–37 | Isolation/audit | Cross-user adversarial isolation, quota/audit tests |
| FR-LAN-009, FR-LAN-010 | SRS LAN | 34, 38 | Operations | Shutdown/recovery and standalone network-capture evidence |
| FR-CLIENT-001, FR-CLIENT-002 | SRS Client | 35 | Pairing/secrets | Physical pairing, TOFU, DPAPI/Keychain evidence |
| FR-CLIENT-003, FR-CLIENT-004 | SRS Client | 35 | Browse/read | Published browse/search/range-reader E2E |
| FR-CLIENT-005, FR-CLIENT-006 | SRS Client | 35 | Offline/sync | Cache quota/eviction and conflict/reconnect suite |
| FR-CLIENT-007, FR-CLIENT-008 | SRS Client | 35–36 | Private state/advisor | Tenant isolation and published-evidence advisor tests |
| FR-CLIENT-009, FR-CLIENT-010 | SRS Client | 35 | Recovery/erasure | Network drop/resume and verifiable clear-data tests |
| FR-CLIENT-011, FR-CLIENT-012, FR-CLIENT-013 | SRS Client | 18, 35, 37 | UX/a11y/diagnostics | State matrix, AT/i18n and redacted support bundle |
| FR-ADMIN-001, FR-ADMIN-002, FR-ADMIN-003 | SRS Admin | 34, 36 | Administration | Role-authorized user/class/publication journeys |
| FR-ADMIN-004, FR-ADMIN-005 | SRS Admin | 36–37 | Policy/quota | Enforcement and concurrent limit tests |
| FR-ADMIN-006, FR-ADMIN-007 | SRS Admin | 27, 36–37 | Managed AI/keys | Host-only key, rotation and gateway conformance |
| FR-ADMIN-008, FR-ADMIN-009 | SRS Admin | 34, 36–37 | Audit/revocation | Redacted tamper evidence and timed revocation |
| FR-ADMIN-010, FR-ADMIN-011 | SRS Admin | 36–38 | Backup/health | Restore drill and operational dashboard evidence |
| FR-ADMIN-012, FR-ADMIN-013 | SRS Admin | 18, 36–37 | Minors/a11y | DPIA policy and physical accessible admin journey |
| FR-EXT-001 | SRS Extensions | 2, 27 | Extension points | Explicit safe provider/module interfaces and architecture tests |
| FR-EXT-002 | SRS Extensions | 34, 37 | Local API | If retained: loopback/read-only/authz tests; otherwise approved deferral |
| FR-EXT-003 | SRS Extensions | 14, 20, 39 | Imports/themes | Validated staged import and theme evidence, or explicit post-release deferral |
| FR-UX-001, FR-UX-002 | SRS UX | 2, 7, 18–19 | First-run/states | Browse-during-scan and complete state-matrix E2E |
| FR-UX-003 | SRS UX | 18 | Command palette | Reachability, keyboard and discoverability tests |
| FR-UX-004 | SRS UX | 18, 39 | Localisation | en/fr complete; es/it/de final gate per approved release tier |
| FR-UX-005 | SRS UX | 18–19, 21, 33 | Accessibility | 2D parity, keyboard, reduced motion, Narrator/VoiceOver |
| FR-UX-006, FR-UX-007 | SRS UX | 17–18, 38 | Degraded/recovery | Offline help and resume-under-60s fault suite |
| FR-UX-008 | SRS UX | 18, 39 | Themes/design | Token/component audit and visual acceptance |
| NFR-OGMA-001, NFR-OGMA-002, NFR-OGMA-003, NFR-OGMA-004, NFR-OGMA-005, NFR-OGMA-006, NFR-OGMA-007, NFR-OGMA-008, NFR-OGMA-009 | SRS NFR | 2–39 | Core quality | Local-first, data integrity, portability, platform, privacy and accessibility evidence |
| NFR-PROD-001, NFR-PROD-002, NFR-PROD-003, NFR-PROD-004 | SRS NFR | 19, 22–23, 38 | Performance | Named hardware 2k/5k/50k startup/catalogue/search/reader results |
| NFR-PROD-005 | SRS NFR | 33, 38 | 3D performance | Real WebView/GPU FPS/frame-time/memory matrix |
| NFR-PROD-006 | SRS NFR | 27–30, 38 | AI latency | Stage and total latency/cost gates |
| NFR-PROD-007, NFR-PROD-008, NFR-PROD-009, NFR-PROD-010, NFR-PROD-011 | SRS NFR | 6, 17, 21, 37–39 | Durability/reliability/a11y | Crash/lease/annotation/AT/soak evidence |
| NFR-PROD-012, NFR-PROD-013, NFR-PROD-014 | SRS NFR | 15, 37–39 | Release/safety | Safe mutations, signed updates, reversible migrations, erasure |
| NFR-LAN-001, NFR-LAN-002, NFR-LAN-003 | SRS NFR | 34, 37–39 | LAN | Physical security/isolation/load evidence |
| NFR-CLIENT-001, NFR-CLIENT-002, NFR-CLIENT-003 | SRS NFR | 35, 37–39 | Client | Physical latency/secret/offline recovery evidence |
| CTRL-001, CTRL-002, CTRL-003, CTRL-004 | SRS Controls | 27, 35, 37 | Secrets/logs | OS stores, redaction, rotation/deletion physical tests |
| CTRL-005, CTRL-006, CTRL-007, CTRL-008 | SRS Controls | 10, 37 | PDF isolation | Real OS escape/resource tests |
| CTRL-009, CTRL-010, CTRL-011 | SRS Controls | 5, 8, 15, 37 | Filesystem/writeback | Canonical/symlink/traversal and confirmed backup/restore tests |
| CTRL-012, CTRL-013, CTRL-014, CTRL-015 | SRS Controls | 37–39 | Release/data | Signing/update tamper, backup and at-rest decision evidence |
| CTRL-016, CTRL-017, CTRL-018, CTRL-019, CTRL-020, CTRL-021, CTRL-022, CTRL-023 | SRS Controls | 27–30, 37 | AI privacy | Enforced gateway, exact payload, provider evidence, retention/erasure |
| CTRL-024, CTRL-025, CTRL-026, CTRL-027, CTRL-028, CTRL-029, CTRL-030, CTRL-031, CTRL-032 | SRS Controls | 34–37 | Classroom | TLS/TOFU/published root/session/isolation/key/quota/minors evidence |

## Explicit exclusions and controlled deferrals

- `Ogma-Library_PublicWebsiteSpec_v2.1_2026-08-13.docx` is excluded from these 39 phases; it is a separate marketing product.
- All mobile applications, mobile clients and mobile-readiness work are rejected by owner direction.
- FR-EXT-002/003 may be deferred from the first public release only through an approved SRS change/risk acceptance in Phase 1; they may not disappear silently.

