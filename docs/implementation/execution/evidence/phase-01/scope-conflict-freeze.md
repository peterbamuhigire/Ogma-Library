# Phase 1 scope and conflict freeze

Decision ID: `OGMA-FREEZE-2026-08-20-01`

Status: APPROVED

Approved by: Peter Bamuhigire, through the implementation execution directive
issued on 2026-08-20

Applies from: execution baseline `de8a42429ee353db800bba5b1439d902b4543733`

## Frozen product scope

Ogma Library is one local-first C# desktop application for Windows and macOS,
using .NET 10 and Avalonia. The same application may operate in standalone,
classroom Host and classroom Client modes. Its authoritative catalogue manages
the user's own PDF collection. AI is optional, privacy-tiered and grounded in
local library evidence. The 2D experience remains complete without AI, internet
access or the 3D shelf.

Excluded from the 39-phase release are mobile apps, a PWA, a separate public
website, Linux release acceptance, a globally sourced recommendation catalogue
and any automatic mutation of user PDFs.

## Authority order

1. Owner corrections in the 2026-08-20 execution directive.
2. The approved 39-phase roadmap and its appendices.
3. The v2.1 SRS for exact requirement behavior and acceptance meaning.
4. Other v2.1 SDLC references for their specialist domain.
5. Architecture decisions and current implementation evidence.
6. Historical plans, reports, comments and status claims.

When the roadmap's requirement matrix abbreviates or mislabels a requirement,
the SRS requirement text controls behavior while the matrix controls phase
assignment until explicitly corrected.

## Conflict register

| ID | Conflict | Frozen resolution | Reopen trigger |
| --- | --- | --- | --- |
| CF-01 | Historical plans describe 24 phases; the approved roadmap describes 39. | Exactly 39 phases are authoritative. | Owner-approved roadmap amendment. |
| CF-02 | Several DOCX bodies retain internal v2.0 labels although filenames and the SRS baseline are v2.1. | Treat them as v2.1 source artifacts with a documented internal-label defect; do not rewrite signed/reference files in place. | A formally reissued reference pack. |
| CF-03 | PRD, reports, source comments and old phase labels claim features are implemented. | A feature is complete only when the current roadmap phase has code, tests, evidence and acceptance. | None; this is the standing evidence rule. |
| CF-04 | Some reader labels in the roadmap matrix do not match the exact FR-READ numbering/text in the SRS. | Implement every FR-READ-001 through FR-READ-015 in Phase 21 according to SRS text; use the matrix only for supporting-phase placement. | Corrected traceability matrix approved by the owner. |
| CF-05 | One SRS privacy-tier passage permits sending notes; the approved roadmap excludes personal notes by default. | Personal notes are excluded from off-device AI payloads by default. Any future opt-in requires an explicit privacy decision, preview and tests. | Approved privacy ADR and SRS change. |
| CF-06 | Historical material includes a public website specification. | Public website delivery is outside this desktop release. | Separate approved project scope. |
| CF-07 | Existing 3D code and tests can be read as production completion. | The current 3D implementation is a scaffold until Phases 31–33 pass native-host, scale, accessibility and physical performance gates. | Phase 33 completion evidence. |
| CF-08 | Existing advisor prose and tests can be read as release-ready RAG. | Advisor and answer-mode claims remain non-release until Phases 27–30 pass catalogue-only retrieval, grounding, hallucination and quality benchmarks. | Phase 30 completion evidence. |
| CF-09 | Existing PDF writeback behavior may be interpreted as routine enrichment. | Automatic writeback is prohibited. Phase 15 must require preview, explicit confirmation, backup, restore and re-fingerprinting. | Approved requirement change. |
| CF-10 | FR-EXT-002 and FR-EXT-003 permit deferral choices. | Both are retained: local API evidence belongs to Phases 34/37; staged imports/themes belong to Phases 14/20/39. | Owner-approved deferral. |
| CF-11 | The audit source baseline is `5514276`; the execution plan was committed later. | Preserve `5514276` as audit provenance and use `de8a424` as the Phase 1 execution baseline. | A new audited baseline. |
| CF-12 | CI includes macOS but this execution host is Windows only. | Windows results are assessed locally; macOS and VoiceOver results remain `NOT ASSESSED` until macOS CI/physical evidence exists. | Matching macOS evidence. |

## Data-classification and evidence rules

| Class | Examples | Evidence treatment |
| --- | --- | --- |
| Public engineering | Source paths, requirement IDs, commit IDs, test names, durations | May be recorded. |
| Operational internal | Database schema, provider names, failure categories, model/version identifiers | Record only what is needed for reproducibility. |
| Confidential | Credentials, tokens, private filesystem roots, user identity data | Redact; record presence/status, never values. |
| Private library content | PDF text, notes, annotations, prompts containing passages, reading history | Do not include in execution evidence; use synthetic fixtures and identifiers. |

Logs and evidence may include counts, stable test fixture IDs, elapsed time and
error categories. They must not include credentials, full prompts, book passages
or private document paths.

## Reference performance environments

- `W-REF-01`: Windows 10 22H2+, x64, 4 cores/8 threads (2020 class), 8 GB RAM,
  SATA SSD, Intel UHD 620-class integrated graphics, 1920×1080.
- `M-REF-01`: macOS 13+, MacBook Air M1, 8 GB unified memory.
- Dataset gates: synthetic/cleared corpora at 50, 250, 1,000, 2,000, 5,000,
  50,000 books as assigned by the testing and performance roadmaps.

Hardware or platform evidence is valid only when the exact machine, OS, commit,
dataset and command are recorded.

## Freeze effect

This decision freezes phase count, desktop scope, requirement authority,
privacy defaults and evidence semantics. Later phases may refine implementation
details but may not silently change these decisions.
