# Reference Review - 2026-07-07

## Key Findings From `docs/references`

1. The PRD v2.0 frames Ogma Library as a local-first personal and classroom PDF
   library operating system, with standalone and LAN/classroom modes in one
   codebase.
2. The SRS traces 101 functional requirements. It reports 87 implemented,
   5 partial, and 9 not started in the detailed table, while the governance
   rollup reports 89 done, 4 in verification, and 8 planned. The execution plan
   treats this as a verification gap until the final traceability pass resolves
   the count difference.
3. The Development Standards document explicitly says the canonical restore is
   blocked by NU1903 for `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 and says the fix is
   a dependency bump, not suppressing warnings.
4. The Test Completion Report records that the canonical test command is
   blocked at restore and that a diagnostic `NuGetAudit=false` run completed
   with 788/789 passing. The one failing test is
   `HealthDashboardTests.BatchEnrichment_2000Books_CompletesWithRetry`.
5. The Risk Register leaves multiple high risks open for the remaining work:
   malformed PDF hardening, cross-jurisdiction data decisions, LAN host threat
   modelling, minors' data handling, and premium icon procurement.
6. The Test Strategy states public beta gates G1-G8 are open. G5-G8 have
   automated evidence, but no formal release-candidate gate run. G1-G4 still
   need platform or reference-hardware evidence.
7. Deployment/Ops states packaging, signing, update trust-chain verification,
   rollback drills, SLO instrumentation, and beta operations are not complete.
8. The DPIA states classroom deployments process minors' reading and AI-query
   data and require stricter controller/deployment decisions before pilots.

## Consequence For The 24 Phases

This is not a green-field product plan. It is a recovery and release plan:

- Phases 01-02 repair the baseline and architecture gates.
- Phases 03-17 harden the implemented product surface into working user flows.
- Phases 18-21 close the formal release blockers named in the references.
- Phase 22 proves installed-app usability.
- Phases 23-24 close beta operations, release evidence, and handover.

