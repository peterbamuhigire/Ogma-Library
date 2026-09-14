# Reference review ledger

Review date: 2026-09-14  
Reviewer scope: complete extracted body paragraph corpus; read-only source review.  
Source extraction: `docs/plans/astra-audit-sept/evidence/reference-extract.txt` (804,726 bytes at capture; document headers and SHA-256 values are preserved in the file).  
Latest supplied phase-status snapshot: `docs/implementation/execution/00-execution-status.md`, normalized 2026-09-06; it is not asserted here as a live 2026-09-14 execution record.  
Scope/accountability authority: `docs/plans/aug-39/README.md` and `docs/plans/aug-39/appendices/01-requirement-phase-matrix.md`.

## Evidence classes

| Code | Meaning |
| --- | --- |
| OBS | Direct observation from the named paragraph or repository line. |
| SYN | Synthesis or arithmetic across named sources. |
| INF | Inference; useful for planning but not a source fact. |
| GAP | Required evidence absent, stale, or contradicted. |
| NA | Not assessed because the required render, platform, external approval, or run evidence was unavailable. |

## Source ingestion register

| # | Document | Extraction header / hash locator | Review result |
| ---: | --- | --- | --- |
| 1 | ADRs | `reference-extract.txt:1-695` | Complete body reviewed; point-in-time implementation evidence and open platform gates retained. |
| 2 | Agile Artifacts | `reference-extract.txt:696-1452` | Complete body reviewed; old 24-phase stories and conflicting status rollup identified. |
| 3 | Audit Report | `reference-extract.txt:1453-1597` | Complete body reviewed; 26df983/800-case baseline treated as historical. |
| 4 | Business Case | `reference-extract.txt:1598-1746` | Complete body reviewed; hypotheses separated from approval/evidence. |
| 5 | Deployment and Operations | `reference-extract.txt:1747-2133` | Complete body reviewed; planned release infrastructure and gate register retained. |
| 6 | Deterministic Checks | `reference-extract.txt:2134-2750` | Complete body reviewed; check/test-state conflict recorded. |
| 7 | Development Standards | `reference-extract.txt:2751-3068` | Complete body reviewed; normative rules separated from evidence. |
| 8 | DPIA | `reference-extract.txt:3069-3451` | Complete body reviewed; jurisdiction/controller approvals remain NA. |
| 9 | HLD | `reference-extract.txt:3452-4203` | Complete body reviewed; split-view, ADMIN, and migration-count conflicts recorded. |
| 10 | Kaizen Overhaul | `reference-extract.txt:4204-4438` | Complete body reviewed; claimed corrections checked against remaining HLD contradictions. |
| 11 | PRD | `reference-extract.txt:4439-5269` | Complete body reviewed; old phase and migration-count claims identified. |
| 12 | Public Website Specification | `reference-extract.txt:5270-5399` | Complete body reviewed; explicitly outside Aug-39 and unassessed. |
| 13 | Risk Register | `reference-extract.txt:5400-5706` | Complete body reviewed; retirement taxonomy ambiguity identified. |
| 14 | SRS | `reference-extract.txt:5707-7112` | Complete body reviewed; 101-FR vocabulary and status/test-link gaps recorded. |
| 15 | Stakeholder Analysis | `reference-extract.txt:7113-7727` | Complete body reviewed; owner/school/store approval boundaries recorded. |
| 16 | Test Completion Report | `reference-extract.txt:7728-7845` | Complete body reviewed; point-in-time 800-case result separated from later CI. |
| 17 | Test Strategy | `reference-extract.txt:7846-8546` | Complete body reviewed; old phase and migration/test-count claims identified. |
| 18 | Traceability Matrix | `reference-extract.txt:8547-8841` | Complete body reviewed; old planned-gap rollup separated from current 39-phase status. |
| 19 | User Guide | `reference-extract.txt:8842-9257` | Complete body reviewed; stale feature limitations and 10-migration claim identified. |

## Material findings ledger

