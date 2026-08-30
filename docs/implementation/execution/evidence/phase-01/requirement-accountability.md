# Phase 1 requirement accountability register

The canonical inventory is the v2.1 SRS. Phase assignment and required evidence
are maintained in the [requirement-to-phase matrix](../../../../plans/aug-39/appendices/01-requirement-phase-matrix.md).
The executable gate is [`scripts/Test-RequirementAccountability.ps1`](../../../../../scripts/Test-RequirementAccountability.ps1).

Gate result on 2026-08-20 at baseline `de8a424`: **PASS — 101 FRs, 29 NFRs,
32 controls; all 162 unique IDs appear in the roadmap matrix and no matrix ID is
absent from the SRS.**

| Requirement family | Count | Accountable delivery phases | Acceptance authority |
| --- | ---: | --- | --- |
| FR-LIB | 7 | 5–9, 17–18 | Product owner; implementation lead supplies integrity evidence |
| FR-CAT | 7 | 3–4, 9, 14, 19–20, 31–33 | Product owner; catalogue/domain phase owner supplies evidence |
| FR-META | 8 | 11–15, 17 | Product owner; metadata phase owner supplies evidence |
| FR-READ | 15 | 10, 17, 20–21, 23–24 | Product owner; reader phase owner supplies evidence |
| FR-SEARCH | 6 | 22–26 | Product owner; search phase owner supplies evidence |
| FR-AI | 11 | 25, 27–30 | Product owner; AI/RAG phase owner supplies evaluation evidence |
| FR-LAN | 10 | 34, 37–38 | Product owner; classroom Host phase owner supplies evidence |
| FR-CLIENT | 13 | 18, 35–37 | Product owner; classroom Client phase owner supplies evidence |
| FR-ADMIN | 13 | 18, 27, 34, 36–38 | Product owner; school administration phase owner supplies evidence |
| FR-EXT | 3 | 2, 14, 20, 27, 34, 37, 39 | Product owner; assigned phase owner supplies evidence |
| FR-UX | 8 | 2, 7, 17–19, 21, 33, 38–39 | Product owner; UX/design phase owner supplies evidence |
| NFR-OGMA | 9 | 2–39 | Every phase owner; release owner accepts cumulative evidence |
| NFR-PROD | 14 | 6, 15, 19, 21–23, 27–30, 33, 37–39 | Performance/reliability phase owner; release owner accepts |
| NFR-LAN | 3 | 34, 37–39 | Classroom Host and hardening phase owners |
| NFR-CLIENT | 3 | 35, 37–39 | Classroom Client and hardening phase owners |
| CTRL | 32 | 5, 8, 10, 15, 27–30, 34–39 | Security/privacy phase owners; release owner accepts |

“Phase owner” means the engineer responsible for the active phase execution,
not the author of an older phase-named implementation. For every ID, completion
requires the matrix's named evidence and a link from the relevant phase record.

The CI workflow runs the accountability gate on both Windows and macOS. It fails
when canonical counts drift, an SRS ID is unassigned or the matrix invents an ID.
