# Ogma Library Kaizen: improvement backlog

Target: 95/100 raw evidence quality. No item is closed by prose alone.

| ID | Priority / root cause | Smallest reversible change | Measure and acceptance evidence | Owner / reviewer / due | Rollback and target |
| --- | --- | --- | --- | --- | --- |
| OG-K-01 | P0; untrusted PDF containment lacks final platform proof | Complete one Windows containment slice: brokered I/O, resource limits, escape test, and evidence record | `Test-ReleaseCandidate.ps1` plus hostile-fixture result; zero escape and bounded resource outcome | Engineering owner / security reviewer `NOT_ASSESSED` / 2026-09-17 | Revert only the containment slice; Phase 10 >=90 |
| OG-K-02 | P0; distribution trust chain is not evidenced | Produce one clean-machine Windows release-candidate rehearsal with checksum, signing, install, update and rollback records | `Test-ReleaseAcceptance.ps1`; release descriptor, checksum, and rollback evidence retained | Release owner / product owner `NOT_ASSESSED` / 2026-09-24 | Keep internal build; do not promote to beta; deployment >=85 |
| OG-K-03 | P1; open criteria are spread across phase records | Make `remaining-gates-2026-09-06.md` the single dated handoff with owner, evidence path, stop rule, and next review | Fresh agent can map every open criterion to one owner and one proof artefact | Programme owner / reviewer `NOT_ASSESSED` / 2026-09-15 | Revert index-only change; traceability >=90 |
| OG-K-04 | P1; physical macOS/accessibility evidence is absent | Schedule one macOS CI/physical slice and record unavailable checks explicitly | Matching current commit evidence, not historical run; no `NOT_ASSESSED` item relabelled | Platform owner / accessibility reviewer `NOT_ASSESSED` / 2026-10-10 | Retain conditional release; NFR >=88 |
| OG-K-05 | P2; advisor and 3D signature paths remain gated by runtime evidence | Do not add feature scope; define one intent-to-evidence advisor fixture and one native-host smoke fixture | Fixture output, failure path, latency/cost fields, and route acceptance record | Feature owners / product reviewer `NOT_ASSESSED` / 2026-10-10 | Keep feature disabled; product-fit >=90 |