| ID | Class | Finding | Exact evidence | Downstream decision |
| --- | --- | --- | --- | --- |
| REF-001 | OBS/GAP | Split view is marked as-built in HLD but not started/planned in the SRS, Agile, deterministic checks, and User Guide. | HLD P0326-P0334; SRS P0364-P0369; Agile P0267-P0270; Deterministic P0219-P0224; User Guide P0350-P0353. | Current Phase 21 evidence must decide implementation status; platform/accessibility acceptance remains separate. |
| REF-002 | OBS/GAP | Kaizen claims split-view and school-admin HLD claims were corrected, but refreshed HLD retains both conflicting statements. | Kaizen P0060-P0069; HLD P0334 and P0566-P0568. | Add correction-verification oracle; do not close Kaizen action from its prose alone. |
| REF-003 | OBS/GAP | ADMIN is "Done" in Agile/SRS but partial in HLD and current Phase 36 still has formal/physical/DPIA gates open. | Agile P0158-P0163; SRS P0502-P0542; HLD P0566-P0568; execution status `:76`. | Use implementation/formal-acceptance/deployment-approval states separately. |
| REF-004 | OBS/GAP | Migration count is reported as 11 in ADR/HLD and 10 in PRD/Test Strategy/Test Completion/User Guide. The repository contains later migration source files, but this documentation audit did not establish the canonical applied-migration count. | ADR P0267; HLD P0221-P0237; PRD P0337/P0502; Test Strategy P0174-P0177; Test Completion P0049-P0050; User Guide P0348-P0349; repository migration paths (read-only `rg` inspection). | Publish one migration ledger/run record, then normalize document references to that evidence. |
| REF-005 | OBS/GAP | Deterministic batch-enrichment check says currently failing; Test Completion says 800/800 passed. | Deterministic P0152-P0157; Test Completion P0022-P0048. | Reconcile check result with command/commit/run artifact before "all green." |
| REF-006 | OBS/GAP | Test totals are 800 point-in-time, 1,160 refresh assertion, and 1,153 per-platform later CI. | Test Completion P0014-P0048/P0063-P0072; execution `:14-32`. | Every result needs commit, date, platform, project totals, and scope. |
| REF-007 | OBS/GAP | Agile status rollup 89/4/8 conflicts with SRS/Audit/Kaizen 87/5/9. | Agile P0499-P0503; Audit P0019-P0024; Kaizen P0060-P0069; SRS P1318. | Keep old rollups historical; current ledger governs. |
| REF-008 | OBS/GAP | Old DOCX uses grand-plan Phases 00-23; approved roadmap has 39 phases and the latest supplied execution status snapshot records 1-9/12 COMPLETE and 10-11/13-39 IN PROGRESS. | Agile P0031-P0140; Test Strategy P0592-P0647; Aug-39 README `:1-41`; execution `:39-79`. | Use the crosswalk in the report; never infer old Done = current COMPLETE. |
| REF-009 | OBS/GAP | Aug-39 READ rows shift IDs: FR-READ-010 is called split view, but SRS defines split view FR-READ-012; export/memory/layer/password/OCR rows also shift. | Matrix `:30-40`; SRS P0350-P0375. | Regenerate affected matrix rows from SRS. |
| REF-010 | OBS/GAP | Aug-39 NFR rows mis-map NFR-PROD-005 to 3D and NFR-PROD-006 to AI latency; SRS defines them as UI responsiveness and crash-free sessions. | Matrix `:82-86`; SRS P0601-P0604. | Mark affected traceability rows failed until corrected. |
| REF-011 | OBS/GAP | SRS labels FR-READ-013, FR-CLIENT-012/013 as code/test evidence while naming planned TCs/no dedicated classes. | SRS P0367-P0369, P0496-P0501. | A planned TC is a gap until a dated run record exists. |
| REF-012 | OBS/INF | Risk Register retires WebView/PDF wrapper risks while physical WebView/PDF/package gates remain open. | Risk P0110-P0133; ADR P0155-P0191/P0232-P0240; execution `:50-51,71,78`. | Split design-risk retirement from release/platform acceptance. |
| REF-013 | OBS/NA | DPIA names jurisdiction, school controller approval, and classroom deployment obligations as prerequisites; no signed deployment approval is supplied. | DPIA P0151-P0153, P0273-P0290; Stakeholder P0409-P0451. | Keep classroom pilot/sale approval NA/blocking. |
| REF-014 | OBS/NA | Public website implementation, deployment, analytics, content/legal approval, and release rollback are explicitly unassessed and outside Aug-39. | Website P0010-P0015/P0062-P0069; matrix `:96-100`. | Maintain separate website evidence register. |
| REF-015 | OBS/INF | User Guide reports split view, answer mode, command palette/help not implemented; current Aug-39 reports split view and command-palette execution delivered, while personal answer status needs current verification. | User Guide P0350-P0356; execution `:58,61,69`. | Refresh guide only after route/interaction evidence is linked. |

## Canonical ID and phase reconciliation

| Inventory | Count | Source | Result |
| --- | ---: | --- | --- |
| Functional requirements (FR) | 101 | SRS P0048-P0068; execution `:5` | Complete vocabulary, grouped into 11 FR modules. |
| Product-specific NFRs | 9 | Aug-39 matrix `:81` | Present in canonical matrix. |
| Productivity NFRs | 14 | Aug-39 matrix `:82-86` | Present, but NFR-PROD-005/006 labels are wrong. |
| LAN NFRs | 3 | Aug-39 matrix `:87` | Present. |
| Client NFRs | 3 | Aug-39 matrix `:88` | Present. |
| Controls | 32 | Aug-39 matrix `:89-94`; execution `:5` | Present as CTRL-001 through CTRL-032. |
| Accountability total | 162 | execution `:5`; phase 1 evidence `:41` | (synthesis) 101 + 29 + 32 = 162; preserve this denominator. |

## Status decision rules for integration

1. Use `docs/implementation/execution/00-execution-status.md` as the latest supplied phase-status snapshot. It is normalized 2026-09-06, not a live 2026-09-14 execution claim; its `COMPLETE`/`IN PROGRESS` definitions explicitly prohibit converting physical, platform-signing, reference-hardware, or independent-review gates into COMPLETE from source code/headless tests [`:81-85`].
2. Use the 39-phase roadmap and matrix for scope and accountability, after correcting the READ and NFR-PROD mappings above.
3. Use the DOCX body paragraphs as historical design/requirements/test intent. A refreshed filename or generic 2026-09-10 appendix does not, by itself, update the body's July phase model or August commit evidence.
4. "Code/test evidence," "automated oracle green," "implemented," and "formal acceptance" are different states. A named class without a dated run result is not acceptance evidence.
5. Mark legal, commercial, platform, visual, signing, store, and reviewer checks `NOT ASSESSED` when the source says they are absent. Do not infer approval from design intent.

## Handoff

The detailed narrative, contradiction register, 162-ID inventory, crosswalk, stale-claim list, and documentation-only remediation plan are in [`docs/plans/astra-audit-sept/03-reference-document-audit.md`](../03-reference-document-audit.md). No source code, DOCX, roadmap, or existing evidence file was edited by this review.


