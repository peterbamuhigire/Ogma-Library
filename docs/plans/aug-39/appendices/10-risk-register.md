# Risk Register

> Part of the canonical [August 39-phase desktop roadmap](../README.md).

| Risk | Category | Likelihood | Impact | Severity | Mitigation | Phase |
| --- | --- | --- | --- | --- | --- | ---: |
| Incorrect file/book identity causes irreversible catalogue corruption | Architecture/Integrity | High | Critical | P0 | Canonical model, reversible migration, property tests and freeze | 3–4 |
| External-drive outage marks books missing/deleted | Library Integrity | High | Critical | P0 | Root health + complete scan evidence before absence transitions | 5, 8 |
| Automatic metadata writeback modifies user PDFs | Metadata/Integrity | High | Critical | P0 | Disable; confirm/backup/verify/restore transaction | 15 |
| Unsafe duplicate matching merges unrelated files | Metadata/Identity | High | Critical | P0 | Deterministic exact copy; scored reviewable edition/work proposals | 9 |
| Migration loses user corrections/annotations | Database | Medium | Critical | P0 | Dry run, immutable backup, alias mapping, restore rehearsal | 4, 38 |
| Malicious PDF accesses user resources | Security | Medium | Critical | P0 | Brokered real OS sandbox and hostile escape tests | 10, 37 |
| Background workers duplicate/lose jobs | Reliability | High | High | P1 | Atomic leases/idempotency/dead letter/fault injection | 6, 17 |
| Provider rate limits/outages degrade ingestion | Metadata | High | Medium | P1 | Durable cache, quotas, backoff and local readiness | 13, 17 |
| Poor extraction/OCR yields misleading knowledge | PDF/Search/AI | High | High | P1 | Quality scores, selective OCR, evidence confidence and corpus | 11, 24 |
| Stale vectors persist after change/delete/version shift | AI/RAG | High | Critical | P0 | Complete compatibility tuple, tombstones, side-by-side rebuild | 25 |
| Vector strategy cannot scale | Performance | High | High | P1 | Bounded/ANN strategy selected by 50k benchmark | 25–26 |
| Hybrid scoring produces irrelevant results | Search | High | High | P1 | Calibrated fusion and labeled IR evaluation | 26 |
| Advisor misses concepts due to literal gating | AI Quality | High | Critical | P0 | Retrieval-first rewrite and benchmark | 28 |
| Advisor hallucinates book contents | AI Quality | High | Critical | P0 | Source evidence, citation validation, abstention, eval | 29–30 |
| Advisor recommends unavailable/duplicate books | AI Quality | Medium | High | P1 | Availability filters and work-level diversity | 28–30 |
| AI costs grow through repeated work/prompts | Cost | Medium | High | P1 | Versioned cache, limits, token accounting and local fallback | 25, 27–30 |
| Private titles/text/notes leak to providers/logs | Privacy | Medium | Critical | P0 | Enforced gateway, note exclusion, preview, redaction and deletion | 27, 37 |
| 3D host fails on one platform | 3D/Platform | High | High | P1 | Native adapter conformance and early physical tests | 31 |
| 3D exhausts GPU memory or frame budget | 3D/Performance | High | High | P1 | Atlas/LOD/virtualisation and actual GPU matrix | 32–33 |
| 3D excludes keyboard/screen-reader/reduced-motion users | Accessibility | Medium | High | P1 | Semantic 2D parity and physical accessibility gate | 33, 39 |
| Classroom exposes unpublished/private data | Security/Privacy | Medium | Critical | P0 | Published read model, RBAC, hostile multi-user tests | 34, 37 |
| School AI keys reach clients | Security | Low-Medium | Critical | P0 | Host-only gateway/OS store/rotation architecture | 36–37 |
| Minors governance is insufficient | Privacy/Governance | Medium | Critical | P0 | DPIA, policies, retention/erasure and approved acceptance | 36–37 |
| Missing macOS physical evidence causes late failure | Platform | High | High | P1 | Continuous physical Mac gates from Phases 5/10/21/31 | 5–39 |
| Unsigned/unnotarized artifacts cannot be trusted/distributed | Deployment | High | Critical | P0 | Protected signing/notarization/update trust pipeline | 38 |
| Update/signing key compromise harms users | Security/Deployment | Medium | Critical | P0 | Independent feed signature, HSM custody, revoke/rollback drills | 38–39 |
| 800 tests continue to codify scaffolds as features | Testing | High | High | P1 | Label/remove scaffold tests and require end-to-end evidence | 1–39 |
| UI remains functional but visually generic/inaccessible | UX | High | High | P1 | Tokenized design system, physical AT/visual acceptance | 18–21, 39 |
| Documentation/version conflicts misdirect implementation | Governance | High | Medium | P1 | Conflict log, signed baseline and archive/index | 1, 39 |
| Bus factor and key operational knowledge remain concentrated | Maintainability | High | High | P1 | Module/runbook/evidence ownership and handover drills | 17, 38–39 |


