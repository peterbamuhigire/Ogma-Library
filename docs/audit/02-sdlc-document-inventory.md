# SDLC Document Inventory

## Corpus and authority

All 19 files under `docs/references/`, including body paragraphs, tables, headers and footers, were structurally extracted and reviewed. The controlled wrapper, filename and footer identify the corpus as v2.1 dated 13 August 2026 and authored by Chwezi Core Systems. Several internal document-control tables still identify v2.0, 6 July 2026, and “Draft for owner sign-off.” This is a governance conflict, not a harmless formatting issue.

For this audit, the August v2.1 corpus is the newest source set, but unresolved internal status/version fields are recorded as conflicts. Claims tied to the historical implementation commit `26df983...` are treated as dated evidence and rechecked against current commit `5514276...` where executable verification is possible.

| Document | Purpose | Version/Date | Status | Areas Covered | Major Requirements | Conflicts |
| --- | --- | --- | --- | --- | --- | --- |
| `Ogma-Library_ADRs_v2.1_2026-08-13.docx` | Architecture decisions | v2.1 / 2026-08-13 | Controlled baseline | Runtime, Avalonia, PDF, SQLite, search, AI, OCR, classroom, packaging | ADR-0001..0015 | Duplicate/stale Markdown ADR tree; some decisions are not implemented |
| `Ogma-Library_AgileArtifacts_v2.1_2026-08-13.docx` | Delivery backlog and acceptance framing | Wrapper v2.1; internal v2.0 / 2026-07-06 | Internal “Draft for owner sign-off” | Epics, stories, increments, DoD | Functional slicing and delivery gates | Internal version/status conflicts with wrapper |
| `Ogma-Library_AuditReport_v2.1_2026-08-13.docx` | Prior independent audit | v2.1 / 2026-08-13 | Controlled assessment | Completeness, evidence gaps, go/no-go | Public beta/production NO-GO | Assesses older commit; cannot prove current state |
| `Ogma-Library_BusinessCase_v2.1_2026-08-13.docx` | Commercial rationale | v2.1 / 2026-08-13 | Controlled hypothesis | Market, value, pricing, viability | Desktop value proposition and launch dependencies | Pricing/buyer evidence remains hypothesis |
| `Ogma-Library_DeploymentOps_v2.1_2026-08-13.docx` | Deployment and operations | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | CI/CD, packaging, signing, feeds, rollback, runbooks | Windows/macOS signed distribution | Internal version; described pipeline is not operational |
| `Ogma-Library_DeterministicChecks_v2.1_2026-08-13.docx` | Repeatable quality gates | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Build, test, performance, security checks | Evidence-grade validation | Several checks lack reference hardware/live-provider execution |
| `Ogma-Library_DevelopmentStandards_v2.1_2026-08-13.docx` | Engineering standards | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | C#, Avalonia, database, security, testing | Architecture and coding rules | Internal version; code has deviations and stale comments |
| `Ogma-Library_DPIA_v2.1_2026-08-13.docx` | Privacy impact assessment | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Local data, AI payloads, minors/classroom | Consent, minimisation, deletion, retention | Operational privacy UI/evidence incomplete |
| `Ogma-Library_HLD_v2.1_2026-08-13.docx` | High-level design | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Components, boundaries, data flows, deployment | Local-first modular desktop architecture | Internal version; diagrams sometimes describe planned code as present |
| `Ogma-Library_KaizenOverhaul_v2.1_2026-08-13.docx` | Documentation improvement assessment | v2.1 / 2026-08-13 | Controlled review | Document quality and readiness | 84.3 documentation score; 55 handoff score | Does not equal product completion |
| `Ogma-Library_PRD_v2.1_2026-08-13.docx` | Product requirements | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Personas, capabilities, roadmap, UX | Personal library, reader, search, AI, 3D, classroom | Internal version; current user excludes all mobile clients |
| `Ogma-Library_PublicWebsiteSpec_v2.1_2026-08-13.docx` | Marketing website specification | v2.1 / 2026-08-13 | Controlled plan | Public acquisition site | Marketing pages and conversion | Outside the corrected 39-phase C# desktop application scope |
| `Ogma-Library_RiskRegister_v2.1_2026-08-13.docx` | Product/technical risk | v2.1 / 2026-08-13 | Active | Integrity, AI, performance, release, privacy | Mitigations and ownership | Several mitigations remain plans, not controls |
| `Ogma-Library_SRS_v2.1_2026-08-13.docx` | Normative requirements | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | 101 FRs, 29 NFRs, 32 controls | Primary requirement baseline | Internal version/status; some acceptance claims exceed code |
| `Ogma-Library_StakeholderAnalysis_v2.1_2026-08-13.docx` | Stakeholder needs | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Individual users, educators, students, administrators | Privacy, usability, classroom roles | Commercial validation is incomplete |
| `Ogma-Library_TestCompletionReport_v2.1_2026-08-13.docx` | Historical test result | v2.1 / 2026-08-13 | Controlled evidence record | Automated test totals and exclusions | 800-test baseline | Older commit; explicitly excludes physical/platform/release/provider gates |
| `Ogma-Library_TestStrategy_v2.1_2026-08-13.docx` | Test architecture | Wrapper v2.1; internal v2.0 / 2026-07-06 | Draft for sign-off | Unit through acceptance, security, perf, AI eval | Multi-layer quality strategy | Internal version; much planned evidence is absent |
| `Ogma-Library_TraceabilityMatrix_v2.1_2026-08-13.docx` | Prior requirements traceability | v2.1 / 2026-08-13 | Controlled matrix | FR/NFR/control mappings | 87 implemented, 5 partial, 9 planned claim | Status inflation from schema/mock/scaffold evidence; older commit |
| `Ogma-Library_UserGuide_v2.1_2026-08-13.docx` | Intended user operation | v2.1 / 2026-08-13 | Controlled guide | Library, reader, AI, sharing, recovery | User-facing behavior and limitations | Some instructions describe inaccessible or placeholder surfaces |

