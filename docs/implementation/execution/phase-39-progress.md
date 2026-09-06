# Phase 39 Progress - Cross-Platform Release Acceptance and Handover

Date: 2026-09-05

## Delivered in this increment

- Added the `ogma-release-acceptance-v1` evidence schema binding acceptance to a
  full source commit SHA, immutable platform artifact digests, descriptor
  signature verification, platform signing, clean install, critical flows,
  performance, reference hardware, migration, rollback, backup/restore, and
  owner approval.
- Added `scripts/Test-ReleaseAcceptance.ps1`, which fails closed when either
  platform, reference machine, signing/notarization, migration, rollback, or
  residual-risk gate is absent.
- Kept the acceptance contract separate from candidate packaging so an unsigned
  or uninstalled build cannot be promoted by reusing a packaging result.
- Recorded the fail-closed acceptance-contract evidence in
  `evidence/phase-39-release-acceptance-2026-09-04.md`.
- Current-head negative acceptance validation is recorded in
  `evidence/phase-39-local-acceptance-reconciliation-2026-09-04.md`.
- The requirement-accountability sub-gate was rerun successfully against source
  commit `ee16da83adbdea017853a2f84f880e85fac7e3aa`: 101 functional
  requirements, 29 non-functional requirements, and 32 controls were all
  assigned across the roadmap matrix. See
  `evidence/phase-39-requirement-accountability-2026-09-05.md`.
- Current-head negative acceptance validation again rejected a missing record
  with exit code 1, preserving the fail-closed handover boundary.
- On 2026-09-06, current-head requirement accountability was rerun at
  `5bef1cc209295b1da452ac342da64f92ef00b5075`, and the complete solution
  regression passed 1,125/1,125 with no failures or skips. These results close
  only repository-verifiable sub-gates; they do not substitute for installed
  reference-machine or owner evidence.
- Protected-main CI then passed on both Windows and macOS at commit
  `75effc78c44350de79e107ce53f2da9955dc6fcf`, with 1,138 tests passing per
  platform. This closes the automated cross-platform CI sub-gate only; the
  explicit hosted-macOS capability diagnostics and physical handover gates
  remain open. Evidence: `evidence/ci-cross-platform-regression-2026-09-06.md`.
- Bound release acceptance to Phase 38's exact beta-v1 migration freeze and
  repaired the JSON-schema/script mismatch for required artifact names.
  Clearly labelled synthetic fixtures prove a matching record passes and a
  stale migration baseline fails; they are not release evidence. See
  `evidence/phase-39-schema-freeze-binding-2026-09-06.md`.
- Added a core cross-platform regression that launches the acceptance
  PowerShell validator and exercises both fixtures. The focused local run
  passed 1/1; protected CI now owns ongoing Windows/macOS reproduction.
- Mirrored the schema's closed property sets in the executable validator and
  added unknown-property rejection to the cross-platform test, preventing
  drift between manual script acceptance and the JSON contract.
- Bound every acceptance assertion to a canonicalized, in-directory evidence
  path and verified SHA-256 digest. The cross-platform contract test rejects a
  tampered digest; the checked-in fixture remains explicitly test-only. See
  `evidence/phase-39-evidence-digest-binding-2026-09-06.md`.
- The executable validator now requires the caller's expected full release
  commit SHA and rejects a record bound to any other commit.

## Remaining handover gate

Phase 39 is not complete. A real record still requires physical W-REF-01 and
M-REF-01 runs, signed Windows and notarized macOS artifacts, installed-build
critical journeys, final performance/accessibility evidence, upgrade
interruption recovery, backup/restore, rollback, and owner acceptance. The
repository now has an executable contract for those facts; it does not invent
them. Contract/schema consistency, schema-freeze binding, and evidence-digest
binding are closed.

## Current CI and Definition-of-Done reconciliation

Protected-main CI run 34028951795 passed on Windows and macOS for commit
`261b68da72f34b54698517def84e6d1071e1f05a`, including requirement
accountability, locked restore, clean format, warnings-as-errors build,
dependency/SAST/secret checks, the reproducible 3D build/budget, and the test
matrix. Later documentation-only runs were still executing when this record
was updated.

No Aug-39 Phase 39 Definition-of-Done criterion is closed. Every top-level
criterion still depends on real signed/notarized installed artifacts, physical
reference-machine journeys, final performance/accessibility/recovery evidence,
or accountable owner acceptance. Passing CI and synthetic acceptance-contract
fixtures are necessary controls, not substitutes for those facts.
