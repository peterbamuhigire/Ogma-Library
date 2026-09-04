# Phase 38 Progress - Performance, Reliability, Packaging and Beta

Date: 2026-09-04

## Delivered in this increment

- Added the `ogma-release-v1` descriptor contract and JSON schema with bounded
  identifiers, safe artifact filenames, supported platform/runtime pairings,
  and mandatory SHA-256 / RSA-PSS-SHA256 fields.
- Added `ReleaseDescriptorVerifier`, which validates descriptor shape before
  verifying the exact signed JSON bytes with the existing protected-key
  verifier. Tampered descriptor and package tests are now covered together.
- Added reproducible Windows and macOS runtime candidate packaging through
  `scripts/New-ReleaseCandidate.ps1`, with digest evidence and a separate
  integrity gate in `scripts/Test-ReleaseCandidate.ps1`.
- Added credential-gated Authenticode verification and Developer ID / Apple
  notarization hooks. The script fails closed when platform signing is required
  but certificate, identity, tooling, or notarization profile is unavailable.
- Added runtime identifiers to the tracked lock files so both `win-x64` and
  `osx-arm64` publish restores remain locked and reproducible.
- Added a manual/tag release-candidate workflow with least-privilege read
  permissions, serialized runs, signature-required-by-default behavior, and
  commit-bound artifact names.
- Added the Phase 38 release pipeline, rollback, migration-drill, key-custody,
  and privacy/observability record.
- Repaired SQLite rollback compatibility for additive-column down paths and
  replaced the Phase 23 foreign-key column removal with a row-preserving table
  rebuild. The migration test class now passes forward, downgrade, remigration,
  and legacy backfill scenarios.
- Closed the locally verifiable migration-compatibility subgate with the
  migration test class passing 9/9. Physical upgrade/rollback drills remain
  release-acceptance work.
- Recorded the local descriptor, packaging-script, and migration evidence in
  `evidence/phase-38-release-candidate-2026-09-04.md`.

## Verification

- Release build after descriptor changes: 0 warnings, 0 errors.
- Update trust slice: 3 passed, including valid descriptor verification,
  descriptor tamper rejection, unsafe filename rejection, and package digest
  rejection.
- Windows `win-x64` candidate was published, zipped, hashed, and integrity
  verified locally; candidate zip was 109,548,465 bytes and was generated in a
  temporary directory rather than committed.
- Fresh unsigned `win-x64` candidate generation and integrity verification
  passed after the packaging script was corrected to omit empty signature-only
  parameters; the temporary candidate was removed after verification.
- PowerShell packaging scripts parsed successfully.
- `actionlint` was not installed on the developer machine; workflow validation
  remains pending CI/actionlint execution.
- SQLite migration test class: 9 passed, including the previously failing
  Phase 18 downgrade and Phase 12 legacy-history rollback scenarios.

## Remaining phase gate

This phase is not complete. Final MSIX/installer production, Authenticode and
Developer ID/notarization evidence, clean W-REF-01/M-REF-01 installation and
performance runs, interrupted-upgrade recovery, and physical rollback drills
remain Phase 39 release-acceptance gates. No private signing key is stored in
the repository.