## Document conflicts and recommended interpretations

1. **Phase count and client scope.** Historical documents use 24 phases or other legacy sequences, and the original audit request asked for 36 phases plus possible mobile readiness. The owner's latest instruction is authoritative for this deliverable: exactly 39 implementation phases, all for the C# desktop application on Windows and macOS, with no mobile applications or mobile-readiness work.
2. **Version/status metadata.** Treat the collection as an August v2.1 candidate baseline, but require owner approval and corrected internal control tables in Phase 1. Do not call it signed merely because filenames say v2.1.
3. **Implementation status.** Replace the prior 87/101 “implemented” claim with current end-to-end evidence in this audit. Tables, views, mocks and tests of placeholders are not completion.
4. **Public website.** Retain the website specification as a separate product artifact. It is intentionally excluded from the 39 desktop phases.
5. **Classroom scope.** The latest SRS explicitly includes opt-in host, client and administration within the desktop codebase. It remains in scope for the same Windows/macOS application; it is not a mobile or separate web client.
6. **Technology vs proof.** Accepted ADRs define targets. They do not prove that WebView hosting, PDF sandboxing, signed distribution, AI grounding or provider behavior works.

## The Intended Ogma Library

Ogma is intended to be one local-first Avalonia desktop product for Windows and macOS. In standalone mode it opens no inbound listener and requires no account. Users select one or more library roots, continue browsing while durable background processing discovers PDFs, and retain catalogue state when a volume is temporarily unavailable. Physical files, bibliographic editions and works are distinct identities. Automated metadata is provenance-aware and confidence-scored; user corrections are protected; writeback is always previewed, confirmed, backed up and reversible.

The ordinary product remains useful offline: catalogue, covers, collections, reader, annotations, structured and full-text search do not depend on an LLM. Search comprises structured/fuzzy catalogue lookup, page-aware full text, versioned semantic retrieval and hybrid ranking. The reading advisor interprets intent, retrieves only available books in the user's own catalogue, reranks evidence-backed candidates, and explains both matches and limitations without inventing content. AI payloads are tiered, previewable, auditable and optional.

The visual product is a premium but restrained library: excellent grid, list and directory views; a rich detail/editor workflow; an accessible reader; complete processing and degraded states; and a usable 3D shelf delivered through a platform WebView. The 3D client consumes the same catalogue contract as the 2D UI, virtualises large collections, renders real cover/spine assets, supports keyboard/reduced-motion/fallback modes, and is measured on reference hardware.

The same desktop executable may opt into classroom host/client/admin roles over a secured local network, with published-root isolation, TLS trust-on-first-use, roles, quotas, private student state and managed AI. This mode must never weaken standalone defaults. Signed Windows installers and signed/notarized macOS artifacts, verified update feeds, rollback drills, accessibility evidence and physical-platform acceptance complete the release definition.

