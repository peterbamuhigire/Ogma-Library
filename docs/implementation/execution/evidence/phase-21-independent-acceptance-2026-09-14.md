# Phase 21 independent acceptance handoff — 2026-09-14

Status: **conditional / not release-approved**.

The first remediation tranche has reproducible commits and a complete serial
Windows test run, but the mandatory independent acceptance gate is not closed.

## Evidence received

- Audited baseline commit: `a93900a2bd7313a2948a29d2813ec3e14696f877`.
- Remediation commits: `57fd674` through `6ad3434` on `main`.
- Full serial suite: 1,161 passed, 0 failed, 0 skipped.
- Native Windows palette check: explicit close control was discoverable and
  removed the overlay through UI Automation; Ctrl/Cmd modifier parity is in the
  source but macOS execution is not yet available.
- Refreshed SRS accountability: 101 FRs, 29 NFRs, 32 controls; gate passes.

## Conditions preventing release acceptance

1. Physical macOS build, native PDF path, accessibility, signing,
   notarization and Gatekeeper launch evidence are absent.
2. The reader still needs a complete in-document find/full-screen/workspace
   acceptance run; current work only adds actionable render failure recovery.
3. Classroom no-full-copy storage semantics and two-machine trust/revocation
   evidence remain open.
4. Representative real-PDF, 2k/50k reference-machine and school-user pilot
   evidence is not supplied by this tranche.
5. Repository-wide format verification remains an existing failure and must be
   resolved or explicitly accepted by the release owner.

## Decision

Accept the tranche as a reversible improvement increment and continue the
Kaizen loop. Do not publish a 95% readiness score, beta approval or production
release decision. The next acceptance review must execute the same critical
journeys on both supported operating systems using the promoted artifact and
must close every condition above or record an explicit approved deferral.
